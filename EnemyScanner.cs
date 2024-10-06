using System.Drawing.Imaging;

namespace SCB
{
    /// <summary>
    /// Class to handle scanning of enemies in the captured screen and determine target positions.
    /// </summary>
    internal static class EnemyScanner
    {

        /// <summary>
        /// Scans the image for an enemy and logs the process. Finds and returns the target position based on aim location.
        /// </summary>
        /// <param name="logger">The logger instance for logging messages.</param>
        /// <param name="image">The image in which to search for an enemy.</param>
        /// <param name="aimLocation">The aim location (head or body).</param>
        /// <param name="targetPosition">The detected target position.</param>
        internal static void ScanForEnemy( ref Logger logger, ref Bitmap image, AimLocation aimLocation, out PointF targetPosition )
        {
            List<PointF> enemyOuterPoints;
            if ( IsEnemyVisible( ref image, out enemyOuterPoints ) )
            {
                logger.Log( "Enemy visible" );
                PointF targetPos = GetAimLocation( ref enemyOuterPoints, ref aimLocation );
                logger.Log( "Aim location: " + targetPos.ToString() );
                targetPosition = targetPos;
            } else
            {
                logger.Log( "Enemy not visible" );
                targetPosition = new PointF( -1, -1 );
            }
        }

        /// <summary>
        /// Scans the image for an enemy and returns the target position based on aim location.
        /// </summary>
        /// <param name="image">The image in which to search for an enemy.</param>
        /// <param name="aimLocation">The aim location (head or body).</param>
        /// <param name="targetPosition">The detected target position.</param>
        internal static void ScanForEnemy( ref Bitmap image, AimLocation aimLocation, out PointF targetPosition )
        {
            List<PointF> enemyOuterPoints;
            if ( IsEnemyVisible( ref image, out enemyOuterPoints ) )
            {
                targetPosition = GetAimLocation( ref enemyOuterPoints, ref aimLocation );
            } else
            {
                targetPosition = new PointF( -1, -1 );
            }
        }

        /// <summary>
        /// Detects whether an enemy is visible in the image and finds key points (head, feet, and sides) of the enemy.
        /// </summary>
        /// <param name="image">The image to scan for an enemy.</param>
        /// <param name="enemyOuterPoints">A list of points outlining the enemy (head, feet, left-most, and right-most points).</param>
        /// <returns>True if an enemy is found, otherwise false.</returns>
        private static unsafe bool IsEnemyVisible( ref Bitmap image, out List<PointF> enemyOuterPoints )
        {
            List<PointF> enemyPoints = new();
            int sourceX = image.Width / 2 - ScreenCap.ScanRadius;
            int sourceY = image.Height / 2 - ScreenCap.ScanRadius;
            BitmapData bmpData = image.LockBits( new Rectangle( sourceX, sourceY, ScreenCap.ScanRadius * 2, ScreenCap.ScanRadius * 2 ), ImageLockMode.ReadOnly, image.PixelFormat );
            int bytesPerPixel = Image.GetPixelFormatSize( image.PixelFormat ) / 8;
            int stride = bmpData.Stride;
            byte* scan0 = ( byte* ) bmpData.Scan0.ToPointer();


            object lockObject = new object();
            bool foundEnemy = false;

            // Top-down pass: Find the head (top-most point)
            for ( int y = 0; y < ScreenCap.ScanRadius * 2; y++ )
            {
                byte* row = scan0 + ( y * stride );
                for ( int x = 0; x < ScreenCap.ScanRadius * 2; x++ )
                {

                    int pixelIndex = x * bytesPerPixel;
                    byte red = row[ pixelIndex + 2 ];
                    byte blue = row[ pixelIndex ];

                    if ( red >= 1 && blue >= 1 )
                    {
                        lock ( lockObject )
                        {
                            if ( enemyPoints.Count == 0 )
                            {
                                enemyPoints.Add( new PointF( x, y ) ); // Add the head
                                foundEnemy = true;
                                goto EnemyFound;
                            }
                        }
                    }
                }
            }

EnemyFound:
            if ( !foundEnemy )
            {
                image.UnlockBits( bmpData );
                enemyOuterPoints = new List<PointF>();
                return false;
            }

            // Bottom-up pass: Find the feet (bottom-most point)
            for ( int y = ScreenCap.ScanRadius * 2 - 1; y >= 0; y-- )
            {
                byte* row = scan0 + ( y * stride );
                for ( int x = ScreenCap.ScanRadius * 2 - 1; x > 0; x-- )
                {
                    int pixelIndex = x * bytesPerPixel;
                    byte red = row[ pixelIndex + 2 ];
                    byte blue = row[ pixelIndex ];

                    if ( red >= 1 && blue >= 1 )
                    {
                        lock ( lockObject )
                        {
                            if ( enemyPoints.Count == 1 ) // Ensure we're looking for the second point (the feet)
                            {
                                enemyPoints.Add( new PointF( x, y ) ); // Add the feet 
                                goto foundEnemyFeet;
                            }
                        }
                    }
                }
            }
foundEnemyFeet:

// Calculate the middle of the enemy
            int middleOfEnemy = 0;
            foreach ( PointF point in enemyPoints )
            {
                middleOfEnemy += ( int ) Math.Round( point.Y );
            }
            middleOfEnemy /= enemyPoints.Count;

            // Find the left-most and right-most points at the middle Y
            byte* middleRow = scan0 + ( middleOfEnemy * stride );
            for ( int x = 0; x < ScreenCap.ScanRadius * 2; x++ )
            {
                int pixelIndex = x * bytesPerPixel;
                byte red = middleRow[ pixelIndex + 2 ];
                byte blue = middleRow[ pixelIndex ];

                if ( red >= 1 && blue >= 1 )
                {
                    enemyPoints.Add( new PointF( x, middleOfEnemy ) ); // Add the left-most point

                    if ( enemyPoints.Count == 4 )
                    {
                        break;
                    }

                    // Skip over some pixels to find the right-most point
                    x += 5;
                }
            }

            image.UnlockBits( bmpData );

            // Upscale the points to the full image size
            for ( int i = 0; i < enemyPoints.Count; i++ )
            {
                enemyPoints[ i ] = new PointF( enemyPoints[ i ].X + sourceX, enemyPoints[ i ].Y + sourceY );
            }

            enemyOuterPoints = new List<PointF>( enemyPoints );

            return true;
        }

