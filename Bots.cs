using System.Diagnostics;
using Utils;
using static SCB.EnemyScanner;

namespace SCB
{
    /// <summary>
    /// Class to handle the aimbot functionality.
    /// </summary>
    internal class AimBot : IDisposable
    {
        private bool disposed = false;
        private readonly object locker = new object();
        private const int LockTimeout = 1000; // Timeout for lock waiting in milliseconds

        // Private fields
        private double aimSpeed = 1;
        private double aimSmoothing = 1;
        private double antiRecoilX = 0;
        private double antiRecoilY = 0;
        private volatile float recoilCompensationX = 0f;
        private volatile float recoilCompensationY = 0f;
        private double aimDelay = 1;
        private int aimKey = 0x01;
        private int deadZone = 15;
        private bool humanize = false;
        private PInvoke.RECT gameRect;
        private Point centerOfGameWindow;
        private AimLocation aimLocation;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Thread? aimBotThread;
        private Logger? logger;



        /// <summary>
        /// Aims at the target using a Bezier curve for smooth movement, with adjusted smoothing and speed scaling.
        /// </summary>
        /// <param name="targetPos">The target position to aim at.</param>
        private void AimUsingBezier( ref PointF targetPos )
        {
            PointF startPos = this.centerOfGameWindow;
            bool isRecoilActive = false;
            Task? recoilTask = null;  // Initialize the recoil task
            CancellationTokenSource recoilTokenSource = new();  // Token source to cancel the recoil task
            CancellationToken recoilToken = recoilTokenSource.Token;  // Token to pass into the recoil task

            if ( Mathf.GetDistance<int>( ref startPos, ref targetPos ) <= DeadZone )
            {
                return;
            }

            lock ( locker )
            {
                PointF controlPoint1;
                PointF controlPoint2;

                // Check if user-selected control points are set
                if ( PlayerData.BezierControlPointsSet() )
                {
                    PlayerData.GetBezierPoints( out PointF userStartPoint, out PointF userControlPoint1, out PointF userControlPoint2, out PointF userEndPoint );
                    controlPoint1 = userControlPoint1;
                    controlPoint2 = userControlPoint2;

                    // Scale and enforce control point angle limit
                    Mathf.ScaleControlPoints( ref controlPoint1, ref controlPoint2, ref startPos, ref targetPos, ref userStartPoint, ref userEndPoint );
                    Mathf.EnforceControlPointAngle( ref controlPoint1, ref controlPoint2, startPos, targetPos );
                } else
                {
                    float smoothingFactor = ( float ) Mathf.Clamp01( AimSmoothing / 100f );
                    controlPoint1 = new PointF( startPos.X + ( targetPos.X - startPos.X ) * 0.3f, startPos.Y + ( ( targetPos.Y - startPos.Y ) * smoothingFactor * 0.15f ) );
                    controlPoint2 = new PointF( startPos.X + ( targetPos.X - startPos.X ) * 0.6f, startPos.Y + ( ( targetPos.Y - startPos.Y ) * smoothingFactor * 0.15f ) );
                }

                double timeElapsed = 0;
                double totalTime = AimDelay / 1000.0;

                // Recoil compensation velocity for smoothing
                float recoilVelocityX = 0f;
                float recoilVelocityY = 0f;
                float smoothTime = 0.1f; // Controls how fast the smoothing happens

                if ( AntiRecoilX > 0 || AntiRecoilY > 0 )
                {
                    isRecoilActive = true;

                    // Start the recoil task
                    recoilTask = Task.Run( () =>
                    {
                        int shootKey = MouseInput.VK_LBUTTON;
                        float deltaTime = 0f;

                        while ( !MouseInput.IsKeyHeld( ref shootKey ) )
                        {
                            Utils.Watch.MicroSleep( 1 );
                            if ( recoilToken.IsCancellationRequested )
                                return;  // Exit task if cancelled
                        }

                        while ( MouseInput.IsKeyHeld( ref shootKey ) && !recoilToken.IsCancellationRequested )
                        {
                            // Recalculate recoil compensation based on antiRecoilX/Y values and time
                            recoilCompensationX = ( float ) ( antiRecoilX * Mathf.SmootherStep( 0, 1, deltaTime ) );
                            recoilCompensationY = ( float ) ( antiRecoilY * Mathf.SmootherStep( 0, 1, deltaTime ) );

                            // Increment deltaTime to gradually reduce recoil over time
                            deltaTime += 0.1f;

                            // Sleep before next update
                            Utils.Watch.MicroSleep( 50 );
                        }

                        // Reset recoil compensation when the task finishes
                        recoilCompensationX = 0f;
                        recoilCompensationY = 0f;
                        isRecoilActive = false;

                    }, recoilToken );  // Pass the token into the task
                }

                Stopwatch stopwatch = Stopwatch.StartNew();

                while ( timeElapsed < totalTime && Utils.MouseInput.IsKeyHeld( ref aimKey ) )
                {
                    timeElapsed = stopwatch.Elapsed.TotalSeconds;
                    float t = ( float ) Mathf.EaseInOut( 0, 1, timeElapsed / totalTime );

                    // Calculate the current Bezier position
                    PointF currentPos = Mathf.BezierCubicCalc( t, ref startPos, ref controlPoint1, ref controlPoint2, ref targetPos );

                    // Calculate movement deltas
                    float deltaX = currentPos.X - startPos.X;
                    float deltaY = currentPos.Y - startPos.Y;

                    // Apply recoil compensation if active
                    if ( isRecoilActive )
                    {
                        // Apply compensated movement
                        Mathf.SmoothDamp( ref deltaX, deltaX + recoilCompensationX, ref recoilVelocityX, smoothTime, ( float ) timeElapsed );
                        Mathf.SmoothDamp( ref deltaY, deltaY + recoilCompensationY, ref recoilVelocityY, smoothTime, ( float ) timeElapsed );
                    }

                    // Move the mouse using relative movement
                    MouseInput.MoveRelativeMouse( ref deltaX, ref deltaY );

                    // Update the start position for the next iteration
                    startPos = currentPos;

                    // Break if within the DeadZone
                    if ( Mathf.GetDistance<int>( ref currentPos, ref targetPos ) <= DeadZone )
                    {
                        break;
                    }

                    // Adjust delay dynamically based on AimSpeed
                    double adjustedDelay = Mathf.Lerp( 10, 100, ( 100 - AimSpeed ) / 100.0 );
                    Utils.Watch.MicroSleep( adjustedDelay );
                }

                // Final movement to reach the target precisely
                float finalMoveX = targetPos.X - startPos.X;
                float finalMoveY = targetPos.Y - startPos.Y;
                MouseInput.MoveRelativeMouse( ref finalMoveX, ref finalMoveY );

                // Cancel the recoil task if still running
                if ( recoilTask != null && !recoilTask.IsCompleted )
                {
                    recoilTokenSource.Cancel();  // Signal task to cancel
                    recoilTask.Wait();  // Wait for the task to finish
                    recoilTask.Dispose();
                    recoilTokenSource.Dispose();
                }
            }
        }




