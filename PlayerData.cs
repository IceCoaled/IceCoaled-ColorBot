using System.Runtime.CompilerServices;
using SCB.DirectX;



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

        private static float localAimSpeed = 0;
        private static float localDeadzone = 0;
        private static float localAimSmoothing = 0;
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
        private static Utils.BezierPointCollection localBezierCollection = new();


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
        internal static void SetAimSpeed( float aimSpeed )
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
        internal static void SetAimSmoothing( float aimSmoothing )
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
        internal static (float aimSpeed, float deadZone, float aimSmoothing, int aimKey, AimLocation location, bool prediction, bool antiRecoil, float mouseSens, float adsScale) GetAimSettings()
        {
            lock ( locker )
            {
                return (localAimSpeed, localDeadzone, localAimSmoothing, localAimKey, localAimLocation, localPrediction, localAntiRecoil, localMouseSens, localAdsScale);
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
    internal readonly struct EnemyData
    {
        /// Head position of the enemy.
        internal PointF Head { get; init; }

        /// Torso position of the enemy.
        internal PointF Body { get; init; }

        /// Time screenshot was taken.
        internal double CaptureTime { get; init; }

        /// Ddistance to the enemy's head from the player's crosshair.
        internal double UserToHead { get; init; }

        /// Distance to the enemy's body from the player's crosshair.
        internal double UserToBody { get; init; }

        internal UInt4x2 BoundingBox { get; init; }


        /// <summary>
        /// Initializes a new instance of the <see cref="EnemyData"/> struct with enemy positions, capture time, and window information.
        /// </summary>
        /// <param name="head">The head position of the enemy.</param>
        /// <param name="body">The center position of the enemy.</param>
        /// <param name="captureTime">The time the data was captured.</param>
        /// <param name="gameRect">The game window's rectangle.</param>
        internal EnemyData( PointF head, PointF body, ref double captureTime, ref UInt4x2 bb, ref PInvoke.RECT gameRect )
        {
            Head = head;
            Body = body;
            CaptureTime = captureTime;
            GetWindowCenter( out PointF center, ref gameRect );
            UserToHead = Mathf.GetDistance<double>( ref center, ref head );
            UserToBody = Mathf.GetDistance<double>( ref center, ref body );
            BoundingBox = bb;
        }

        /// <summary>
        /// Calculates the distance from the center of the screen to the enemy's head position.
        /// </summary>
        /// <returns>The calculated distance.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private static void GetWindowCenter( out PointF windowCenter, ref PInvoke.RECT gameRect )
        {
            // Calculate the center point of the game window.
            windowCenter = new( gameRect.left + ( ( gameRect.right - gameRect.left ) / 2 ),
                                gameRect.top + ( ( gameRect.bottom - gameRect.top ) / 2 ) );
        }


        public static bool operator >( EnemyData a, EnemyData b ) => a.UserToHead > b.UserToHead;
        public static bool operator <( EnemyData a, EnemyData b ) => a.UserToHead < b.UserToHead;
        public static bool operator >( EnemyData a, double b ) => a.UserToHead > b;
        public static bool operator <( EnemyData a, double b ) => a.UserToHead < b;
        public static bool operator >( double a, EnemyData b ) => a > b.UserToHead;
        public static bool operator <( double a, EnemyData b ) => a < b.UserToHead;
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
