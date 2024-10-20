#if DEBUG
//#define PRINTDELTAS
#endif

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

        // Lock objects for thread safety
        private static readonly object locker = new();
        private static readonly object bufferLock = new();
        private const int LockTimeout = 1000; // Timeout for lock waiting in milliseconds

        // Event for new frame captured
        private static event Action OnNewFrameCaptured;

        // Aimbot settings
        private static double aimSpeed = 1;
        private static double aimSmoothing = 1;
        private static bool antiRecoil = false;
        private static bool prediction = false;
        private static int aimKey = 0x01;
        private static int shootKey = Utils.MouseInput.VK_LBUTTON;
        private static int deadZone = 15;
        private static AimLocation aimLocation;

        // In game settings
        private static float mouseSensitivity = 0.0f;
        private static float adsMultiplier = 0.0f;

        // Aimbot variables
        private static PInvoke.RECT gameRect;
        private static PointF centerOfGameWindow;

        // Thread and cancellation token for enemy scanning
        private static readonly CancellationTokenSource enemeyCancellation = new();
        private static Thread? enemyScannerThread;

        // buffer for the aimbot
        private static readonly PingPongBuffer<EnemyData> enemyBuffer = new( 10 );  // Buffer size is 10 frames



        /// <summary>
        /// Aims at the target using a Bezier curve for smooth mouse movement, with adjusted smoothing and speed scaling.
        /// </summary>
        /// <param name="targetPos">The target position to aim at.</param>
        /// <param name="enemyDistance">The distance to the enemy to adjust speed and smoothing.</param>
        /// <param name="sleepTime">The calculated base sleep time passed in.</param>
        private static void AimUsingBezier( ref PointF targetPos, double enemyDistance, double sleepTime )
        {
            PointF startPos = centerOfGameWindow;
            Utils.BezierPointCollection bezierPoints;
            List<PointF> cursorPath;

            lock ( locker )
            {
                // Adjust factors based on distance (closer enemies = faster movements, higher smoothing)
                float distanceFactor = Mathf.SmootherStep<float>( 50, 5, ( float ) enemyDistance ); // Scale between 5m (close) to 50m (far)
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
                    List<PointF> controlPoints = new()
                    {
                        new PointF(startPos.X + (targetPos.X - startPos.X) * 0.2f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.1f)),
                        new PointF(startPos.X + (targetPos.X - startPos.X) * 0.4f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.2f)),
                        new PointF(startPos.X + (targetPos.X - startPos.X) * 0.6f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.3f)),
                        new PointF(startPos.X + (targetPos.X - startPos.X) * 0.7f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.5f)),
                        new PointF(startPos.X + (targetPos.X - startPos.X) * 0.85f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.7f)),
                        new PointF(startPos.X + (targetPos.X - startPos.X) * 0.95f, startPos.Y + ((targetPos.Y - startPos.Y) * 0.85f))
                    };

                    bezierPoints = new Utils.BezierPointCollection( startPos, targetPos, controlPoints );
                    cursorPath = bezierPoints.CalculateOcticBezierPoints( ( int ) ( ( dynamicSleepTime / distanceFactor ) * adjustedSmoothingFactor ) );
                }

                // Anti-recoil setup (if needed)
                bool activateAntiRecoil = AntiRecoil && Utils.MouseInput.IsKeyPressed( ref shootKey );
                RecoilPattern recoilPattern = RecoilPatternProcessor.CurrentPattern;
                Stopwatch recoilTimer = new();

                recoilTimer.Start();

                // Interpolate along the Bezier curve over the total time (single curve)
                for ( int i = 0; i < cursorPath.Count && Utils.MouseInput.IsKeyHeld( ref aimKey ); i++ )
                {
                    PointF currentPos = cursorPath[ i ];

                    // Calculate movement deltas
                    float deltaX = currentPos.X - startPos.X;
                    float deltaY = currentPos.Y - startPos.Y;


#if PRINTDELTAS
                    Logger.Log( $"Delta's Pre Game Compensation, DeltaX: {deltaX}, DeltaY: {deltaY}" );
#endif


                    // Apply recoil compensation
                    if ( AntiRecoil && activateAntiRecoil )
                    {
                        double elapsed = recoilTimer.Elapsed.TotalMilliseconds;
                        float recoilX = centerOfGameWindow.X - recoilPattern[ elapsed ].X;
                        float recoilY = centerOfGameWindow.Y - recoilPattern[ elapsed ].Y;

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
                    deltaX /= MouseSensitivity * AdsMultiplier;
                    deltaY /= MouseSensitivity * AdsMultiplier;


#if PRINTDELTAS
                    Logger.Log( $"Delta's Post Game Compensation, DeltaX: {deltaX}, DeltaY: {deltaY}" );
#endif


                    // Move the mouse using the smoothed deltas
                    MouseInput.MoveRelativeMouse( ref deltaX, ref deltaY );

                    // Update the start position for the next iteration
                    startPos = currentPos;

                    if ( Utils.Mathf.GetDistance<int>( ref currentPos, ref targetPos ) <= DeadZone )
                    {
                        break;
                    }

                    // Check if shoot key is still held
                    if ( activateAntiRecoil && !Utils.MouseInput.IsKeyHeld( ref shootKey ) )
                    {
                        activateAntiRecoil = false;
                    }

                    // Sleep for dynamic sleep time
                    Utils.Watch.MicroSleep( dynamicSleepTime );
                }
            }
        }





        /// <summary>
        /// Starts the background enemy scanning process, capturing and filtering screen images and scanning for enemies.
        /// Adds detected enemies to the enemy buffer and triggers new frame events.
        /// </summary>
        private static void StartEnemyScanning()
        {
            Bitmap? screenCap = null;
            int originalAimRad = PlayerData.GetAimFov();
            List<EnemyData> enemies = new();

            while ( !enemeyCancellation.Token.IsCancellationRequested )
            {

                // Null out bitmap if the aim fov has changed
                if ( PlayerData.GetAimFov() != originalAimRad &&
                    screenCap != null )
                {
                    screenCap.Dispose();
                    screenCap = null;
                }


                //capture the screen and filter, and scan for enemies
                enemies = ScreenCap.CaptureAndFilter( ref screenCap );

                //if the screen capture is null, continue
                if ( screenCap == null )
                {
                    continue;
                }

                //if there are no enemies, clear the write buffer.
                //Check if aimbot is invoked, if not, clear the read buffer
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
                        OnNewFrameCaptured?.Invoke();
                    }
                }
            }

            //dispose of the screen capture
            screenCap?.Dispose();
            screenCap = null;
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
            PointF predictedPos = Utils.Mathf.MotionExtrapolation( currentPos, previousPos, deltaTime, ( float ) extrapolationTime );

            return predictedPos;
        }



        /// <summary>
        /// Processes the latest frame of enemy data, either using prediction or the most recent enemy data directly.
        /// </summary>
        private static void ProcessNewFrame()
        {
            if ( Prediction )
            {
                // Get last few frames for prediction
                (EnemyData, EnemyData) recentFrames;
                lock ( bufferLock )
                {
                    // If the buffer does not have enough frames, return
                    if ( enemyBuffer.Count < 2 )
                    {
                        return;
                    }
                    // Retrieve the last 2 frames
                    recentFrames = enemyBuffer.GetTwoMostRecentEntries();
                }

                if ( recentFrames.Item1.CaptureTime == recentFrames.Item2.CaptureTime )
                {
                    return;
                }

                if ( ( Location == AimLocation.head ? recentFrames.Item1.DistanceFromCenter.toHead : recentFrames.Item1.DistanceFromCenter.toBody ) >= DeadZone ||
                      ( Location == AimLocation.head ? recentFrames.Item2.DistanceFromCenter.toHead : recentFrames.Item2.DistanceFromCenter.toBody ) >= DeadZone )
                {
                    // Perform prediction based on historical data
                    var targetPos = PredictEnemy( recentFrames, Location, 5.0f );

                    // Check if the target position is within the DeadZone
                    if ( Mathf.GetDistance<int>( ref targetPos, ref centerOfGameWindow ) <= DeadZone )
                    {
                        return;
                    }

                    if ( Utils.MouseInput.IsKeyPressed( ref aimKey ) )
                    {
                        AimUsingBezier( ref targetPos, recentFrames.Item2.Distance, recentFrames.Item2.SleepTime );
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
                        if ( ( Location == AimLocation.head ? enemy.DistanceFromCenter.toHead : enemy.DistanceFromCenter.toBody ) <= DeadZone )
                        {
                            return;
                        }
                    } else
                    {
                        return;
                    }
                }
                if ( Utils.MouseInput.IsKeyPressed( ref aimKey ) )
                {
                    PointF targetPos = Location == AimLocation.head ? enemy.Head : enemy.Body;
                    AimUsingBezier( ref targetPos, enemy.Distance, enemy.SleepTime );
                }
            }

            // Clear the read buffer after processing
            enemyBuffer.ClearReadBuffer();
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


            OnNewFrameCaptured += ProcessNewFrame;
            enemyScannerThread = new Thread( StartEnemyScanning );
            enemyScannerThread.Start();
#if DEBUG
            Logger.Log( "Aimbot started" );
#endif                   
        }


        /// <summary>
        /// Stops the aimbot.
        /// </summary>
        internal static void Stop()
        {
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

            if ( enemyScannerThread != null )
            {
                enemeyCancellation.Cancel();
                enemyScannerThread.Join();
            }

            enemeyCancellation.Dispose();

            OnNewFrameCaptured -= ProcessNewFrame;
#if DEBUG
            Logger.Log( "Aimbot cleaned up" );
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


        internal static float MouseSensitivity
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return mouseSensitivity;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for MouseSensitivity getter." ) );
                    return 0;
                }
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        mouseSensitivity = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for MouseSensitivity setter." ) );
                }
            }
        }


        internal static float AdsMultiplier
        {
            get
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        return adsMultiplier;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for MouseAccelerationMultiplier getter." ) );
                    return 0;
                }
            }
            set
            {
                if ( Monitor.TryEnter( locker, LockTimeout ) )
                {
                    try
                    {
                        adsMultiplier = value;
                    } finally
                    {
                        Monitor.Exit( locker );
                    }
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new TimeoutException( "Failed to acquire lock for MouseAccelerationMultiplier setter." ) );
                }
            }
        }
    }



    /// <summary>
    /// Circular buffer class to store and retrieve the most recent N items.
    /// This implementation uses a ping-pong buffer mechanism to alternate between two buffers for reading and writing.
    /// </summary>
    /// <typeparam name="T">The type of data the buffer will store. Must be unmanaged.</typeparam>
    internal unsafe class PingPongBuffer<T> where T : unmanaged
    {
        private T[] bufferA;
        private T[] bufferB;
        private T* currentWriteBufferPtr;
        private T* currentReadBufferPtr;
        private bool isReadingFromBufferA;
        private int writeIndex = 0;
        private int readIndex = 0;
        private readonly int capacity;
        private readonly object internalBufferlock = new();

        /// <summary>
        /// Gets the number of items in the read buffer.
        /// </summary>
        internal int Count => readIndex;

        /// <summary>
        /// Gets the total capacity of the buffer.
        /// </summary>
        internal int Capacity => capacity;

        /// <summary>
        /// Initializes a new instance of the PingPongBuffer class with the specified capacity.
        /// </summary>
        /// <param name="size">The maximum number of items the buffer can hold.</param>
        internal PingPongBuffer( int size )
        {
            capacity = size;
            bufferA = new T[ size ];
            bufferB = new T[ size ];
            isReadingFromBufferA = true; // Initially read from bufferA

            // Initialize pointers to the beginning of each buffer
            fixed ( T* bufferAPtr = bufferA, bufferBPtr = bufferB )
            {
                currentWriteBufferPtr = bufferAPtr;
                currentReadBufferPtr = bufferBPtr;
            }
        }

        /// <summary>
        /// Swaps the read and write buffers without copying data.
        /// </summary>
        internal void SwapBuffers()
        {
            lock ( internalBufferlock )
            {
                T* temp = currentReadBufferPtr;
                currentReadBufferPtr = currentWriteBufferPtr;
                currentWriteBufferPtr = temp;
                readIndex = writeIndex;
                writeIndex = 0;
            }

            isReadingFromBufferA = false;
        }

        /// <summary>
        /// Writes data to the current write buffer.
        /// </summary>
        /// <param name="data">The data array to be written to the buffer.</param>
        /// <exception cref="ArgumentException">Thrown if the data size exceeds the buffer capacity.</exception>
        internal void WriteBuffer( T[] data )
        {
            lock ( internalBufferlock )
            {
                if ( data.Length + writeIndex > capacity )
                {
                    ErrorHandler.HandleException( new ArgumentException( "Data size exceeds buffer capacity." ) );
                }

                // Copy data into the buffer using memory copy to handle unmanaged types.
                fixed ( T* dataPtr = data )
                {
                    Buffer.MemoryCopy( dataPtr, currentWriteBufferPtr + writeIndex, ( capacity - writeIndex ) * sizeof( T ), data.Length * sizeof( T ) );
                    writeIndex += data.Length; // Update write index
                }
            }
        }

        /// <summary>
        /// Retrieves the most recent entry in the read buffer.
        /// </summary>
        /// <returns>The most recent entry in the buffer.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the buffer is empty.</exception>
        internal T GetLatestEntry()
        {
            lock ( internalBufferlock )
            {
                if ( readIndex == 0 )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Buffer is empty" ) );
                }

                // Return the latest entry from the read buffer.
                return currentReadBufferPtr[ readIndex - 1 ];
            }
        }

        /// <summary>
        /// Retrieves the two most recent entries in the read buffer for prediction purposes.
        /// </summary>
        /// <returns>A tuple containing the two most recent entries.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the buffer does not contain at least two entries.</exception>
        internal (T, T) GetTwoMostRecentEntries()
        {
            lock ( internalBufferlock )
            {
                if ( readIndex < 2 )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Not enough data in the buffer" ) );
                }

                // Return the two most recent entries
                return (currentReadBufferPtr[ readIndex - 2 ], currentReadBufferPtr[ readIndex - 1 ]);
            }
        }

        /// <summary>
        /// Clears the current write buffer, resetting it to default values.
        /// </summary>
        internal void ClearWriteBuffer()
        {
            lock ( internalBufferlock )
            {
                for ( int i = 0; i < writeIndex; i++ )
                {
                    currentWriteBufferPtr[ i ] = default( T ); // Clear each element to default
                }
                writeIndex = 0; // Reset write index
            }
        }

        /// <summary>
        /// Clears the current read buffer, resetting it to default values.
        /// </summary>
        internal void ClearReadBuffer()
        {
            lock ( internalBufferlock )
            {
                for ( int i = 0; i < readIndex; i++ )
                {
                    currentReadBufferPtr[ i ] = default( T ); // Clear each element in the read buffer
                }
                readIndex = 0; // Reset read index
            }
        }

        /// <summary>
        /// Checks if the read buffer is empty.
        /// </summary>
        /// <returns>True if the read buffer is empty; otherwise, false.</returns>
        internal bool IsReadBufferEmpty()
        {
            lock ( internalBufferlock )
            {
                return readIndex == 0;
            }
        }

        /// <summary>
        /// Sorts the read buffer in place using the default comparer for type T.
        /// </summary>
        public void Sort()
        {
            if ( isReadingFromBufferA )
            {
                Array.Sort( bufferA, 0, readIndex ); // Sort bufferA up to the current readIndex
            } else
            {
                Array.Sort( bufferB, 0, readIndex ); // Sort bufferB up to the current readIndex
            }
        }
    }

}


