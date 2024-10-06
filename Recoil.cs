#if DEBUG
//#define GETRECOILPATTERN
#endif

using System.Drawing.Imaging;
using System.Text;
using SCB;
using Tesseract;




namespace Recoil
{
    internal class ScreenCaptureOCR : IDisposable
    {
        private bool disposed;
        internal PInvoke.RECT GameRect { get; set; }

        internal ScreenCaptureOCR( ref PInvoke.RECT gameRect )
        {
            GameRect = gameRect;
        }


        ~ScreenCaptureOCR()
        {
            Dispose( false );
        }


        /// <summary>
        /// Performs OCR on a dynamically calculated region of the screen.
        /// </summary>
        /// <param name="gameRect">The current game window size.</param>
        internal string PerformOCRForWeaponName()
        {
            // Get the coordinates for the weapon name area
            Rectangle weaponCaptureArea = CalculateWeaponNameRegion( GameRect );

            // Perform OCR on the specified region
            string unfixedName = PerformOCR( ref weaponCaptureArea );
            return CorrectOCRText( ref unfixedName );
        }

        /// <summary>
        /// General function to perform OCR on a specific capture area.
        /// </summary>
        /// <param name="captureArea">The specific region of the screen to capture and analyze.</param>
        /// <returns>The extracted text from the image.</returns>
        private static string PerformOCR( ref Rectangle captureArea )
        {
            Bitmap screenshot = new( captureArea.Width, captureArea.Height, PixelFormat.Format32bppArgb );
            try
            {
                // Capture the region from the screen into the bitmap
                CaptureScreenRegion( ref captureArea, ref screenshot );

                // Preprocess the image (convert to grayscale, thresholding, etc.)
                Bitmap processedImage = PreprocessImage( ref screenshot );
                try
                {
                    // Perform OCR using Tesseract
                    return ExtractTextFromImage( ref processedImage );
                } finally
                {
                    // Manually dispose processedImage
                    processedImage.Dispose();
                }
            } finally
            {
                // Manually dispose screenshot
                screenshot.Dispose();
            }
        }

        /// <summary>
        /// Captures a specific region of the screen into a provided bitmap.
        /// </summary>
        private static void CaptureScreenRegion( ref Rectangle captureArea, ref Bitmap screenshot )
        {
            using Graphics g = Graphics.FromImage( screenshot );
            // Capture the region from the screen
            g.CopyFromScreen( captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, CopyPixelOperation.SourceCopy );
#if DEBUG
            screenshot.Save( "C:\\Users\\peter\\Documents\\ColorbotOutput\\WeaponCapture.png" );
#endif
        }

        /// <summary>
        /// Preprocess the image to grayscale and apply thresholding, with minimal copying.
        /// </summary>
        private static Bitmap PreprocessImage( ref Bitmap img )
        {
            // Convert to grayscale, apply thresholding
            Bitmap grayImage = new Bitmap( img.Width, img.Height );
            using ( Graphics g = Graphics.FromImage( grayImage ) )
            {
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]{
            new float[] {0.3f, 0.3f, 0.3f, 0, 0},
            new float[] {0.59f, 0.59f, 0.59f, 0, 0},
            new float[] {0.11f, 0.11f, 0.11f, 0, 0},
            new float[] {0, 0, 0, 1, 0},
            new float[] {0, 0, 0, 0, 1}
                    } );
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix( colorMatrix );
                g.DrawImage( img, new Rectangle( 0, 0, img.Width, img.Height ), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes );
            }

            // Apply thresholding to increase contrast
            for ( int x = 0; x < grayImage.Width; x++ )
            {
                for ( int y = 0; y < grayImage.Height; y++ )
                {
                    Color pixelColor = grayImage.GetPixel( x, y );
                    int thresholdValue = 150; // Adjust threshold if necessary
                    int newColorValue = ( pixelColor.R + pixelColor.G + pixelColor.B ) / 3 < thresholdValue ? 0 : 255;
                    grayImage.SetPixel( x, y, Color.FromArgb( newColorValue, newColorValue, newColorValue ) );
                }
            }