        private void AimAtTarget( ref PointF targetPos )
        {
            lock ( locker )
            {
                PointF middle = this.centerOfGameWindow;
                bool isRecoilActive = false;
                Task? recoilTask = null;  // Initialize the recoil task
                CancellationTokenSource recoilTokenSource = new();  // Token source to cancel the recoil task
                CancellationToken recoilToken = recoilTokenSource.Token;  // Token to pass into the recoil task

                // Calculate the initial distance to the target
                double initialDistance = Utils.Mathf.GetDistance<double>( ref middle, ref targetPos );
                if ( initialDistance <= DeadZone )
                    return;

                // Calculate the total time to aim based on AimDelay (converted to seconds)
                float totalTime = ( float ) ( AimDelay / 1000.0f );

                // Normalize AimSmoothing (affects how smooth the movements are)
                float smoothingFactor = Mathf.Clamp( ( float ) AimSmoothing / 100.0f, 0.1f, 1.0f );

                // Normalize AimSpeed (controls how fast the aim moves)
                float speedFactor = Mathf.Clamp( ( float ) AimSpeed / 100.0f, 0.1f, 1.0f );

                // Recoil compensation velocity for smoothing
                float recoilVelocityX = 0f;
                float recoilVelocityY = 0f;
                float smoothTime = 0.1f; // Controls how fast the smoothing happens

                // Check if anti-recoil is enabled, if so, start the anti-recoil thread
                if ( AntiRecoilX > 0 || AntiRecoilY > 0 )
                {
                    isRecoilActive = true;

                    // Start the recoil task
                    recoilTask = Task.Run( () =>
                    {
                        int shootKey = MouseInput.VK_LBUTTON;
                        float deltaTime = 0f;

                        while ( !MouseInput.IsKeyHeld( ref shootKey ) )
                        {
                            Utils.Watch.MicroSleep( 1 );
                            if ( recoilToken.IsCancellationRequested )
                                return;  // Exit task if cancelled
                        }

                        while ( MouseInput.IsKeyHeld( ref shootKey ) && !recoilToken.IsCancellationRequested )
                        {
                            // Recalculate recoil compensation based on antiRecoilX/Y values and time
                            recoilCompensationX = ( float ) ( antiRecoilX * Mathf.SmootherStep( 0, 1, deltaTime ) );
                            recoilCompensationY = ( float ) ( antiRecoilY * Mathf.SmootherStep( 0, 1, deltaTime ) );

                            // Increment deltaTime to gradually reduce recoil over time
                            deltaTime += 0.1f;

                            // Sleep before next update
                            Utils.Watch.MicroSleep( 50 );
                        }

                        // Reset recoil compensation when the task finishes
                        recoilCompensationX = 0f;
                        recoilCompensationY = 0f;
                        isRecoilActive = false;

                    }, recoilToken );  // Pass the token into the task
                }

                Stopwatch stopwatch = Stopwatch.StartNew();

                // Time-based loop for moving the aim over time
                while ( stopwatch.Elapsed.TotalSeconds < totalTime && Utils.MouseInput.IsKeyHeld( ref this.aimKey ) )
                {
                    // Calculate the progress based on elapsed time
                    float timeElapsed = ( float ) stopwatch.Elapsed.TotalSeconds;

                    // Adjust t (interpolation factor) by time and smoothing, with speed adjustment
                    float t = Mathf.SmootherStep( 0, 1, ( timeElapsed / totalTime ) * smoothingFactor ); // Smoothing factor applied here

                    // Smoothly interpolate the position using AimSpeed to control overall rate
                    float smoothX = Mathf.Lerp( middle.X, targetPos.X, t * speedFactor ); // Speed factor applied here
                    float smoothY = Mathf.Lerp( middle.Y, targetPos.Y, t * speedFactor ); // Speed factor applied here

                    // Calculate the delta movement
                    float deltaX = smoothX - middle.X;
                    float deltaY = smoothY - middle.Y;

                    // Apply recoil compensation if active
                    if ( isRecoilActive )
                    {
                        Mathf.SmoothDamp( ref deltaX, deltaX + recoilCompensationX, ref recoilVelocityX, smoothTime, timeElapsed );
                        Mathf.SmoothDamp( ref deltaY, deltaY + recoilCompensationY, ref recoilVelocityY, smoothTime, timeElapsed );
                    }

                    // Move the mouse cursor by the calculated delta
                    MouseInput.MoveRelativeMouse( ref deltaX, ref deltaY );

                    // Update middle to reflect the new position
                    middle.X += deltaX;
                    middle.Y += deltaY;

                    // Check if we're within the DeadZone
                    double distance = Utils.Mathf.GetDistance<double>( ref middle, ref targetPos );
                    if ( distance <= DeadZone )
                        break;

                    // Adjust delay dynamically based on AimSpeed for better control
                    double adjustedDelay = Mathf.Lerp( 5, 50, 1 - speedFactor ); // Faster speeds have shorter delays
                    Utils.Watch.MicroSleep( adjustedDelay );
                }

                // Final precise movement to the target if necessary
                float finalMoveX = targetPos.X - middle.X;
                float finalMoveY = targetPos.Y - middle.Y;
                MouseInput.MoveRelativeMouse( ref finalMoveX, ref finalMoveY );

                // Cancel the recoil task if still running
                if ( recoilTask != null && !recoilTask.IsCompleted )
                {
                    recoilTokenSource.Cancel();  // Signal task to cancel
                    recoilTask.Wait();  // Wait for the task to finish
                    recoilTask.Dispose();
                    recoilTokenSource.Dispose();
                }
            }
        }





