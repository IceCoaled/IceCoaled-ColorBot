using System.Runtime.InteropServices;
using Recoil;

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

        Thread? smartuiKeyThread;
        Thread? smartGameThread;

        // Notify icon
        internal NotifyIcon? trayIcon;


        /// <summary>
        /// Constructor for ManagerInit
        /// </summary>
        /// <param name="rDocClass"></param>
        internal ManagerInit( [Optional] RenderDocApi rDocClass )
        {
            if ( rDocClass != null )
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

            // Initialize enemy scanning
            EnemyScanning.Initialize( colorToleranceManager.SwapColorsList );

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
            await Task.Delay( 5000 );

            // Start smartUiKey thread
            smartuiKeyThread = new( () => Utils.UtilsThreads.UiSmartKey( trayIcon!, iceBot! ) );

            // Start smartGame thread
            smartGameThread = new( () => Utils.UtilsThreads.SmartGameCheck( ref fileManager! ) );

            smartuiKeyThread.Start();
            smartGameThread.Start();
        }

        private void GetInitialGameSettings()
        {
            // Get game settings from config file
            var aimSettings = fileManager.GetInGameSettings();
            var enemyOutline = fileManager.GetEnemyOutlineColor();

            // Set the initial settings
            PlayerData.SetAdsScale( aimSettings.adsScale );
            PlayerData.SetMouseSens( aimSettings.mouseSens );
            PlayerData.SetOutlineColor( enemyOutline.colorName == "custom" ? enemyOutline.Rgb : enemyOutline.colorName );

        }


        /// <summary>
        /// Cleanup all threads and dispose of aimbot
        /// </summary>
        internal void CleanUp()
        {
            smartGameThread?.Join();
            smartuiKeyThread?.Join();

            recoilPatternProcessor?.RecoilPatternSource?.Cancel();
            recoilPatternProcessor?.RecoilPatternSource?.Dispose();

            aimbot?.Dispose();
            trayIcon?.Dispose();
            directX11?.Dispose();
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
    }
}
