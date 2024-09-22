#if DEBUG
//#define PRINT
#endif

using System.Collections.Concurrent;



namespace SCB
{

    /// <summary>
    /// class to handle the screen capture and filtering
    /// </summary>
    public class ScreenCap : IDisposable
    {
        private bool disposed = false;
        private readonly CancellationTokenSource cancellation;
        private readonly object locker = new();
        private nint hWnd;
        private ThreadSafeGraphics tsGraphics;
        private PInvoke.RECT rect;
        private int scanRadius;
        private readonly Logger logger;
        public readonly TargetPos targetPos;
        private List<IEnumerable<int>> colorRange;
        private readonly List<Thread> threads;
        private List<IEnumerable<int>> purpleRange;


#if DEBUG
        public ScreenCap( nint hwnd, PInvoke.RECT rect, ref Logger logger, int scanRad, List<IEnumerable<int>> colorTolerance )
        {

            if ( hwnd == 0 )
            {
                throw new Exception( "Error getting Game window" );
            }
            this.hWnd = hwnd;
            this.rect = rect;
            this.tsGraphics = new( hwnd );
            this.logger = logger;
            this.scanRadius = scanRad;
            this.colorRange = colorTolerance;
            this.targetPos = new TargetPos( ref logger );
            this.threads = new List<Thread>();

            this.purpleRange = new List<IEnumerable<int>>()
            {
                new List<int> { 122, 134 },
                new List<int> { 0, 10 },
                new List<int> { 122, 134 }
            };

            this.cancellation = new CancellationTokenSource();

#if PRINT
            if ( !System.IO.Directory.Exists( "C:\\Users\\< ENTER USER NAME HERE >\\Pictures\\ColorFilter" )
            {
                System.IO.Directory.CreateDirectory( "C:\\Users\\< ENTER USER NAME HERE >\\Pictures\\ColorFilter" );
            }
            
#endif

#if DEBUG
            this.logger.Log( "Screen Capture Initialized" );
#endif
        }
#else
        public ScreenCap( nint hwnd, PInvoke.RECT rect, int scanRad, List<IEnumerable<int>> colorTolerance )
        {

            if ( hwnd == 0 )
            {
                throw new Exception( "Error getting Game window" );
            }
            this.hWnd = hwnd;
            this.rect = rect;
            this.tsGraphics = new( hwnd );
            this.scanRadius = scanRad;
            this.colorRange = colorTolerance;
            this.targetPos = new TargetPos( ref logger );
            this.threads = new List<Thread>();

            this.purpleRange = new List<IEnumerable<int>>()
            {
                new List<int> { 122, 134 },
                new List<int> { 0, 10 },
                new List<int> { 122, 134 }
            };

            this.cancellation = new CancellationTokenSource();
        }
#endif


        ~ScreenCap()
        {
            Dispose( false );
        }


        /// <summary>
        /// checks if pixel color is within the color range
        /// </summary>
        /// <param name="pixel"></param>
        /// <param name="colorRange"></param>
        /// <returns></returns>
        private bool IsColorInRange( ref Color pixel, ref List<IEnumerable<int>> colorRange )
        {
            while ( Monitor.IsEntered( this.locker ) )
            {
                Thread.Sleep( 1 );
            }

            if ( colorRange[ 0 ].Contains( pixel.R ) && colorRange[ 1 ].Contains( pixel.G ) && colorRange[ 2 ].Contains( pixel.B ) )
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// filters the image based on the color, in debug mode it filters the entire image, in release mode it only filters the area within the scan radius
        /// </summary>
        /// <param name="image"></param>
        /// <param name="colorRange"></param>
        private void FilterImage( ref Bitmap image, ref List<IEnumerable<int>> colorRange )
        {
            while ( Monitor.IsEntered( this.locker ) )
            {
                Thread.Sleep( 1 );
            }

#if DEBUG
            this.logger.Log( "Filtering Image" );
#endif
            Point center = new Point( image.Width / 2, image.Height / 2 );


            for ( int y = 0; y < image.Height; y++ )
            {
                for ( int x = 0; x < image.Width; x++ )
                {
#if DEBUG
                    if ( Math.Pow( y - center.Y, 2 ) + Math.Pow( x - center.X, 2 ) <= Math.Pow( this.scanRadius, 2 ) )
                    {
                        Color pixel = image.GetPixel( x, y );
                        if ( IsColorInRange( ref pixel, ref colorRange ) )
                        {
                            image.SetPixel( x, y, Color.Purple );
                        } else
                        {
                            image.SetPixel( x, y, Color.Black );
                        }

                    } else
                    {
                        image.SetPixel( x, y, Color.Black );
                    }
#else
                    if ( Math.Pow( y - center.Y, 2 ) + Math.Pow( x - center.X, 2 ) <= Math.Pow( this.scanRadius, 2 ) )
                    {
                        Color pixel = image.GetPixel( x, y );
                        if ( IsColorInRange( ref pixel, ref colorRange ) )
                        {
                            image.SetPixel( x, y, Color.Purple );
                        } else
                        {
                            image.SetPixel( x, y, Color.Black );
                        }
                    }
#endif
                }
            }
        }



        /// <summary>
        /// finds the first pixel that matches the color range, sets the target position to that pixel in the stack
        /// </summary>
        /// <param name="screenShot"></param>
        private void FindHead( ref Bitmap screenShot )
        {

#if DEBUG
            this.logger.Log( "Finding Head" );
            for ( int y = 0; y < screenShot.Height; y++ )
            {
                for ( int x = 0; x < screenShot.Width; x++ )
                {
                    Color pixel = screenShot.GetPixel( x, y );
                    if ( pixel.R > 0 &&
                        pixel.B > 0 )
                    {
                        this.logger.Log( "Color Found: (R: " + pixel.R + ", G: " + pixel.G + ", B: " + pixel.B + ")" );
                        this.logger.Log( "Position: (X: " + x + ", Y: " + y + ")" );
                        this.targetPos.SetPos( new Point( x, y ) );
                        return;
                    }
                }
            }
#else
            Point center = new( screenShot.Width / 2, screenShot.Height / 2 );
            for ( int y = 0; y < screenShot.Height; y++ )
            {
                for ( int x = 0; x < screenShot.Width; x++ )
                {
                    if ( Math.Pow( y - center.Y, 2 ) + Math.Pow( x - center.X, 2 ) <= Math.Pow( this.scanRadius, 2 ) )
                    {
                        Color pixel = screenShot.GetPixel( x, y );
                        if ( pixel.R > 0 &&
                        pixel.B > 0 )
                        {
                            this.targetPos.SetPos( new Point( x, y ) );
                            return;
                        }
                    }
                }
            }
#endif
        }

        /// <summary>
        /// main function for capturing the screen and filtering the colors
        /// </summary>
        private void CaptureAndFilter()
        {
            int width = this.rect.right - this.rect.left;
            int height = this.rect.bottom - this.rect.top;


            Rectangle bounds = new Rectangle( this.rect.left, this.rect.top, width, height );
            Bitmap screenShot = new( bounds.Width, bounds.Height );

            while ( this.tsGraphics.IsLocked() )
            {
                Thread.Sleep( 1 );
            }
            this.tsGraphics.CopyScreen( ref screenShot, ref bounds );
#if DEBUG
            this.logger.Log( "Screen Captured" );
#endif

            this.FilterImage( ref screenShot, ref this.colorRange );
#if DEBUG
            this.logger.Log( "Color Filtering Done" );
#endif

            FindHead( ref screenShot );




#if PRINT
            string randomNum = new Random().Next( 0, 1000 ).ToString();
            screenShot.Save( "C:\\Users\\< ENTER USER NAME HERE >\\Pictures\\ColorFilter\\" + randomNum + ".png" );
#endif
            screenShot.Dispose();
        }



        /// <summary>
        /// main loop for the screen capture threads
        /// </summary>
        private void MainLoop()
        {
            while ( !this.cancellation.Token.IsCancellationRequested )
            {
                CaptureAndFilter();
            }
        }


        /// <summary>
        /// main entry point for the screen capture
        /// </summary>
        public void Start()
        {
            this.targetPos.Start();
            this.threads.Add( new Thread( MainLoop ) );
            this.threads.Add( new Thread( MainLoop ) );
            this.threads.Add( new Thread( MainLoop ) );
            this.threads.Add( new Thread( MainLoop ) );
            this.threads.Add( new Thread( MainLoop ) );
            this.threads.Add( new Thread( MainLoop ) );

            foreach ( Thread thread in this.threads )
            {
                thread.Start();
#if DEBUG
                this.logger.Log( "Thread: " + thread.ManagedThreadId + " Started" );
#endif
                Thread.Sleep( 4 );
            }
        }



        public void Stop()
        {
            this.targetPos.Stop();
            this.cancellation.Cancel();
            foreach ( Thread thread in this.threads )
            {
                thread.Join();
            }

#if DEBUG
            this.logger.Log( "Screen Capture Stopped" );
#endif
        }


        public void SetColorRange( ref List<IEnumerable<int>> colorRange )
        {
            lock ( locker )
            {
                this.colorRange = colorRange;
            }
        }

        public void SetScanRadius( ref int scanRadius )
        {
            lock ( locker )
            {
                this.scanRadius = scanRadius;
            }
        }


        public void SetRect( ref PInvoke.RECT rect )
        {
            lock ( locker )
            {
                this.rect = rect;
            }
        }

        public int GetScanRadius()
        {
            lock ( locker )
            {
                return this.scanRadius;
            }
        }

        public PInvoke.RECT GetRect()
        {
            lock ( locker )
            {
                return this.rect;
            }
        }

        public void SetHwnd( ref nint hwnd )
        {
            lock ( locker )
            {
                this.hWnd = hwnd;
                this.tsGraphics = new( hwnd );
            }
        }

        public nint GetHwnd()
        {
            lock ( locker )
            {
                return this.hWnd;
            }
        }

        public List<IEnumerable<int>> GetColorRange()
        {
            lock ( locker )
            {
                return this.colorRange;
            }
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
                if ( !this.cancellation.Token.IsCancellationRequested )
                {
                    this.Stop();
                }
                this.targetPos.Dispose();
                this.tsGraphics.Dispose();
            }

            disposed = true;
        }

    }



