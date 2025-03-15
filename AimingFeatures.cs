using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Recoil;
using SCB.Atomics;
using Utils;



namespace SCB
{
    internal unsafe partial class Aimbot : IDisposable
    {
        // IDisposable implementation
        private bool disposed;

        // Aiming variables struct
        private IntPtr aimVsAllocHeader;
        private ref AimingVariables AimVs() => ref Unsafe.AsRef<AimingVariables>( aimVsAllocHeader.ToPointer() );

        // Recoil stopwatch
        private readonly Stopwatch recoilTimer = new();

        // Aimbot settings
        private readonly AtomicFloat AimSpeed;
        private readonly AtomicFloat AimSmoothing;
        private readonly NonNullableAtomicThreadSignal<bool> AntiRecoil;
        private readonly NonNullableAtomicThreadSignal<bool> Prediction;
        private readonly AtomicInt32 AimKey;
        private readonly AtomicFloat DeadZone;
        private readonly AtomicInt32 AimLoc;

        // Bezier variables
        private BezierPointCollection bezierPoints = new();
        private List<PointF> cursorPath = [];
        private List<PointF> controlPoints = [];

        // In game settings
        private readonly AtomicFloat MouseSensitivity;
        private readonly AtomicFloat AdsScale;

        // Thread and cancellation token for enemy scanning
        private DirectX11? directX11;
        private Thread? enemyScannerThread;
        private Thread? aimFeatureThread;
        private CancellationTokenSource enemeyCancellation = new();
        private readonly PingPongBuffer<EnemyData> enemyBuffer = new( 10 );
        private readonly NonNullableAtomicThreadSignal<bool> BufferReady;

        // Recoil pattern processor
        private readonly RecoilPatternProcessor recoilPatternProcessor;
        private RecoilPattern? currentPattern;

        // Lock objects for thread safety
        private readonly System.Threading.Lock aimingLock = new();
        private readonly System.Threading.Lock bufferLock = new();

        // Game window variables
        private PointF centerOfGameWindow;

        internal Aimbot( ref RecoilPatternProcessor recoilProcessor )
        {
            // Set the UserSettings
            var playerSettings = PlayerData.GetAimSettings();
            AimSpeed = new AtomicFloat( playerSettings.aimSpeed );
            AimSmoothing = new AtomicFloat( playerSettings.aimSmoothing );
            AntiRecoil = new NonNullableAtomicThreadSignal<bool>( playerSettings.antiRecoil );
            Prediction = new NonNullableAtomicThreadSignal<bool>( playerSettings.prediction );
            AimKey = new AtomicInt32( playerSettings.aimKey );
            DeadZone = new AtomicFloat( playerSettings.deadZone );
            AimLoc = new AtomicInt32( ( int ) playerSettings.location );
            // Set in game settings
            MouseSensitivity = new AtomicFloat( playerSettings.mouseSens );
            AdsScale = new AtomicFloat( playerSettings.adsScale );

            // Initialize the buffer ready flag 
            BufferReady = new NonNullableAtomicThreadSignal<bool>( false );

            // Get the game window rect
            var gameRect = PlayerData.GetRect();

            // Calculate the center of the game window
            centerOfGameWindow = new PointF
            {
                X = ( ( float ) ( ( gameRect.right - gameRect.left ) >> 1 ) ),
                Y = ( ( float ) ( ( gameRect.bottom - gameRect.top ) >> 1 ) )
            };

            // Initialize the recoil pattern processor
            recoilPatternProcessor = recoilProcessor;

            // Subscribe to update events
            PlayerData.OnUpdate += AimSettingsUpdate;
            recoilPatternProcessor.RecoilPatternChanged += RecoilPatternUpdate!;



            // Alloc memory for aimVs struct
            // We do this so the struct isnt contained in this class
            // This takes it off the heap; for better performance
            var aimVs = new AimingVariables();
            aimVsAllocHeader = Marshal.AllocHGlobal( Marshal.SizeOf<AimingVariables>() );
            if ( aimVsAllocHeader == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new OutOfMemoryException( "Failed to allocate memory for aim variables struct." ) );
            }


