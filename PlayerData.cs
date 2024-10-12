using Utils;



namespace SCB
{
    /// <summary>
    /// Static class to store and manage player-related data such as aim settings, window properties, and control points.
    /// </summary>
    internal static class PlayerData
    {
        private static readonly object locker = new();  // Unified lock object for thread safety

        private static double localAimSpeed = 0;
        private static int localDeadzone = 0;
        private static double localAimSmoothing = 0;
        private static int localAimFov = 0;
        private static int localAimKey = 0;
        private static bool localAntiRecoil = false;
        private static bool localPrediction = false;
        private static nint localHWnd = nint.MaxValue;
        private static PInvoke.RECT localRect = new();
        private static ColorTolerance? localColorTolerance = null;
        private static ColorTolerance? localTanCarrier;
        private static ColorTolerance? localBrownCarrier;
        private static AimLocation localAimLocation;
        private static PointF bezierStartPoint = new();
        private static PointF bezierControlPoint1 = new();
        private static PointF bezierControlPoint2 = new();
        private static PointF bezierEndPoint = new();
        private static bool bezierControlPointsSet = false;

        /// <summary>
        /// Unified method to handle thread-safe access and updates to AimBot settings.
        /// </summary>
        private static void UpdateAimBotIfNeeded( Action updateAction )
        {
            lock ( locker )
            {
                updateAction.Invoke();
            }
        }


