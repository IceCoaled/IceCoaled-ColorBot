
using System.Drawing.Imaging;

namespace SCB
{
    internal unsafe static class EnemyScanning
    {
        private static readonly object scanLock = new();
        private static List<Color> swapColors = [];

        private static ParallelOptions Options => new()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };


        internal static void Initialize( List<Color> colors )
        {
            lock ( scanLock )
            {
                swapColors = colors;
            }
        }

        internal static void FilterNonEnemies( Bitmap? screenCap )
        {
            lock ( scanLock )
            {
                if ( screenCap == null )
                {
                    return;
                }

                var bmpData = screenCap.LockBits( new Rectangle( 0, 0, screenCap.Width, screenCap.Height ), ImageLockMode.ReadWrite, screenCap.PixelFormat );
                try
                {
                    var bytesPerPixel = Image.GetPixelFormatSize( screenCap.PixelFormat ) / 8;
                    var heightInPixels = bmpData.Height;
                    var widthInBytes = bmpData.Width * bytesPerPixel;
                    var ptrFirstPixel = ( byte* ) bmpData.Scan0;
                    Parallel.For( 0, heightInPixels, Options, y =>
                    {
                        var currentLine = ptrFirstPixel + y * bmpData.Stride;
                        for ( int x = 0; x < widthInBytes; x += bytesPerPixel )
                        {
                            var blue = currentLine[ x ];
                            var green = currentLine[ x + 1 ];
                            var red = currentLine[ x + 2 ];
                            var color = Color.FromArgb( red, green, blue );
                            if ( !swapColors.Contains( color ) )
                            {
                                currentLine[ x ] = 0;
                                currentLine[ x + 1 ] = 0;
                                currentLine[ x + 2 ] = 0;
                            }
                        }
                    } );
                } finally
                {
                    screenCap.UnlockBits( bmpData );
                }
            }

            string randomNum = new Random().Next( 0, 1000 ).ToString();
            screenCap.Save( FileManager.enemyScansFolder + "BlackFiltered" + $"{randomNum}.png", ImageFormat.Png );
        }
    }
}
