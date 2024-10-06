using System.Collections.Concurrent;

namespace SCB
{
    /// <summary>
    /// Class to handle logging messages to the console.
    /// </summary>
    public class Logger : IDisposable
    {
        private bool disposed = false;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private Thread? thread;

        /// <summary>
        /// Initializes a new instance of the Logger class.
        /// </summary>
        public Logger() { }

        /// <summary>
        /// Destructor for the Logger class. Calls Dispose.
        /// </summary>
        ~Logger()
        {
            Dispose( false );
        }

        /// <summary>
        /// Adds a message to the logging queue.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log( string message )
        {
            queue.Enqueue( message );
        }

        /// <summary>
        /// Starts the logger thread and allocates the console for output.
        /// </summary>
        public void Start()
        {
            WinApi.AllocConsole();
            var hWnd = WinApi.GetConsoleWindow();
            WinApi.ShowWindow( hWnd, WinApi.SW_SHOW );
            Console.Title = "IceColorBot Log";
            Console.WriteLine( "IceColorBot Logger Started" );

            // Start a new thread that continuously processes log messages
            this.thread = new Thread( () =>
            {
                while ( !this.cancellation.Token.IsCancellationRequested )
                {
                    if ( queue.Count > 0 && queue.TryDequeue( out string? msg ) )
                    {
                        Console.WriteLine( msg );
                    }
                    Thread.Sleep( 100 ); // Small delay to avoid excessive CPU usage
                }
            } );
            this.thread.Start();
        }

        /// <summary>
        /// Stops the logger thread and frees the console.
        /// </summary>
        public void Stop()
        {
            this.cancellation.Cancel();
            this.thread?.Join();
            WinApi.FreeConsole();
        }

        /// <summary>
        /// Releases the resources used by the Logger class.
        /// </summary>
        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Protected method to dispose of resources. Stops the logger thread if running.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose of managed resources.</param>
        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed && disposing )
            {
                Stop();
                WinApi.FreeConsole();
                this.cancellation.Dispose();
            }
            disposed = true;
        }
    }
}
