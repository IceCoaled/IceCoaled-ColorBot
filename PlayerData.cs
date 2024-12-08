using System.Diagnostics.CodeAnalysis;



namespace SCB
{
    /// <summary>
    /// Public delegate for a player data update callback event.
    /// </summary>
    /// <typeparam name="T"> this will always just be object, its the easiest way to pass generic variables in events</typeparam>
    /// <param name="sender">Null</param>
    /// <param name="e">Event args</param>
    public delegate void PlayerDataChangedEventHandler( object sender, PlayerUpdateCallbackEventArgs e );


    /// <summary>
    /// Represents event data for a player data update callback, containing the updated variable and its name.
    /// </summary>
    /// <typeparam name="T">The type of the updated variable.</typeparam>
    public sealed class PlayerUpdateCallbackEventArgs( dynamic updatedVar, UpdateType key ) : EventArgs
    {
        /// <summary>
        /// Gets the updated variable.
        /// </summary>
        public dynamic UpdatedVar { get; private set; } = updatedVar;

        /// <summary>
        /// Gets the update type.
        /// </summary>
        public UpdateType Key { get; private set; } = key;
    }





    /// <summary>
    /// Static class to store and manage player-related data such as aim settings, window properties, and control points.
    /// </summary>
    internal static class PlayerData
    {
        public static event PlayerDataChangedEventHandler? OnUpdate;
        private static readonly object locker = new();

        private static double localAimSpeed = 0;
        private static int localDeadzone = 0;
        private static double localAimSmoothing = 0;
        private static int localAimFov = 0;
        private static int localAimKey = 0;
        private static bool localAntiRecoil = false;
        private static bool localPrediction = false;
        private static float localMouseSens = 0f;
        private static float localAdsScale = 0f;
        private static nint localHWnd = nint.MaxValue;
        private static PInvoke.RECT localRect = new();
        private static string localOutlineColor = "";
        private static AimLocation localAimLocation;
        private static bool localBezierControlPointsSet = false;
        private static Utils.BezierPointCollection localBezierCollection = new( new PointF(), new PointF(), new List<PointF>() );


        /// <summary>
        /// This method is used to check if the player data is set before starting the aimBot.
        /// </summary>
        internal static bool PreStartDataCheck() =>
            localAimSpeed != 0 &&
            localDeadzone != 0 &&
            localAimSmoothing != 0 &&
            localAimKey != 0 &&
            localHWnd != nint.MaxValue &&
            localAimFov != 0 &&
            localOutlineColor != "";



        /// <summary>
        /// Unified method to handle thread-safe PlayerData updates.
        /// I added this for future-proofing, in case we need to add more complex logic to the update process.
        /// This way you only need to update the logic in one place.
        /// </summary>
        private static void UpdatePlayerData<T>( Func<T> updateFunc, UpdateType key )
        {
            lock ( locker )
            {
                OnUpdate?.Invoke( null!, new PlayerUpdateCallbackEventArgs( updateFunc()!, key ) );
            }
        }



        /// <summary>
        /// Sets the aim speed and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAimSpeed( double aimSpeed )
        {
            UpdatePlayerData( () =>
            {
                localAimSpeed = aimSpeed;
                return localAimSpeed;
            }, UpdateType.AimSpeed );
        }

        /// <summary>
        /// Sets the deadzone and updates the AimBot if necessary.
        /// </summary>
        internal static void SetDeadzone( int deadzone )
        {
            UpdatePlayerData( () =>
            {
                localDeadzone = deadzone;
                return localDeadzone;
            }, UpdateType.Deadzone );
        }

        /// <summary>
        /// Sets the aim smoothing and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAimSmoothing( double aimSmoothing )
        {
            UpdatePlayerData( () =>
            {
                localAimSmoothing = aimSmoothing;
                return localAimSmoothing;
            }, UpdateType.AimSmoothing );
        }

        /// <summary>
        /// Sets the aim FOV.
        /// </summary>
        internal static void SetAimFov( int aimFov )
        {
            UpdatePlayerData( () =>
            {
                localAimFov = aimFov;
                return localAimFov;
            }, UpdateType.AimFov );
        }


        /// <summary>
        /// Sets the aim key and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAimKey( int aimKey )
        {
            UpdatePlayerData( () =>
            {
                localAimKey = aimKey;
                return localAimKey;
            }, UpdateType.AimKey );
        }

