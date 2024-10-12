using System.Collections.Concurrent;

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
            WinApi.AllocConsole();
            var hWnd = WinApi.GetConsoleWindow();
            WinApi.ShowWindow( hWnd, WinApi.SW_SHOW );
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
            WinApi.FreeConsole();
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

            WinApi.FreeConsole();
            cancellation.Dispose();
        }
    }
}
