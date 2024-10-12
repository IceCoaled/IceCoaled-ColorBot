using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using SCB;

namespace Utils
{

    /// <summary>
    /// Enumeration of thread affinities for the application.
    /// simply to keep things organized and avoid confusion.
    /// </summary>
    internal enum ThreadAffinities
    {
        aimbot,
        triggerbot,
        enemyScan,
        recoilManager,
        gameCheck,
        uiQuickAccess,
    }




    /// <summary>
    /// files and folders used by the application.
    /// used for easy access to file paths and folder names.
    /// </summary>
    internal struct FilesAndFolders
    {
        //Folders
        internal const string configFolder = @"./config/";

        internal const string tessdataFolder = @"./tessdata";

        internal const string debugFolder = @"./debug/";

        internal const string enemyScansFolder = @"./debug/enemyscans/";

        internal const string recoilFolder = @"./recoil/";

        internal const string recoilPatterns = @"./recoil/patterns/";

        //Files
        internal const string recoilPatternFile = @"./recoil/RecoilPattern.txt";

        internal const string exceptionLogFile = @"./debug/exceptionLog.txt";

        internal const string tessEngineFile = @"./tessdata/eng.traineddata";
    }




    internal class Mathf
    {

        //internal interface INumericType { }
        //internal struct Float : INumericType { }
        //internal struct Double : INumericType { }
        //internal struct Int : INumericType { }


        internal static void ScaleControlPoints( ref PointF controlPoint1, ref PointF controlPoint2, ref PointF startPos, ref PointF targetPos, ref PointF userStart, ref PointF userEnd )
        {
            // Calculate the distance between user start and end points
            float userDistanceX = Math.Abs( userEnd.X - userStart.X );
            float userDistanceY = Math.Abs( userEnd.Y - userStart.Y );

            // Calculate the distance between the game start and target positions
            float gameDistanceX = Math.Abs( targetPos.X - startPos.X );
            float gameDistanceY = Math.Abs( targetPos.Y - startPos.Y );

            // Calculate scaling factors, ensuring we don't divide by zero
            float scaleX = userDistanceX > 0 ? gameDistanceX / userDistanceX : 1.0f;
            float scaleY = userDistanceY > 0 ? gameDistanceY / userDistanceY : 1.0f;

            // Scale the control points accordingly (we're using head-on scaling here)
            controlPoint1.X = startPos.X + ( controlPoint1.X - userStart.X ) * scaleX;
            controlPoint1.Y = startPos.Y + ( controlPoint1.Y - userStart.Y ) * scaleY;

            controlPoint2.X = startPos.X + ( controlPoint2.X - userStart.X ) * scaleX;
            controlPoint2.Y = startPos.Y + ( controlPoint2.Y - userStart.Y ) * scaleY;
        }

        internal static float CalculateAngleBetweenVectors( PointF v1, PointF v2 )
        {
            // Calculate the dot product
            float dotProduct = ( v1.X * v2.X + v1.Y * v2.Y );

            // Calculate the magnitude (length) of the vectors
            float magnitudeV1 = ( float ) Math.Sqrt( v1.X * v1.X + v1.Y * v1.Y );
            float magnitudeV2 = ( float ) Math.Sqrt( v2.X * v2.X + v2.Y * v2.Y );

            // Calculate the cosine of the angle
            float cosTheta = dotProduct / ( magnitudeV1 * magnitudeV2 );

            // Use arccos to find the angle in radians, then convert to degrees
            return ( float ) ( Math.Acos( cosTheta ) * ( 180.0 / Math.PI ) );
        }


        internal static void EnforceControlPointAngle( ref PointF controlPoint1, ref PointF controlPoint2, PointF startPos, PointF targetPos )
        {
            // Calculate vectors from start to controlPoint1 and from controlPoint2 to end
            PointF vector1 = new PointF( controlPoint1.X - startPos.X, controlPoint1.Y - startPos.Y );
            PointF vector2 = new PointF( targetPos.X - controlPoint2.X, targetPos.Y - controlPoint2.Y );

            // Calculate the angle between these two vectors
            float angle = CalculateAngleBetweenVectors( vector1, vector2 );

            // If the angle exceeds 45 degrees, adjust control points
            if ( angle > 45.0f )
            {
                // Scale down the control points to limit the angle
                float adjustmentFactor = 45.0f / angle;

                // Adjust the control points based on the angle reduction factor
                controlPoint1.X = startPos.X + ( controlPoint1.X - startPos.X ) * adjustmentFactor;
                controlPoint1.Y = startPos.Y + ( controlPoint1.Y - startPos.Y ) * adjustmentFactor;

                controlPoint2.X = targetPos.X + ( controlPoint2.X - targetPos.X ) * adjustmentFactor;
                controlPoint2.Y = targetPos.Y + ( controlPoint2.Y - targetPos.Y ) * adjustmentFactor;
            }
        }



