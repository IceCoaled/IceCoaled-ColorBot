using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace Utils
{

    /// <summary>
    /// Represents a collection of points used to define a Bezier curve, including the start point, 
    /// end point, and control points for the curve. This class allows for manipulation and computation 
    /// of complex Bezier curves of varying degrees, facilitating smoother and more controlled aiming functions.
    /// </summary>
    /// <remarks>
    /// The first point in the collection is the start point, and the last point is the end point. 
    /// All other points in between are treated as control points, used to influence the shape of the curve.
    /// </remarks>
    internal class BezierPointCollection
    {
        // Properties for the points
        internal PointF Start { get; set; }
        internal PointF End { get; set; }
        internal List<PointF> ControlPoints { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierPointCollection"/> class
        /// with the specified start point, end point, and list of control points.
        /// </summary>
        /// <param name="start">The starting point of the Bezier curve.</param>
        /// <param name="end">The ending point of the Bezier curve.</param>
        /// <param name="controlPoints">A list of control points to define the curve.</param>
        internal BezierPointCollection( ref PointF start, ref PointF end, ref List<PointF> controlPoints )
        {
            Start = start;
            End = end;
            ControlPoints = controlPoints ?? [];
        }

        /// <summary>
        /// Overload constructor for creating an empty Bezier curve collection.
        /// </summary>
        internal BezierPointCollection()
        {
            Start = new( 0, 0 );
            End = new( 0, 0 );
            ControlPoints = [];
        }


        /// <summary>
        /// Specifically used for user-selected control points. It scales the control points to the game's resolution.
        /// </summary>
        /// <param name="startPos">The start position in the game (center of screen).</param>
        /// <param name="targetPos">The target position in the game.</param>
        internal List<PointF> ScaleAndCalculate( ref PointF startPos, ref PointF targetPos, int numPoints )
        {
            // Scale the control points
            ScaleControlPointsRecursive( ControlPoints, startPos, targetPos, Start, End, 0 );

            // Update the start and end points to the startPos and targetPos
            Start = startPos;
            End = targetPos;

            // Calculate the points along the curve
            return CalculateOcticBezierPoints( numPoints );
        }

        /// <summary>
        /// Recursively scales the control points of an octic Bezier curve based on the distance between user-defined points and game-defined points.
        /// </summary>
        /// <param name="controlPoints">The list of control points to be scaled.</param>
        /// <param name="startPos">The start position of the curve in the game.</param>
        /// <param name="targetPos">The target position of the curve in the game.</param>
        /// <param name="userStart">The user-defined start position used for scaling.</param>
        /// <param name="userEnd">The user-defined end position used for scaling.</param>
        /// <param name="index">The current index of the control point being processed (default is 0).</param>
        internal static void ScaleControlPointsRecursive( List<PointF> controlPoints, PointF startPos,
            PointF targetPos, PointF userStart, PointF userEnd, int index = 0 )
        {
            if ( index >= controlPoints.Count )
                return; // Base case: no more points to scale

            // Calculate the distance between user start and end points
            float userDistanceX = Math.Abs( userEnd.X - userStart.X );
            float userDistanceY = Math.Abs( userEnd.Y - userStart.Y );

            // Calculate the distance between the game start and target positions
            float gameDistanceX = Math.Abs( targetPos.X - startPos.X );
            float gameDistanceY = Math.Abs( targetPos.Y - startPos.Y );

            // Calculate scaling factors, ensuring we don't divide by zero
            float scaleX = userDistanceX > 0 ? gameDistanceX / userDistanceX : 1.0f;
            float scaleY = userDistanceY > 0 ? gameDistanceY / userDistanceY : 1.0f;

            // Scale the current control point
            controlPoints[ index ] = new PointF(
                startPos.X + ( controlPoints[ index ].X - userStart.X ) * scaleX,
                startPos.Y + ( controlPoints[ index ].Y - userStart.Y ) * scaleY );

            // Recur for the next control point
            ScaleControlPointsRecursive( controlPoints, startPos, targetPos, userStart, userEnd, index + 1 );
        }




        /// <summary>
        /// Calculates the points along an octic Bezier curve using the internal start, control, and end points.
        /// </summary>
        /// <param name="numPoints">Number of points to compute along the curve.</param>
        /// <returns>A list of interpolated points on the Bezier curve.</returns>
        internal List<PointF> CalculateOcticBezierPoints( int numPoints )
        {
            // Ensure numPoints is at least 2 to create a curve
            numPoints = Math.Max( numPoints, 2 );

            List<PointF> bezierPoints = new( numPoints );

            // Create a list that includes start, control, and end points
            List<PointF> allPoints = [];
            allPoints.Capacity = ControlPoints.Count + 2;

            // Add start point
            allPoints.Add( Start );

            // Add control points
            allPoints.AddRange( ControlPoints );

            // Add end point
            allPoints.Add( End );

            // Step size to divide the curve evenly between 0 and 1
            float step = 1.0f / ( numPoints - 1 ); // Ensures that t reaches 1 exactly at the last point

            // Pre-allocated point for the result
            PointF result;

            // Compute points along the curve using the recursive function
            for ( int i = 0; i < numPoints; i++ )
            {
                float t = i * step;

                // Compute the point on the curve and add it to the result list
                result = ComputeBezierRecursive( allPoints, allPoints.Count - 1, t );
                bezierPoints.Add( result );
            }

            return bezierPoints;
        }



        /// <summary>
        /// Computes a point on an octic (8-point) Bezier curve at a given time using recursion.
        /// </summary>
        /// <param name="points">The array of control points, including start, internal, and end points.</param>
        /// <param name="n">The number of control points minus 1 (i.e., the highest index).</param>
        /// <param name="t">The interpolation factor, between 0 and 1.</param>
        /// <returns>The interpolated point at time t on the curve.</returns>
        private static PointF ComputeBezierRecursive( List<PointF> points, int n, float t )
        {
            // Base case: if there's only one point, return it
            if ( n == 0 )
            {
                return points[ 0 ];
            }

            // Create a list for the next level of points
            List<PointF> nextLevel = [];
            nextLevel.Capacity = n;

            // Linear interpolation between each pair of points
            for ( int i = 0; i < n; i++ )
            {
                PointF point = new()
                {
                    X = ( 1 - t ) * points[ i ].X + t * points[ i + 1 ].X,
                    Y = ( 1 - t ) * points[ i ].Y + t * points[ i + 1 ].Y
                };
                nextLevel.Add( point );
            }

            // Recursively process the next level of points
            return ComputeBezierRecursive( nextLevel, n - 1, t );
        }
    }



    /// <summary>
    /// Custom sleep implementations
    /// </summary>
    internal static class Watch
    {
        private readonly static int optimalSpinCount = Environment.ProcessorCount * 10;


        /// <summary>
        /// Provides a precise microsecond-level sleep function that balances CPU usage and precision
        /// using active spinning, yielding, and dynamic contention-based spin adjustments.
        /// </summary>
        /// <param name="microSeconds">The duration to sleep, specified in microseconds.</param>
        /// <remarks>
        /// The method adapts its behavior based on the required sleep duration and system contention:
        /// <list type="bullet">
        ///   <item><description>For very short durations, it uses <see cref="Thread.SpinWait"/> with low contention to achieve minimal delay overhead.</description></item>
        ///   <item><description>For moderate durations, it dynamically adjusts spin count to balance precision and contention.</description></item>
        ///   <item><description>For higher durations (above 1.5 milliseconds), it yields the CPU using <see cref="Thread.Yield"/> to reduce contention and allow other threads to execute.</description></item>
        /// </list>
        /// This approach ensures efficient CPU usage under varying contention levels while maintaining precise sleep timing.
        /// </remarks>
        internal static void MicroSleep( double microSeconds )
        {
            Stopwatch sleepStopWatch = Stopwatch.StartNew();
            long ticks = ( long ) ( microSeconds * Stopwatch.Frequency / 1000000 );
            int spinCount = 0;
            while ( sleepStopWatch.ElapsedTicks < ticks )
            {
                spinCount++;

                if ( ticks > ( ( Stopwatch.Frequency / 1000 ) * 1.5 ) )
                {
                    Thread.Yield();
                } else if ( spinCount > optimalSpinCount )
                {
                    Thread.SpinWait( spinCount << 1 );
                } else if ( microSeconds > 0.01 )
                {
                    Thread.SpinWait( 1 );
                }
            }
        }

        /// <summary>
        /// Forwards sleep to micro sleep with nanoseconds count
        /// </summary>
        /// <param name="nanoSeconds">The number of nanoseconds to sleep.</param>
        internal static void NanoSleep( double nanoSeconds )
        {
            MicroSleep( nanoSeconds / 1000.0 );
        }

        /// <summary>
        /// Forwards sleep to micro sleep with seconds count
        /// </summary>
        /// <param name="seconds">The number of seconds to sleep.</param>
        internal static void SecondsSleep( double seconds )
        {
            MicroSleep( seconds * 1000000.0 );
        }

        /// <summary>
        /// Forwards sleep to micro sleep with milliseconds count
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to sleep.</param>
        internal static void MilliSleep( double milliseconds )
        {
            MicroSleep( milliseconds * 1000.0 );
        }


        /// <summary>
        /// For async sleep operations
        /// Swapped  <see cref="Thread.Yield"/> for <see cref="Task.Yield"/>
        /// To keep it async
        /// <see cref="MicroSleep"/>
        /// </summary>
        internal static async Task AsyncMicroSleep( double microSeconds )
        {
            Stopwatch sleepStopWatch = Stopwatch.StartNew();
            long ticks = ( long ) ( microSeconds * Stopwatch.Frequency / 1000000 );
            int spinCount = 0;
            while ( sleepStopWatch.ElapsedTicks < ticks )
            {
                spinCount++;

                if ( ticks > ( ( Stopwatch.Frequency / 1000 ) * 1.5 ) )
                {
                    await Task.Yield();
                } else if ( spinCount > optimalSpinCount )
                {
                    Thread.SpinWait( spinCount << 1 );
                } else if ( microSeconds > 0.01 )
                {
                    Thread.SpinWait( 1 );
                }
            }
        }

        /// <summary>
        /// For async sleep operations
        /// <see cref="NanoSleep"/>
        /// </summary>
        internal static async Task AsyncNanoSleep( double nanoSeconds )
        {
            await AsyncMicroSleep( nanoSeconds / 1000.0 );
        }

        /// <summary>
        /// For async sleep operations
        /// <see cref="SecondsSleep"/>
        /// </summary>
        internal static async Task AsyncSecondsSleep( double seconds )
        {
            await AsyncMicroSleep( seconds * 1000000.0 );
        }

        /// <summary>
        /// For async sleep operations
        /// <see cref="MilliSleep"/>
        /// </summary>
        internal static async Task AsyncMilliSleep( double milliseconds )
        {
            await AsyncMicroSleep( milliseconds * 1000.0 );
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
            WindowsPrincipal principal = new( identity );
            return principal.IsInRole( WindowsBuiltInRole.Administrator );
        }

        /// <summary>
        /// Restarts the program with administrator privileges if it is not already running as an administrator.
        /// </summary>
        private static void RestartInAdmin()
        {
            ProcessStartInfo startInfo = new()
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

    internal static partial class DarkMode
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
            DwmSetWindowAttribute( handle, attribute, ref useImmersiveDarkMode, sizeof( int ) );
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


        /// <summary>
        /// Sets the value of a specified attribute for a window.
        /// </summary>
        /// <param name="hwnd">A handle to the window.</param>
        /// <param name="attr">The attribute to set.</param>
        /// <param name="attrValue">A reference to the value to set.</param>
        /// <param name="attrSize">The size of the attribute value in bytes.</param>
        /// <returns>Returns 0 if the function succeeds, otherwise a non-zero error code.</returns>
        [DllImport( "dwmapi.dll", SetLastError = true )]
        private static extern int DwmSetWindowAttribute( nint hwnd, int attr, ref int attrValue, int attrSize );
    }
}