        /// <summary>
        /// Determines the aim location based on the enemy's outer points and the desired aim location (head or body).
        /// </summary>
        /// <param name="enemyOuterPoints">The points outlining the enemy's head, feet, left, and right sides.</param>
        /// <param name="aimLocation">The desired aim location (head or body).</param>
        /// <returns>The calculated aim location point.</returns>
        private static PointF GetAimLocation( ref List<PointF> enemyOuterPoints, ref AimLocation aimLocation )
        {
            // Top-most point is always enemyOuterPoints[0]
            // Bottom-most point is always enemyOuterPoints[1]
            // Left-most point is always enemyOuterPoints[2]
            // Right-most point is always enemyOuterPoints[3]

            PointF targetPos = new();
            bool isWholeBodyVisible = enemyOuterPoints.Count == 4;

            if ( isWholeBodyVisible && aimLocation == AimLocation.head )
            {
                float targetX = ( enemyOuterPoints[ 2 ].X + enemyOuterPoints[ 3 ].X ) / 2;

                // Compensate for outline being above the head
                float targetY = enemyOuterPoints[ 0 ].Y + 10;

                targetPos = new PointF { X = targetX, Y = targetY };
            } else if ( isWholeBodyVisible && aimLocation == AimLocation.body )
            {
                float targetX = ( enemyOuterPoints[ 2 ].X + enemyOuterPoints[ 3 ].X ) / 2;
                float targetY = enemyOuterPoints[ 0 ].Y + ( enemyOuterPoints[ 1 ].Y ) / 2;
                targetPos = new PointF { X = targetX, Y = targetY };
            } else
            {
                // Default to the average of all points if not fully visible
                float targetX = 0;
                float targetY = 0;

                foreach ( PointF p in enemyOuterPoints )
                {
                    targetX += p.X;
                    targetY += p.Y;
                }

                targetX /= enemyOuterPoints.Count;
                targetY /= enemyOuterPoints.Count;
            }

            return targetPos;
        }

        /// <summary>
        /// Enum to specify aim location on the enemy.
        /// </summary>
        internal enum AimLocation
        {
            head,
            body,
        }
    }
}