            // Initialize input struct inside aiming variables struct
            aimVs.mouseInput.Type = HidInputs.INPUT_MOUSE;
            aimVs.mouseInput.Data.Mouse.ExtraInfo = nint.Zero;
            aimVs.mouseInput.Data.Mouse.Flags = HidInputs.MOUSEEVENTF_MOVE;
            aimVs.mouseInput.Data.Mouse.MouseData = 0;
            aimVs.mouseInput.Data.Mouse.Time = 0;
            aimVs.mouseInput.Data.Mouse.X = 0;
            aimVs.mouseInput.Data.Mouse.Y = 0;

            // Copy the struct to the allocated memory
            // As well as Tell to collect the original struct
            Marshal.StructureToPtr( aimVs, aimVsAllocHeader, false );

#if DEBUG
            Logger.Log( "Aimbot initialized" );
#endif
        }


        /// <summary>
        /// Deconstructor for the Aimbot class.
        /// </summary>
        ~Aimbot()
        {
            Dispose( false );
        }

        /// <summary>
        /// Predicts the future position of an enemy based on recent frames using motion extrapolation.
        /// </summary>
        /// <param name="recentFrames">The recent frames of enemy data to use for prediction.</param>
        /// <param name="location">The target location (head or body) to predict.</param>
        /// <param name="extrapolationTime">The time (in seconds) to extrapolate into the future.</param>
        /// <returns>The predicted position of the enemy.</returns>
        private static PointF PredictEnemy( (EnemyData, EnemyData) recentFrames, AimLocation location, float extrapolationTime )
        {

            // Retrieve the most recent frame and the second most recent frame
            var currentFrame = recentFrames.Item1;
            var previousFrame = recentFrames.Item2;

            // Extract positions based on the aim location (head or body)
            PointF currentPos = location == AimLocation.head ? currentFrame.Head : currentFrame.Body;
            PointF previousPos = location == AimLocation.head ? previousFrame.Head : previousFrame.Body;

            // Calculate the time difference between frames
            float deltaTime = ( float ) ( currentFrame.CaptureTime - previousFrame.CaptureTime );

            // Use the provided motion extrapolation function to predict the future position
            PointF predictedPos = Mathf.MotionExtrapolation( ref currentPos, ref previousPos, deltaTime, extrapolationTime );

            return predictedPos;
        }


