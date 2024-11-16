#if DEBUG
//#define PRINT
//#define FILTERTIMER
#endif
using System.Drawing.Imaging;

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
        internal static int AimFov { get; set; } = 0;

        /// <summary>
        /// Captures a screenshot of the window based on the current window handle and scan radius.
        /// </summary>
        /// <param name="screenShot">The captured screenshot image.</param>
        private static void CaptureWindow( ref Bitmap? screenShot, ref double captureTime )
        {
            // Calculate the game window dimensions and aspect ratio
            int gameWindowWidth = WindowRect.right - WindowRect.left;
            int gameWindowHeight = WindowRect.bottom - WindowRect.top;

            // Create a new bitmap if it has not been created yet
            screenShot ??= new Bitmap( gameWindowWidth, gameWindowHeight );

            // Capture the screen from the calculated area
            using Graphics graphics = Graphics.FromImage( screenShot );
            graphics.CopyFromScreen( 0, 0, 0, 0, new Size( gameWindowWidth, gameWindowHeight ) );
            captureTime = Utils.Watch.GetCaptureTime();
        }


        /// <summary>
        /// Filters the image by converting pixels within the color range to purple and others to black.
        /// </summary>
        /// <param name="image">The image to be filtered.</param>
        /// <param name="colorRange">The list of color ranges for filtering.</param>
        private static unsafe List<PointF> FilterImageParallel( ref Bitmap image )
        {
            List<PointF> outlinePoints = new();
            var userSelected = PlayerData.GetColorTolerance();
            int sourceX = image.Width / 2 - ( ScreenCap.AimFov / 2 );
            int sourceY = image.Height / 2 - ( ScreenCap.AimFov / 2 );
            BitmapData bmpData = image.LockBits( new Rectangle( sourceX, sourceY, ScreenCap.AimFov, ScreenCap.AimFov ), ImageLockMode.ReadOnly, image.PixelFormat );
            int bytesPerPixel = Image.GetPixelFormatSize( image.PixelFormat ) / 8;
            int height = ScreenCap.AimFov;
            int width = ScreenCap.AimFov;

            byte* scan0 = ( byte* ) bmpData.Scan0.ToPointer();

            //setup parallel options
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2,
            };


            // Parallelize the image filtering process for better performance
            Parallel.For( 0, height, options, y =>
            {

                byte* row = scan0 + ( y * bmpData.Stride );
                for ( int x = 0; x < width; x++ )
                {
                    int pixelIndex = x * bytesPerPixel;

                    byte blue = row[ pixelIndex ];
                    byte green = row[ pixelIndex + 1 ];
                    byte red = row[ pixelIndex + 2 ];

                    // Check if the pixel is within the defined color range
                    if ( userSelected.IsColorInRange( red, green, blue ) )
                    {
                        outlinePoints.Add( new PointF( x + sourceX, y + sourceY ) );

                        // Set the pixel to purple
                        row[ pixelIndex ] = 255;
                        row[ pixelIndex + 1 ] = 0;
                        row[ pixelIndex + 2 ] = 255;

                    } else
                    {
                        // Set the pixel to black
                        row[ pixelIndex ] = 0;
                        row[ pixelIndex + 1 ] = 0;
                        row[ pixelIndex + 2 ] = 0;
                    }
                }
            } );

            // Unlock the image after processing
            image.UnlockBits( bmpData );

            if ( outlinePoints.Count == 0 )
            {
                //return empty list
                return outlinePoints;
            }

            return outlinePoints;
        }



        /// <summary>
        /// Captures the screen and applies the color filtering to the image.
        /// </summary>
        /// <param name="screenShot">The captured and filtered screenshot image.</param>
        internal static List<EnemyData> CaptureAndFilter( ref Bitmap? screenShot )
        {
            double captureTime = 0;
            List<PointF> outlinePoints = new();
            List<EnemyData> enemies = new();
            // Capture the window screenshot
            CaptureWindow( ref screenShot, ref captureTime );


#if PRINT
            string randomNum = new Random().Next( 0, 1000 ).ToString();
            screenShot!.Save( FileManager.enemyScansFolder + randomNum + "Unfiltered.png" );
#endif


            // Apply the color filter to the captured image
            if ( screenShot != null )
            {
                outlinePoints = FilterImageParallel( ref screenShot );
            }

            if ( outlinePoints.Count == 0 )
            {
                return enemies;
            }


#if FILTERTIMER
            double postFilterTime = Utils.Watch.GetCaptureTime();
            Logger.Log( $"Capture time: {captureTime}ms, Filter time: {postFilterTime - captureTime}ms" );
#endif
#if PRINT
            screenShot.Save( FileManager.enemyScansFolder + randomNum + "Filtered.png" );
#endif

            var clusters = GroupConnectedPoints( outlinePoints );
            if ( clusters.Count == 0 )
            {
                return enemies;
            }


            foreach ( var cluster in clusters )
            {
                PInvoke.RECT rect = WindowRect;
                var refCluster = cluster;
                var center = GetTargetCenter( ref refCluster );
                var head = GetTargetHead( ref refCluster );
#if DEBUG
                //Logger.Log( $"Center: {center}, Head: {head}" );
#endif               
                double pixelHeight = GetPixelHeight( ref refCluster );

                enemies.Add( new EnemyData( ref head, ref center, ref captureTime, ref pixelHeight, rect ) );
            }

            return enemies;
        }


        internal static List<List<PointF>> GroupConnectedPoints( List<PointF> outlinePoints, int maxDistance = 5, int minClusterSize = 50, int clusterMergeDistance = 20 )
        {
            List<List<PointF>> clusters = new();
            HashSet<PointF> visited = new();

            // First pass: Group points into clusters
            foreach ( var point in outlinePoints )
            {
                if ( !visited.Contains( point ) )
                {
                    // Start a new cluster
                    List<PointF> newCluster = new();
                    Stack<PointF> stack = new();
                    stack.Push( point );

                    while ( stack.Count > 0 )
                    {
                        var currentPoint = stack.Pop();
                        if ( !visited.Contains( currentPoint ) )
                        {
                            visited.Add( currentPoint );
                            newCluster.Add( currentPoint );

                            // Find neighbors of the current point
                            foreach ( var neighbor in outlinePoints )
                            {
                                PointF neighborPoint = neighbor;
                                if ( !visited.Contains( neighbor ) &&
                                     MathF.Sqrt( MathF.Pow( neighborPoint.Y - currentPoint.Y, 2 ) + MathF.Pow( neighborPoint.X - currentPoint.X, 2 ) ) <= maxDistance )
                                {
                                    stack.Push( neighbor );
                                }
                            }
                        }
                    }

                    // Only add significant clusters, discard small ones
                    if ( newCluster.Count >= minClusterSize )
                    {
                        clusters.Add( newCluster );
                    }
                }
            }

            // Second pass: Merge clusters that are close to each other
            List<List<PointF>> mergedClusters = new();
            bool[] merged = new bool[ clusters.Count ];

            for ( int i = 0; i < clusters.Count; i++ )
            {
                if ( merged[ i ] )
                    continue;

                List<PointF> mergedCluster = new( clusters[ i ] );
                merged[ i ] = true;

                for ( int j = i + 1; j < clusters.Count; j++ )
                {
                    if ( merged[ j ] )
                        continue;

                    // Check if the clusters are close enough to be merged
                    if ( AreClustersClose( clusters[ i ], clusters[ j ], clusterMergeDistance ) )
                    {
                        mergedCluster.AddRange( clusters[ j ] );
                        merged[ j ] = true;
                    }
                }

                mergedClusters.Add( mergedCluster );
            }

            return mergedClusters;
        }

        // Function to determine if two clusters are close enough to be merged
        private static bool AreClustersClose( List<PointF> cluster1, List<PointF> cluster2, int maxDistance )
        {
            foreach ( var point1 in cluster1 )
            {
                foreach ( var point2 in cluster2 )
                {
                    if ( MathF.Sqrt( MathF.Pow( point1.Y - point2.Y, 2 ) + MathF.Pow( point1.X - point2.X, 2 ) ) <= maxDistance )
                    {
                        return true;
                    }
                }
            }
            return false;
        }



        /// <summary>
        /// Get the pixel location of the target body.
        /// </summary>
        /// <param name="outlinePoints"> target ouline from filtering </param>
        /// <returns></returns>
        private static PointF GetTargetCenter( ref List<PointF> outlinePoints )
        {
            float xSum = 0;
            float ySum = 0;
            foreach ( var point in outlinePoints )
            {
                xSum += point.X;
                ySum += point.Y;
            }

            float xCenter = xSum / outlinePoints.Count;
            float yCenter = ySum / outlinePoints.Count;

            return new PointF( xCenter, yCenter );
        }



        /// <summary>
        /// Get the pixel location of the target head.
        /// </summary>
        /// <param name="outlinePoints"> target ouline from filtering </param>
        /// <returns></returns>
        private static PointF GetTargetHead( ref List<PointF> outlinePoints )
        {
            PointF highestPoint = outlinePoints[ 0 ];

            foreach ( var point in outlinePoints )
            {
                if ( point.Y < highestPoint.Y )
                {
                    highestPoint = point;
                }
            }

            return highestPoint;
        }


        /// <summary>
        /// Gets the pixel height of the target outline.
        /// </summary>
        /// <param name="outlinePoints"> target ouline from filtering </param>
        /// <returns></returns>
        private static double GetPixelHeight( ref List<PointF> outlinePoints )
        {
            PointF lowestPoint = outlinePoints[ 0 ];
            PointF highestPoint = outlinePoints[ 0 ];

            foreach ( var point in outlinePoints )
            {
                if ( point.Y > lowestPoint.Y )
                {
                    lowestPoint = point;
                }

                if ( point.Y < highestPoint.Y )
                {
                    highestPoint = point;
                }
            }

            return ( double ) MathF.Sqrt( MathF.Pow( lowestPoint.Y - highestPoint.Y, 2 ) + MathF.Pow( lowestPoint.X - highestPoint.X, 2 ) );
        }
    }
}
