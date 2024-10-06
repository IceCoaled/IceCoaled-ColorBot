using static SCB.EnemyScanner;


//TODO: add locker checks to all methods
namespace SCB
{
    /// <summary>
    /// Class to store and manage player-related data such as aim settings, window properties, and control points.
    /// </summary>
    static class PlayerData
    {
        private static readonly object locker = new();
        private static double localAimDelay = 0;
        private static double localAimSpeed = 15;
        private static int localAimRad = 15;
        private static int loacalDeadzone = 15;
        private static bool localHumanize = false;
        private static double localAimSmoothing = 15;
        private static double antiRecoilX = 0;
        private static double antiRecoilY = 0;
        private static int localAimKey = 0x06;
        private static nint localHWnd = nint.MaxValue;
        private static PInvoke.RECT localRect = new();
        private static List<IEnumerable<int>> localColorRange = new List<IEnumerable<int>>();
        private static AimLocation aimLocation;
        private static PointF bezierStartPoint = new();
        private static PointF bezierControlPoint1 = new();
        private static PointF bezierControlPoint2 = new();
        private static PointF bezierEndPoint = new();
        private static bool bezierControlPointsSet = false;

        /// <summary>
        /// Sets the aim delay and updates the AimBot if necessary.
        /// </summary>
        /// <param name="aimDelay">The new aim delay.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAimDelay( ref double aimDelay, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimDelay = aimDelay;
                if ( aimBot != null &&
                    aimBot.AimDelay != localAimDelay )
                {
                    aimBot.AimDelay = aimDelay;
                }
            }
        }

        /// <summary>
        /// Gets the current aim delay.
        /// </summary>
        /// <returns>The current aim delay.</returns>
        internal static double GetAimDelay()
        {
            lock ( locker )
            {
                return localAimDelay;
            }
        }

        /// <summary>
        /// Sets the deadzone and updates the AimBot if necessary.
        /// </summary>
        /// <param name="deadzone">The new deadzone value.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetDeadzone( ref int deadzone, ref AimBot aimBot )
        {
            lock ( locker )
            {
                loacalDeadzone = deadzone;
                if ( aimBot != null &&
                    aimBot.DeadZone != loacalDeadzone )
                {
                    aimBot.DeadZone = deadzone;
                }
            }
        }

        /// <summary>
        /// Sets the humanize option and updates the AimBot if necessary.
        /// </summary>
        /// <param name="humanize">Whether to enable or disable humanization.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetHumanize( bool humanize, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localHumanize = humanize;
                if ( aimBot != null &&
                    aimBot.Humanize != localHumanize )
                {
                    aimBot.Humanize = humanize;
                }
            }
        }

        /// <summary>
        /// Sets the aim speed and updates the AimBot if necessary.
        /// </summary>
        /// <param name="aimSpeed">The new aim speed.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAimSpeed( ref double aimSpeed, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimSpeed = aimSpeed;
                if ( aimBot != null &&
                    aimBot.AimSpeed != localAimSpeed )
                {
                    aimBot.AimSpeed = aimSpeed;
                }
            }
        }

        /// <summary>
        /// Sets the aim smoothing and updates the AimBot if necessary.
        /// </summary>
        /// <param name="aimSmoothing">The new aim smoothing value.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAimSmoothing( ref double aimSmoothing, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimSmoothing = aimSmoothing;
                if ( aimBot != null &&
                    aimBot.AimSmoothing != localAimSmoothing )
                {
                    aimBot.AimSmoothing = aimSmoothing;
                }
            }
        }

        /// <summary>
        /// Sets the anti-recoil X value and updates the AimBot if necessary.
        /// </summary>
        /// <param name="antiRecoilX">The new anti-recoil X value.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAntiRecoilX( ref double antiRecoilX, ref AimBot aimBot )
        {
            lock ( locker )
            {
                PlayerData.antiRecoilX = antiRecoilX;
                if ( aimBot != null &&
                    aimBot.AntiRecoilX != PlayerData.antiRecoilX )
                {
                    aimBot.AntiRecoilX = antiRecoilX;
                }
            }
        }

        /// <summary>
        /// Sets the anti-recoil Y value and updates the AimBot if necessary.
        /// </summary>
        /// <param name="antiRecoilY">The new anti-recoil Y value.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAntiRecoilY( ref double antiRecoilY, ref AimBot aimBot )
        {
            lock ( locker )
            {
                PlayerData.antiRecoilY = antiRecoilY;
                if ( aimBot != null &&
                    aimBot.AntiRecoilY != PlayerData.antiRecoilY )
                {
                    aimBot.AntiRecoilY = antiRecoilY;
                }
            }
        }

        /// <summary>
        /// Gets the current deadzone value.
        /// </summary>
        /// <returns>The current deadzone value.</returns>
        internal static int GetDeadzone()
        {
            lock ( locker )
            {
                return loacalDeadzone;
            }
        }

        /// <summary>
        /// Gets the current anti-recoil X value.
        /// </summary>
        /// <returns>The current anti-recoil X value.</returns>
        internal static double GetAntiRecoilX()
        {
            lock ( locker )
            {
                return antiRecoilX;
            }
        }

        /// <summary>
        /// Gets the current anti-recoil Y value.
        /// </summary>
        /// <returns>The current anti-recoil Y value.</returns>
        internal static double GetAntiRecoilY()
        {
            lock ( locker )
            {
                return antiRecoilY;
            }
        }

        /// <summary>
        /// Gets the current aim smoothing value.
        /// </summary>
        /// <returns>The current aim smoothing value.</returns>
        internal static double GetAimSmoothing()
        {
            lock ( locker )
            {
                return localAimSmoothing;
            }
        }

        /// <summary>
        /// Gets the current aim speed value.
        /// </summary>
        /// <returns>The current aim speed value.</returns>
        internal static double GetAimSpeed()
        {
            lock ( locker )
            {
                return localAimSpeed;
            }
        }

        /// <summary>
        /// Gets the current humanize setting.
        /// </summary>
        /// <returns>True if humanization is enabled, otherwise false.</returns>
        internal static bool GetHumanize()
        {
            lock ( locker )
            {
                return localHumanize;
            }
        }

        /// <summary>
        /// Sets the aim radius and updates the screen capture's scan radius.
        /// </summary>
        /// <param name="aimRad">The new aim radius.</param>
        internal static void SetAimRad( ref int aimRad )
        {
            lock ( locker )
            {
                localAimRad = aimRad;
                ScreenCap.ScanRadius = aimRad;
            }
        }

        /// <summary>
        /// Gets the current aim radius.
        /// </summary>
        /// <returns>The current aim radius.</returns>
        internal static int GetAimRad()
        {
            lock ( locker )
            {
                return localAimRad;
            }
        }

        /// <summary>
        /// Sets the aim key and updates the AimBot if necessary.
        /// </summary>
        /// <param name="aimKey">The new aim key.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAimKey( ref int aimKey, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimKey = aimKey;
                if ( aimBot != null &&
                    aimBot.AimKey != localAimKey )
                {
                    aimBot.AimKey = aimKey;
                }
            }
        }

        /// <summary>
        /// Gets the current aim key.
        /// </summary>
        /// <returns>The current aim key.</returns>
        internal static int GetAimKey()
        {
            lock ( locker )
            {
                return localAimKey;
            }
        }

        /// <summary>
        /// Sets the window handle and updates the screen capture's window handle.
        /// </summary>
        /// <param name="hWnd">The new window handle.</param>
        internal static void SetHwnd( ref nint hWnd )
        {
            lock ( locker )
            {
                localHWnd = hWnd;
                ScreenCap.WindowHandle = hWnd;
            }
        }

        /// <summary>
        /// Gets the current window handle.
        /// </summary>
        /// <returns>The current window handle.</returns>
        internal static nint GetHwnd()
        {
            lock ( locker )
            {
                return localHWnd;
            }
        }

        /// <summary>
        /// Sets the game window rectangle and updates the AimBot if necessary.
        /// </summary>
        /// <param name="rect">The new window rectangle.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetRect( ref PInvoke.RECT rect, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localRect = rect;
                if ( ScreenCap.WindowRect.left != rect.left ||
                    ScreenCap.WindowRect.right != rect.right ||
                    ScreenCap.WindowRect.top != rect.top ||
                    ScreenCap.WindowRect.bottom != rect.bottom )
                {
                    ScreenCap.WindowRect = rect;

                    if ( aimBot != null )
                    {
                        aimBot.GameRect = rect;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current game window rectangle.
        /// </summary>
        /// <returns>The current game window rectangle.</returns>
        internal static PInvoke.RECT GetRect()
        {
            lock ( locker )
            {
                return localRect;
            }
        }

        /// <summary>
        /// Sets the color range and updates the screen capture's color range.
        /// </summary>
        /// <param name="colorRange">The new color range.</param>
        internal static void SetColorRange( ref List<IEnumerable<int>> colorRange )
        {
            lock ( locker )
            {
                localColorRange = colorRange;

                if ( ScreenCap.ColorRange != localColorRange )
                {
                    ScreenCap.ColorRange = colorRange;
                }
            }
        }

        /// <summary>
        /// Gets the current color range.
        /// </summary>
        /// <returns>The current color range.</returns>
        internal static List<IEnumerable<int>> GetColorRange()
        {
            lock ( locker )
            {
                return localColorRange;
            }
        }

        /// <summary>
        /// Sets the aim location and updates the AimBot if necessary.
        /// </summary>
        /// <param name="aimLocation">The new aim location.</param>
        /// <param name="aimBot">Reference to the AimBot instance.</param>
        internal static void SetAimLocation( ref AimLocation aimLocation, ref AimBot aimBot )
        {
            lock ( locker )
            {
                PlayerData.aimLocation = aimLocation;
                if ( aimBot != null &&
                    aimLocation != aimBot.AimLocation )
                {
                    aimBot.AimLocation = aimLocation;
                }
            }
        }

        /// <summary>
        /// Gets the current aim location.
        /// </summary>
        /// <returns>The current aim location.</returns>
        internal static AimLocation GetAimLocation()
        {
            lock ( locker )
            {
                return aimLocation;
            }
        }

        /// <summary>
        /// Sets the Bezier curve control points, including 3 control points for cubic Bezier interpolation.
        /// </summary>
        /// <param name="startPoint">The start point of the Bezier curve.</param>
        /// <param name="controlPoint1">The first control point of the Bezier curve.</param>
        /// <param name="controlPoint2">The second control point of the Bezier curve.</param>
        /// <param name="controlPoint3">The third control point of the Bezier curve (new for cubic Bezier).</param>
        /// <param name="endPoint">The end point of the Bezier curve.</param>
        internal static void SetBezierPoints( ref PointF startPoint, ref PointF controlPoint1, ref PointF controlPoint2, ref PointF endPoint )
        {
            lock ( locker )
            {
                bezierStartPoint = startPoint;
                bezierControlPoint1 = controlPoint1;
                bezierControlPoint2 = controlPoint2;
                bezierEndPoint = endPoint;
                bezierControlPointsSet = true; // Mark that control points have been set
            }
        }


        /// <summary>
        /// Gets the Bezier curve control points, including 3 control points for cubic Bezier interpolation.
        /// </summary>
        /// <param name="startPoint">Outputs the start point of the Bezier curve.</param>
        /// <param name="controlPoint1">Outputs the first control point of the Bezier curve.</param>
        /// <param name="controlPoint2">Outputs the second control point of the Bezier curve.</param>
        /// <param name="controlPoint3">Outputs the third control point of the Bezier curve (new for cubic Bezier).</param>
        /// <param name="endPoint">Outputs the end point of the Bezier curve.</param>
        internal static void GetBezierPoints( out PointF startPoint, out PointF controlPoint1, out PointF controlPoint2, out PointF endPoint )
        {
            lock ( locker )
            {
                startPoint = bezierStartPoint;
                controlPoint1 = bezierControlPoint1;
                controlPoint2 = bezierControlPoint2;
                endPoint = bezierEndPoint;
            }
        }


        /// <summary>
        /// Checks if the Bezier control points have been set.
        /// </summary>
        /// <returns>True if the Bezier control points have been set, otherwise false.</returns>
        internal static bool BezierControlPointsSet()
        {
            lock ( locker )
            {
                return bezierControlPointsSet;
            }
        }
    }
}