        /// <summary>
        /// Aims at the target using a Bezier curve for smooth mouse movement, with adjusted smoothing and speed scaling.
        /// </summary>
        /// <param name="targetPos">The target position to aim at.</param>
        /// <param name="enemyDistance">The distance to the enemy to adjust speed and smoothing.</param>
        /// <param name="sleepTime">The calculated base sleep time passed in.</param>
        private void AimUsingBezier( PointF targetPos, double enemyDistance, ref int aimKey )
        {
            using var sL = LockForAim();

            // Set the start position to the center of the game window
            AimVs().startPos = centerOfGameWindow;

            // Adjust factors based on distance (closer enemies = faster movements, higher smoothing)
            AimVs().distanceFactor = Mathf.SmoothStep( 50, 5, ( float ) enemyDistance ); // Scale between 5m (close) to 50m (far)
            AimVs().adjustedSmoothingFactor = Mathf.Clamp01( ( AimSmoothing / 100f ) * AimVs().distanceFactor );

            // Dynamic sleep time based on speed factor, closer enemies = less sleep time. 50 microseconds to 1 millisecond
            // Reverse the behavior: Higher AimSpeed = Faster movement (shorter sleep time)
            AimVs().dynamicSleepTime = AimVs().distanceFactor * ( 101 - AimSpeed );


            // Check if user-selected control points are set
            if ( PlayerData.BezierControlPointsSet() )
            {
                bezierPoints = PlayerData.GetBezierPoints();
                cursorPath = bezierPoints.ScaleAndCalculate( ref AimVs().startPos, ref targetPos, ( int ) ( ( AimVs().dynamicSleepTime / AimVs().distanceFactor ) * AimVs().adjustedSmoothingFactor ) );
            } else
            {
                // Default control points if user hasn't selected their own
                controlPoints =
                [
                    new PointF(AimVs().startPos.X + (targetPos.X - AimVs().startPos.X) * 0.2f,  AimVs().startPos.Y + ((targetPos.Y - AimVs().startPos.Y) * 0.1f)),
                    new PointF(AimVs().startPos.X + (targetPos.X - AimVs().startPos.X) * 0.4f,  AimVs().startPos.Y + ((targetPos.Y - AimVs().startPos.Y) * 0.2f)),
                    new PointF(AimVs().startPos.X + (targetPos.X - AimVs().startPos.X) * 0.6f,  AimVs().startPos.Y + ((targetPos.Y - AimVs().startPos.Y) * 0.3f)),
                    new PointF(AimVs().startPos.X + (targetPos.X - AimVs().startPos.X) * 0.7f,  AimVs().startPos.Y + ((targetPos.Y - AimVs().startPos.Y) * 0.5f)),
                    new PointF(AimVs().startPos.X + (targetPos.X - AimVs().startPos.X) * 0.85f, AimVs().startPos.Y + ((targetPos.Y - AimVs().startPos.Y) * 0.7f)),
                    new PointF(AimVs().startPos.X + (targetPos.X - AimVs().startPos.X) * 0.95f, AimVs().startPos.Y + ((targetPos.Y - AimVs().startPos.Y) * 0.85f))
                ];

                bezierPoints = new Utils.BezierPointCollection( ref AimVs().startPos, ref targetPos, ref controlPoints );
                cursorPath = bezierPoints.CalculateOcticBezierPoints( ( int ) ( ( AimVs().dynamicSleepTime / AimVs().distanceFactor ) * AimVs().adjustedSmoothingFactor ) );
            }

            // Anti-recoil setup (if needed)
            AimVs().activateAntiRecoil = AntiRecoil.GetValue() && HidInputs.IsKeyPressed( ref AimVs().shootKey );

            recoilTimer.Restart();

            // Interpolate along the Bezier curve over the total time (single curve)
            for ( int i = 0; i < cursorPath.Count && HidInputs.IsKeyPressed( ref aimKey ); i++ )
            {
                AimVs().currentPos = cursorPath[ i ];

                // Calculate movement deltas
                AimVs().deltaX = AimVs().currentPos.X - AimVs().startPos.X;
                AimVs().deltaY = AimVs().currentPos.Y - AimVs().startPos.Y;

                // Apply recoil compensation
                if ( AimVs().activateAntiRecoil )
                {
                    AimVs().elapsed = recoilTimer.Elapsed.TotalMilliseconds;
                    float recoilX = centerOfGameWindow.X - currentPattern!.Pattern.ElementAtOrDefault( ( ( int ) AimVs().elapsed ) ).Key.X;
                    float recoilY = centerOfGameWindow.Y - currentPattern!.Pattern.ElementAtOrDefault( ( ( int ) AimVs().elapsed ) ).Key.Y;

                    // Apply recoil compensation to the movement deltas
                    AimVs().deltaX += recoilX;
                    AimVs().deltaY += recoilY;
                }

                // Apply mouse sensitivity and ADS multiplier
                AimVs().mouseInput.Data.Mouse.X = ( ( int ) ( AimVs().deltaX / ( MouseSensitivity * AdsScale ) ) );
                AimVs().mouseInput.Data.Mouse.Y = ( ( int ) ( AimVs().deltaY / ( MouseSensitivity * AdsScale ) ) );


                // Move the mouse using the smoothed deltas
                HidInputs.CustomMoveRelativeMouse( ref AimVs().mouseInput );

                // Update the start position for the next iteration
                AimVs().startPos = AimVs().currentPos;

                if ( Mathf.GetDistance<float>( ref AimVs().currentPos, ref targetPos ) <= DeadZone )
                {
                    break;
                }

                // Check if shoot key is still held
                if ( AimVs().activateAntiRecoil && !HidInputs.IsKeyPressed( ref AimVs().shootKey ) )
                {
                    AimVs().activateAntiRecoil = false;
                }

                // Sleep for dynamic sleep time
                Utils.Watch.MicroSleep( AimVs().dynamicSleepTime );
            }

        }


