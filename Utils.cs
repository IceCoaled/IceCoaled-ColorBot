using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using SCB;

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
        internal BezierPointCollection( PointF start, PointF end, List<PointF> controlPoints )
        {
            Start = start;
            End = end;
            ControlPoints = controlPoints ?? [];
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
        internal void ScaleControlPointsRecursive( List<PointF> controlPoints, PointF startPos,
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
            List<PointF> allPoints = new();
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
        private PointF ComputeBezierRecursive( List<PointF> points, int n, float t )
        {
            // Base case: if there's only one point, return it
            if ( n == 0 )
            {
                return points[ 0 ];
            }

            // Create a list for the next level of points
            List<PointF> nextLevel = new();
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




    internal static class Watch
    {

        private static Stopwatch? captureWatch;
        private readonly static int optimalSpinCount = Environment.ProcessorCount * 10;


        /// <summary>
        /// starts the stopwatch to capture the time at each screenshot.
        /// </summary>
        internal static void StartCaptureWatch()
        {
            captureWatch = Stopwatch.StartNew();
        }

        internal static void StopCaptureWatch()
        {
            captureWatch!.Stop();
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
                } else
                {
                    Thread.SpinWait( 0 );
                }
            }
        }

        /// <summary>
        /// Pauses the program execution for a specified number of nanoseconds.
        /// </summary>
        /// <param name="nanoSeconds">The number of nanoseconds to sleep.</param>
        internal static void NanoSleep( double nanoSeconds )
        {
            MicroSleep( nanoSeconds / 1000.0 );
        }

        /// <summary>
        /// Pauses the program execution for a specified number of seconds.
        /// </summary>
        /// <param name="seconds">The number of seconds to sleep.</param>
        internal static void SecondsSleep( double seconds )
        {
            MicroSleep( seconds * 1000000.0 );
        }

        /// <summary>
        /// Pauses the program execution for a specified number of milliseconds.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to sleep.</param>
        internal static void MilliSleep( double milliseconds )
        {
            MicroSleep( milliseconds * 1000.0 );
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
        [DllImport( "dwmapi.dll" )]
        private static extern int DwmSetWindowAttribute( nint hwnd, int attr, ref int attrValue, int attrSize );
    }



    internal static class UtilsThreads
    {
        internal static void UiSmartKey( NotifyIcon trayIcon, IceColorBot mainClass )
        {
            int insert = MouseInput.VK_INSERT;
            bool isKeyPressed = false;

            while ( !Environment.HasShutdownStarted )
            {
                // Check if the INSERT key is pressed
                if ( MouseInput.IsKeyPressed( ref insert ) )
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
                                SetWindowPos( mainClass.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE );


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
        }



        internal static void SmartGameCheck( ref FileManager fileManager )
        {
            string gameSettingsMD5 = fileManager.GetFileMD5Hash( FileManager.gameSettingsFile );
#if DEBUG
            Logger.Log( $"Smart Game Check Thread Started, File Hash: {gameSettingsMD5}" );
#endif

            PInvoke.RECT rect = new();
            nint firstHwnd;
            while ( !Environment.HasShutdownStarted )
            {
                firstHwnd = FindWindow();

                if ( firstHwnd == PlayerData.GetHwnd() )
                {
                    Thread.Sleep( 1000 );


                    if ( firstHwnd != nint.MaxValue &&
                        GetWindowRect( PlayerData.GetHwnd(), ref rect ) &&
                        PlayerData.GetRect().left != rect.left ||
                            PlayerData.GetRect().right != rect.right ||
                            PlayerData.GetRect().top != rect.top ||
                            PlayerData.GetRect().bottom != rect.bottom )
                    {
                        PlayerData.SetRect( rect );
#if DEBUG
                        Logger.Log( "Game Rect Changed" );
#endif 
                    }

                    // check if player changed settings, if so, reload settings
                    string gameSettingsMD5New = fileManager.GetFileMD5Hash( FileManager.gameSettingsFile );
                    if ( gameSettingsMD5New != gameSettingsMD5 )
                    {
                        gameSettingsMD5 = gameSettingsMD5New;
                        var gameSettings = fileManager.GetInGameSettings();

                        // Set the new settings
                        PlayerData.SetMouseSens( gameSettings.mouseSens );
                        PlayerData.SetAdsScale( gameSettings.adsScale );
#if DEBUG
                        Logger.Log( "Game Settings Changed" );
                        Logger.Log( $"New Game Settings File Hash: {gameSettingsMD5}" );
#endif

                    }

                    continue;
                }



                if ( firstHwnd != nint.MaxValue && PlayerData.GetHwnd() == nint.MaxValue )
                {
                    PlayerData.SetHwnd( firstHwnd );

                    if ( !GetWindowRect( PlayerData.GetHwnd(), ref rect ) )
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
        }

        /// <summary>
        /// Sets the position, size, and Z order of a specified window.
        /// This function allows you to change the position of a window in relation to the screen and other windows.
        /// </summary>
        /// <param name="hWnd">A handle to the window that should be moved.</param>
        /// <param name="hWndInsertAfter">
        /// </param>
        /// <param name="X">The new position of the left side of the window, in screen coordinates.</param>
        /// <param name="Y">The new position of the top of the window, in screen coordinates.</param>
        /// <param name="cx">The new width of the window, in pixels. If set to zero, the width will not change.</param>
        /// <param name="cy">The new height of the window, in pixels. If set to zero, the height will not change.</param>
        /// <param name="uFlags">Flags specifying window sizing and positioning options. Common values include:
        /// </param>
        /// <returns>Returns `true` if the function succeeds, or `false` otherwise.</returns>
        [DllImport( "user32.dll" )]
        private static extern bool SetWindowPos( nint hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags );


        /// <summary>
        /// Retrieves the dimensions of the bounding rectangle of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="lpRect">A pointer to a RECT structure that receives the dimensions of the bounding rectangle.</param>
        /// <returns>Returns true if the function succeeds, otherwise false.</returns>
        [DllImport( "user32.dll" )]
        private static extern bool GetWindowRect( nint hWnd, ref PInvoke.RECT lpRect );

        // Delegate for EnumWindows callback
        private delegate bool EnumWindowsProc( nint hWnd, nint lParam );

        /// <summary>
        /// Enumerates all top-level windows on the screen by passing their handles to an application-defined callback function.
        /// </summary>
        /// <param name="lpEnumFunc">A pointer to an application-defined callback function.</param>
        /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
        /// <returns>Returns true if the function succeeds, otherwise false.</returns>
        [DllImport( "user32.dll" )]
        private static extern bool EnumWindows( EnumWindowsProc lpEnumFunc, nint lParam );

        /// <summary>
        /// Copies the text of the specified window's title bar (if it has one) into a buffer.
        /// </summary>
        /// <param name="hWnd">A handle to the window or control containing the text.</param>
        /// <param name="lpString">The buffer that will receive the text.</param>
        /// <param name="nMaxCount">The maximum number of characters to copy to the buffer.</param>
        /// <returns>If the function succeeds, the return value is the length, in characters, of the copied string.</returns>
        [DllImport( "user32.dll" )]
        private static extern int GetWindowText( nint hWnd, StringBuilder lpString, int nMaxCount );

        /// <summary>
        /// Retrieves the title of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <returns>The window title as a string.</returns>
        private static string GetWindowTitle( nint hWnd )
        {
            StringBuilder sb = new( 256 );
            GetWindowText( hWnd, sb, 256 );
            return sb.ToString();
        }

        /// <summary>
        /// Finds the window with a title containing the keyword "Spectre".
        /// </summary>
        /// <returns>A handle to the found window, or nint.MaxValue if not found.</returns>
        private static nint FindWindow()
        {
            nint hwnd = nint.MaxValue;
            EnumWindows( delegate ( nint wnd, nint param )
            {
                string title = GetWindowTitle( wnd );
                if ( title.Contains( "Spectre" ) )
                {
                    hwnd = wnd;
                    return false;
                }
                return true;
            }, nint.Zero );

            return hwnd;
        }

        // constants for SetWindowPos
        private const int HWND_TOP = 0;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
    }


    internal static class SafeReleaseHelper
    {
        internal static readonly Action<IDisposable?> SafeDispose = obj =>
        {
            if ( obj is not null )
            {
                obj.Dispose();
                obj = default!;
            }
        };

    }
}
