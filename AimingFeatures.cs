using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Recoil;
using SCB.Atomics;



namespace SCB
{
    internal partial class Aimbot : IDisposable
    {
        // IDisposable implementation
        private bool disposed;

        // Aimbot settings
        private readonly AtomicDouble AimSpeed;
        private readonly AtomicDouble AimSmoothing;
        private readonly AtomicBool AntiRecoil;
        private readonly AtomicBool Prediction;
        private readonly AtomicInt32 AimKey;
        private readonly AtomicInt32 DeadZone;
        private readonly AtomicInt32 AimLoc;

        // In game settings
        private readonly AtomicFloat MouseSensitivity;
        private readonly AtomicFloat AdsScale;

        // Thread and cancellation token for enemy scanning
        private DirectX11? directX11;
        private Thread? enemyScannerThread;
        private Thread? aimFeatureThread;
        private CancellationTokenSource enemeyCancellation = new();
        private readonly PingPongBuffer<EnemyData> enemyBuffer = new( 10 );
        private readonly AtomicBool BufferReady;

        // Recoil pattern processor
        private readonly RecoilPatternProcessor recoilPatternProcessor;
        private RecoilPattern? currentPattern;

        // Lock objects for thread safety
        private readonly Lock recoilLock = new();
        private readonly Lock bufferLock = new();

        // Game window variables
        private PointF centerOfGameWindow;

        internal Aimbot( ref RecoilPatternProcessor recoilProcessor )
        {
            // Set the UserSettings
            var playerSettings = PlayerData.GetAimSettings();
            AimSpeed = new AtomicDouble( playerSettings.aimSpeed );
            AimSmoothing = new AtomicDouble( playerSettings.aimSmoothing );
            AntiRecoil = new AtomicBool( playerSettings.antiRecoil );
            Prediction = new AtomicBool( playerSettings.prediction );
            AimKey = new AtomicInt32( playerSettings.aimKey );
            DeadZone = new AtomicInt32( playerSettings.deadZone );
            AimLoc = new AtomicInt32( ( int ) playerSettings.location );
            // Set in game settings
            MouseSensitivity = new AtomicFloat( playerSettings.mouseSens );
            AdsScale = new AtomicFloat( playerSettings.adsScale );

            // Initialize the buffer ready flag 
            BufferReady = new AtomicBool( false );

            // Get the game window rect
            var gameRect = PlayerData.GetRect();

            // Calculate the center of the game window
            centerOfGameWindow = new PointF
            {
                X = ( float ) ( gameRect.right - gameRect.left ) / 2,
                Y = ( float ) ( gameRect.bottom - gameRect.top ) / 2
            };

            // Initialize the recoil pattern processor
            recoilPatternProcessor = recoilProcessor;

            // Subscribe to update events
            PlayerData.OnUpdate += AimSettingsUpdate;
            recoilPatternProcessor.RecoilPatternChanged += RecoilPatternUpdate!;

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
        private static PointF PredictEnemy( (EnemyData, EnemyData) recentFrames, AimLocation location, double extrapolationTime )
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
            PointF predictedPos = Mathf.MotionExtrapolation( currentPos, previousPos, deltaTime, ( float ) extrapolationTime );

            return predictedPos;
        }