            return grayImage;
        }


        /// <summary>
        /// Extracts text from the given preprocessed bitmap using Tesseract OCR.
        /// </summary>
        private static string ExtractTextFromImage( ref Bitmap img )
        {
            using var engine = new TesseractEngine( @"./tessdata", "eng", EngineMode.Default );
            // Convert Bitmap to Pix format
            using Pix pix = BitmapToPix( ref img );
            using var page = engine.Process( pix );
            return page.GetText();
        }

        private static string CorrectOCRText( ref string ocrText )
        {


            // Dictionary for common replacements
            Dictionary<char, char> replacements = new()
            {
                { 'b', '6' },
                { 'Q', 'O' }
            };

            // Characters to remove
            HashSet<char> removeChars = new() {
            'é', 'è', 'ê', 'à', 'â', 'ô', 'û', 'ù', 'ç', 'î', 'ï', 'ö', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')',
            '_', '+', '=', '[', ']', '{', '}', '|', '\\', ';', ':', '"', '\'', '<', '>', ',', '.', '/', '?', '\n', '\t', '\r'
            };

            // Perform replacements and removal
            StringBuilder result = new();
            foreach ( char c in ocrText )
            {
                if ( replacements.ContainsKey( c ) )
                {
                    result.Append( replacements[ c ] );
                } else if ( !removeChars.Contains( c ) )
                {
                    result.Append( c );
                }
            }

            // Specific replacements for common OCR errors

            switch ( ocrText )
            {
                case "M4 FURY\n":
                case "M43 FURY\n":
                return "M49 FURY\n";
                case "M667 REAVER\n":
                return "M67 REAVER\n";
                //no default just in case
            }

            return result.ToString();
        }


        /// <summary>
        /// Converts a bitmap to a Pix object for Tesseract OCR.
        /// </summary>
        private static Pix BitmapToPix( ref Bitmap bitmap )
        {
            using var ms = new MemoryStream();
            bitmap.Save( ms, System.Drawing.Imaging.ImageFormat.Png );
            ms.Seek( 0, SeekOrigin.Begin );

            // Load Pix from the memory stream
            return Pix.LoadFromMemory( ms.ToArray() );
        }

        /// <summary>
        /// Calculates the region where the weapon name is located based on the game window size.
        /// </summary>
        /// <param name="gameRect">The current game window size.</param>
        /// <returns>The rectangle defining the region to capture.</returns>

        private static Rectangle CalculateWeaponNameRegion( PInvoke.RECT gameRect )
        {
            // Adjusted ratios for weapon name capture area
            double weaponRegionStartXRatio = 0.82;  // Keep X adjustment
            double weaponRegionStartYRatio = 0.83;  // Move Y slightly up by reducing ratio
            double weaponRegionWidthRatio = 0.18;   // Keep the width the same
            double weaponRegionHeightInPixels = 33; // Height is 30 pixels

            // Calculate width and height of game window
            int gameWidth = gameRect.right - gameRect.left;
            int gameHeight = gameRect.bottom - gameRect.top;

            // Calculate absolute pixel positions using the current gameRect size
            int startX = ( int ) ( gameWidth * weaponRegionStartXRatio );
            int startY = ( int ) ( gameHeight * weaponRegionStartYRatio ); // Adjust start Y up
            int width = ( int ) ( gameWidth * weaponRegionWidthRatio );

            // Height remains at 30 pixels
            int height = ( int ) ( weaponRegionHeightInPixels );

            return new Rectangle( startX, startY, width, height );
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed && disposing )
            {
                // Dispose of managed resources
            }

            disposed = true;
        }

    }


