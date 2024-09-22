using System.Diagnostics;
using System.Security.Principal;

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
            CheckAndRunAdmin();
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run( new IceColorBot() );
        }

        private static bool IsRunningAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal( identity );
            return principal.IsInRole( WindowsBuiltInRole.Administrator );
        }

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

        private static void CheckAndRunAdmin()
        {
            if ( !IsRunningAdmin() )
            {
                RestartInAdmin();
            }
        }
    }
}