        private void CaptureAndScan()
        {
            nint hThread = GetCurrentThread();
            nint dwAffinity = SetThreadAffinityMask( hThread, 2 );
            if ( dwAffinity == 0 )
            {
                ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to set thread affinity mask." ) );
            }

            List<EnemyData> enemies = [];

            while ( !enemeyCancellation.Token.IsCancellationRequested )
            {
                // If the user changed the outline color, the directX11 class needs to be reset
                directX11!.ResettingClass.Wait();

                _ = directX11.GetEnemyDetails( ref enemies );

                //if there are no enemies, clear the VALUE buffer.
                //Check if aimbot is invoked, if not, clear the VALUE buffer
                if ( enemies.Count == 0 )
                {
                    enemyBuffer.ClearWriteBuffer();
                } else
                {
                    lock ( bufferLock )
                    {
                        foreach ( var enemy in enemies )
                        {
                            enemyBuffer.WriteBuffer( [ enemy ] );
                        }

                        if ( enemyBuffer.Count > 1 )
                        {
                            enemyBuffer.Sort();
                        }

                        enemyBuffer.SwapBuffers();
                        if ( !BufferReady.GetValue() )
                        {
                            BufferReady.SetValue( true );
                        }
                    }
                }
            }
            SetThreadAffinityMask( hThread, dwAffinity );
        }




        private void AimBot()
        {
            nint hThread = GetCurrentThread();
            nint dwAffinity = SetThreadAffinityMask( hThread, 1 );
            if ( dwAffinity == 0 )
            {
                ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to set thread affinity mask." ) );
            }

            int aimKey;

            while ( !enemeyCancellation.Token.IsCancellationRequested )
            {
                aimKey = AimKey.Read();
                if ( BufferReady.GetValue() )
                {
                    if ( Prediction.GetValue() )
                    {
                        var recentFrames = enemyBuffer.GetTwoMostRecentEntries();

                        if ( double.Abs( recentFrames.Item1.CaptureTime - recentFrames.Item2.CaptureTime ) < 50.0 )
                        {
                            continue;
                        }

                        if ( ( int ) ( AimLoc.Read() == ( ( int ) AimLocation.head ) ? recentFrames.Item1.UserToHead : recentFrames.Item1.UserToBody ) >= DeadZone ||
                        ( int ) ( AimLoc.Read() == ( ( int ) AimLocation.head ) ? recentFrames.Item2.UserToHead : recentFrames.Item2.UserToBody ) >= DeadZone )
                        {
                            // Perform prediction based on historical data
                            var targetPos = PredictEnemy( recentFrames, ( ( AimLocation ) AimLoc.Read() ), 5.0f );

                            // Check if the target position is within the DeadZone
                            if ( Mathf.GetDistance<float>( ref targetPos, ref centerOfGameWindow ) <= DeadZone )
                            {
                                return;
                            }

                            if ( HidInputs.IsKeyPressed( ref aimKey ) )
                            {
                                AimUsingBezier( targetPos, ( AimLoc.Read() == ( ( int ) AimLocation.head ) ) ? recentFrames.Item1.UserToHead : recentFrames.Item1.UserToBody, ref aimKey );
                            }
                        }

                    } else
                    {
                        // Use the latest frame directly
                        EnemyData enemy;
                        using ( var bufferSL = bufferLock.EnterScope() )
                        {
                            if ( enemyBuffer.Count > 0 )
                            {
                                // Retrieve the last frame
                                enemy = enemyBuffer.GetLatestEntry();

                                // Check if the enemy is within the DeadZone
                                if ( ( int ) ( AimLoc.Read() == ( ( int ) AimLocation.head ) ? enemy.UserToHead : enemy.UserToBody ) <= DeadZone )
                                {
                                    return;
                                }
                            } else
                            {
                                return;
                            }
                        }
                        if ( HidInputs.IsKeyPressed( ref aimKey ) )
                        {
                            AimUsingBezier( AimLoc.Read() == ( ( int ) AimLocation.head ) ? enemy.Head : enemy.Body, ( AimLoc.Read() == ( ( int ) AimLocation.head ) ) ? enemy.UserToHead : enemy.UserToBody, ref aimKey );
                        }
                    }
                }

                // Clear the VALUE buffer after processing
                enemyBuffer.ClearReadBuffer();

                // Toggle the buffer ready flag
                if ( BufferReady.GetValue() )
                {
                    BufferReady.SetValue( false );
                }

                // Sleep for 1 millisecond
                Utils.Watch.SecondsSleep( 1 );
            }

            SetThreadAffinityMask( hThread, dwAffinity );
        }



