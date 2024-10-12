using System.Collections.Concurrent;
using System.Diagnostics;
using Recoil;
using Utils;

namespace SCB
{
    /// <summary>
    /// Class to handle the aimbot functionality.
    /// </summary>
    internal static class AimBot
    {
        private static readonly object locker = new();
        private const int LockTimeout = 1000; // Timeout for lock waiting in milliseconds

        // private staticfields
        private static double aimSpeed = 1;
        private static double aimSmoothing = 1;
        private static bool antiRecoil = false;
        private static bool prediction = false;
        private static int aimKey = 0x01;
        private static int shootKey = Utils.MouseInput.VK_LBUTTON;
        private static int deadZone = 15;
        private static PInvoke.RECT gameRect;
        private static Point centerOfGameWindow;
        private static AimLocation aimLocation;
        private static readonly CancellationTokenSource aimBotCancellation = new();
        private static readonly CancellationTokenSource enemeyCancellation = new();
        private static Thread? aimBotThread;
        private static Thread? enemyScannerThread;

        //blocking queue for the aimbot
        private static BlockingCollection<EnemyData> enemyData;




        /// <summary>
        /// Aims at the target using a Bezier curve for smooth movement, with adjusted smoothing and speed scaling.
        /// </summary>
        /// <param name="target">The target EnemyData to aim at.</param>
        private static void AimUsingBezier( ref EnemyData target )
        {
            PointF startPos = centerOfGameWindow;

            // Get the target position based on the selected location
            PointF targetPos, originalPos;
            if ( Location == AimLocation.head )
            {
                originalPos = targetPos = target.Head;
            } else
            {
                originalPos = targetPos = target.Center;
            }

            lock ( locker )
            {
                PointF controlPoint1;
                PointF controlPoint2;

                // Check if user-selected control points are set
                if ( PlayerData.BezierControlPointsSet() )
                {
                    var (start, control1, control2, end) = PlayerData.GetBezierPoints();
                    controlPoint1 = new PointF( control1.X, control1.Y );
                    controlPoint2 = new PointF( control2.X, control2.Y );

                    // Scale and enforce control point angle limit
                    Mathf.ScaleControlPoints( ref controlPoint1, ref controlPoint2, ref startPos, ref targetPos, ref start, ref end );
                    Mathf.EnforceControlPointAngle( ref controlPoint1, ref controlPoint2, startPos, targetPos );
                } else
                {
                    // Default control points if user hasn't selected their own
                    controlPoint1 = new PointF( startPos.X + ( targetPos.X - startPos.X ) * 0.3f, startPos.Y + ( targetPos.Y - startPos.Y ) * 0.15f );
                    controlPoint2 = new PointF( startPos.X + ( targetPos.X - startPos.X ) * 0.6f, startPos.Y + ( targetPos.Y - startPos.Y ) * 0.15f );
                }

                // Use smoothing and AimSpeed to control movement time and speed
                float timeElapsed = 0;
                float totalTime = Mathf.Clamp( AimSpeed * Mathf.GetDistance<float>( ref startPos, ref targetPos ), 0.5f, 2.0f );

                // Anti-recoil setup
                int ari = 0;
                float recoilVelocityX = 0.0f, recoilVelocityY = 0.0f;
                float smoothingVelocityX = 0.0f, smoothingVelocityY = 0.0f;
                float recoilAcceleration = 0.1f;
                RecoilPattern recoilPattern = RecoilPatternProcessor.CurrentPattern;

                Stopwatch stopwatch = Stopwatch.StartNew();

                while ( timeElapsed < totalTime && Utils.MouseInput.IsKeyHeld( ref aimKey ) )
                {
                    timeElapsed = ( float ) stopwatch.Elapsed.TotalSeconds;
                    float t = Mathf.EaseInOut( 0, 1, ( timeElapsed / totalTime ) );

                    if ( Prediction )
                    {
                        enemyData.TryTake( out EnemyData enemy );
                        targetPos = enemy.PredictPos( ref originalPos, Location, target.CaptureTime );
                    }

                    // Calculate the current Bezier position
                    PointF currentPos = Mathf.BezierCubicCalc( t, ref startPos, ref controlPoint1, ref controlPoint2, ref targetPos );

                    // Calculate movement deltas
                    float deltaX = currentPos.X - startPos.X;
                    float deltaY = currentPos.Y - startPos.Y;

                    // Apply recoil compensation
                    if ( AntiRecoil && Utils.MouseInput.IsKeyHeld( ref shootKey ) )
                    {
                        if ( stopwatch.ElapsedMilliseconds >= recoilPattern.TotalTime || ari >= recoilPattern.Pattern.Count )
                        {
                            break;
                        }
                        float recoilX = recoilPattern.Pattern[ ari ].X;
                        float recoilY = recoilPattern.Pattern[ ari ].Y;

                        // Smooth recoil compensation
                        Mathf.SmoothDamp( ref deltaX, deltaX + recoilX, ref recoilVelocityX, recoilAcceleration, t );
                        Mathf.SmoothDamp( ref deltaY, deltaY + recoilY, ref recoilVelocityY, recoilAcceleration, t );
                    }

                    // Apply AimSmoothing to the final movement delta
                    float smoothingFactor = Mathf.Clamp01( ( float ) ( AimSmoothing / 100f ) );
                    Mathf.SmoothDamp( ref deltaX, deltaX, ref smoothingVelocityX, smoothingFactor, t );
                    Mathf.SmoothDamp( ref deltaY, deltaY, ref smoothingVelocityY, smoothingFactor, t );

                    // Move the mouse using relative movement
                    MouseInput.MoveRelativeMouse( ref deltaX, ref deltaY );

                    // Update the start position for the next iteration
                    startPos = currentPos;

                    // Break if within the DeadZone
                    if ( Mathf.GetDistance<int>( ref currentPos, ref targetPos ) <= DeadZone )
                    {
                        break;
                    }

                    // Increment the anti-recoil index for the next recoil value
                    ari++;

                    // Sleep for a short time (1ms intervals to match recoil pattern capture)
                    Thread.Sleep( 1 );
                }

                // Final adjustment to reach the target precisely
                float finalMoveX = targetPos.X - startPos.X;
                float finalMoveY = targetPos.Y - startPos.Y;
                MouseInput.MoveRelativeMouse( ref finalMoveX, ref finalMoveY );
            }
        }