#if DEBUG
#if GETRECOILPATTERN
    /// <summary>
    /// this class is used to capture the recoil pattern of a weapon in a game.
    /// </summary>
    internal class RecoilPatternCapture : IDisposable
    {
        private bool disposed;
        private readonly List<(PointF, double)> recoilData;
        private bool capturing;
        private readonly PInvoke.RECT gameRect; // Capture the game window size
        private Color crosshairColor; // The manually entered hex code for the crosshair color
        private int shootKey = MouseInput.VK_LBUTTON; // Private internal variable for shoot key
        private readonly CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        private readonly object recoilDataLock = new(); // Create a lock object

        public RecoilPatternCapture( ref PInvoke.RECT gameRect, string colorCode )
        {
            this.gameRect = gameRect;
            recoilData = new List<(PointF, double)>();
            capturing = false;

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            // Create file if it doesn't exist
            if ( !Directory.Exists( "C:\\Users\\peter\\Documents\\ColorbotOutput" ) )
            {
                Directory.CreateDirectory( "C:\\Users\\peter\\Documents\\ColorbotOutput" );
                if ( !File.Exists( "C:\\Users\\peter\\Documents\\ColorbotOutput\\RecoilPattern.txt" ) )
                {
                    File.Create( "C:\\Users\\peter\\Documents\\ColorbotOutput\\RecoilPattern.txt" );
                }
            }

            SetCrosshairColor( colorCode );

            // Monitor shoot key without the need for custom key variable
            Task.Run( () => MonitorShootKey(), cancellationToken );
        }

        /// <summary>
        /// Finalizer to ensure the object is properly disposed of.
        /// </summary>
        ~RecoilPatternCapture()
        {
            Dispose( false );
        }

        /// <summary>
        /// Starts capturing the recoil pattern at small intervals.
        /// </summary>
        public void StartCapture()
        {
            if ( capturing )
                return; // Already capturing

            capturing = true;
            Task.Run( () =>
            {
                Stopwatch stopwatch = new();

                while ( !cancellationToken.IsCancellationRequested )
                {
                    stopwatch.Start();
                    while ( capturing && !cancellationToken.IsCancellationRequested )
                    {
                        // Capture crosshair position
                        PointF crosshairPosition = CaptureCrosshairPosition();

                        lock ( recoilDataLock )
                        {
                            // Record the crosshair position and elapsed time
                            recoilData.Add( (crosshairPosition, stopwatch.Elapsed.TotalMilliseconds) );
                        }

                        Thread.Sleep( 1 ); // Example interval in nanoseconds
                    }
                    Thread.Sleep( 1 );
                    stopwatch.Reset();
                }
            }, cancellationToken );
        }

        /// <summary>
        /// Stops capturing the recoil pattern and processes the data.
        /// </summary>
        public void StopCapture()
        {
            capturing = false;
            SaveRecoilPatternToFile();
        }

        /// <summary>
        /// Monitor the shoot key to trigger capture.
        /// </summary>
        private void MonitorShootKey()
        {
            while ( !cancellationToken.IsCancellationRequested )
            {
                if ( MouseInput.IsKeyHeld( ref shootKey ) && !capturing )
                {
                    // Start capturing when the shoot key is held
                    StartCapture();
                } else if ( !MouseInput.IsKeyHeld( ref shootKey ) && capturing )
                {
                    // Stop capturing when the shoot key is released
                    StopCapture();
                }

                // Sleep to prevent excessive CPU usage
                Utils.Watch.MicroSleep( 500 ); // Adjust the interval as necessary
            }
        }

        /// <summary>
        /// Capture the crosshair position from the screen.
        /// </summary>
        private PointF CaptureCrosshairPosition()
        {
            // Implement logic to capture the center of the screen from the game window rect
            Rectangle captureArea = new( 0, 0, ( gameRect.right - gameRect.left ), ( gameRect.bottom - gameRect.top ) );
            Bitmap screenGrab = ScreenCapture( ref captureArea );

            // Use an image processing algorithm to detect the red crosshair
            PointF crosshairPosition = DetectCrosshair( ref screenGrab );
            screenGrab.Dispose();
            return crosshairPosition;
        }

        /// <summary>
        /// Captures the screen area as a bitmap.
        /// </summary>
        private static Bitmap ScreenCapture( ref Rectangle captureArea )
        {
            Bitmap screenshot = new( captureArea.Width, captureArea.Height, PixelFormat.Format32bppArgb );
            using ( Graphics g = Graphics.FromImage( screenshot ) )
            {
                g.CopyFromScreen( captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, CopyPixelOperation.SourceCopy );
            }
            return screenshot;
        }

        /// <summary>
        /// Detects the crosshair from the captured screen area by scanning a narrow block in the middle.
        /// </summary>
        private unsafe PointF DetectCrosshair( ref Bitmap screenGrab )
        {
            // Narrow block in the center of the screen for detecting crosshair
            int blockWidth = 200; // Narrow vertical strip
            int blockStartX = ( screenGrab.Width / 2 ) - ( blockWidth / 2 ); // Center the block horizontally
            Rectangle lockRect = new( blockStartX, 0, blockWidth, screenGrab.Height );

            // Lock the block in the bitmap for fast access
            BitmapData bmpData = screenGrab.LockBits( lockRect, ImageLockMode.ReadOnly, screenGrab.PixelFormat );
            byte* scan0 = ( byte* ) bmpData.Scan0.ToPointer();
            int bytesPerPixel = Image.GetPixelFormatSize( screenGrab.PixelFormat ) / 8;

            // Default crosshair position at the center of the screen
            PointF crosshairPosition = GetGameWindowCenterArea();

            // Parallel loop to speed up scanning for the crosshair
            Parallel.For( 0, lockRect.Height, ( y, state ) =>
            {
                byte* row = scan0 + ( y * bmpData.Stride );

                for ( int x = 0; x < lockRect.Width; x++ )
                {
                    // Calculate the memory address of the pixel
                    int pixelIndex = x * bytesPerPixel;
                    byte blue = row[ pixelIndex ];
                    byte green = row[ pixelIndex + 1 ];
                    byte red = row[ pixelIndex + 2 ];

                    Color pixel = Color.FromArgb( red, green, blue );
                    // Check if this pixel matches the crosshair color
                    if ( IsMatchingColor( ref pixel, ref crosshairColor ) )
                    {
                        // Update crosshair position with the correct screen coordinates                        
                        crosshairPosition = new PointF( x + lockRect.X, y + lockRect.Y );
                        // Stop the parallel loop
                        state.Stop();
                    }
                }
            } );

            // Unlock the bitmap memory
            screenGrab.UnlockBits( bmpData );

            // If the crosshair wasn't found, return the center point as fallback
            return crosshairPosition;
        }

        /// <summary>
        /// Determines if a pixel matches the set crosshair color within a certain tolerance.
        /// </summary>
        private static bool IsMatchingColor( ref Color pixel, ref Color targetColor )
        {
            int tolerance = 10; // Adjust based on actual crosshair color precision
            return Math.Abs( pixel.R - targetColor.R ) < tolerance
                && Math.Abs( pixel.G - targetColor.G ) < tolerance
                && Math.Abs( pixel.B - targetColor.B ) < tolerance;
        }

        private void SaveRecoilPatternToFile()
        {
            lock ( recoilDataLock ) // Lock the recoilData for safe access
            {
                using StreamWriter writer = new( "C:\\Users\\peter\\Documents\\ColorbotOutput\\RecoilPattern.txt" );

                foreach ( var data in recoilData )
                {
                    writer.WriteLine( $"Position: {data.Item1}, Time: {data.Item2} ms" );
                }
            }
        }


        private void SetCrosshairColor( string colorCode )
        {
            crosshairColor = ColorTranslator.FromHtml( colorCode );
        }

        private PointF GetGameWindowCenterArea()
        {
            int centerX = ( gameRect.left + gameRect.right ) / 2;
            int centerY = ( gameRect.top + gameRect.bottom ) / 2;

            return new PointF( centerX, centerY );
        }

        internal void StopMonitoring()
        {
            StopCapture();
            cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed && disposing )
            {
                StopMonitoring();
                cancellationTokenSource.Dispose();
            }

            disposed = true;
        }
    }
