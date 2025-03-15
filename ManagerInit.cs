using System.Runtime.InteropServices;
using System.Text;
using Recoil;
using SCB.DirectX;

namespace SCB
{
    internal partial class ManagerInit : IDisposable
    {
        // Disposal flag
        private bool disposed;

        // Main form class
        internal IceColorBot? iceBot;

        // Class instances
        internal Aimbot? aimbot;
        internal DirectX11? directX11;
        internal RecoilPatternProcessor? recoilPatternProcessor;
        internal ColorToleranceManager? colorToleranceManager;
        internal FileManager? fileManager;

        // RenderDoc class
        internal RenderDocApi? renderDocApi;

        private Thread? smartuiKeyThread;
        private Thread? smartGameThread;

        // Notify icon
        internal NotifyIcon? trayIcon;


        /// <summary>
        /// Constructor for ManagerInit
        /// </summary>
        /// <param name="rDocClass"></param>
        internal ManagerInit( [Optional] RenderDocApi rDocClass )
        {
            if ( rDocClass is not null )
            {
                renderDocApi = rDocClass;
            }

            // Initialize class instances
            fileManager = new();
            fileManager.Initialize();

            recoilPatternProcessor = new(); // Starts recoil pattern thread in constructor
            colorToleranceManager = new(); // Initializes color tolerances in constructor


            // Get our inital in game settings
            GetInitialGameSettings();

            // Initialize Aimbot
            aimbot = new( ref recoilPatternProcessor! );

            // Start util threads
            Task.Run( async () => await SetupThreads() );
        }


        /// <summary>
        /// Delayed thread setup to allow main form to load
        /// </summary>
        /// <returns></returns>
        private async Task SetupThreads()
        {
            // Sleep for 10 seconds to let main form to load
            await Utils.Watch.AsyncSecondsSleep( 5 );

            // Start smartUiKey thread
            smartuiKeyThread = new( () => UiSmartKey( trayIcon!, iceBot! ) );

            // Start smartGame thread
            smartGameThread = new( () => SmartGameCheck( this ) );

            smartuiKeyThread.Start();
            smartGameThread.Start();
        }

        private void GetInitialGameSettings()
        {
            // Get game settings from config file
            var aimSettings = fileManager!.GetInGameSettings();
            var enemyOutline = fileManager.GetEnemyOutlineColor();

            // Set the initial settings
            PlayerData.SetAdsScale( aimSettings.adsScale );
            PlayerData.SetMouseSens( aimSettings.mouseSens );
            PlayerData.SetOutlineColor( enemyOutline.colorName == "custom" ? enemyOutline.Rgb : enemyOutline.colorName );

        }



        internal static void UiSmartKey( NotifyIcon trayIcon, IceColorBot mainClass )
        {
            int insert = HidInputs.VK_INSERT;
            bool isKeyPressed = false;

            while ( !Environment.HasShutdownStarted )
            {
                // Check if the INSERT key is pressed
                if ( HidInputs.IsKeyPressed( ref insert ) )
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
                                HidInputs.ShowTaskbarViaShortcut();
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


        internal static void SmartGameCheck( ManagerInit initManager )
        {
            var fileManagerRef = initManager.fileManager;
            var initManagerRef = initManager;
            string gameSettingsMD5 = fileManagerRef!.GetFileMD5Hash( FileManager.gameSettingsFile );
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
                    string gameSettingsMD5New = fileManagerRef.GetFileMD5Hash( FileManager.gameSettingsFile );
                    if ( gameSettingsMD5New != gameSettingsMD5 )
                    {
                        gameSettingsMD5 = gameSettingsMD5New;
                        var gameSettings = fileManagerRef.GetInGameSettings();
                        var outlineColor = fileManagerRef.GetEnemyOutlineColor();

                        // Get current Settings
                        var currentAimSettings = PlayerData.GetAimSettings();
                        var currentOutlineColor = PlayerData.GetOutlineColor();

                        if ( outlineColor.colorName != currentOutlineColor )
                        {
                            // Set new outline color
                            PlayerData.SetOutlineColor( outlineColor.colorName == "custom" ? outlineColor.Rgb : outlineColor.colorName ); // If the user is using custom rgb we send the rgb values through to translate into a tolerance
                        } else if ( ( float.Abs( gameSettings.mouseSens - currentAimSettings.mouseSens ) > 0.001f || float.Abs( gameSettings.adsScale - currentAimSettings.adsScale ) > 0.001f ) )
                        {
                            // Set the new settings
                            PlayerData.SetMouseSens( gameSettings.mouseSens );
                            PlayerData.SetAdsScale( gameSettings.adsScale );
                        }
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

                    // Initialize directx class
                    if ( initManagerRef.directX11 is null )
                    {
                        initManagerRef.directX11 = new( ref initManager!.colorToleranceManager!, initManager?.renderDocApi! ?? null );
                    }

                    ErrorHandler.PrintToStatusBar( "Everything Is Initialized and Ready!" );
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
        /// Cleanup all threads and dispose of aimbot
        /// </summary>
        internal void CleanUp()
        {
            smartGameThread?.Join();
            smartuiKeyThread?.Join();

            Utils.Watch.SecondsSleep( 5 );

            recoilPatternProcessor?.RecoilPatternSource?.Cancel();
            recoilPatternProcessor?.RecoilPatternSource?.Dispose();

            aimbot?.Dispose();
            trayIcon?.Dispose();
            directX11?.Dispose();
            recoilPatternProcessor?.Dispose();
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                CleanUp();
            }

            disposed = true;
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
        [DllImport( "user32.dll", SetLastError = true )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool SetWindowPos( nint hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags );


        /// <summary>
        /// Retrieves the dimensions of the bounding rectangle of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="lpRect">A pointer to a RECT structure that receives the dimensions of the bounding rectangle.</param>
        /// <returns>Returns true if the function succeeds, otherwise false.</returns>
        [DllImport( "user32.dll", SetLastError = true )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool GetWindowRect( nint hWnd, ref PInvoke.RECT lpRect );

        // Delegate for EnumWindows callback
        private delegate bool EnumWindowsProc( nint hWnd, nint lParam );

        /// <summary>
        /// Enumerates all top-level windows on the screen by passing their handles to an application-defined callback function.
        /// </summary>
        /// <param name="lpEnumFunc">A pointer to an application-defined callback function.</param>
        /// <param name="lParam">An application-defined value to be passed to the callback function.</param>
        /// <returns>Returns true if the function succeeds, otherwise false.</returns>
        [DllImport( "user32.dll", SetLastError = true )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool EnumWindows( EnumWindowsProc lpEnumFunc, nint lParam );

        /// <summary>
        /// Copies the text of the specified window's title bar (if it has one) into a buffer.
        /// </summary>
        /// <param name="hWnd">A handle to the window or control containing the text.</param>
        /// <param name="lpString">The buffer that will receive the text.</param>
        /// <param name="nMaxCount">The maximum number of characters to copy to the buffer.</param>
        /// <returns>If the function succeeds, the return value is the length, in characters, of the copied string.</returns>
        [DllImport( "user32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
        private static extern int GetWindowText( nint hWnd, StringBuilder lpString, int nMaxCount );

        /// <summary>
        /// Retrieves the title of the specified window.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <returns>The window title as a string.</returns>
        private static string GetWindowTitle( nint hWnd )
        {
            StringBuilder sb = new( 256 );
            _ = GetWindowText( hWnd, sb, 256 );
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
}