        /// <summary>
        /// Calculates the cubic Bezier curve interpolation at a given time using 4 control points.
        /// </summary>
        /// <param name="time">The time parameter (0.0 to 1.0) representing the interpolation step.</param>
        /// <param name="currentPos">The starting point of the curve.</param>
        /// <param name="controlPoint1">The first control point, which influences the start of the curve.</param>
        /// <param name="controlPoint2">The second control point, which influences the middle of the curve.</param>
        /// <param name="controlPoint3">The third control point, which influences the end of the curve.</param>
        /// <param name="targetPos">The end point of the curve.</param>
        /// <returns>Returns the interpolated position (PointF) on the Bezier curve at the given time.</returns>
        internal static PointF BezierCubicCalc( double time, ref PointF currentPos, ref PointF controlPoint1, ref PointF controlPoint2, ref PointF targetPos )
        {
            float oneMinusT = ( float ) ( 1 - time );

            // Compute the X coordinate for the cubic Bezier curve
            float x = ( float ) ( Math.Pow( oneMinusT, 3 ) * currentPos.X +
                              3 * Math.Pow( oneMinusT, 2 ) * time * controlPoint1.X +
                              3 * oneMinusT * Math.Pow( time, 2 ) * controlPoint2.X +
                              Math.Pow( time, 3 ) * targetPos.X );

            // Compute the Y coordinate for the cubic Bezier curve
            float y = ( float ) ( Math.Pow( oneMinusT, 3 ) * currentPos.Y +
                              3 * Math.Pow( oneMinusT, 2 ) * time * controlPoint1.Y +
                              3 * oneMinusT * Math.Pow( time, 2 ) * controlPoint2.Y +
                              Math.Pow( time, 3 ) * targetPos.Y );

            // Return the calculated position on the Bezier curve
            return new PointF( x, y );
        }


        /// <summary>
        /// Calculates the Euclidean distance between two points in 2D space.
        /// </summary>
        /// <typeparam name="T">A numeric type that implements INumericType.</typeparam>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>Returns the distance between <paramref name="p1"/> and <paramref name="p2"/>.</returns>
        internal static T GetDistance<T>( ref PointF p1, ref PointF p2 ) where T : struct, IComparable<T>
        {
            dynamic dx = ( dynamic ) p2.X - ( dynamic ) p1.X;
            dynamic dy = ( dynamic ) p2.Y - ( dynamic ) p1.Y;

            dynamic distanceSquared = dx * dx + dy * dy;

            return ( T ) Convert.ChangeType( Math.Sqrt( ( double ) distanceSquared ), typeof( T ) );
        }



        /// <summary>
        /// Clamps the given value between 0 and 1.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Clamp01.html#:~:text=The%20random%20number%20is%20clamped%20//%20to%20between
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="value">The value to clamp.</param>
        /// <returns>Returns 0 if the value is less than 0, 1 if the value is greater than 1, otherwise the value itself.</returns>
        internal static T Clamp01<T>( T value ) where T : struct, IComparable<T>
        {
            T zero = ( T ) Convert.ChangeType( 0, typeof( T ) );
            T one = ( T ) Convert.ChangeType( 1, typeof( T ) );

            if ( value.CompareTo( zero ) < 0 )
            {
                return zero;
            } else if ( value.CompareTo( one ) > 0 )
            {
                return one;
            } else
            {
                return value;
            }
        }



        /// <summary>
        /// Clamps the given value between the specified minimum and maximum values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Clamp.html#:~:text=Use%20Clamp%20to%20restrict%20a%20value%20to%20a
        /// </summary>
        public static dynamic Clamp( dynamic value, dynamic min, dynamic max )
        {
            if ( value < min )
                return min;
            if ( value > max )
                return max;
            return value;
        }

        /// <summary>
        /// Applies a fade function to smooth out a value.
        /// https://thebookofshaders.com/11/
        /// </summary>
        /// <param name="t">The value to fade (should be between 0 and 1).</param>
        /// <returns>Returns the smoothed value using a quintic fade curve.</returns>
        internal static double Fade( double t )
        {
            return t * t * t * ( t * ( t * 6 - 15 ) + 10 ); // Quintic curve for smoother transitions
        }

        /// <summary>
        /// Computes gradient based on hash and coordinates for Perlin noise.
        /// https://mrl.cs.nyu.edu/~perlin/noise/
        /// </summary>
        /// <param name="hash">The hash value for determining gradient direction.</param>
        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <returns>Returns the dot product of the gradient vector and the distance vector.</returns>
        internal static double Grad( int hash, double x, double y )
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h is 12 or 14 ? x : 0;
            return ( ( h & 1 ) == 0 ? u : -u ) + ( ( h & 2 ) == 0 ? v : -v );
        }

