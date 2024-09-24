
namespace SCB
{
    internal static class EnemyScanner
    {

        public static void ScanForEnemy( ref Bitmap image, ref Point middle, AimLocation aimLocation, out Point targetPosition )
        {
            List<Point> enemyOuterPoints;
            if ( IsEnemyVisible( ref image, ref middle, out enemyOuterPoints ) )
            {
                targetPosition = GetAimLocation( ref enemyOuterPoints, ref aimLocation );
            }
            targetPosition = new Point( -1, -1 );
        }


        private static bool IsEnemyVisible( ref Bitmap image, ref Point midddle, out List<Point> enemyOuterPoints )
        {

            bool isEnemyVisible = false;

            for ( int i = 0; i < image.Width; i++ )
            {
                for ( int y = 0; y < image.Height; y++ )
                {
                    Color pixel = image.GetPixel( i, y );
                    if ( pixel.R >= 1 && pixel.G <= 1 )
                    {
                        isEnemyVisible = true;
                    }
                }
            }

            if ( isEnemyVisible )
            {
                enemyOuterPoints = new List<Point>();
                int middleX = midddle.X;
                int middleY = midddle.Y;

                for ( int y = 0; y < middleY; y++ )
                {
                    if ( image.GetPixel( middleX, y ).R >= 1 && image.GetPixel( middleX, y ).B >= 1 )
                    {
                        enemyOuterPoints.Add( new Point { X = middleX, Y = y } );
                    }
                }

                for ( int y = image.Height; y > middleY; y-- )
                {
                    if ( image.GetPixel( middleX, y ).R >= 1 && image.GetPixel( middleX, y ).B >= 1 )
                    {
                        enemyOuterPoints.Add( new Point { X = middleX, Y = y } );
                    }
                }

                for ( int x = 0; x < middleX; x++ )
                {
                    if ( image.GetPixel( x, middleY ).R >= 1 && image.GetPixel( x, middleY ).B >= 1 )
                    {
                        enemyOuterPoints.Add( new Point { X = x, Y = middleY } );
                    }
                }

                for ( int x = image.Width; x > middleX; x-- )
                {
                    if ( image.GetPixel( x, middleY ).R >= 1 && image.GetPixel( x, middleY ).B >= 1 )
                    {
                        enemyOuterPoints.Add( new Point { X = x, Y = middleY } );
                    }
                }
            } else
            {
                enemyOuterPoints = new List<Point>();
            }

            return isEnemyVisible;
        }

        private static Point GetAimLocation( ref List<Point> enemyOuterPoints, ref AimLocation aimLocation )
        {
            //top most point is always enemyOuterPoints[0]
            //bottom most point is always enemyOuterPoints[1]
            //left most point is always enemyOuterPoints[2]
            //right most point is always enemyOuterPoints[3]

            Point targetPos = new Point();
            bool iswholeBodyVisible = false;

            if ( enemyOuterPoints.Count == 4 )
            {
                iswholeBodyVisible = true;
            }

            if ( iswholeBodyVisible && aimLocation == AimLocation.head )
            {
                int targetX = enemyOuterPoints[ 0 ].X + ( enemyOuterPoints[ 3 ].X - enemyOuterPoints[ 2 ].X ) / 2;
                int targetY = enemyOuterPoints[ 0 ].Y + 10;

                targetPos = new Point { X = targetX, Y = targetY };
            } else if ( iswholeBodyVisible && aimLocation == AimLocation.body )
            {
                int targetX = enemyOuterPoints[ 0 ].X + ( enemyOuterPoints[ 3 ].X - enemyOuterPoints[ 2 ].X ) / 2;
                int targetY = enemyOuterPoints[ 0 ].Y + ( enemyOuterPoints[ 1 ].Y - enemyOuterPoints[ 0 ].Y ) / 2;
                targetPos = new Point { X = targetX, Y = targetY };
            } else
            {

                int targetX = 0;
                int targetY = 0;

                foreach ( Point p in enemyOuterPoints )
                {
                    targetX += p.X;
                    targetY += p.Y;
                }

                targetX /= enemyOuterPoints.Count;
                targetY /= enemyOuterPoints.Count;
            }

            return targetPos;
        }


        public enum AimLocation
        {
            head,
            body,
        }
    }
}