        /// <summary>
        /// Sets the anti-recoil flag and updates the AimBot if necessary.
        /// </summary>
        internal static void SetAntiRecoil( bool antiRecoil )
        {
            UpdatePlayerData( () =>
            {
                localAntiRecoil = antiRecoil;
                return localAntiRecoil;
            }, UpdateType.AntiRecoil );
        }


        /// <summary>
        /// Sets the anti-recoil flag and updates the AimBot if necessary.
        /// </summary>
        internal static void SetPrediction( bool prediction )
        {
            UpdatePlayerData( () =>
            {
                localPrediction = prediction;
                return localPrediction;
            }, UpdateType.Prediction );
        }

        /// <summary>
        ///  Sets the aim location and updates the aimbot if necessary.   
        /// </summary>
        internal static void SetAimLocation( AimLocation aimlocation )
        {
            UpdatePlayerData( () =>
            {
                localAimLocation = aimlocation;
                return localAimLocation;
            }, UpdateType.AimLocation );
        }

        /// <summary>
        /// Set the in game mouse sensitivity.
        /// </summary>
        /// <param name="mouseSens"></param>
        internal static void SetMouseSens( float mouseSens )
        {
            UpdatePlayerData( () =>
            {
                localMouseSens = mouseSens;
                return localMouseSens;
            }, UpdateType.MouseSens );
        }


        /// <summary>
        /// Sets in game ads scale.
        /// </summary>
        /// <param name="adsScale"></param>
        internal static void SetAdsScale( float adsScale )
        {
            UpdatePlayerData( () =>
            {
                localAdsScale = adsScale;
                return localAdsScale;
            }, UpdateType.AdsScale );
        }

        /// <summary>
        /// Unified getter for AimBot-related settings.
        /// </summary>
        internal static (double aimSpeed, int deadzone, double aimSmoothing, int aimKey, AimLocation location, bool prediction, bool antiRecoil, int deadZone, float mouseSens, float adsScale) GetAimSettings()
        {
            lock ( locker )
            {
                return (localAimSpeed, localDeadzone, localAimSmoothing, localAimKey, localAimLocation, localPrediction, localAntiRecoil, localDeadzone, localMouseSens, localAdsScale);
            }
        }

        /// <summary>
        /// Sets the Bezier curve control points for aim interpolation.
        /// </summary>
        internal static void SetBezierPoints( Utils.BezierPointCollection bezierPoints )
        {
            UpdatePlayerData( () =>
            {
                localBezierCollection = bezierPoints;
                localBezierControlPointsSet = true;
                return localBezierCollection;
            }, UpdateType.BezierPoints );
        }

        /// <summary>
        /// Gets the Bezier control points for aim interpolation.
        /// </summary>
        internal static Utils.BezierPointCollection GetBezierPoints()
        {
            lock ( locker )
            {
                if ( localBezierControlPointsSet )
                {
                    return localBezierCollection;
                }
                ErrorHandler.HandleException( new Exception( "Bezier control points not set" ) );
            }

            // This line should never be reached, but the compiler requires a return value.
            return localBezierCollection;
        }


        /// <summary>
        /// Checks if the Bezier control points have been set.
        /// </summary>
        internal static bool BezierControlPointsSet()
        {
            lock ( locker )
            {
                return localBezierControlPointsSet;
            }
        }

        /// <summary>
        /// Get color tolerance name.
        /// <summary>
        internal static string GetOutlineColor()
        {
            lock ( locker )
            {
                return localOutlineColor;
            }
        }


        /// <summary>
        /// Set color tolerance name.
        /// </summary>
        /// <param name="colorToleranceName"></param>
        internal static void SetOutlineColor( string outlineColor )
        {
            UpdatePlayerData( () =>
            {
                localOutlineColor = outlineColor;
                return localOutlineColor;
            }, UpdateType.OutlineColor );
        }



        // Additional methods for window handle, game rectangle, etc., can remain unchanged

        /// <summary>
        /// Sets the game window rectangle (RECT).
        /// </summary>
        /// <param name="rect">The new window rectangle.</param>
        internal static void SetRect( PInvoke.RECT rect )
        {
            UpdatePlayerData( () =>
            {
                localRect = rect;
                return localRect;
            }, UpdateType.WindowRect );

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
            UpdatePlayerData( () =>
            {
                localHWnd = hWnd;
                return localHWnd;
            }, UpdateType.Hwnd );
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

        internal static int GetFov()
        {
            lock ( locker )
            {
                return localAimFov;
            }
        }
    }




