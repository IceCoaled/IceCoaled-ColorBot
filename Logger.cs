using System.Collections.Concurrent;

namespace SCB
{

    /// <summary>
    /// class to handle logging to the console
    /// </summary>
    public class Logger
    {
        private bool disposed = false;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private Thread thread;


        public Logger() { }

        ~Logger()
        {
            Dispose( false );
        }

        public void Log( string message )
        {
            queue.Enqueue( message );
        }


        /// <summary>
        /// main entry point for the logger
        /// </summary>
        public void Start()
        {
            WinApi.AllocConsole();
            var hWnd = WinApi.GetConsoleWindow();
            WinApi.ShowWindow( hWnd, WinApi.SW_SHOW );
            Console.Title = "IceColorBot Log";
            Console.WriteLine( "IceColorBot Logger Started" );

            this.thread = new Thread( () =>
            {

                while ( !this.cancellation.Token.IsCancellationRequested )
                {
                    if ( queue.Count > 0 &&
                    queue.TryDequeue( out string? msg ) )
                    {
                        Console.WriteLine( msg );
                    }
                    Thread.Sleep( 100 );
                }

            } );
            this.thread.Start();
        }


        public void Stop()
        {
            this.cancellation.Cancel();
            this.thread.Join();
            WinApi.FreeConsole();
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
                Stop();
                WinApi.FreeConsole();
                this.cancellation.Dispose();
            }
            disposed = true;
        }
    }
}
