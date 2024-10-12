using System.Drawing.Imaging;

namespace SCB
{

    /// <summary>
    /// Class to handle scanning of enemies in the captured screen and determine target positions.
    /// </summary>
    internal static class EnemyScanner
    {
        /// <summary>
        /// Scans the image for potential enemies by looking for plate carrier colors (yellow) and then validating with enemy colors (purple).
        /// Returns a list of EnemyData for all detected enemies.
        /// </summary>
        /// <param name="image">The image to scan for enemies.</param>
        /// <param name="enemyDataList">The list of detected enemies.</param>
        /// <param name="captureTime">The capture time to store in EnemyData.</param>
        /// <returns>True if any enemies were found, otherwise false.</returns>
        internal static unsafe bool ScanForEnemies( ref Bitmap? image, out List<EnemyData> enemyDataList, double captureTime )
        {
            List<EnemyData> enemyList = new();
            int sourceX = image!.Width / 2 - ( ScreenCap.ScanRadius / 2 );
            int sourceY = image!.Height / 2 - ( ScreenCap.ScanRadius / 2 );
            BitmapData bmpData = image!.LockBits( new Rectangle( sourceX, sourceY, ScreenCap.ScanRadius, ScreenCap.ScanRadius ), ImageLockMode.ReadOnly, image!.PixelFormat );
            int bytesPerPixel = Image.GetPixelFormatSize( image!.PixelFormat ) / 8;
            int stride = bmpData.Stride;
            byte* scan0 = ( byte* ) bmpData.Scan0.ToPointer();

            // Scan for potential enemies by finding yellow pixels (plate carriers)
            Parallel.For( 0, ScreenCap.ScanRadius, y =>
            {
                byte* row = scan0 + ( y * stride );
                for ( int x = 0; x < ScreenCap.ScanRadius; x++ )
                {
                    int pixelIndex = x * bytesPerPixel;
                    byte red = row[ pixelIndex + 2 ];
                    byte green = row[ pixelIndex + 1 ];
                    byte blue = row[ pixelIndex ];

                    // Check for plate carrier color (yellow)
                    if ( red >= 200 && green >= 180 && blue <= 100 )
                    {
                        // Determine the horizontal range of the yellow region
                        int leftX = x;
                        int rightX = x;

                        // Expand to the left
                        for ( int left = x - 1; left >= 0; left-- )
                        {
                            int leftPixelIndex = left * bytesPerPixel;
                            byte leftRed = row[ leftPixelIndex + 2 ];
                            byte leftGreen = row[ leftPixelIndex + 1 ];
                            byte leftBlue = row[ leftPixelIndex ];

                            if ( leftRed >= 200 && leftGreen >= 180 && leftBlue <= 100 )
                            {
                                leftX = left;
                            } else
                            {
                                break;
                            }
                        }

                        // Expand to the right
                        for ( int right = x + 1; right < ScreenCap.ScanRadius; right++ )
                        {
                            int rightPixelIndex = right * bytesPerPixel;
                            byte rightRed = row[ rightPixelIndex + 2 ];
                            byte rightGreen = row[ rightPixelIndex + 1 ];
                            byte rightBlue = row[ rightPixelIndex ];

                            if ( rightRed >= 200 && rightGreen >= 180 && rightBlue <= 100 )
                            {
                                rightX = right;
                            } else
                            {
                                break;
                            }
                        }

                        // Calculate the midpoint of the detected yellow region
                        int midX = ( leftX + rightX ) / 2;

                        // Scan upward from the midpoint to check for purple pixels (enemy)
                        for ( int upY = y - 1; upY >= 0; upY-- )
                        {
                            byte* upRow = scan0 + ( upY * stride );
                            int upPixelIndex = midX * bytesPerPixel;
                            byte upRed = upRow[ upPixelIndex + 2 ];
                            byte upGreen = upRow[ upPixelIndex + 1 ];
                            byte upBlue = upRow[ upPixelIndex ];

                            // Check if the pixel is purple (enemy color)
                            if ( upRed >= 100 && upBlue >= 100 && upGreen <= 50 )
                            {
                                // Valid enemy found; add to the list
                                PointF head = new( midX + sourceX, upY + sourceY );
                                PointF center = new( midX + sourceX, y + sourceY );
                                PInvoke.RECT windowRect = ScreenCap.WindowRect;

                                enemyList.Add( new EnemyData( ref head, ref center, ref captureTime, ref windowRect ) );

                                break; // Stop scanning upwards once a purple pixel is found
                            }
                        }
                    }
                }
            } );

            image.UnlockBits( bmpData );

            // Set the output enemy data list
            enemyDataList = new();
            lock ( enemyDataList )
            {
                enemyDataList = enemyList;
            }

            return enemyDataList.Count > 0;
        }
    }

}
