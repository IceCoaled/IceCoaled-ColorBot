

using System.Diagnostics;
using System.Runtime.InteropServices;
using Recoil;
using SCB.Atomics;

namespace SCB
{
    internal class Aimbot : IDisposable
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
        private readonly UnsafeAtomicNumerics<AimLocation> AimLoc;

        // In game settings
        private readonly AtomicFloat MouseSensitivity;
        private readonly AtomicFloat AdsScale;

        // Thread and cancellation token for enemy scanning
        private readonly DirectX11 directX11;
        private Thread? enemyScannerThread;
        private Thread? aimFeatureThread;
        private CancellationTokenSource enemeyCancellation = new();
        private readonly PingPongBuffer<EnemyData> enemyBuffer = new( 10 );
        private readonly UnsafeAtomicNumerics<bool> BufferReady = new( false );

        // Recoil pattern processor
        private readonly RecoilPatternProcessor recoilPatternProcessor;
        private RecoilPattern? currentPattern;

        // Lock objects for thread safety
        private readonly object recoilLock = new();
        private readonly object bufferLock = new();

        // Game window variables
        private PointF centerOfGameWindow;

        internal Aimbot( ref RecoilPatternProcessor recoilProcessor, ref DirectX11 dx11 )
        {
            // Set the UserSettings
            var playerSettings = PlayerData.GetAimSettings();
            AimSpeed = new AtomicDouble( playerSettings.aimSpeed );
            AimSmoothing = new AtomicDouble( playerSettings.aimSmoothing );
            AntiRecoil = new AtomicBool( playerSettings.antiRecoil );
            Prediction = new AtomicBool( playerSettings.prediction );
            AimKey = new AtomicInt32( playerSettings.aimKey );
            DeadZone = new AtomicInt32( playerSettings.deadZone );
            AimLoc = new UnsafeAtomicNumerics<AimLocation>( playerSettings.location );
            // Set in game settings
            MouseSensitivity = new AtomicFloat( playerSettings.mouseSens );
            AdsScale = new AtomicFloat( playerSettings.adsScale );


            // Get the game window rect
            var gameRect = PlayerData.GetRect();

            // Initialize the DirectX11 capture
            directX11 = dx11;

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
                    float recoilX = centerOfGameWindow.X - currentPattern.Pattern.ElementAtOrDefault( ( int ) elapsed ).Key.X;
                    float recoilY = centerOfGameWindow.Y - currentPattern.Pattern.ElementAtOrDefault( ( int ) elapsed ).Key.Y;

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

                directX11.ProcessFrameAsBitmap( ref screenCap );

                if ( screenCap != null )
                {
                    string randomNum = new Random().Next( 0, 1000000 ).ToString() + ".png";
                    screenCap.Save( FileManager.enemyScansFolder + randomNum );
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

                        if ( ( int ) ( AimLoc.VALUE() == AimLocation.head ? recentFrames.Item1.DistanceFromCenter.toHead : recentFrames.Item1.DistanceFromCenter.toBody ) >= DeadZone ||
                          ( int ) ( AimLoc.VALUE() == AimLocation.head ? recentFrames.Item2.DistanceFromCenter.toHead : recentFrames.Item2.DistanceFromCenter.toBody ) >= DeadZone )
                        {
                            // Perform prediction based on historical data
                            var targetPos = PredictEnemy( recentFrames, AimLoc.VALUE(), 5.0f );

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
                                if ( ( int ) ( AimLoc.VALUE() == AimLocation.head ? enemy.DistanceFromCenter.toHead : enemy.DistanceFromCenter.toBody ) <= DeadZone )
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
                            PointF targetPos = AimLoc.VALUE() == AimLocation.head ? enemy.Head : enemy.Body;
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


        private void AimSettingsUpdate( object sender, PlayerUpdateCallbackEventArgs e )
        {
            switch ( e.Key )
            {
                case UpdateType.AimSpeed:
                AimSpeed.VALUE( e.UpdatedVar );
                break;
                case UpdateType.Deadzone:
                DeadZone.VALUE( e.UpdatedVar );
                break;
                case UpdateType.AimSmoothing:
                AimSmoothing.VALUE( e.UpdatedVar );
                break;
                case UpdateType.AimKey:
                AimKey.VALUE( e.UpdatedVar );
                break;
                case UpdateType.AntiRecoil:
                AntiRecoil.VALUE( e.UpdatedVar );
                break;
                case UpdateType.Prediction:
                Prediction.VALUE( e.UpdatedVar );
                break;
                case UpdateType.MouseSens:
                MouseSensitivity.VALUE( e.UpdatedVar );
                break;
                case UpdateType.AdsScale:
                AdsScale.VALUE( e.UpdatedVar );
                break;
                case UpdateType.AimLocation:
                AimLoc.VALUE( e.UpdatedVar );
                break;
                case UpdateType.WindowRect:
                var gameRect = e.UpdatedVar;
                centerOfGameWindow = new PointF
                {
                    X = ( float ) ( gameRect.right - gameRect.left ) / 2,
                    Y = ( float ) ( gameRect.bottom - gameRect.top ) / 2
                };
                break;
            }
        }


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
            enemeyCancellation?.Dispose();
            enemyScannerThread?.Join();
            aimFeatureThread?.Join();
        }

        internal void Start()
        {
            enemeyCancellation = new CancellationTokenSource();
            enemyScannerThread = new Thread( CaptureAndScan );
            aimFeatureThread = new Thread( AimBot );

            enemyScannerThread.Start();
            aimFeatureThread.Start();
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
                enemeyCancellation?.Cancel();
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
                PlayerData.OnUpdate -= AimSettingsUpdate;
                recoilPatternProcessor.RecoilPatternChanged -= RecoilPatternUpdate;
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