#endif
#endif


    /// <summary>
    /// Represents a recoil pattern with a list of positions and total time.
    /// </summary>
    internal class RecoilPattern : IDisposable
    {
        private bool disposed;

        internal List<PointF> Pattern { get; private set; }
        internal double TotalTime { get; private set; }

        public RecoilPattern( List<PointF> pattern, double totalTime )
        {
            this.Pattern = pattern;
            this.TotalTime = totalTime;
        }

        public RecoilPattern()
        {
            this.Pattern = [];
            this.TotalTime = 0;
        }

        /// <summary>
        /// finalizer to ensure the object is properly disposed of.
        /// </summary>
        ~RecoilPattern()
        {
            Dispose( false );
        }

        /// <summary>
        /// Reads a recoil pattern from a text file based on the given format.
        /// </summary>
        public static RecoilPattern ReadFromFile( string filePath )
        {
            List<PointF> positions = new();
            double firstTime = 0, lastTime = 0;
            bool isFirstEntry = true;

            using ( StreamReader reader = new( filePath ) )
            {
                string? line;
                while ( ( line = reader.ReadLine() ) != null )
                {
                    if ( line.StartsWith( "Position:" ) )
                    {
                        // Extract X, Y, and Time
                        string[] parts = line.Split( new[] { 'X', 'Y', '=', ',', '{', '}', ' ' }, StringSplitOptions.RemoveEmptyEntries );
                        float x = float.Parse( parts[ 1 ] );
                        float y = float.Parse( parts[ 2 ] );
                        double time = double.Parse( parts[ 4 ] );

                        positions.Add( new PointF( x, y ) );

                        // Capture the first and last time entries
                        if ( isFirstEntry )
                        {
                            firstTime = time;
                            isFirstEntry = false;
                        }
                        lastTime = time;
                    }
                }
            }

            double totalTime = lastTime - firstTime;
            return new RecoilPattern( positions, totalTime );
        }

        /// <summary>
        /// Averages multiple recoil patterns into a single pattern.
        /// </summary>
        internal static RecoilPattern AveragePatterns( List<RecoilPattern> patterns )
        {
            // Use the pattern with the least positions, to avoid out of range exceptions
            int length = patterns.Min( p => p.Pattern.Count );

            List<PointF> averagedPattern = new( length );

            for ( int i = 0; i < length; i++ )
            {
                float avgX = patterns.Select( pattern => pattern.Pattern[ i ].X ).Average();
                float avgY = patterns.Select( pattern => pattern.Pattern[ i ].Y ).Average();

                averagedPattern.Add( new PointF( avgX, avgY ) );
            }

            return new RecoilPattern( averagedPattern, patterns.Select( pattern => pattern.TotalTime ).Average() );
        }

        /// <summary>
        /// Refactors the pattern based on the current game window size.
        /// </summary>
        public RecoilPattern RefactorPatternForWindowSize( ref float scaleX, ref float scaleY )
        {
            List<PointF> refactoredPattern = new();

            foreach ( var position in Pattern )
            {
                PointF scaledPosition = new( position.X * scaleX, position.Y * scaleY );
                refactoredPattern.Add( scaledPosition );
            }

            return new RecoilPattern( refactoredPattern, TotalTime );
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed && disposing )
            {
                Pattern.Clear();
            }

            disposed = true;
        }
    }


    internal static class RecoilPatternProcessor
    {
        private static Dictionary<string, RecoilPattern> patternDatabase = new();

        //original window size is static at 1440x2560       
        private static PInvoke.RECT originalWindowRect = new() { left = 0, top = 0, right = 1440, bottom = 2560 };
        internal static PInvoke.RECT OriginalWindowRect { get => originalWindowRect; }
        internal static CancellationTokenSource RecoilPatternSource { get; set; } = new();

        private static readonly Object RecoilPatternLock = new();
        private static RecoilPattern? currentPattern = null;


        /// <summary>
        /// Processes all patterns for each gun folder in the main directory, averages them, and stores them in the database.
        /// </summary>
        internal static void ProcessAllGunPatterns( string mainFolderPath )
        {
            // Get all subdirectories (which represent each gun folder)
            string[] gunFolders = Directory.GetDirectories( mainFolderPath );

            foreach ( string gunFolder in gunFolders )
            {
                ProcessGunPatterns( gunFolder );
            }
        }

        /// <summary>
        /// Processes all patterns for a specific gun folder, averages them, and stores them in the database.
        /// </summary>
        internal static void ProcessGunPatterns( string gunFolder )
        {
            List<RecoilPattern> patterns = new();

            for ( int i = 1; i <= 3; i++ )
            {
                string patternFile = Path.Combine( gunFolder, $"{Path.GetFileName( gunFolder )}-{i}.txt" );
                RecoilPattern pattern = RecoilPattern.ReadFromFile( patternFile );
                patterns.Add( pattern );
            }

            RecoilPattern averagedPattern = RecoilPattern.AveragePatterns( patterns );
            patternDatabase[ Path.GetFileName( gunFolder ) ] = averagedPattern;
        }

        /// <summary>
        /// Retrieves the recoil pattern for a specific gun.
        /// </summary>
        internal static RecoilPattern GetRecoilPattern( string gunName )
        {
            if ( patternDatabase.TryGetValue( gunName, out RecoilPattern? value ) )
            {
                return value;
            }

            throw new Exception( $"No recoil pattern found for gun: {gunName}" );
        }

        /// <summary>
        /// Refactors all stored patterns based on a new game window size.
        /// </summary>
        internal static void RefactorAllPatterns( ref PInvoke.RECT currentWindowRect )
        {

            float scaleX = ( float ) ( currentWindowRect.right - currentWindowRect.left ) / ( originalWindowRect.right - originalWindowRect.left );
            float scaleY = ( float ) ( currentWindowRect.bottom - currentWindowRect.top ) / ( originalWindowRect.bottom - originalWindowRect.top );

            foreach ( var gunName in patternDatabase.Keys.ToList() )
            {
                patternDatabase[ gunName ] = patternDatabase[ gunName ].RefactorPatternForWindowSize( ref scaleX, ref scaleY );
            }
        }


        internal static RecoilPattern CurrentPattern
        {
            get
            {
                lock ( RecoilPatternLock )
                {
                    return currentPattern!;
                }
            }
            set
            {
                lock ( RecoilPatternLock )
                {
                    currentPattern = value;
                }
            }
        }


#if DEBUG
        internal static async Task RecoilPatternThread( Logger logger )
        {
            List<string> CurrentAddedGuns = new()
            {
                "BERSERKER RB3", "BLACKOUT", "BUZZSAW RT40", "CRUSADER",
                "CYCLONE", "M25 HORNET", "M49 FURY", "M67 REAVER", "WHISPER"
            };

            Logger localLogger = logger;
            string lastName = "";
            PInvoke.RECT gameRect = PlayerData.GetRect();
            ScreenCaptureOCR screenCaptureOCR = new( ref gameRect );
            int buyKey = Utils.MouseInput.VK_B;
            int escapeKey = Utils.MouseInput.VK_ESCAPE;

            // Refactor patterns if the game window has changed
            if ( gameRect.top != OriginalWindowRect.top ||
                gameRect.bottom != OriginalWindowRect.bottom ||
                gameRect.right != OriginalWindowRect.right ||
                gameRect.left != OriginalWindowRect.left )
            {
                RefactorAllPatterns( ref gameRect );
            }

            // Automatically monitor the screen for weapon name changes
            while ( !RecoilPatternSource.Token.IsCancellationRequested )
            {
                if ( Utils.MouseInput.IsKeyPressed( ref buyKey ) )
                {
                    await HandleBuyState( escapeKey );
                    // Automatically check for weapon name change using OCR
                    string weaponName = screenCaptureOCR.PerformOCRForWeaponName();
                    if ( weaponName != lastName )
                    {
                        lastName = weaponName;
                        CurrentPattern = CurrentAddedGuns.Contains( weaponName )
                            ? GetRecoilPattern( weaponName )
                            : GetRecoilPattern( "BLACKOUT" );  // Default to BLACKOUT
                        localLogger.Log( $"Weapon name detected: {weaponName}" );
                    }
                }


                // If game window has changed, refactor patterns and update OCR rect
                var newGameRect = PlayerData.GetRect();
                if ( HasWindowChanged( ref gameRect, ref newGameRect ) )
                {
                    gameRect = newGameRect;
                    RefactorAllPatterns( ref gameRect );
                    screenCaptureOCR.GameRect = gameRect;
                }

                await Task.Delay( 500 ); // Adjust interval for how often you want to check the weapon name
            }

            screenCaptureOCR.Dispose();
        }
#endif

        internal static async Task RecoilPatternThread()
        {
            List<string> CurrentAddedGuns = new()
            {
                "BERSERKER RB3", "BLACKOUT", "BUZZSAW RT40", "CRUSADER",
                "CYCLONE", "M25 HORNET", "M49 FURY", "M67 REAVER", "WHISPER"
            };

            string lastName = "";
            PInvoke.RECT gameRect = PlayerData.GetRect();
            ScreenCaptureOCR screenCaptureOCR = new( ref gameRect );
            int buyKey = Utils.MouseInput.VK_B;
            int escapeKey = Utils.MouseInput.VK_ESCAPE;

            // Refactor patterns if the game window has changed
            if ( gameRect.top != OriginalWindowRect.top ||
                gameRect.bottom != OriginalWindowRect.bottom ||
                gameRect.right != OriginalWindowRect.right ||
                gameRect.left != OriginalWindowRect.left )
            {
                RefactorAllPatterns( ref gameRect );
            }

            // Automatically monitor the screen for weapon name changes
            while ( !RecoilPatternSource.Token.IsCancellationRequested )
            {
                if ( Utils.MouseInput.IsKeyPressed( ref buyKey ) )
                {
                    await HandleBuyState( escapeKey );
                    // Automatically check for weapon name change using OCR
                    string weaponName = screenCaptureOCR.PerformOCRForWeaponName();
                    if ( weaponName != lastName )
                    {
                        lastName = weaponName;
                        CurrentPattern = CurrentAddedGuns.Contains( weaponName )
                            ? GetRecoilPattern( weaponName )
                            : GetRecoilPattern( "BLACKOUT" );  // Default to BLACKOUT
                    }
                }


                // If game window has changed, refactor patterns and update OCR rect
                var newGameRect = PlayerData.GetRect();
                if ( HasWindowChanged( ref gameRect, ref newGameRect ) )
                {
                    gameRect = newGameRect;
                    RefactorAllPatterns( ref gameRect );
                    screenCaptureOCR.GameRect = gameRect;
                }

                await Task.Delay( 500 ); // Adjust interval for how often you want to check the weapon name
            }

            screenCaptureOCR.Dispose();
        }


        /// <summary>
        /// Handles the state while the player is buying (key press "B") and exits on ESC.
        /// </summary>
        private static async Task HandleBuyState( int escapeKey )
        {
            while ( true )
            {
                Utils.Watch.MicroSleep( 100 );
                if ( Utils.MouseInput.IsKeyPressed( ref escapeKey ) )
                {
                    await Task.Delay( 1300 ); // delay to ensure the buy menu is closed, and gun name is visible
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if the game window size has changed.
        /// </summary>
        private static bool HasWindowChanged( ref PInvoke.RECT oldRect, ref PInvoke.RECT newRect )
        {
            return oldRect.top != newRect.top || oldRect.bottom != newRect.bottom ||
                   oldRect.right != newRect.right || oldRect.left != newRect.left;
        }
    }

}





