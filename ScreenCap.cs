#if DEBUG
#define PRINT
#endif


namespace SCB
{

    /// <summary>
    /// class to handle the screen capture and filtering
    /// </summary>
    public static class ScreenCap
    {
        private static nint hWnd;
        private static ThreadSafeGraphics tsGraphics;
        private static PInvoke.RECT rect;
        private static int scanRadius;
        private static Logger logger;
        private static List<IEnumerable<int>> colorRange;


        /// <summary>
        /// checks if pixel color is within the color range
        /// </summary>
        /// <param name="pixel"></param>
        /// <param name="colorRange"></param>
        /// <returns></returns>
        private static bool IsColorInRange( ref Color pixel, ref List<IEnumerable<int>> colorRange )
        {

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
        private static void FilterImage( ref Bitmap image, ref List<IEnumerable<int>> colorRange )
        {


#if DEBUG
            logger.Log( "Filtering Image" );
#endif
            Point center = new Point( image.Width / 2, image.Height / 2 );


            for ( int y = 0; y < image.Height; y++ )
            {
                for ( int x = 0; x < image.Width; x++ )
                {
#if DEBUG
                    if ( Math.Pow( y - center.Y, 2 ) + Math.Pow( x - center.X, 2 ) <= Math.Pow( scanRadius, 2 ) )
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
                    if ( Math.Pow( y - center.Y, 2 ) + Math.Pow( x - center.X, 2 ) <= Math.Pow( scanRadius, 2 ) )
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
        /// main function for capturing the screen and filtering the colors
        /// </summary>
        public static void CaptureAndFilter( out Bitmap screenshot )
        {

            int width = scanRadius * 2;
            int height = scanRadius * 2;
            Rectangle bounds = new( rect.left + ( rect.right - rect.left ) / 2 - scanRadius, rect.top + ( rect.bottom - rect.top ) / 2 - scanRadius, width, height );

            Bitmap screenShot = new( bounds.Width, bounds.Height );

            while ( tsGraphics.IsLocked() )
            {
                Thread.Sleep( 1 );
            }
            tsGraphics.CopyScreen( ref screenShot, ref bounds );
#if DEBUG
            logger.Log( "Screen Captured" );
#endif

            FilterImage( ref screenShot, ref colorRange );
#if DEBUG
            logger.Log( "Color Filtering Done" );
#endif
#if PRINT
            string randomNum = new Random().Next( 0, 1000 ).ToString();
            screenShot.Save( "C:\\Users\\peter\\Pictures\\ColorFilter\\" + randomNum + ".png" );
#endif
            screenShot.Dispose();

            screenshot = screenShot;
        }


        public static void SetColorRange( ref List<IEnumerable<int>> inputColorRange )
        {
            colorRange = inputColorRange;
        }

        public static void SetScanRadius( ref int inputScanRadius )
        {
            scanRadius = inputScanRadius;
        }


        public static void SetRect( ref PInvoke.RECT inputRect )
        {
            rect = inputRect;
        }

        public static int GetScanRadius()
        {
            return scanRadius;
        }

        public static PInvoke.RECT GetRect()
        {
            return rect;
        }

        public static void SetHwnd( ref nint hwnd )
        {

            hWnd = hwnd;
            tsGraphics = new( hwnd );
        }

        public static nint GetHwnd()
        {
            return hWnd;
        }

        public static List<IEnumerable<int>> GetColorRange()
        {
            return colorRange;
        }

        public static void SetLogger( ref Logger inputLogger )
        {
            logger = inputLogger;
        }
    }



    internal class ThreadSafeGraphics : IDisposable
    {
        private bool disposed = false;
        private Graphics graphics;
        private readonly object locker = new object();

        public ThreadSafeGraphics( nint hWnd )
        {
            graphics = Graphics.FromHwnd( hWnd );
        }


        ~ThreadSafeGraphics()
        {
            Dispose( false );
        }


        public ref Graphics GetGraphics()
        {
            return ref graphics;
        }


        public bool IsLocked()
        {
            return Monitor.IsEntered( locker );
        }


        public void CopyScreen( ref Bitmap image, ref Rectangle bounds )
        {
            lock ( locker )
            {
                using ( graphics = Graphics.FromImage( image ) )
                {
                    graphics.CopyFromScreen( bounds.Left, bounds.Top, 0, 0, bounds.Size );
                }
            }
        }


        public void Dispose()
        {
            lock ( locker )
            {
                graphics.Dispose();
                GC.SuppressFinalize( this );
            }
        }


        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                graphics.Dispose();
            }

            disposed = true;
        }
    }
}