    /// <summary>
    /// Struct that holds information about enemy data, including position, visibility, and capture time.
    /// This must be a struct so that its non nullable.
    /// </summary>
    internal struct EnemyData : IComparable
    {
        /// <summary>
        /// Gets the head position of the enemy.
        /// </summary>
        internal PointF Head { get; private set; }

        /// <summary>
        /// Gets the center position of the enemy.
        /// </summary>
        internal PointF Body { get; private set; }

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
        /// Base sleep time based of enemy distance.
        /// </summary>
        internal double SleepTime { get; private set; }

        /// <summary>
        /// Distance from crosshair to enemy head.
        /// </summary>
        internal (double toHead, double toBody) DistanceFromCenter { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnemyData"/> struct with enemy positions, capture time, and window information.
        /// </summary>
        /// <param name="head">The head position of the enemy.</param>
        /// <param name="body">The center position of the enemy.</param>
        /// <param name="captureTime">The time the data was captured.</param>
        /// <param name="windowRect">The current window rectangle.</param>
        internal EnemyData( ref PointF head, ref PointF body, ref double captureTime, ref double pixelHeight, PInvoke.RECT windowRect )
        {
            Head = head;
            Body = body;
            CaptureTime = captureTime;
            WindowRect = windowRect;
            (Distance, SleepTime) = CalculateActualDistance( pixelHeight, windowRect );
            CalculateDistance( Head, Body ); // Calculate the distance to the enemy.
        }

        /// <summary>
        /// Calculates the distance from the center of the screen to the enemy's head position.
        /// </summary>
        /// <returns>The calculated distance.</returns>
        private void CalculateDistance( PointF head, PointF body )
        {
            // Calculate the center point of the game window.
            PointF screenCenter = new( WindowRect.left + ( WindowRect.right - WindowRect.left ) / 2,
                                             WindowRect.top + ( WindowRect.bottom - WindowRect.top ) / 2 );

            // Compute the distance.
            double headDistance = Mathf.GetDistance<double>( ref screenCenter, ref head );
            double bodyDistance = Mathf.GetDistance<double>( ref screenCenter, ref body );

            DistanceFromCenter = (headDistance, bodyDistance);
        }


        /// <summary>
        /// Calculates the actual distance to the enemy based on the pixel height of the enemy in the image,
        /// and scales the pixel height based on the current window size.
        /// </summary>
        /// <param name="pixelHeight">The pixel height of the enemy.</param>
        /// <param name="windowRect">The current game window size.</param>
        /// <returns>The calculated distance in meters.</returns>
        private static (double distance, double sleepTime) CalculateActualDistance( double pixelHeight, PInvoke.RECT windowRect )
        {
            // Original resolution (2560x1440) where the pixel heights were measured
            const double originalHeight = 1440.0;

            // Known pixel heights at specific distances (measured at 2560x1440 resolution)
            var distanceData = new (double Distance, double PixelHeight)[]
            {
                (5.0, 300.0), // 5m = 342px
                (10.0, 167.0), // 10m = 167px
                (15.0, 119.0), // 15m = 119px
                (20.0, 90.0), // 20m = 90px
                (25.0, 71.0), // 25m = 71px
                (30.0, 61.0), // 30m = 61px
                (35.0, 50.0), // 35m = 50px
                (40.0, 45.0) // 40m = 45px
            };

            // Calculate the current window height based on the RECT
            double currentHeight = windowRect.bottom - windowRect.top;

            // Scale the pixel height based on the current window height compared to the original height
            double scaledPixelHeight = pixelHeight * ( originalHeight / currentHeight );

            // Find the closest distance range or extrapolate
            double distance = GetDistanceFromPixelHeight( scaledPixelHeight, distanceData );

            var sleepThresholds = new List<(double Distance, double SleepTime)>
            {
                (10, 50),   // Close range (0-10m) = 50 sleep time
                (20, 200),  // Mid-range (10-20m) = 200 sleep time
                (30, 500),  // Long-range (20-30m) = 500 sleep time
                (double.MaxValue, 1000) // Very far range (30m+) = 1000 sleep time
            };

            // Find the sleep time corresponding to the calculated distance
            double sleepTime = sleepThresholds.First( t => distance <= t.Distance ).SleepTime;

            return (distance, sleepTime);
        }

        /// <summary>
        /// Determines the actual distance based on the scaled pixel height, using interpolation or extrapolation.
        /// </summary>
        /// <param name="scaledPixelHeight">The scaled pixel height of the enemy.</param>
        /// <param name="distanceData">An array of known distances and corresponding pixel heights.</param>
        /// <returns>The interpolated or extrapolated distance.</returns>
        private static double GetDistanceFromPixelHeight( double scaledPixelHeight, (double Distance, double PixelHeight)[] distanceData )
        {
            // Check if the scaled pixel height is larger than the closest point (meaning distance is less than 5m)
            if ( scaledPixelHeight >= distanceData[ 0 ].PixelHeight )
            {
                return distanceData[ 0 ].Distance;
            }

            // Loop through the data points to find the range
            for ( int i = 0; i < distanceData.Length - 1; i++ )
            {
                if ( scaledPixelHeight <= distanceData[ i ].PixelHeight && scaledPixelHeight > distanceData[ i + 1 ].PixelHeight )
                {
                    // Interpolate between the two data points
                    return Mathf.Lerp(
                        distanceData[ i ].Distance,
                        distanceData[ i + 1 ].Distance,
                        Mathf.InverseLerp( distanceData[ i ].PixelHeight, distanceData[ i + 1 ].PixelHeight, scaledPixelHeight )
                    );
                }
            }

            // If pixel height is smaller than the smallest known height, extrapolate beyond 40m
            double pixelHeightDiff = distanceData[ ^1 ].PixelHeight - distanceData[ ^2 ].PixelHeight;
            double distanceDiff = distanceData[ ^1 ].Distance - distanceData[ ^2 ].Distance;

            // Calculate ratio and extrapolate beyond 40m
            double ratio = ( scaledPixelHeight - distanceData[ ^1 ].PixelHeight ) / pixelHeightDiff;
            return distanceData[ ^1 ].Distance + ratio * distanceDiff;
        }

        /// <summary>
        /// Updates the enemy data with new positions, capture time, and window rectangle.
        /// </summary>
        /// <param name="head">The new head position of the enemy.</param>
        /// <param name="center">The new center position of the enemy.</param>
        /// <param name="captureTime">The new capture time.</param>
        /// <param name="windowRect">The updated window rectangle.</param>
        internal void Update( ref PointF head, ref PointF body, ref double captureTime, ref double pixelHeight, PInvoke.RECT windowRect )
        {
            Head = head;
            Body = body;
            CaptureTime = captureTime;
            WindowRect = windowRect;
            (Distance, SleepTime) = CalculateActualDistance( pixelHeight, windowRect );
            CalculateDistance( Head, Body ); // Recalculate the distance based on the updated data.
        }


        public readonly int CompareTo( object? obj )
        {
            if ( obj == null )
            {
                return 1;
            }

            if ( obj is EnemyData other )
            {
                return Distance.CompareTo( other.Distance );
            } else
            {
                ErrorHandler.HandleException( new ArgumentException( "Object is not an EnemyData" ) );
            }

            return -1;
        }

        public readonly override Boolean Equals( [NotNullWhen( true )] Object? obj )
        {
            return base.Equals( obj );
        }

        public readonly override int GetHashCode()
        {
            return base.GetHashCode();
        }

        // operator overloads

        public static bool operator ==( EnemyData left, EnemyData right )
        {
            return left.Equals( right );
        }

        public static bool operator !=( EnemyData left, EnemyData right )
        {
            return !( left == right );
        }

        public static bool operator <( EnemyData left, EnemyData right )
        {
            return left.CompareTo( right ) < 0;
        }

        public static bool operator >( EnemyData left, EnemyData right )
        {
            return left.CompareTo( right ) > 0;
        }

        public static bool operator <=( EnemyData left, EnemyData right )
        {
            return left.CompareTo( right ) <= 0;
        }

        public static bool operator >=( EnemyData left, EnemyData right )
        {
            return left.CompareTo( right ) >= 0;
        }
    }



    public enum AimLocation
    {
        head,
        body
    }


    public enum UpdateType
    {
        AimSpeed = 0xC00C00,
        Deadzone = 0xA55,
        AimSmoothing = 0xDADB0D,
        AimKey = 0xB00B5,
        AntiRecoil = 0x1E65,
        Prediction = 0xBADA55,
        AimFov = 0xBAC5075,
        BezierPoints = 0x7EA5E,
        OutlineColor = 0xFA7A55,
        AimLocation = 0xFA27ED,
        WindowRect = 0xB16E60,
        Hwnd = 0xC0FFEE,
        MouseSens = 0x5E5,
        AdsScale = 0x5CA1E,
    }

}