        /// <summary>
        /// Applies a smoother step interpolation to ease transitions between two values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.SmoothStep.html#:~:text=Description.%20Interpolates%20between%20min%20and%20max%20with%20smoothing
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="edge0">The start edge.</param>
        /// <param name="edge1">The end edge.</param>
        /// <param name="t">The value to smooth (should be between 0 and 1).</param>
        /// <returns>Returns a smoother step transition.</returns>
        internal static T SmootherStep<T>( T edge0, T edge1, T t ) where T : struct, IComparable<T>
        {

            t = Clamp01( ( T ) Convert.ChangeType( ( ( dynamic ) t - ( dynamic ) edge0 ) / ( ( dynamic ) edge1 - ( dynamic ) edge0 ), typeof( T ) ) );

            return ( T ) Convert.ChangeType( ( dynamic ) t * ( dynamic ) t * ( dynamic ) t * ( ( dynamic ) t * ( ( dynamic ) t * 6 - 15 ) + 10 ), typeof( T ) );
        }


        /// <summary>
        /// Performs inverse linear interpolation between two values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.InverseLerp.html#:~:text=Description.%20Determines%20where%20a%20value%20lies%20between%20two
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns>Returns the inversely scaled value.</returns>
        internal static T InverseLerp<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( 1.0 / ( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * Clamp01( t ) ), typeof( T ) );
        }


        /// <summary>
        /// Linearly interpolates between two values, clamped between 0 and 1.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Lerp.html#:~:text=Mathf.Lerp.%20Switch%20to%20Manual.%20Declaration.%20public%20static%20float
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns>Returns the interpolated value.</returns>
        internal static T Lerp<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * Clamp01( t ), typeof( T ) );
        }


        /// <summary>
        /// Performs cosine-based ease-in-out interpolation for smoother transitions.
        /// https://docs.unity3d.com/ScriptReference/AnimationCurve.EaseInOut.html#:~:text=Declaration.%20public%20static%20AnimationCurve%20EaseInOut%20(float%20timeStart,%20float
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns>Returns the eased value.</returns>
        internal static T EaseInOut<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * ( 0.5 * ( 1 - Math.Cos( Math.PI * ( double ) ( dynamic ) t ) ) ), typeof( T ) );
        }


        /// <summary>
        /// Smoothly transitions a value towards a target over time, using smoothing parameters for gradual movement.
        /// https://docs.unity3d.com/ScriptReference/Mathf.SmoothDamp.html#:~:text=Description.%20Gradually%20moves%20the%20current%20value%20towards%20a
        /// </summary>
        /// <param name="current">The current value that will be updated towards the target.</param>
        /// <param name="target">The target value that the current value should move towards.</param>
        /// <param name="currentVelocity">A reference to the current velocity, modified each call for damping effect.</param>
        /// <param name="smoothTime">The time in seconds it takes to reach the target value. Smaller values result in faster response.</param>
        /// <param name="deltaTime">The time since the last update (typically the frame duration).</param>
        /// <param name="maxSpeed">The maximum speed at which the value can move towards the target. Optional, with a default of <see cref="float.PositiveInfinity"/>.</param>
        /// <remarks>
        /// This function is particularly useful for smoothing movements, applying a spring-like effect, where the transition towards the target becomes progressively slower as it approaches.
        /// Internally, it prevents overshooting by clamping the result based on <paramref name="maxSpeed"/> and adjusting <paramref name="currentVelocity"/> over time.
        /// </remarks>
        /// <example>
        /// Example usage:
        /// <code>
        /// float currentValue = 0f;
        /// float targetValue = 10f;
        /// float velocity = 0f;
        /// float deltaTime = 0.02f; // Assuming a 50 FPS game loop
        /// float smoothTime = 0.5f;
        /// SmoothDamp(ref currentValue, targetValue, ref velocity, smoothTime, deltaTime);
        /// </code>
        /// </example>
        internal static void SmoothDamp( ref float current, float target, ref float currentVelocity, float smoothTime, float deltaTime, float maxSpeed = float.PositiveInfinity )
        {
            // Ensure smoothTime doesn't go too low to prevent erratic behavior, but allow fast response
            smoothTime = Mathf.Clamp( smoothTime, 0.01f, 1.0f );  // Set minimum smoothTime for stability

            // Adjusting the smoothing effect
            float num = 2f / smoothTime;  // The smaller smoothTime, the faster this reacts
            float num2 = num * deltaTime;
            float num3 = 1f / ( 1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2 );

            // Calculate the current difference between the target and the current value
            float num4 = current - target;
            float maxDelta = maxSpeed * smoothTime;

            // Clamp to prevent overshooting
            num4 = Mathf.Clamp( num4, -maxDelta, maxDelta );
            target = current - num4;

            // Compute velocity adjustments
            float num6 = ( currentVelocity + num * num4 ) * deltaTime;
            currentVelocity = ( currentVelocity - num * num6 ) * num3;

            // Update current to new smooth position
            current = target + ( num4 + num6 ) * num3;
        }



        /// <summary>
        /// Generates 2D Perlin noise at a given point.
        /// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
        /// https://flafla2.github.io/2014/08/09/perlinnoise.html
        /// </summary>
        /// <param name="x">The x-coordinate of the point.</param>
        /// <param name="y">The y-coordinate of the point.</param>
        /// <returns>Returns the Perlin noise value at the point.</returns>
        internal static double PerlinNoise( double x, double y )
        {
            int X = ( int ) Math.Floor( x ) & 255;
            int Y = ( int ) Math.Floor( y ) & 255;
            x -= Math.Floor( x );
            y -= Math.Floor( y );
            double u = Fade( x );
            double v = Fade( y );
            int A = p[ X ] + Y;
            int AA = p[ A ];
            int AB = p[ A + 1 ];
            int B = p[ X + 1 ] + Y;
            int BA = p[ B ];
            int BB = p[ B + 1 ];

            return Lerp(
                Lerp( Grad( p[ AA ], x, y ), Grad( p[ BA ], x - 1, y ), u ),
                Lerp( Grad( p[ AB ], x, y - 1 ), Grad( p[ BB ], x - 1, y - 1 ), u ),
                v
            );
        }

        /// <summary>
        /// Predicts the future position of a moving object using its current and previous positions, and the time difference.
        /// https://docs.unity.cn/Packages/com.unity.physics@1.2/api/Unity.Physics.GraphicsIntegration.GraphicalSmoothingUtility.Extrapolate.html#:~:text=Extrapolate(in%20RigidTransform,%20in%20PhysicsVelocity,%20in%20PhysicsMass,%20float)%20Compute
        /// </summary>
        /// <param name="currentPos">The current position of the object.</param>
        /// <param name="previousPos">The previous position of the object.</param>
        /// <param name="deltaTime">The time difference between the two positions.</param>
        /// <param name="extrapolationTime">The time into the future for which to predict the position.</param>
        /// <returns>Returns the predicted future position.</returns>
        internal static PointF MotionExtrapolation( PointF currentPos, PointF previousPos, float deltaTime, float extrapolationTime )
        {
            // Calculate velocity (change in position over time)
            float velocityX = ( currentPos.X - previousPos.X ) / deltaTime;
            float velocityY = ( currentPos.Y - previousPos.Y ) / deltaTime;

            // Extrapolate future position assuming constant velocity
            float futurePosX = currentPos.X + ( velocityX * extrapolationTime );
            float futurePosY = currentPos.Y + ( velocityY * extrapolationTime );

            return new PointF( futurePosX, futurePosY );
        }

        // Permutation table for Perlin noise
        private static readonly int[] p = new int[ 512 ];

        private static int[] permutation = { 151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142,
            8, 99, 37, 240, 21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26,
            197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237,
            149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134,
            139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133,
            230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25,
            63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200,
            196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3,
            64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255,
            82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42,
            223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153,
            101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79,
            113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242,
            193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249,
            14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204,
            176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222,
            114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
        };

        internal static void SetupPermutationTable()
        {
            for ( int i = 0; i < 256; i++ )
            {
                p[ i ] = permutation[ i ];
                p[ 256 + i ] = permutation[ i ];  // Repeat the array
            }
        }
    }






    internal static class Watch
    {

        private static Stopwatch? captureWatch;


        /// <summary>
        /// starts the stopwatch to capture the time at each screenshot.
        /// </summary>
        internal static void StartCaptureWatch()
        {
            captureWatch = Stopwatch.StartNew();
        }


        /// <summary>
        /// returns the time elapsed since the stopwatch was started.
        /// </summary>
        /// <returns></returns>
        internal static double GetCaptureTime()
        {
            return captureWatch!.Elapsed.TotalMilliseconds;
        }


        //custom implementation of thread sleep to pause the program execution for a specified number of microseconds without pinning cpu.

        /// <summary>
        /// Pauses the program execution for a specified number of microseconds.
        /// </summary>
        /// <param name="microSeconds">The number of microseconds to sleep.</param>
        internal static void MicroSleep( double microSeconds )
        {
            SpinWait spinWait = new SpinWait();
            long ticks = ( long ) ( microSeconds * Stopwatch.Frequency / 1000000 );
            Stopwatch sleepStopWatch = Stopwatch.StartNew();
            while ( sleepStopWatch.ElapsedTicks < ticks )
            {
                spinWait.SpinOnce();
            }
        }

        /// <summary>
        /// Pauses the program execution for a specified number of nanoseconds.
        /// </summary>
        /// <param name="nanoSeconds">The number of nanoseconds to sleep.</param>
        internal static void NanoSleep( double nanoSeconds )
        {
            MicroSleep( nanoSeconds / 1000 );
        }

        /// <summary>
        /// Pauses the program execution for a specified number of seconds.
        /// </summary>
        /// <param name="seconds">The number of seconds to sleep.</param>
        internal static void SecondsSleep( double seconds )
        {
            MicroSleep( seconds * 1000 );
        }

        /// <summary>
        /// Pauses the program execution for a specified number of milliseconds.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to sleep.</param>
        internal static void MilliSleep( double milliseconds )
        {
            MicroSleep( milliseconds );
        }
    }



    internal static class Admin
    {
        /// <summary>
        /// Checks whether the current process is running with administrator privileges.
        /// </summary>
        /// <returns>Returns true if the process is running as an administrator, otherwise false.</returns>
        private static bool IsRunningAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal( identity );
            return principal.IsInRole( WindowsBuiltInRole.Administrator );
        }

        /// <summary>
        /// Restarts the program with administrator privileges if it is not already running as an administrator.
        /// </summary>
        private static void RestartInAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start( startInfo );
            } catch ( Exception ex )
            {
                MessageBox.Show( "Failed To Restart In Admin With Error: " + ex.Message );
            }
            Thread.Sleep( 1000 );
            Environment.Exit( 0 );
        }

        /// <summary>
        /// Checks if the program is running with administrator privileges and restarts it with admin if necessary.
        /// </summary>
        internal static void CheckAndRunAdmin()
        {
            if ( !IsRunningAdmin() )
            {
                RestartInAdmin();
            }

            MessageBox.Show( "Already Running As Admin" );
        }
    }

    internal static class DarkMode
    {
        /// <summary>
        /// Determines if the current theme is using light mode.
        /// </summary>
        /// <returns>Returns true if light mode is being used, otherwise false.</returns>
        private static bool UsingLightTheme()
        {
            var registryKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" );
            var appsUseLightTheme = registryKey?.GetValue( "AppsUseLightTheme" );

            if ( appsUseLightTheme is null )
            {
                return true;
            } else
            {
                return Convert.ToBoolean( appsUseLightTheme,
                    CultureInfo.InvariantCulture );
            }
        }

        /// <summary>
        /// Enables or disables immersive dark mode for the specified window.
        /// </summary>
        /// <param name="handle">A handle to the window.</param>
        /// <param name="enabled">True to enable dark mode, false to disable it.</param>
        private static void UseImmersiveDarkMode( IntPtr handle, bool enabled )
        {
            int attribute = 20;
            int useImmersiveDarkMode = enabled ? 1 : 0;
            WinApi.DwmSetWindowAttribute( handle, attribute, ref useImmersiveDarkMode, sizeof( int ) );
        }

        /// <summary>
        /// Sets the dark mode for a given window if the system is not using the light theme.
        /// </summary>
        /// <param name="handle">A handle to the window.</param>
        internal static void SetDarkMode( nint handle )
        {
            if ( !UsingLightTheme() )
            {
                UseImmersiveDarkMode( handle, true );
            }
        }
    }

    internal static class MouseInput
    {
        /// <summary>
        /// Checks whether a specified key is held down.
        /// </summary>
        /// <param name="aimKey">The virtual key code of the key to check.</param>
        /// <returns>Returns true if the key is being held, otherwise false.</returns>
        internal static bool IsKeyHeld( ref int Key )
        {
            return ( ( int ) WinApi.GetAsyncKeyState( Key ) & 0x8000 ) != 0;
        }


        /// <summary>
        /// Checks whether a specified key was pressed since the last call.
        /// </summary>
        /// <param name="Key">The virtual key code of the key to check.</param>
        /// <returns>Returns true if the key is being pressed, otherwise false.</returns>
        internal static bool IsKeyPressed( ref int Key )
        {
            return ( WinApi.GetAsyncKeyState( Key ) & 0x0001 ) != 0; // Low-order bit indicates if the key was pressed since last call.
        }

        /// <summary>
        /// Moves the mouse cursor relative to its current position.
        /// </summary>
        /// <param name="x">The X movement (relative).</param>
        /// <param name="y">The Y movement (relative).</param>
        internal static void MoveRelativeMouse( ref float x, ref float y )
        {
            if ( x != 0 || y != 0 )
            {
                INPUT[] tempInput = new INPUT[ 1 ];
                ref INPUT temp = ref tempInput[ 0 ];
                temp.Type = INPUT_MOUSE;
                temp.Data.Mouse.ExtraInfo = nint.Zero;
                temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE;
                temp.Data.Mouse.MouseData = 0;
                temp.Data.Mouse.Time = 0;
                temp.Data.Mouse.X = ( int ) Math.Round( x );
                temp.Data.Mouse.Y = ( int ) Math.Round( y );
                SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
            }
        }

        /// <summary>
        /// Moves the mouse cursor to an absolute position on the virtual desktop.
        /// </summary>
        /// <param name="x">The X coordinate (range [0.0, 1.0]).</param>
        /// <param name="y">The Y coordinate (range [0.0, 1.0]).</param>
        internal static void MoveAbsoluteMouse( ref float x, ref float y )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_ABSOLUTE;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;

            // Ensure x and y are clamped within the range [0.0, 1.0]
            x = Mathf.Clamp01( x );
            y = Mathf.Clamp01( y );

            // Multiply by ABSOLUTE_MOUSE_COOR_MAX (65535.0) to convert to absolute coordinates
            temp.Data.Mouse.X = ( int ) ( x * ABSOLUTE_MOUSE_COOR_MAX );
            temp.Data.Mouse.Y = ( int ) ( y * ABSOLUTE_MOUSE_COOR_MAX );

            // Send the input event
            SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
        }

        /// <summary>
        /// Simulates a mouse button press.
        /// </summary>
        /// <param name="mouseButton">The button to press.</param>
        internal static void MouseButtonPress( ref uint mouseButton )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = mouseButton;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;
            temp.Data.Mouse.X = 0;
            temp.Data.Mouse.Y = 0;
            SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
        }

        /// <summary>
        /// Simulates a mouse button release.
        /// </summary>
        /// <param name="mouseButton">The button to release.</param>
        internal static void MouseButtonRelease( ref uint mouseButton )
        {
            MouseButtonPress( ref mouseButton );
        }

        /// <summary>
        /// Simulates pressing Windows key + T to show the taskbar.
        /// </summary>
        internal static void ShowTaskbarViaShortcut()
        {
            INPUT[] inputs = new INPUT[ 4 ];

            // Press Windows Key
            inputs[ 0 ].Type = INPUT_KEYBOARD;
            inputs[ 0 ].Data.Keyboard.Vk = 0x5B; // Virtual key code for left Windows key

            // Press T Key
            inputs[ 1 ].Type = INPUT_KEYBOARD;
            inputs[ 1 ].Data.Keyboard.Vk = 0x54; // Virtual key code for T

            // Release T Key
            inputs[ 2 ].Type = INPUT_KEYBOARD;
            inputs[ 2 ].Data.Keyboard.Vk = 0x54; // Virtual key code for T
            inputs[ 2 ].Data.Keyboard.Flags = KEYEVENTF_KEYUP;

            // Release Windows Key
            inputs[ 3 ].Type = INPUT_KEYBOARD;
            inputs[ 3 ].Data.Keyboard.Vk = 0x5B; // Virtual key code for left Windows key
            inputs[ 3 ].Data.Keyboard.Flags = KEYEVENTF_KEYUP;

            SendInput( ( uint ) inputs.Length, inputs, Marshal.SizeOf( typeof( INPUT ) ) );
        }

        // Structs and constants used for mouse and keyboard input.
        [StructLayout( LayoutKind.Sequential )]
        internal struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout( LayoutKind.Explicit )]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset( 0 )]
            public HARDWAREINPUT Hardware;
            [FieldOffset( 0 )]
            public KEYBDINPUT Keyboard;
            [FieldOffset( 0 )]
            public MOUSEINPUT Mouse;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }

        internal const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04, VK_XBUTTON1 = 0x05, VK_XBUTTON2 = 0x06;
        internal const int VK_INSERT = 0x2D, VK_DELETE = 0x2E, VK_HOME = 0x24, VK_END = 0x23, VK_PRIOR = 0x21, VK_NEXT = 0x22;
        internal const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        internal const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
        internal const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        internal const int VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SPACE = 0x20, VK_ESCAPE = 0x1B, VK_BACK = 0x08;
        internal const int VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F6 = 0x75, VK_F7 = 0x76, VK_F8 = 0x77, VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B;
        internal const int VK_NUMPAD0 = 0x60, VK_NUMPAD1 = 0x61, VK_NUMPAD2 = 0x62, VK_NUMPAD3 = 0x63, VK_NUMPAD4 = 0x64, VK_NUMPAD5 = 0x65, VK_NUMPAD6 = 0x66, VK_NUMPAD7 = 0x67, VK_NUMPAD8 = 0x68, VK_NUMPAD9 = 0x69;
        internal const int VK_MULTIPLY = 0x6A, VK_ADD = 0x6B, VK_SUBTRACT = 0x6D, VK_DECIMAL = 0x6E, VK_DIVIDE = 0x6F;
        internal const int VK_OEM_1 = 0xBA, VK_OEM_PLUS = 0xBB, VK_OEM_COMMA = 0xBC, VK_OEM_MINUS = 0xBD, VK_OEM_PERIOD = 0xBE, VK_OEM_2 = 0xBF, VK_OEM_3 = 0xC0, VK_OEM_4 = 0xDB, VK_OEM_5 = 0xDC, VK_OEM_6 = 0xDD, VK_OEM_7 = 0xDE, VK_OEM_8 = 0xDF;
        internal const int VK_OEM_102 = 0xE2, VK_OEM_CLEAR = 0xFE;
        internal const int VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45, VK_F = 0x46, VK_G = 0x47, VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F, VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54, VK_U = 0x55, VK_V = 0x56, VK_W = 0x57, VK_X = 0x58, VK_Y = 0x59, VK_Z = 0x5A;
        internal const int VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34, VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39;
        internal const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, INPUT_HARDWARE = 2;
        internal const Single ABSOLUTE_MOUSE_COOR_MAX = 65535.0f;
        internal const uint MOUSEEVENTF_MOVE = 1, // just apply X/Y (delta due to not setting absolute flag)
            MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4,
            MOUSEEVENTF_RIGHTDOWN = 8, MOUSEEVENTF_RIGHTUP = 16,
            MOUSEEVENTF_MIDDLEDOWN = 32, MOUSEEVENTF_MIDDLEUP = 64,
            MOUSEEVENTF_XBUTTONDOWN = 128, MOUSEEVENTF_XBUTTONUP = 256,
            MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000,
            KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2, MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000,
            MOUSEEVENTF_MIDDLEWDOWN = 0x0020, MOUSEEVENTF_MIDDLEWUP = 0x0040,
            KEYEVENTF_SCANCODE = 0x0008, MAPVK_VK_TO_VSC = 0, KEYEVENTF_UNICODE = 0x0004, EXTENDED_FLAG = 0x100;

        // P/Invoke for sending input and getting/setting cursor position.
        [DllImport( "user32.dll" )]
        private static extern uint SendInput( uint numberOfInputs, INPUT[] inputs, int sizeOfInputs );

        [DllImport( "user32.dll" )]
        public static extern bool GetCursorPos( ref Point lpPoint );

        [DllImport( "user32.dll" )]
        public static extern void SetCursorPos( int x, int y );
    }


    internal static class UtilsThreads
    {
        internal static void UiSmartKey( NotifyIcon trayIcon, IceColorBot mainClass, CancellationTokenSource formClose )
        {
            nint hThread = WinApi.GetCurrentThread();
            nint originalAffinity = WinApi.SetThreadAffinityMask( hThread, ( int ) ThreadAffinities.uiQuickAccess );
            if ( originalAffinity == 0 )
            {
                ErrorHandler.HandleException( new Exception( "Error setting thread affinity" ) );
            }

            int insert = MouseInput.VK_INSERT;
            bool isKeyPressed = false;

            while ( !formClose.Token.IsCancellationRequested )
            {
                // Check if the INSERT key is pressed
                if ( Utils.MouseInput.IsKeyPressed( ref insert ) )
                {
                    // Only toggle if the key wasn't pressed in the last loop iteration
                    if ( !isKeyPressed )
                    {
                        mainClass.Invoke( new Action( () =>
                        {
                            if ( mainClass.Visible )
                            {
                                mainClass.Hide();
                                trayIcon.Visible = true;
                                mainClass.WindowState = FormWindowState.Minimized;  // Minimize the form
                            } else
                            {
                                MouseInput.ShowTaskbarViaShortcut();
                                Thread.Sleep( 100 );
                                mainClass.Show();
                                mainClass.WindowState = FormWindowState.Normal;  // Restore the form
                                mainClass.Activate();
                                mainClass.BringToFront();
                                WinApi.SetWindowPos( mainClass.Handle, WinApi.HWND_TOP, 0, 0, 0, 0, WinApi.SWP_NOMOVE | WinApi.SWP_NOSIZE );


                                trayIcon.Visible = false;
                            }
                        } ) );

                        isKeyPressed = true;  // Mark that the key has been pressed
                    }
                } else
                {
                    isKeyPressed = false;  // Reset state when the key is released
                }

                // Small delay to prevent high CPU usage
                Thread.Sleep( 10 );
            }

            WinApi.SetThreadAffinityMask( hThread, originalAffinity );
        }



        internal static void SmartGameCheck( ref CancellationTokenSource formClose )
        {
            nint hThread = WinApi.GetCurrentThread();
            nint originalAffinity = WinApi.SetThreadAffinityMask( hThread, ( int ) ThreadAffinities.gameCheck );
            if ( originalAffinity == 0 )
            {
                ErrorHandler.HandleException( new Exception( "Error setting thread affinity" ) );
            }



            PInvoke.RECT rect = new();
            nint firstHwnd;
            while ( !formClose.Token.IsCancellationRequested )
            {
                firstHwnd = WinApi.FindWindow();

                if ( firstHwnd == PlayerData.GetHwnd() )
                {
                    Thread.Sleep( 1000 );

                    if ( firstHwnd != nint.MaxValue )
                    {
                        if ( WinApi.GetWindowRect( PlayerData.GetHwnd(), ref rect ) )
                        {
                            if ( PlayerData.GetRect().left != rect.left ||
                                PlayerData.GetRect().right != rect.right ||
                                PlayerData.GetRect().top != rect.top ||
                                PlayerData.GetRect().bottom != rect.bottom )
                            {
                                PlayerData.SetRect( rect );

#if DEBUG
                                Logger.Log( "Game Rect Changed" );
#endif
                            }
                        }
                    }

                    continue;
                }



                if ( firstHwnd != nint.MaxValue && PlayerData.GetHwnd() == nint.MaxValue )
                {
                    PlayerData.SetHwnd( firstHwnd );

                    if ( !WinApi.GetWindowRect( PlayerData.GetHwnd(), ref rect ) )
                    {
                        ErrorHandler.HandleException( new Exception( "Error getting window rect" ) );
                    }
                    PlayerData.SetRect( rect );
#if DEBUG
                    Logger.Log( "Game is active with HWND: " + PlayerData.GetHwnd() );
#endif
                }

                Thread.Sleep( 10000 );

                if ( firstHwnd == nint.MaxValue && PlayerData.GetHwnd() != nint.MaxValue )
                {

                    PlayerData.SetHwnd( firstHwnd );
#if DEBUG
                    Logger.Log( "Game is not active" );
#endif
                }
            }

            WinApi.SetThreadAffinityMask( hThread, originalAffinity );
        }
    }


    /// <summary>
    /// class for saving and loading config files.
    /// </summary>
    internal static class PlayerConfigs
    {
        internal struct Settings
        {
            internal double AimSpeed { get; set; }
            internal double AimSmoothing { get; set; }
            internal int AimFov { get; set; }
            internal int Deadzone { get; set; }
            internal bool Predication { get; set; }
            internal bool AntiRecoil { get; set; }
            internal int AimKey { get; set; }
            internal AimLocation Location { get; set; }
            internal string? ColorSelected { get; set; }
            internal PointF BezierStart { get; set; }
            internal PointF BezierControlPoint1 { get; set; }
            internal PointF BezierControlPoint2 { get; set; }
            internal PointF BezierEnd { get; set; }

            internal Settings( double aimSpeed, double aimSmoothing,
                int aimFov, int deadzone, bool predication,
                bool antiRecoil, int aimKey, AimLocation aimLocation,
                string colorSelected )
            {
                AimSpeed = aimSpeed;
                AimSmoothing = aimSmoothing;
                AimFov = aimFov;
                Deadzone = deadzone;
                Predication = predication;
                AntiRecoil = antiRecoil;
                AimKey = aimKey;
                Location = aimLocation;
                ColorSelected = colorSelected;
            }

            internal void SetBezierPoints( PointF start, PointF control1, PointF control2, PointF end )
            {
                BezierStart = start;
                BezierControlPoint1 = control1;
                BezierControlPoint2 = control2;
                BezierEnd = end;
            }

            internal bool BezierPointsSet()
            {
                return BezierStart != PointF.Empty && BezierControlPoint1 != PointF.Empty &&
                    BezierControlPoint2 != PointF.Empty && BezierEnd != PointF.Empty;
            }
        }



        /// <summary>
        /// saves the current config to a .json file.
        /// </summary>
        internal static void SaveConfig( int configNum )
        {
            //create a new json serializer options object
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            //get the current player settings
            Settings settings = GetPlayerSettings();


            //serialize the settings object to a json string
            string jsonString = JsonSerializer.Serialize( settings, options );

            //write the json string to a file
            File.WriteAllText( $"config{configNum}.json", jsonString );
        }



        /// <summary>
        /// loads a config file and applies the settings to the player.
        /// </summary>
        /// <param name="configNum"></param>
        /// <param name="aimBot"></param>
        internal static void LoadConfig( int configNum )
        {
            //read the json string from the file
            string jsonString = File.ReadAllText( $"config{configNum}.json" );

            //deserialize the json string to a settings object
            Settings settings = JsonSerializer.Deserialize<Settings>( jsonString );

            //set the player settings
            SetPlayerSettings( ref settings );
        }



        /// <summary>
        /// Gets the current player settings.
        /// </summary>
        /// <returns></returns>
        private static Settings GetPlayerSettings()
        {

            //get player aim settings
            var aimSettings = PlayerData.GetAimSettings();

            //get player color settings
            string colorSetting = ColorTolerances.GetColorName( PlayerData.GetColorTolerance() );

            //get player fov
            int aimFov = PlayerData.GetAimFov();

            //create a new settings object
            Settings settings = new( aimSettings.aimSpeed, aimSettings.aimSmoothing, aimFov,
               aimSettings.deadzone, aimSettings.prediction, aimSettings.antiRecoil,
               aimSettings.aimKey, aimSettings.location, colorSetting );

            //if the player has bezier points set, add them to the settings object
            if ( PlayerData.BezierControlPointsSet() )
            {
                var bezierSettings = PlayerData.GetBezierPoints();
                settings.SetBezierPoints( bezierSettings.start, bezierSettings.control1, bezierSettings.control2, bezierSettings.end );
            }

            return settings;
        }



        /// <summary>
        /// sets the player settings to player data.
        /// </summary>
        /// <param name="playerSettings"></param>
        /// <param name="aimBot"></param>
        private static void SetPlayerSettings( ref Settings playerSettings )
        {
            //set the aim fov
            PlayerData.SetAimFov( playerSettings.AimFov );

            //set the aim settings
            PlayerData.SetPrediction( playerSettings.Predication );
            PlayerData.SetAntiRecoil( playerSettings.AntiRecoil );
            PlayerData.SetAimKey( playerSettings.AimKey );
            PlayerData.SetAimSpeed( playerSettings.AimSpeed );
            PlayerData.SetAimSmoothing( playerSettings.AimSmoothing );
            PlayerData.SetDeadzone( playerSettings.Deadzone );
            PlayerData.SetAimLocation( playerSettings.Location );

            //set the color tolerance
            if ( playerSettings.ColorSelected != null )
            {
                PlayerData.SetColorTolerance( playerSettings.ColorSelected );
            }

            //if the player has bezier points set, add them to player data
            if ( playerSettings.BezierPointsSet() )
            {
                PlayerData.SetBezierPoints( playerSettings.BezierStart, playerSettings.BezierControlPoint1,
                    playerSettings.BezierControlPoint2, playerSettings.BezierEnd );
            }
        }

    }

}
