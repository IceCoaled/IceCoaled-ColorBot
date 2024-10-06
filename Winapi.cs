using System.Runtime.InteropServices;
using System.Text;

namespace SCB
{
    /// <summary>
    /// Class to handle all Windows API functions used in the application.
    /// </summary>
    internal class WinApi
    {


        [DllImport( "user32.dll" )]
        internal static extern bool SetWindowPos( nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags );

        /// <summary>
        /// Retrieves a handle to the foreground window (the window with which the user is currently interacting).
        /// </summary>
        /// <returns>A handle to the foreground window.</returns>
        [DllImport( "user32.dll" )]
        internal static extern nint GetForegroundWindow();

        /// <summary>
        /// Enumerates all top-level windows on the screen by passing their handles to an application-defined callback function.
        /// </summary>
        /// <param name="lpEnumFunc">A pointer to an application-defined callback function.</param>
        /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
        /// <returns>Returns true if the function succeeds, otherwise false.</returns>
        [DllImport( "user32.dll" )]
        internal static extern bool EnumWindows( EnumWindowsProc lpEnumFunc, nint lParam );

        /// <summary>
        /// Copies the text of the specified window's title bar (if it has one) into a buffer.
        /// </summary>
        /// <param name="hWnd">A handle to the window or control containing the text.</param>
        /// <param name="lpString">The buffer that will receive the text.</param>
        /// <param name="nMaxCount">The maximum number of characters to copy to the buffer.</param>
        /// <returns>If the function succeeds, the return value is the length, in characters, of the copied string.</returns>
        [DllImport( "user32.dll" )]
        internal static extern int GetWindowText( nint hWnd, StringBuilder lpString, int nMaxCount );

        /// <summary>
        /// Retrieves the dimensions of the bounding rectangle of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="lpRect">A pointer to a RECT structure that receives the dimensions of the bounding rectangle.</param>
        /// <returns>Returns true if the function succeeds, otherwise false.</returns>
        [DllImport( "user32.dll" )]
        internal static extern bool GetWindowRect( nint hWnd, ref PInvoke.RECT lpRect );

        /// <summary>
        /// Retrieves a pseudo handle for the calling thread.
        /// </summary>
        /// <returns>A handle to the calling thread.</returns>
        [DllImport( "kernel32.dll" )]
        internal static extern IntPtr GetCurrentThread();

        /// <summary>
        /// Sets a processor affinity mask for the specified thread.
        /// </summary>
        /// <param name="hThread">A handle to the thread whose affinity mask is to be set.</param>
        /// <param name="dwThreadAffinityMask">The processor affinity mask.</param>
        /// <returns>If the function succeeds, the return value is the previous affinity mask.</returns>
        [DllImport( "kernel32.dll" )]
        internal static extern IntPtr SetThreadAffinityMask( nint hThread, nint dwThreadAffinityMask );

        /// <summary>
        /// Determines whether a key is up or down at the time the function is called.
        /// </summary>
        /// <param name="vKey">The virtual-key code of the key to check.</param>
        /// <returns>If the key is pressed, the function returns a non-zero value.</returns>
        [DllImport( "user32.dll" )]
        internal static extern short GetAsyncKeyState( int vKey );

        /// <summary>
        /// Retrieves the specified system metric or system configuration setting.
        /// </summary>
        /// <param name="nIndex">The system metric or configuration setting to be retrieved.</param>
        /// <returns>The return value is the requested system metric or configuration setting.</returns>
        [DllImport( "user32.dll" )]
        internal static extern int GetSystemMetrics( int nIndex );

        /// <summary>
        /// Sets the value of a specified attribute for a window.
        /// </summary>
        /// <param name="hwnd">A handle to the window.</param>
        /// <param name="attr">The attribute to set.</param>
        /// <param name="attrValue">A reference to the value to set.</param>
        /// <param name="attrSize">The size of the attribute value in bytes.</param>
        /// <returns>Returns 0 if the function succeeds, otherwise a non-zero error code.</returns>
        [DllImport( "dwmapi.dll" )]
        internal static extern int DwmSetWindowAttribute( nint hwnd, int attr, ref int attrValue, int attrSize );

        /// <summary>
        /// Allocates a new console for the calling process.
        /// </summary>
        /// <returns>If the function succeeds, the return value is true, otherwise false.</returns>
        [DllImport( "kernel32.dll" )]
        internal static extern bool AllocConsole();

        /// <summary>
        /// Frees the console associated with the calling process.
        /// </summary>
        /// <returns>If the function succeeds, the return value is true, otherwise false.</returns>
        [DllImport( "kernel32.dll" )]
        internal static extern bool FreeConsole();

        /// <summary>
        /// Retrieves the window handle used by the console associated with the calling process.
        /// </summary>
        /// <returns>A handle to the console window, or null if there is no console.</returns>
        [DllImport( "kernel32.dll" )]
        internal static extern nint GetConsoleWindow();

        /// <summary>
        /// Sets the specified window's show state.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="nCmdShow">The command to set the window state.</param>
        /// <returns>If the window was previously visible, the return value is non-zero, otherwise 0.</returns>
        [DllImport( "user32.dll" )]
        internal static extern bool ShowWindow( nint hWnd, int nCmdShow );

        // Constants used for window visibility settings
        internal const int SW_HIDE = 0;
        internal const int SW_SHOW = 5;
        internal const int SW_OK = 1;
        internal const int SW_FALSE = 0;
        internal const int SW_RESTORE = 9;
        internal const nint HWND_TOPMOST = -1;
        internal const nint HWND_NOTOPMOST = -2;
        internal const nint HWND_TOP = 0;
        internal const nint HWND_BOTTOM = 1;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;

        // Delegate for EnumWindows callback
        internal delegate bool EnumWindowsProc( nint hWnd, nint lParam );

        /// <summary>
        /// Retrieves the title of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <returns>The window title as a string.</returns>
        internal static string GetWindowTitle( nint hWnd )
        {
            StringBuilder sb = new StringBuilder( 256 );
            GetWindowText( hWnd, sb, 256 );
            return sb.ToString();
        }

        /// <summary>
        /// Finds the window with a title containing the keyword "Spectre".
        /// </summary>
        /// <returns>A handle to the found window, or nint.MaxValue if not found.</returns>
        internal static nint FindWindow()
        {
            nint hwnd = nint.MaxValue;
            WinApi.EnumWindows( delegate ( nint wnd, nint param )
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
    }
}




