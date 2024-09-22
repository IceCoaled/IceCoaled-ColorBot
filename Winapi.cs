using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SCB
{

    /// <summary>
    /// class to handle all the windows api functions
    /// </summary>
    internal class WinApi
    {
        [DllImport( "user32.dll" )]
        public static extern nint GetForegroundWindow();

        [DllImport( "user32.dll" )]
        public static extern bool EnumWindows( EnumWindowsProc lpEnumFunc, nint lParam );

        [DllImport( "user32.dll" )]
        public static extern int GetWindowText( nint hWnd, StringBuilder lpString, int nMaxCount );

        [DllImport( "user32.dll" )]
        public static extern bool GetWindowRect( nint hWnd, ref PInvoke.RECT lpRect );

        [DllImport( "user32.dll" )]
        public static extern short GetAsyncKeyState( int vKey );

        [DllImport( "dwmapi.dll" )]
        public static extern int DwmSetWindowAttribute( nint hwnd, int attr, ref int attrValue, int attrSize );

        [DllImport( "kernel32.dll" )]
        public static extern bool AllocConsole();

        [DllImport( "kernel32.dll" )]
        public static extern bool FreeConsole();

        [DllImport( "kernel32.dll" )]
        public static extern IntPtr GetConsoleWindow();

        [DllImport( "user32.dll" )]
        public static extern bool ShowWindow( IntPtr hWnd, int nCmdShow );

        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
        public const int SW_OK = 1;
        public const int SW_FALSE = 0;


        public delegate bool EnumWindowsProc( nint hWnd, IntPtr lParam );

        public static string GetWindowTitle( nint hWnd )
        {
            StringBuilder sb = new StringBuilder( 256 );
            GetWindowText( hWnd, sb, 256 );
            return sb.ToString();
        }


        public static nint FindWindow()
        {
            nint hwnd = nint.MaxValue;
            WinApi.EnumWindows( delegate ( nint wnd, IntPtr param )
            {
                string title = WinApi.GetWindowTitle( wnd );
                if ( title.Contains( "Spectre" ) )
                {
                    hwnd = wnd;
                    return false;
                }
                return true;
            }, nint.Zero );

            return hwnd;
        }



        /// <summary>
        /// checks to see if the current system is using the light theme
        /// </summary>
        /// <returns></returns>
        internal static bool UsingLightTheme()
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
        /// if the system is using dark mode, this function will enable the dark mode for the application
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="enabled"></param>
        /// <returns></returns>
        internal static bool UseImmersiveDarkMode( IntPtr handle, bool enabled )
        {
            int attribute = 20;
            int useImmersiveDarkMode = enabled ? 1 : 0;

            return WinApi.DwmSetWindowAttribute( handle, attribute, ref useImmersiveDarkMode, sizeof( int ) ) == 0;
        }
    }


}
