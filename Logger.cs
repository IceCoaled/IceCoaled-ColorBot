using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace SCB
{
    /// <summary>
    /// Class to handle logging messages to the console.
    /// </summary>
    internal static class Logger
    {
        readonly private static CancellationTokenSource cancellation = new();
        readonly private static ConcurrentQueue<string> queue = new();
        private static Thread? thread;


        /// <summary>
        /// Adds a message to the logging queue.
        /// </summary>
        /// <param name="message">The message to log.</param>
        internal static void Log( string message )
        {
            queue.Enqueue( message );
        }

        /// <summary>
        /// Starts the logger thread and allocates the console for output.
        /// </summary>
        internal static void Start()
        {
            AllocConsole();
            var hWnd = GetConsoleWindow();
            ShowWindow( hWnd, SW_SHOW );
            Console.Title = "IceColorBot Log";
            Console.WriteLine( "IceColorBot Logger Started" );

            // Start a new thread that continuously processes log messages
            thread = new Thread( () =>
            {
                while ( !cancellation.Token.IsCancellationRequested )
                {
                    if ( !queue.IsEmpty && queue.TryDequeue( out string? msg ) )
                    {
                        Console.WriteLine( msg );
                    }
                    Thread.Sleep( 100 ); // Small delay to avoid excessive CPU usage
                }
            } );
            thread.Start();
        }

        /// <summary>
        /// Stops the logger thread and frees the console.
        /// </summary>
        internal static void Stop()
        {
            cancellation.Cancel();
            thread?.Join();
            FreeConsole();
        }



        /// <summary>
        /// Cleans up the logger thread and console.
        /// </summary>
        internal static void CleanUp()
        {
            if ( thread != null )
            {
                cancellation.Cancel();
                thread.Join();
            }

            FreeConsole();
            cancellation.Dispose();
        }


        /// <summary>
        /// Allocates a new console for the calling process.
        /// </summary>
        /// <returns>If the function succeeds, the return value is true, otherwise false.</returns>
        [DllImport( "kernel32.dll" )]
        private static extern bool AllocConsole();

        /// <summary>
        /// Frees the console associated with the calling process.
        /// </summary>
        /// <returns>If the function succeeds, the return value is true, otherwise false.</returns>
        [DllImport( "kernel32.dll" )]
        private static extern bool FreeConsole();

        /// <summary>
        /// Retrieves the window handle used by the console associated with the calling process.
        /// </summary>
        /// <returns>A handle to the console window, or null if there is no console.</returns>
        [DllImport( "kernel32.dll" )]
        private static extern nint GetConsoleWindow();

        /// <summary>
        /// Sets the specified window's show state.
        /// </summary>
        /// <param name="hWnd">A handle to the window.</param>
        /// <param name="nCmdShow">The command to set the window state.</param>
        /// <returns>If the window was previously visible, the return value is non-zero, otherwise 0.</returns>
        [DllImport( "user32.dll" )]
        private static extern bool ShowWindow( nint hWnd, int nCmdShow );

        // Show window visibility setting   
        private const int SW_SHOW = 5;

    }
}
