using Recoil;

namespace SCB
{
    internal class ManagerInit : IDisposable
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

        Thread? smartuiKeyThread;
        Thread? smartGameThread;

        // Notify icon
        internal NotifyIcon? trayIcon;

        internal ManagerInit()
        {
            // Initialize class instances
            fileManager = new();
            fileManager.Initialize();
            recoilPatternProcessor = new(); // Starts recoil pattern thread in constructor
            colorToleranceManager = new(); // Initializes color tolerances in constructor

            // Initialize directx11
            directX11 = new( ref colorToleranceManager );

            aimbot = new( ref recoilPatternProcessor, ref directX11 );

            Task.Run( async () => await SetupThreads() );
        }


        /// <summary>
        /// Delayed thread setup to allow main form to load
        /// </summary>
        /// <returns></returns>
        private async Task SetupThreads()
        {
            // Sleep for 10 seconds to let main form to load
            await Task.Delay( 10000 );

            // Start smartUiKey thread
            smartuiKeyThread = new( () => Utils.UtilsThreads.UiSmartKey( trayIcon!, iceBot! ) );

            // Start smartGame thread
            smartGameThread = new( () => Utils.UtilsThreads.SmartGameCheck( ref fileManager! ) );
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