        /// <summary>
        /// Updates the atomic variable with the new value.
        /// Just an abstraction to make the code cleaner.
        /// Because we need to add a reference to the atomic variable before updating it, as the update is done in a different thread.
        /// </summary>
        /// <typeparam name="T">UnsafeAtomicNumerics</typeparam>
        /// <param name="atomicVar">Version of atomic class</param>
        /// <param name="update">Updated variable</param>
        /// <param name="updateType">Update type</param>
        /// <returns>void</returns>
        private static Action UpdateAtomic<T>( T atomicVar, dynamic update, UpdateType updateType ) where T : class
        {

            // Because we can access private methods with reflection, we are going to bypass the derived classes and access the base class directly.
            // This is because our VALUE method is overloaded and may cause issues when trying to access the method.
            const string write = "Write";
            const string addRef = "AddReference";
            const string removeRef = "RemoveReference";

            var addReference = atomicVar.GetType().GetMethod( addRef, System.Reflection.BindingFlags.Public );
            var setValue = atomicVar.GetType().GetMethod( write, System.Reflection.BindingFlags.NonPublic );
            var removeReference = atomicVar.GetType().GetMethod( removeRef, System.Reflection.BindingFlags.Public );


            return () =>
            {
                //Add the reference to the atomic variable
                addReference?.Invoke( atomicVar, null );
                //Update the atomic variable
                setValue?.Invoke( atomicVar, updateType == UpdateType.AimLocation ? ( int ) update : update );
                //Remove the reference to the atomic variable
                removeReference?.Invoke( atomicVar, null );
            };
        }

        private Action UpdateBezier( BezierPointCollection newBezier )
        {
            return () =>
            {
                var sL = aimingLock.EnterScope();
                bezierPoints = newBezier;
                sL.Dispose();
            };
        }

        /// <summary>
        /// Delegate for the RecoilPatternChanged event.
        /// We use a lock for this as its just easier to manage for the aimbot usage.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="recoilPattern">New recoil Pattern</param>
        private void RecoilPatternUpdate( object sender, RecoilPattern recoilPattern )
        {
            var sL = aimingLock.EnterScope();
            currentPattern = recoilPattern;
            sL.Dispose();
        }



