namespace SCB
{


    internal static class Program
    {

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Initialize manager
            ManagerInit managerInit = new();

            // setup perlin noise table
            Mathf.SetupPermutationTable();

#if DEBUG
            Logger.Start();
            Logger.Log( "Logger Initialized" );
#endif          


            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run( managerInit.iceBot = new( managerInit.trayIcon, ref managerInit.aimbot ) );

            // Clean up
            managerInit.Dispose();
        }
    }
}