        /// <summary>
        /// Aims at the target using a Bezier curve for smooth mouse movement, with adjusted smoothing and speed scaling.
        /// </summary>
        /// <param name="targetPos">The target position to aim at.</param>
        /// <param name="enemyDistance">The distance to the enemy to adjust speed and smoothing.</param>
        /// <param name="sleepTime">The calculated base sleep time passed in.</param>
        private void AimUsingBezier( ref PointF targetPos, double enemyDistance, double sleepTime, ref int aimKey )
        {
            PointF startPos = centerOfGameWindow;
            Utils.BezierPointCollection bezierPoints;
            List<PointF> cursorPath;
            int shootKey = MouseInput.VK_LBUTTON;


            // Adjust factors based on distance (closer enemies = faster movements, higher smoothing)
            float distanceFactor = Mathf.SmootherStep( 50, 5, ( float ) enemyDistance ); // Scale between 5m (close) to 50m (far)
            float adjustedSmoothingFactor = Mathf.Clamp01( ( float ) ( AimSmoothing / 100f ) * distanceFactor );

            // Dynamic sleep time based on speed factor, closer enemies = less sleep time. 50 microseconds to 1 millisecond
            // Reverse the behavior: Higher AimSpeed = Faster movement (shorter sleep time)
            double dynamicSleepTime = sleepTime * ( 101 - AimSpeed ) / 100.0;


            // Check if user-selected control points are set
            if ( PlayerData.BezierControlPointsSet() )
            {
                bezierPoints = PlayerData.GetBezierPoints();
                cursorPath = bezierPoints.ScaleAndCalculate( ref startPos, ref targetPos, ( int ) ( ( dynamicSleepTime / distanceFactor ) * adjustedSmoothingFactor ) );
            } else
            {
                // Default control points if user hasn't selected their own
                List<PointF> controlPoints =
                [
                    new PointF(startPos.X + (targetPos.X - startPos.X) * 0.2f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.1f)),
                    new PointF(startPos.X + (targetPos.X - startPos.X) * 0.4f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.2f)),
                    new PointF(startPos.X + (targetPos.X - startPos.X) * 0.6f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.3f)),
                    new PointF(startPos.X + (targetPos.X - startPos.X) * 0.7f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.5f)),
                    new PointF(startPos.X + (targetPos.X - startPos.X) * 0.85f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.7f)),
                    new PointF(startPos.X + (targetPos.X - startPos.X) * 0.95f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.85f))
                ];

                bezierPoints = new Utils.BezierPointCollection( startPos, targetPos, controlPoints );
                cursorPath = bezierPoints.CalculateOcticBezierPoints( ( int ) ( ( dynamicSleepTime / distanceFactor ) * adjustedSmoothingFactor ) );
            }

            // Anti-recoil setup (if needed)
            bool activateAntiRecoil = AntiRecoil.VALUE() && MouseInput.IsKeyPressed( ref shootKey );
            Stopwatch recoilTimer = new();

            recoilTimer.Start();

            // Interpolate along the Bezier curve over the total time (single curve)
            for ( int i = 0; i < cursorPath.Count && MouseInput.IsKeyHeld( ref aimKey ); i++ )
            {
                PointF currentPos = cursorPath[ i ];

                // Calculate movement deltas
                float deltaX = currentPos.X - startPos.X;
                float deltaY = currentPos.Y - startPos.Y;

                // Apply recoil compensation
                if ( activateAntiRecoil )
                {
                    double elapsed = recoilTimer.Elapsed.TotalMilliseconds;
                    float recoilX = centerOfGameWindow.X - currentPattern!.Pattern.ElementAtOrDefault( ( int ) elapsed ).Key.X;
                    float recoilY = centerOfGameWindow.Y - currentPattern!.Pattern.ElementAtOrDefault( ( int ) elapsed ).Key.Y;

                    // Check if return values arent default( if recoilX = centerOfGameWindow.X, or recoilY = centerOfGameWindow.Y, then the pattern is empty)
                    if ( recoilX == centerOfGameWindow.X && recoilY == centerOfGameWindow.Y )
                    {
                        break;
                    }

                    // Apply recoil compensation to the movement deltas
                    deltaX += recoilX;
                    deltaY += recoilY;
                }

                // Apply mouse sensitivity and ADS multiplier
                deltaX /= MouseSensitivity * AdsScale;
                deltaY /= MouseSensitivity * AdsScale;


                // Move the mouse using the smoothed deltas
                MouseInput.MoveRelativeMouse( ref deltaX, ref deltaY );

                // Update the start position for the next iteration
                startPos = currentPos;

                if ( Mathf.GetDistance<int>( ref currentPos, ref targetPos ) <= DeadZone )
                {
                    break;
                }

                // Check if shoot key is still held
                if ( activateAntiRecoil && !MouseInput.IsKeyHeld( ref shootKey ) )
                {
                    activateAntiRecoil = false;
                }

                // Sleep for dynamic sleep time
                Utils.Watch.MicroSleep( dynamicSleepTime );
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

            Bitmap? screenCap = null;
            int originalAimRad = PlayerData.GetFov();
            List<EnemyData> enemies = [];

            while ( !enemeyCancellation.Token.IsCancellationRequested )
            {
                // Null out bitmap if the aim fov has changed
                if ( PlayerData.GetFov() != originalAimRad &&
                    screenCap != null )
                {
                    screenCap.Dispose();
                    screenCap = null;
                }

                // If the user changed the outline color, the directX11 class needs to be reset
                while ( directX11.ResettingClass.Wait( 10 ) )
                {
                    // we use yield to just play nice with the system
                    Thread.Yield();
                }

                directX11.ProcessFrameAsBitmap( ref screenCap );

                if ( screenCap != default )
                {
#if DEBUG
                    string randomNum = new Random().Next( 0, 1000000 ).ToString() + ".png";
                    screenCap.Save( FileManager.enemyScansFolder + randomNum, ImageFormat.Png );
                    //EnemyScanning.FilterNonEnemies( screenCap );
#endif
                } else
                {
                    Utils.Watch.MicroSleep( 1000 );
                    continue;
                }

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
                        BufferReady.CompareExchange( true, false );
                    }
                }
            }

            //dispose of the screen capture
            screenCap?.Dispose();

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
                aimKey = AimKey.VALUE();
                if ( BufferReady.VALUE() )
                {
                    if ( Prediction.VALUE() )
                    {
                        var recentFrames = enemyBuffer.GetTwoMostRecentEntries();

                        if ( recentFrames.Item1.CaptureTime == recentFrames.Item2.CaptureTime )
                        {
                            continue;
                        }

                        if ( ( int ) ( AimLoc.VALUE() == ( ( int ) AimLocation.head ) ? recentFrames.Item1.DistanceFromCenter.toHead : recentFrames.Item1.DistanceFromCenter.toBody ) >= DeadZone ||
                        ( int ) ( AimLoc.VALUE() == ( ( int ) AimLocation.head ) ? recentFrames.Item2.DistanceFromCenter.toHead : recentFrames.Item2.DistanceFromCenter.toBody ) >= DeadZone )
                        {
                            // Perform prediction based on historical data
                            var targetPos = PredictEnemy( recentFrames, ( ( AimLocation ) AimLoc.VALUE() ), 5.0f );

                            // Check if the target position is within the DeadZone
                            if ( Mathf.GetDistance<int>( ref targetPos, ref centerOfGameWindow ) <= DeadZone )
                            {
                                return;
                            }

                            if ( MouseInput.IsKeyPressed( ref aimKey ) )
                            {
                                AimUsingBezier( ref targetPos, recentFrames.Item2.Distance, recentFrames.Item2.SleepTime, ref aimKey );
                            }
                        }

                    } else
                    {
                        // Use the latest frame directly
                        EnemyData enemy;
                        lock ( bufferLock )
                        {
                            if ( enemyBuffer.Count > 0 )
                            {
                                // Retrieve the last frame
                                enemy = enemyBuffer.GetLatestEntry();

                                // Check if the enemy is within the DeadZone
                                if ( ( int ) ( AimLoc.VALUE() == ( ( int ) AimLocation.head ) ? enemy.DistanceFromCenter.toHead : enemy.DistanceFromCenter.toBody ) <= DeadZone )
                                {
                                    return;
                                }
                            } else
                            {
                                return;
                            }
                        }
                        if ( MouseInput.IsKeyPressed( ref aimKey ) )
                        {
                            PointF targetPos = AimLoc.VALUE() == ( ( int ) AimLocation.head ) ? enemy.Head : enemy.Body;
                            AimUsingBezier( ref targetPos, enemy.Distance, enemy.SleepTime, ref aimKey );
                        }
                    }
                }

                // Clear the VALUE buffer after processing
                enemyBuffer.ClearReadBuffer();

                // Toggle the buffer ready flag
                BufferReady.CompareExchange( false, true );

                // Sleep for 1 millisecond
                Thread.Sleep( 1 );
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
        private Action UpdateAtomic<T>( T atomicVar, dynamic update, UpdateType updateType ) where T : class
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
                goto LOG;
                case UpdateType.Deadzone:
                UpdateAtomic( DeadZone, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.AimSmoothing:
                UpdateAtomic( AimSmoothing, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.AimKey:
                UpdateAtomic( AimKey, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.AntiRecoil:
                UpdateAtomic( AntiRecoil, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.Prediction:
                UpdateAtomic( Prediction, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.MouseSens:
                UpdateAtomic( MouseSensitivity, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.AdsScale:
                UpdateAtomic( AdsScale, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.AimLocation:
                UpdateAtomic( AimLoc, e.UpdatedVar, e.Key )();
                goto LOG;
                case UpdateType.WindowRect:
                var gameRect = e.UpdatedVar;
                centerOfGameWindow = new PointF
                {
                    X = ( float ) ( gameRect.right - gameRect.left ) / 2,
                    Y = ( float ) ( gameRect.bottom - gameRect.top ) / 2
                };
                goto LOG;
                default:
                goto END;
            }
LOG:
#if DEBUG
            Logger.Log( $"Aimbot setting updated: {e.Key} = {e.UpdatedVar}" );
#endif

END:
            return; //< this is only here so the goto statement doesn't throw an error
        }


        /// <summary>
        /// Delegate for the RecoilPatternChanged event.
        /// We use a lock for this as its just easier to manage for the aimbot usage.
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="recoilPattern">New recoil Pattern</param>
        private void RecoilPatternUpdate( object sender, RecoilPattern recoilPattern )
        {
            lock ( recoilLock )
            {
                currentPattern = recoilPattern;
            }
        }


        internal void Stop()
        {
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
            if ( d3d11 != null )
            {
                directX11 = d3d11;
            }

            enemeyCancellation = new CancellationTokenSource();
            enemyScannerThread = new Thread( CaptureAndScan );
            aimFeatureThread = new Thread( AimBot );

            enemyScannerThread.IsBackground = true;
            aimFeatureThread.IsBackground = true;

            enemyScannerThread.Start();
            aimFeatureThread.Start();

#if DEBUG

            string aimBotSettings = $"Aim Speed: {AimSpeed.VALUE()}\n" +
                                    $"Aim Smoothing: {AimSmoothing.VALUE()}\n" +
                                    $"Anti-Recoil: {AntiRecoil.VALUE()}\n" +
                                    $"Prediction: {Prediction.VALUE()}\n" +
                                    $"Aim Key: {AimKey.VALUE()}\n" +
                                    $"Dead Zone: {DeadZone.VALUE()}\n" +
                                    $"Aim Location: {AimLoc.VALUE()}\n" +
                                    $"Mouse Sensitivity: {MouseSensitivity.VALUE()}\n" +
                                    $"ADS Scale: {AdsScale.VALUE()}";

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
            }

            disposed = true;
        }

        /// <summary>
        /// Retrieves a pseudo handle for the calling thread.
        /// </summary>
        /// <returns>A handle to the calling thread.</returns>
        [DllImport( "kernel32.dll" )]
        private static extern nint GetCurrentThread();

        /// <summary>
        /// Sets a processor affinity mask for the specified thread.
        /// </summary>
        /// <param name="hThread">A handle to the thread whose affinity mask is to be set.</param>
        /// <param name="dwThreadAffinityMask">The processor affinity mask.</param>
        /// <returns>If the function succeeds, the return value is the previous affinity mask.</returns>
        [DllImport( "kernel32.dll" )]
        private static extern nint SetThreadAffinityMask( nint hThread, nint dwThreadAffinityMask );

    };

}