        /// <summary>
        /// Main loop for the aimbot.
        /// </summary>
        internal void StartAimBot()
        {
            Bitmap? screenCap = null;
            int originalAimRad = PlayerData.GetAimRad();

            nint hThread = WinApi.GetCurrentThread();
            nint originalAffinity = WinApi.SetThreadAffinityMask( hThread, 1 );
            if ( originalAffinity == 0 )
            {
                throw new InvalidOperationException( "Failed to set thread affinity mask." );
            }


            while ( !this.cancellation.Token.IsCancellationRequested )
            {
                if ( PlayerData.GetAimRad() != originalAimRad &&
                    screenCap != null )
                {
                    screenCap.Dispose();
                    screenCap = null;
                }

                ScreenCap.CaptureAndFilter( ref screenCap );

                if ( screenCap == null )
                {
                    continue;
                }

#if DEBUG                
                EnemyScanner.ScanForEnemy( ref this.logger!, ref screenCap, this.AimLocation, out PointF targetPos );
#else
                EnemyScanner.ScanForEnemy( ref screenCap, this.aimLocation, out PointF targetPos );
#endif

                if ( targetPos.X == -1 && targetPos.Y == -1 || targetPos.X == 0 && targetPos.Y == 0 )
                {
                    continue;
                }

                if ( MouseInput.IsKeyHeld( ref this.aimKey ) )
                {
                    if ( !Humanize )
                    {
                        AimAtTarget( ref targetPos );
                    } else
                    {
                        AimUsingBezier( ref targetPos );
                    }
                }
            }

            screenCap?.Dispose();
            WinApi.SetThreadAffinityMask( hThread, originalAffinity );
        }