        /// <summary>
        /// Sets the aim speed and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAimSpeed( double aimSpeed )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localAimSpeed != aimSpeed )
                {
                    localAimSpeed = aimSpeed;
                    AimBot.AimSpeed = aimSpeed;
                }
            } );
        }

        /// <summary>
        /// Sets the deadzone and updates the AimBot if necessary.
        /// </summary>
        internal static void SetDeadzone( int deadzone )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localDeadzone != deadzone )
                {
                    localDeadzone = deadzone;
                    AimBot.DeadZone = deadzone;
                }
            } );
        }

        /// <summary>
        /// Sets the aim smoothing and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAimSmoothing( double aimSmoothing )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localAimSmoothing != aimSmoothing )
                {
                    localAimSmoothing = aimSmoothing;
                    AimBot.AimSmoothing = aimSmoothing;
                }
            } );
        }

        /// <summary>
        /// Sets the aim FOV.
        /// </summary>
        internal static void SetAimFov( int aimFov )
        {
            lock ( locker )
            {
                localAimFov = aimFov;
            }
        }


        /// <summary>
        /// Gets the aim FOV.
        /// </summary>
        internal static int GetAimFov()
        {
            lock ( locker )
            {
                if ( localAimFov > 0 )
                {

                    return localAimFov;
                }
                ErrorHandler.HandleException( new Exception( "No aim FOV selected" ) );
            }

            // This line should never be reached, but the compiler requires a return value.
            return localAimFov;
        }

        /// <summary>
        /// Sets the aim key and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAimKey( int aimKey )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localAimKey != aimKey )
                {
                    localAimKey = aimKey;
                    AimBot.AimKey = aimKey;
                }
            } );

            ErrorHandler.PrintToStatusBar( $"Aim Key: {aimKey}" );
        }

        /// <summary>
        /// Sets the anti-recoil flag and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAntiRecoil( bool isEnabled )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localAntiRecoil != isEnabled )
                {
                    localAntiRecoil = isEnabled;
                    AimBot.AntiRecoil = isEnabled;
                }
            } );
        }


        /// <summary>
        /// Sets the anti-recoil flag and updates the AimBot if necessary.
        /// </summary>
        internal static void SetPrediction( bool isEnabled )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localPrediction != isEnabled )
                {
                    localPrediction = isEnabled;
                    AimBot.Prediction = isEnabled;
                }
            } );
        }

        /// <summary>
        ///  Sets the aim location and updates the aimbot if necessary.   
        /// </summary>
        internal static void SetAimLocation( AimLocation location )
        {
            UpdateAimBotIfNeeded( () =>
            {
                if ( localAimLocation != location )
                {
                    localAimLocation = location;
                    AimBot.Location = location;
                }
            } );
        }

        /// <summary>
        /// Unified getter for AimBot-related settings.
        /// </summary>
        internal static (double aimSpeed, int deadzone, double aimSmoothing, int aimKey, AimLocation location, bool prediction, bool antiRecoil) GetAimSettings()
        {
            lock ( locker )
            {
                return (localAimSpeed, localDeadzone, localAimSmoothing, localAimKey, localAimLocation, localPrediction, localAntiRecoil);
            }
        }

        /// <summary>
        /// Sets the Bezier curve control points for aim interpolation.
        /// </summary>
        internal static void SetBezierPoints( PointF startPoint, PointF controlPoint1, PointF controlPoint2, PointF endPoint )
        {
            lock ( locker )
            {
                bezierStartPoint = startPoint;
                bezierControlPoint1 = controlPoint1;
                bezierControlPoint2 = controlPoint2;
                bezierEndPoint = endPoint;
                bezierControlPointsSet = true;
            }
        }

        /// <summary>
        /// Gets the Bezier control points for aim interpolation.
        /// </summary>
        internal static (PointF start, PointF control1, PointF control2, PointF end) GetBezierPoints()
        {
            lock ( locker )
            {
                if ( bezierControlPointsSet )
                {
                    return (bezierStartPoint, bezierControlPoint1, bezierControlPoint2, bezierEndPoint);
                }
                ErrorHandler.HandleException( new Exception( "Bezier control points not set" ) );
            }

            // This line should never be reached, but the compiler requires a return value.
            return (bezierStartPoint, bezierControlPoint1, bezierControlPoint2, bezierEndPoint);
        }


        /// <summary>
        /// Checks if the Bezier control points have been set.
        /// </summary>
        internal static bool BezierControlPointsSet()
        {
            lock ( locker )
            {
                return bezierControlPointsSet;
            }
        }

        /// <summary>
        /// Sets the color tolerance for aim calculations.
        /// </summary>
        internal static void SetColorTolerance( string userSelected )
        {
            const string bPC = "brownPlateCarrier";
            const string tPC = "tanPlateCarrier";
            lock ( locker )
            {
                if ( localColorTolerance == null )
                {
                    localColorTolerance = ColorTolerances.GetColorTolerance( userSelected );
                    localTanCarrier = ColorTolerances.GetColorTolerance( tPC );
                    localBrownCarrier = ColorTolerances.GetColorTolerance( bPC );
                } else
                {
                    localColorTolerance = ColorTolerances.GetColorTolerance( userSelected );
                }

            }
        }

        /// <summary>
        /// Gets the current color tolerance.
        /// </summary>
        internal static ColorTolerance GetColorTolerance()
        {

            lock ( locker )
            {
                if ( localColorTolerance != null )
                {
                    return localColorTolerance;
                }
                ErrorHandler.HandleException( new Exception( "No color tolerance selected" ) );
            }

            // This line should never be reached, but the compiler requires a return value.
            return localColorTolerance;
        }


        internal static (ColorTolerance userSelected, ColorTolerance tanCarrier, ColorTolerance brownCarrier) GetColorTolerances()
        {
            lock ( locker )
            {
                if ( localColorTolerance != null && localTanCarrier != null && localBrownCarrier != null )
                {
                    return (localColorTolerance, localTanCarrier, localBrownCarrier);
                }
                ErrorHandler.HandleException( new Exception( "No color tolerances selected" ) );
            }

            // This line should never be reached, but the compiler requires a return value.
            return (localColorTolerance, localTanCarrier, localBrownCarrier);
        }


        // Additional methods for window handle, game rectangle, etc., can remain unchanged

        /// <summary>
        /// Sets the game window rectangle (RECT).
        /// </summary>
        /// <param name="rect">The new window rectangle.</param>
        internal static void SetRect( PInvoke.RECT rect )
        {
            lock ( locker )
            {
                localRect = rect;
            }
        }

        /// <summary>
        /// Gets the current game window rectangle (RECT).
        /// </summary>
        /// <returns>The current game window rectangle.</returns>
        internal static PInvoke.RECT GetRect()
        {
            lock ( locker )
            {
                if ( localRect.left != 0 && localRect.right != 0 && localRect.top != 0 && localRect.bottom != 0 )
                {
                    return localRect;
                }

            }

            return localRect;
        }

        /// <summary>
        /// Sets the window handle (HWND) for the game.
        /// </summary>
        /// <param name="hWnd">The new window handle.</param>
        internal static void SetHwnd( nint hWnd )
        {
            lock ( locker )
            {
                localHWnd = hWnd;
            }
        }

        /// <summary>
        /// Gets the current window handle (HWND).
        /// </summary>
        /// <returns>The current window handle.</returns>
        internal static nint GetHwnd()
        {
            lock ( locker )
            {
                return localHWnd;
            }
        }
    }




    /// <summary>
    /// Struct that holds information about enemy data, including position, visibility, and capture time.
    /// </summary>
    internal struct EnemyData
    {
        /// <summary>
        /// Gets the head position of the enemy.
        /// </summary>
        internal PointF Head { get; private set; }

        /// <summary>
        /// Gets the center position of the enemy.
        /// </summary>
        internal PointF Center { get; private set; }

        /// <summary>
        /// Gets the time at which the enemy's data was captured.
        /// </summary>
        internal double CaptureTime { get; private set; }

        /// <summary>
        /// Gets the current game window size and position.
        /// </summary>
        internal PInvoke.RECT WindowRect { get; private set; }

        /// <summary>
        /// Gets the distance of the enemy from the player.
        /// </summary>
        internal double Distance { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnemyData"/> struct with enemy positions, capture time, and window information.
        /// </summary>
        /// <param name="head">The head position of the enemy.</param>
        /// <param name="center">The center position of the enemy.</param>
        /// <param name="captureTime">The time the data was captured.</param>
        /// <param name="windowRect">The current window rectangle.</param>
        internal EnemyData( ref PointF head, ref PointF center, ref double captureTime, ref PInvoke.RECT windowRect )
        {
            Head = head;
            Center = center;
            CaptureTime = captureTime;
            WindowRect = windowRect;
            Distance = CalculateDistance(); // Calculate the distance to the enemy.
        }

        /// <summary>
        /// Calculates the distance from the center of the screen to the enemy's head position.
        /// </summary>
        /// <returns>The calculated distance.</returns>
        private readonly double CalculateDistance()
        {
            // Calculate the center point of the game window.
            PointF screenCenter = new( WindowRect.left + ( WindowRect.right - WindowRect.left ) / 2,
                                             WindowRect.top + ( WindowRect.bottom - WindowRect.top ) / 2 );

            // Use a hypothetical utility function (Mathf.GetDistance) to compute the distance.
            var head = Head;
            return Mathf.GetDistance<double>( ref screenCenter, ref head );
        }

        /// <summary>
        /// Updates the enemy data with new positions, capture time, and window rectangle.
        /// </summary>
        /// <param name="head">The new head position of the enemy.</param>
        /// <param name="center">The new center position of the enemy.</param>
        /// <param name="captureTime">The new capture time.</param>
        /// <param name="windowRect">The updated window rectangle.</param>
        internal void Update( ref PointF head, ref PointF center, ref double captureTime, ref PInvoke.RECT windowRect )
        {
            Head = head;
            Center = center;
            CaptureTime = captureTime;
            WindowRect = windowRect;
            Distance = CalculateDistance(); // Recalculate the distance based on the updated data.
        }

        /// <summary>
        /// Compares this instance to another instance of <see cref="EnemyData"/> based on the distance to the enemy.
        /// </summary>
        /// <param name="other">The other <see cref="EnemyData"/> instance to compare to.</param>
        /// <returns>A value indicating the relative order of the objects being compared.</returns>
        readonly internal bool CompareTo( double distance )
        {
            return Distance < distance;
        }

        readonly internal PointF PredictPos( ref PointF previousPos, AimLocation location, double previousCaptrueTime )
        {
            PointF currentPos = Head;
            if ( location == AimLocation.body )
            {
                currentPos = Center;
            }
            float timeDelta = ( float ) ( CaptureTime - previousCaptrueTime );
            return Mathf.MotionExtrapolation( currentPos, previousPos, timeDelta, 3 );
        }
    }



    enum AimLocation
    {
        head,
        body
    }

}