        /// <summary>
        /// Main loop for scanning for enemies.
        /// </summary>
        private static void StartEnemyScanning()
        {
            Bitmap? screenCap = null;
            int originalAimRad = PlayerData.GetAimFov();


            //change the affinity of the thread to the third core
            nint hThread = WinApi.GetCurrentThread();
            nint originalAffinity = WinApi.SetThreadAffinityMask( hThread, ( int ) ThreadAffinities.enemyScan );
            if ( originalAffinity == 0 )
            {
                ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to set thread affinity mask." ) );
            }

            while ( !enemeyCancellation.Token.IsCancellationRequested )
            {

                // Null out bitmap if the aim fov has changed
                if ( PlayerData.GetAimFov() != originalAimRad &&
                    screenCap != null )
                {
                    screenCap.Dispose();
                    screenCap = null;
                }


                //capture the screen and filter it
                ScreenCap.CaptureAndFilter( ref screenCap, out double captureTime );


                //if the screen capture is null, continue
                if ( screenCap == null )
                {
                    continue;
                }


                //scan for enemies
                EnemyScanner.ScanForEnemies( ref screenCap, out List<EnemyData> enemies, captureTime );

                //if there are no enemies, continue, else add them to the queue
                if ( enemies.Count == 0 )
                {
                    //do nothing, usually continue but this is the bottom of the loop
                } else
                {
                    // if more than one enemy, add to queue in order based on distance
                    if ( enemies.Count > 1 )
                    {
                        for ( var i = 0; i < enemies.Count; i++ )
                        {
                            for ( var j = i + 1; j < enemies.Count; j++ )
                            {
                                if ( enemies[ i ].Distance > enemies[ j ].Distance )
                                {
                                    (enemies[ j ], enemies[ i ]) = (enemies[ i ], enemies[ j ]);
                                }
                            }
                        }

                        foreach ( var enemy in enemies )
                        {
                            enemyData.Add( enemy );
                        }

                    } else
                    {
                        enemyData.Add( enemies[ 0 ] );
                    }
                }
            }

            //dispose of the screen capture
            screenCap?.Dispose();
            screenCap = null;

            //reset the affinity of the thread
            WinApi.SetThreadAffinityMask( hThread, originalAffinity );
        }