        /// <summary>
        /// Delegate for the PlayerUpdate event.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">Updated variable class</param>
        private void AimSettingsUpdate( object sender, PlayerUpdateCallbackEventArgs e )
        {

            switch ( e.Key )
            {
                case UpdateType.AimSpeed:
                UpdateAtomic( AimSpeed, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.Deadzone:
                UpdateAtomic( DeadZone, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.AimSmoothing:
                UpdateAtomic( AimSmoothing, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.AimKey:
                UpdateAtomic( AimKey, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.AntiRecoil:
                UpdateAtomic( AntiRecoil, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.Prediction:
                UpdateAtomic( Prediction, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.MouseSens:
                UpdateAtomic( MouseSensitivity, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.AdsScale:
                UpdateAtomic( AdsScale, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.AimLocation:
                UpdateAtomic( AimLoc, e.UpdatedVar, e.Key )();
                break;
                case UpdateType.WindowRect:
                var gameRect = e.UpdatedVar;
                centerOfGameWindow = new PointF
                {
                    X = ( ( float ) ( ( gameRect.right - gameRect.left ) >> 1 ) ),
                    Y = ( ( float ) ( ( gameRect.bottom - gameRect.top ) >> 1 ) ),
                };
                break;
                case UpdateType.BezierPoints:
                UpdateBezier( e.UpdatedVar )();
                break;
                default:
                break;
            }
#if DEBUG
            Logger.Log( $"Aimbot setting updated: {e.Key} = {e.UpdatedVar}" );
#endif
        }

        private System.Threading.Lock.Scope LockForAim() => aimingLock.EnterScope();


        internal void Stop()
        {
            // Stop window Capture session
            directX11?.windowCapture?.StopCaptureSession();

            enemeyCancellation?.Cancel();
            enemyScannerThread?.Join();
            aimFeatureThread?.Join();
            enemyBuffer?.ClearReadBuffer();
            enemyBuffer?.ClearWriteBuffer();
            enemeyCancellation?.TryReset();
#if DEBUG
            Logger.Log( "Aimbot stopped" );
#endif
        }

        internal void Start( [Optional] DirectX11? d3d11 )
        {
            // Initialize dx11
            directX11 ??= d3d11;
            // Start window Capture session
            directX11?.windowCapture?.StartCaptureSession();

            enemeyCancellation = new CancellationTokenSource();
            enemyScannerThread = new Thread( CaptureAndScan );
            aimFeatureThread = new Thread( AimBot );

            enemyScannerThread.IsBackground = true;
            aimFeatureThread.IsBackground = true;
            enemyScannerThread.Start();
            aimFeatureThread.Start();



#if DEBUG

            string aimBotSettings = $"Aim Speed: {AimSpeed.Read()}\n" +
                                    $"Aim Smoothing: {AimSmoothing.Read()}\n" +
                                    $"Anti-Recoil: {AntiRecoil.GetValue()}\n" +
                                    $"Prediction: {Prediction.GetValue()}\n" +
                                    $"Aim Key: {AimKey.Read()}\n" +
                                    $"Dead Zone: {DeadZone.Read()}\n" +
                                    $"Aim Location: {AimLoc.Read()}\n" +
                                    $"Mouse Sensitivity: {MouseSensitivity.Read()}\n" +
                                    $"ADS Scale: {AdsScale.Read()}";

            Logger.Log( aimBotSettings );
            Logger.Log( "Aimbot started" );
#endif
        }



        /// <summary>
        /// Checks if the enemy scanner and aim feature threads are running.
        /// </summary>
        /// <returns>true if they are else false</returns>
        internal bool IsRunning()
        {
            if ( enemyScannerThread?.IsAlive == true &&
                aimFeatureThread?.IsAlive == true )
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// Restarts the enemy scanner and aim feature threads.
        /// </summary>
        internal void Restart()
        {
#if DEBUG
            Logger.Log( "Restarting aimbot" );
#endif

            Stop();
            Start();
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                // Cancel and join the threads
                enemeyCancellation?.TryReset();
                enemyScannerThread?.Join();
                aimFeatureThread?.Join();
                enemeyCancellation?.Dispose();

                // Clear the buffer
                enemyBuffer?.ClearReadBuffer();
                enemyBuffer?.ClearWriteBuffer();

                // Dispose of the atomic numerics
                AimSpeed?.Dispose();
                AimSmoothing?.Dispose();
                AntiRecoil?.Dispose();
                Prediction?.Dispose();
                AimKey?.Dispose();
                DeadZone?.Dispose();
                AimLoc?.Dispose();
                MouseSensitivity?.Dispose();
                AdsScale?.Dispose();
                BufferReady?.Dispose();

                // Unsubscribe from the event
                PlayerData.OnUpdate -= AimSettingsUpdate!;
                recoilPatternProcessor.RecoilPatternChanged -= RecoilPatternUpdate!;

                if ( aimVsAllocHeader != IntPtr.Zero )
                {
                    Marshal.FreeHGlobal( aimVsAllocHeader );
                    aimVsAllocHeader = IntPtr.Zero;
                }
            }
            disposed = true;
        }

        /// <summary>
        /// Retrieves a pseudo handle for the calling thread.
        /// </summary>
        /// <returns>A handle to the calling thread.</returns>
        [DllImport( "kernel32.dll", SetLastError = true )]
        private static extern nint GetCurrentThread();

        /// <summary>
        /// Sets a processor affinity mask for the specified thread.
        /// </summary>
        /// <param name="hThread">A handle to the thread whose affinity mask is to be set.</param>
        /// <param name="dwThreadAffinityMask">The processor affinity mask.</param>
        /// <returns>If the function succeeds, the return value is the previous affinity mask.</returns>
        [DllImport( "kernel32.dll", SetLastError = true )]
        private static extern nint SetThreadAffinityMask( nint hThread, nint dwThreadAffinityMask );


        /// <summary>
        /// This is a struct that holds all the variables needed for the aimming function.
        /// This stops us from having to create all new variables each time the function is called.
        /// </summary>
        [StructLayout( LayoutKind.Sequential )]
        private struct AimingVariables()
        {
            public HidInputs.INPUT mouseInput = new();
            public PointF startPos = new( 0, 0 );
            public PointF currentPos = new( 0, 0 );
            public float distanceFactor = 0.0f;
            public float adjustedSmoothingFactor = 0.0f;
            public float deltaX = 0.0f;
            public float deltaY = 0.0f;
            public double dynamicSleepTime = 0.0;
            public double elapsed = 0.0;
            public int shootKey = HidInputs.VK_LBUTTON;
            public bool activateAntiRecoil = false;
        }


    };

}


