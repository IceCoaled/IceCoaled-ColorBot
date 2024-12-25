#if DEBUG
#define RENDORDOC_DEBUG //< Enable RenderDoc debugging.
#define RENDERDOC_INJECTION_WARNING //< Show a message box to remind user to attach renderdoc, if youre using this, you dont start the process from renderdoc.
#endif //< renderdoc debug


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

#if RENDERDOC_INJECTION_WARNING
            // Pop windows message box to remind that we need to attach renderdoc, we are using a windows message box as we cant use MaterialSkin till its initialized
            var diagResult = System.Windows.Forms.MessageBox.Show( "Please attach RenderDoc to this process before continuing.", "Information", MessageBoxButtons.OKCancel, MessageBoxIcon.Information );


            // If user cancels, exit
            if ( diagResult == DialogResult.OK )
            {
                // Continue
            } else
            {
                return;
            }
#endif


#if RENDORDOC_DEBUG
            RenderDocApi renderDocApi = new();
#endif

#if DEBUG
            Logger.Start();
            Logger.Log( "Logger Initialized" );
#endif

            // Initialize manager
#if RENDORDOC_DEBUG
            ManagerInit managerInit = new( renderDocApi );
#else
            ManagerInit managerInit = new();
#endif

            // setup perlin noise table
            Mathf.SetupPermutationTable();


            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run( managerInit.iceBot = new( ref managerInit ) );

            // Clean up
            managerInit?.Dispose();
        }
    }
}