        /// <summary>
        /// Main loop for the aimbot.
        /// </summary>
        internal static void StartAimBot()
        {

            nint hThread = WinApi.GetCurrentThread();
            nint originalAffinity = WinApi.SetThreadAffinityMask( hThread, ( int ) ThreadAffinities.aimbot );
            if ( originalAffinity == 0 )
            {
                ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to set thread affinity mask." ) );
            }

            while ( !aimBotCancellation.Token.IsCancellationRequested )
            {
                if ( enemyData.TryTake( out EnemyData enemyPlayer ) )
                {
                    if ( enemyPlayer.Distance <= DeadZone )
                    {
                        continue;
                    }

                    // Aim at the target using the Bezier curve
                    if ( MouseInput.IsKeyHeld( ref aimKey ) )
                    {
                        AimUsingBezier( ref enemyPlayer );
                    }
                }

                // Sleep for a short time to prevent high CPU usage
                Thread.Sleep( 1 );
            }

            WinApi.SetThreadAffinityMask( hThread, originalAffinity );
        }


        /// <summary>
        /// main entry point for the aimbot
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="screenCap"></param>
        internal static void Start( PInvoke.RECT rect )
        {
            gameRect = rect;

            centerOfGameWindow = new Point
            {
                X = ( gameRect.right - gameRect.left ) / 2,
                Y = ( gameRect.bottom - gameRect.top ) / 2
            };

#if DEBUG
            Logger.Log( "Settings: " );
            Logger.Log( "Aim Speed: " + aimSpeed );
            Logger.Log( "Aim Key: " + aimKey );
            Logger.Log( "Aim Smoothing: " + aimSmoothing );
            Logger.Log( "Aim Location: " + aimLocation );
            Logger.Log( "Dead Zone: " + deadZone );
            Logger.Log( "Anti-Recoil: " + antiRecoil );
            Logger.Log( "Prediction: " + prediction );
#endif


            enemyData = new();
            enemyScannerThread = new Thread( StartEnemyScanning );
            aimBotThread = new Thread( StartAimBot );
            enemyScannerThread.Start();
            aimBotThread.Start();
#if DEBUG
            Logger.Log( "Aimbot started" );
#endif
        }


        // Properties with lock and wait
        internal static double AimSpeed
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
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimSpeed getter." ) );
                    return 0;
                }
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
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimSpeed setter." ) );
                }
            }
        }

        internal static double AimSmoothing
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
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimSmoothing getter." ) );
                    return 0;
                }
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
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimSmoothing setter." ) );
                }
            }
        }



        internal static AimLocation Location
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
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimLocation getter." ) );
                    return AimLocation.head;
                }
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
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimLocation setter." ) );
                }
            }
        }


        internal static int AimKey
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
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimKey getter." ) );
                    return 0;
                }
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
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for AimKey setter." ) );
                }
            }
        }

        internal static bool AntiRecoil
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return antiRecoil;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for Anti-Recoil getter." ) );
                    return false;
                }
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        antiRecoil = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for Anti-Recoil setter." ) );
                }
            }
        }


        internal static bool Prediction
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return prediction;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for Prediction getter." ) );
                    return false;
                }
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        prediction = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for Prediction setter." ) );
                }
            }
        }

        internal static int DeadZone
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
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for DeadZone getter." ) );
                    return 0;
                }
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
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for DeadZone setter." ) );
                }
            }
        }

        internal static PInvoke.RECT GameRect
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
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for GameRect getter." ) );
                    return new PInvoke.RECT();
                }
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
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for GameRect setter." ) );
                }
            }
        }


        /// <summary>
        /// Stops the aimbot.
        /// </summary>
        internal static void Stop()
        {
            aimBotCancellation.Cancel();
            enemeyCancellation.Cancel();
            aimBotCancellation.Cancel();
            enemeyCancellation.Cancel();

#if DEBUG
            Logger.Log( "Aimbot stopped" );
#endif
        }



        /// <summary>
        /// Cleans up the aimbot.
        /// </summary>
        internal static void CleanUp()
        {
            if ( enemyData != null )
            {
                enemyData.Dispose();
            }

            if ( enemyScannerThread != null )
            {
                enemeyCancellation.Cancel();
                enemyScannerThread.Join();
            }

            if ( aimBotThread != null )
            {
                aimBotCancellation.Cancel();
                aimBotThread.Join();
            }

            aimBotCancellation.Dispose();
            enemeyCancellation.Dispose();
        }
    }
}


