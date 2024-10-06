#if DEBUG
//#define PRINT
#endif
using System.Drawing.Imaging;
using Utils;

namespace SCB
{
    /// <summary>
    /// Class to handle screen capturing and image filtering.
    /// </summary>
    internal static class ScreenCap
    {
        /// <summary>
        /// Gets or sets the window handle for the target window to capture.
        /// </summary>
        internal static nint WindowHandle { get; set; } = 0;

        /// <summary>
        /// Gets or sets the rectangle representing the target window's dimensions.
        /// </summary>
        internal static PInvoke.RECT WindowRect { get; set; } = new PInvoke.RECT { left = 0, top = 0, right = 0, bottom = 0 };

        /// <summary>
        /// Gets or sets the scan radius used for capturing.
        /// </summary>
        internal static int ScanRadius { get; set; } = 0;

        /// <summary>
        /// Gets or sets the logger instance for logging messages during capture.
        /// </summary>
        internal static Logger? Logger { get; set; } = null;

        /// <summary>
        /// Gets or sets the color range used to filter captured images.
        /// </summary>
        internal static List<IEnumerable<int>> ColorRange { get; set; } = new List<IEnumerable<int>>();



        /// <summary>
        /// Captures a screenshot of the window based on the current window handle and scan radius.
        /// </summary>
        /// <param name="screenShot">The captured screenshot image.</param>
        private static void CaptureWindow( ref Bitmap? screenShot )
        {
            // Calculate the game window dimensions and aspect ratio
            float gameWindowWidth = WindowRect.right - WindowRect.left;
            float gameWindowHeight = WindowRect.bottom - WindowRect.top;


            // Clamp game window dimensions to ensure they're positive values
            gameWindowWidth = ( float ) Mathf.Clamp( gameWindowWidth, 1, float.MaxValue );
            gameWindowHeight = ( float ) Mathf.Clamp( gameWindowHeight, 1, float.MaxValue );

            // Create a new bitmap if it has not been created yet
            screenShot ??= new Bitmap( ( int ) gameWindowWidth, ( int ) gameWindowHeight );

            // Capture the screen from the calculated area
            using ( Graphics graphics = Graphics.FromImage( screenShot ) )
            {
                graphics.CopyFromScreen( 0, 0, 0, 0, new Size( ( int ) gameWindowWidth, ( int ) gameWindowHeight ) );
            }
        }



        /// <summary>
        /// Checks if a pixel's color is within the specified color range.
        /// </summary>
        /// <param name="red">The red component of the pixel.</param>
        /// <param name="green">The green component of the pixel.</param>
        /// <param name="blue">The blue component of the pixel.</param>
        /// <param name="colorRange">The list of color ranges for filtering.</param>
        /// <returns>True if the pixel color is within the range, otherwise false.</returns>
        private static bool IsColorInRange( byte red, byte green, byte blue, List<IEnumerable<int>> colorRange )
        {
            return colorRange[ 0 ].Contains( red ) && colorRange[ 1 ].Contains( green ) && colorRange[ 2 ].Contains( blue );
        }



        /// <summary>
        /// Filters the image by converting pixels within the color range to purple and others to black.
        /// </summary>
        /// <param name="image">The image to be filtered.</param>
        /// <param name="colorRange">The list of color ranges for filtering.</param>
        private static unsafe void FilterImageParallel( ref Bitmap image, List<IEnumerable<int>> colorRange )
        {
            int sourceX = image.Width / 2 - ScreenCap.ScanRadius;
            int sourceY = image.Height / 2 - ScreenCap.ScanRadius;
            BitmapData bmpData = image.LockBits( new Rectangle( sourceX, sourceY, ScreenCap.ScanRadius * 2, ScreenCap.ScanRadius * 2 ), ImageLockMode.ReadOnly, image.PixelFormat );
            int bytesPerPixel = Image.GetPixelFormatSize( image.PixelFormat ) / 8;
            int height = ScreenCap.ScanRadius * 2;
            int width = ScreenCap.ScanRadius * 2;

            byte* scan0 = ( byte* ) bmpData.Scan0.ToPointer();

            // Parallelize the image filtering process for better performance
            Parallel.For( 0, height, y =>
            {
                byte* row = scan0 + ( y * bmpData.Stride );
                for ( int x = 0; x < width; x++ )
                {
                    int pixelIndex = x * bytesPerPixel;

                    byte blue = row[ pixelIndex ];
                    byte green = row[ pixelIndex + 1 ];
                    byte red = row[ pixelIndex + 2 ];

                    // Check if the pixel is within the defined color range
                    if ( IsColorInRange( red, green, blue, colorRange ) )
                    {
                        // Set pixel to purple (R:128, G:0, B:128)
                        row[ pixelIndex ] = 128;    // B
                        row[ pixelIndex + 1 ] = 0;  // G
                        row[ pixelIndex + 2 ] = 128; // R
                    } else
                    {
                        // Set pixel to black
                        row[ pixelIndex ] = 0;    // B
                        row[ pixelIndex + 1 ] = 0;  // G
                        row[ pixelIndex + 2 ] = 0;  // R
                    }
                }
            } );

            // Unlock the image after processing
            image.UnlockBits( bmpData );
        }



        /// <summary>
        /// Captures the screen and applies the color filtering to the image.
        /// </summary>
        /// <param name="screenShot">The captured and filtered screenshot image.</param>
        internal static void CaptureAndFilter( ref Bitmap? screenShot )
        {
            // Capture the window screenshot
            CaptureWindow( ref screenShot );

#if DEBUG
#if PRINT
            string randomNum = new Random().Next( 0, 1000 ).ToString();
            screenShot.Save( "C:\Users\peter\Documents\ColorbotOutput\\" + randomNum + "Unfiltered.png" );
#endif
#endif

            // Apply the color filter to the captured image
            if ( screenShot != null )
            {
                FilterImageParallel( ref screenShot, ColorRange );
            }

#if DEBUG
#if PRINT
            screenShot.Save( "C:\Users\peter\Documents\ColorbotOutput\\" + randomNum + "Filtered.png" );
#endif
#endif
        }
    }
}