    internal class ThreadSafeGraphics : IDisposable
    {
        private bool disposed = false;
        private Graphics graphics;
        private readonly object locker = new object();

        public ThreadSafeGraphics( nint hWnd )
        {
            this.graphics = Graphics.FromHwnd( hWnd );
        }


        ~ThreadSafeGraphics()
        {
            Dispose( false );
        }


        public ref Graphics GetGraphics()
        {
            return ref this.graphics;
        }


        public bool IsLocked()
        {
            return Monitor.IsEntered( locker );
        }


        public void CopyScreen( ref Bitmap image, ref Rectangle bounds )
        {
            lock ( locker )
            {
                using ( this.graphics = Graphics.FromImage( image ) )
                {
                    this.graphics.CopyFromScreen( bounds.Left, bounds.Top, 0, 0, bounds.Size );
                }
            }
        }


        public void Dispose()
        {

            lock ( locker )
            {
                this.graphics.Dispose();
                GC.SuppressFinalize( this );
            }
        }


        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                this.graphics.Dispose();
            }

            disposed = true;
        }
    }


    public class TargetPos : IDisposable
    {
        private bool disposed = false;
        private readonly ConcurrentStack<Point> stack = new ConcurrentStack<Point>();
        private readonly ConcurrentQueue<Point> queue = new ConcurrentQueue<Point>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Thread Thread;
        private Logger logger;

        public TargetPos( ref Logger logger )
        {
            this.logger = logger;
        }

        ~TargetPos()
        {
            Dispose( false );
        }

        public void SetPos( Point pos )
        {
            this.stack.Push( pos );
        }

        public Point GetPosStack()
        {
            Point pos;
            if ( !cancellation.Token.IsCancellationRequested &&
                this.stack.TryPop( out pos ) )
            {
#if DEBUG
                logger.Log( "TargetPos Popped: (X: " + pos.X + ", Y: " + pos.Y + ")" );
#endif
                return pos;
            }

            return new Point( -1, -1 );
        }


        public Point GetPos()
        {
            Point pos;
            if ( !cancellation.Token.IsCancellationRequested &&
                this.queue.TryDequeue( out pos ) )
            {
#if DEBUG
                logger.Log( "TargetPos Dequeued: (X: " + pos.X + ", Y: " + pos.Y + ")" );
#endif
                return pos;
            }

            return new Point( -1, -1 );
        }

        public void Clear()
        {
            this.stack.Clear();
            this.queue.Clear();
        }



        /// <summary>
        /// for every 3 points in the stack, calculate the average and add it to the queue
        /// </summary>
        private void MainLoop()
        {
            while ( !cancellation.Token.IsCancellationRequested )
            {

                if ( this.stack.Count > 3 )
                {
                    Point[] points = new Point[ 3 ];
                    for ( int i = 0; i < 3; i++ )
                    {
                        this.stack.TryPop( out points[ i ] );
                    }

                    int x = 0;
                    int y = 0;
                    for ( int i = 0; i < 3; i++ )
                    {
                        x += points[ i ].X;
                        y += points[ i ].Y;
                    }

                    x /= 3;
                    y /= 3;

#if DEBUG
                    this.logger.Log( "TargetPos In Queue: (X: " + x + ", Y: " + y + ")" );
#endif
                    this.queue.Enqueue( new Point( x, y ) );
                }
            }
        }



        public void Start()
        {
            //this.Thread = new Thread( MainLoop );
            //this.Thread.Start();
        }


        public void Stop()
        {
            this.cancellation.Cancel();
            //this.Thread.Join();
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
                this.stack.Clear();
                this.queue.Clear();
                this.Stop();
                this.cancellation.Dispose();
            }

            disposed = true;
        }

    }
}