        /// <summary>
        /// main entry point for the aimbot
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="screenCap"></param>
#if DEBUG
        internal void Start( ref Logger logger, PInvoke.RECT rect )
        {
            this.Logger = logger;
            this.gameRect = rect;
            ScreenCap.Logger = logger;

            this.centerOfGameWindow = new Point
            {
                X = ( this.gameRect.right - this.gameRect.left ) / 2,
                Y = ( this.gameRect.bottom - this.gameRect.top ) / 2
            };

            logger.Log( "Settings: " );
            logger.Log( "Aim Speed: " + this.aimSpeed );
            logger.Log( "Aim Delay: " + this.aimDelay );
            logger.Log( "Aim Key: " + this.aimKey );
            logger.Log( "Aim Smoothing: " + this.aimSmoothing );
            logger.Log( "Anti-Recoil X: " + this.antiRecoilX );
            logger.Log( "Anti-Recoil Y: " + this.antiRecoilY );
            logger.Log( "Aim Location: " + this.aimLocation );
            logger.Log( "Humanize: " + this.humanize );
            logger.Log( "Dead Zone: " + this.deadZone );


            this.aimBotThread = new Thread( StartAimBot );
            this.aimBotThread.Start();

            logger.Log( "Aimbot started" );
        }
#else
        internal void Start( PInvoke.RECT rect )
        {

            this.gameRect = rect;

            this.centerOfGameWindow = new Point
            {
                X = ( this.gameRect.right - this.gameRect.left ) / 2,
                Y = ( this.gameRect.bottom - this.gameRect.top ) / 2
            };

            this.aimBotThread = new Thread( StartAimBot );
            this.aimBotThread.Start();
        }
#endif

        // Properties with lock and wait
        internal double AimSpeed
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return aimSpeed;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AimSpeed getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        aimSpeed = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AimSpeed setter." );
                }
            }
        }

        internal double AimSmoothing
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return aimSmoothing;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AimSmoothing getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        aimSmoothing = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AimSmoothing setter." );
                }
            }
        }

        internal double AntiRecoilX
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return antiRecoilX;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AntiRecoilX getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        antiRecoilX = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AntiRecoilX setter." );
                }
            }
        }

        internal double AntiRecoilY
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return antiRecoilY;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AntiRecoilY getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        antiRecoilY = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AntiRecoilY setter." );
                }
            }
        }

        internal AimLocation AimLocation
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return aimLocation;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AimLocation getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        aimLocation = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AimLocation setter." );
                }
            }
        }

        internal double AimDelay
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return aimDelay;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AimDelay getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        aimDelay = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AimDelay setter." );
                }
            }
        }

        internal int AimKey
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return aimKey;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for AimKey getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        aimKey = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for AimKey setter." );
                }
            }
        }

        internal bool Humanize
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return humanize;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for Humanize getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        humanize = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for Humanize setter." );
                }
            }
        }

        internal int DeadZone
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return deadZone;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for DeadZone getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        deadZone = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for DeadZone setter." );
                }
            }
        }


        internal Logger Logger
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return logger!;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for Logger getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        logger = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for Logger setter." );
                }
            }
        }


        internal PInvoke.RECT GameRect
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return gameRect;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                }
                throw new TimeoutException( "Failed to acquire lock for GameRect getter." );
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        gameRect = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    throw new TimeoutException( "Failed to acquire lock for GameRect setter." );
                }
            }
        }


        /// <summary>
        /// Stops the aimbot.
        /// </summary>
        internal void Stop()
        {
            this.cancellation.Cancel();
            this.aimBotThread!.Join();

#if DEBUG
            logger!.Log( "Aimbot stopped" );
#endif
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed && disposing && aimBotThread != null && aimBotThread.IsAlive )
            {
                cancellation.Cancel();
                aimBotThread.Join();
                cancellation.Dispose();
            }
            disposed = true;
        }
    }
}


