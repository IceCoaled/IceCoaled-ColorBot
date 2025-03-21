#if DEBUG
//#define GETRECOILPATTERN
#define GUNNAME_SAVE
#endif

using System.Drawing.Imaging;
using System.Text;
using SCB;
using Tesseract;




namespace Recoil
{
    internal partial class ScreenCaptureOCR : IDisposable
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
#if  GUNNAME_SAVE
            screenshot.Save( FileManager.recoilFolder + "\\GunName.png", System.Drawing.Imaging.ImageFormat.Png );
#endif
            try
            {
                // Capture the region from the screen into the bitmap
                CaptureScreenRegion( ref captureArea, ref screenshot );

                // Preprocess the image (convert to grayscale, thresholding, etc.)
                PreprocessImage( ref screenshot );
                try
                {
                    // Perform OCR using Tesseract
                    return ExtractTextFromImage( ref screenshot );
                } finally
                {
                    // Manually dispose processedImage
                    screenshot.Dispose();
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
            screenshot.Save( FileManager.recoilFolder + "GunName.png" );
#endif
        }

        /// <summary>
        /// Preprocess the image to grayscale and apply thresholding, with minimal copying.
        /// </summary>
        private static void PreprocessImage( ref Bitmap img )
        {
            // Convert to grayscale, apply thresholding
            using ( Graphics g = Graphics.FromImage( img ) )
            {
                ColorMatrix colorMatrix = new
                (
                    [
                        [0.3f, 0.3f, 0.3f, 0, 0],
                        [0.59f, 0.59f, 0.59f, 0, 0],
                        [0.11f, 0.11f, 0.11f, 0, 0],
                        [0, 0, 0, 1, 0],
                        [0, 0, 0, 0, 1]
                    ]
                );
                ImageAttributes attributes = new();
                attributes.SetColorMatrix( colorMatrix );
                g.DrawImage( img, new Rectangle( 0, 0, img.Width, img.Height ), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes );
            }

            // Apply thresholding to increase contrast
            for ( int x = 0; x < img.Width; x++ )
            {
                for ( int y = 0; y < img.Height; y++ )
                {
                    Color pixelColor = img.GetPixel( x, y );
                    int thresholdValue = 150; // Adjust threshold if necessary
                    int newColorValue = ( pixelColor.R + pixelColor.G + pixelColor.B ) / 3 < thresholdValue ? 0 : 255;
                    img.SetPixel( x, y, Color.FromArgb( newColorValue, newColorValue, newColorValue ) );
                }
            }
        }


        /// <summary>
        /// Extracts text from the given preprocessed bitmap using Tesseract OCR.
        /// </summary>
        private static string ExtractTextFromImage( ref Bitmap img )
        {
            using var engine = new TesseractEngine( FileManager.tessdataFolder, "eng", EngineMode.Default );
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
                default:
                break;
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




    /// <summary>
    /// Represents a recoil pattern with a list of positions and total time.
    /// </summary>
    internal partial class RecoilPattern : IDisposable
    {
        private bool disposed;

        internal Dictionary<PointF, double> Pattern { get; private set; }

        public RecoilPattern( Dictionary<PointF, double> pattern )
        {
            this.Pattern = pattern;
        }

        public RecoilPattern()
        {
            this.Pattern = [];
        }

        /// <summary>
        /// finalizer to ensure the object is properly disposed of.
        /// </summary>
        ~RecoilPattern()
        {
            Dispose( false );
        }

        /// <summary>
        /// Refactors the pattern based on the current game window size.
        /// </summary>
        internal RecoilPattern RefactorPatternForWindowSize( ref float scaleX, ref float scaleY )
        {
            Dictionary<PointF, double> refactoredPattern = new();


            foreach ( var position in Pattern )
            {
                // Scale the X and Y positions based on the window size
                PointF scaledPosition = new( position.Key.X * scaleX, position.Key.Y * scaleY );
                double time = position.Value;
                refactoredPattern.Add( scaledPosition, time );
            }

            return new RecoilPattern( refactoredPattern );
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


    internal partial class RecoilPatternProcessor : IDisposable
    {
        private bool Disposed { get; set; } = false;

        // Recoil detection thread
        private Thread RecoilDetection { get; init; }
        internal CancellationTokenSource RecoilPatternSource { get; set; } = new();

        // recoil changed event for aiming features to sub to
        public event EventHandler<RecoilPattern> RecoilPatternChanged = delegate { };//< Shutting the compiler up

        // Recoil Database
        private readonly Dictionary<string, RecoilPattern> patternDatabase = [];
        private readonly object databaseLock = new();

        //original window size is static at 1440x2560       
        private PInvoke.RECT OriginalWindowRect { get; set; } = new() { left = 0, top = 0, right = 2560, bottom = 1440 };

        // For aiming features to get recoil pattern
        private readonly object RecoilPatternLock = new();
        private RecoilPattern? currentPattern = null;

        internal RecoilPatternProcessor()
        {
            // Read and process all recoil patterns
            ProcessAllGunPatterns();

            // Start the thread to monitor the recoil pattern           
            RecoilDetection = new( RecoilPatternThread );
            RecoilDetection.Start();
        }

        ~RecoilPatternProcessor()
        {
            Dispose( false );
        }

        private void RecoilPatternChangedHandler( RecoilPattern pattern )
        {
            RecoilPatternChanged?.Invoke( this, pattern );
        }


        /// <summary>
        /// Processes all patterns for each gun folder in the main directory, averages them, and stores them in the database.
        /// </summary>
        internal unsafe void ProcessAllGunPatterns()
        {
            // Get all subdirectories (which represent each gun folder)
            string[] gunFolders = Directory.GetDirectories( FileManager.recoilPatterns );

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            };

            try
            {
                Parallel.ForEach( gunFolders, parallelOptions, gunFolder =>
                {
                    ProcessGunPatterns( gunFolder );
                } );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( ex );
            }

        }

        /// <summary>
        /// Processes all patterns for a specific gun folder, averages them, and stores them in the database.
        /// </summary>
        private void ProcessGunPatterns( string gunFolder )
        {
            List<RecoilPattern> patterns = [];

            for ( int i = 1; i < 4; i++ )
            {
                string patternFile = Path.Combine( gunFolder, $"{Path.GetFileName( gunFolder )}-{i}.txt" );


                patterns.Add( ReadFromFile( patternFile ) );

#if DEBUG
                Logger.Log( $"Processed pattern: {patternFile}" );
#endif

            }

            bool locktaken = false;
            try
            {

                Monitor.Enter( RecoilPatternLock, ref locktaken );

                lock ( databaseLock )
                {
                    patternDatabase[ Path.GetFileName( gunFolder ) ] = AveragePatterns( patterns );
                }
            } finally
            {
                if ( locktaken )
                {
                    Monitor.Exit( RecoilPatternLock );
                }
            }


        }


        /// <summary>
        /// Reads a recoil pattern from a text file based on the given format.
        /// </summary>
        private static RecoilPattern ReadFromFile( string filePath )
        {

            // Create a dictionary to store the pattern
            Dictionary<PointF, double> pattern = [];

            try
            {
                using StreamReader reader = new( filePath );
                string? line;

                while ( ( line = reader.ReadLine() ) is not null )
                {
                    line = line.Trim();
                    try
                    {
                        if ( line.StartsWith( "Position:" ) )
                        {
                            // Extract X, Y, and Time
                            string[] parts = line.Split( [ 'X', 'Y', '=', ',', '{', '}', ' ' ], StringSplitOptions.RemoveEmptyEntries );
                            float x = float.Parse( parts[ 1 ] );
                            float y = float.Parse( parts[ 2 ] );
                            double time = double.Parse( parts[ 4 ] );

                            if ( pattern.ContainsKey( new PointF( x, y ) ) )
                            {
                                continue;
                            } else
                            {
                                // Add the position and time to the lists
                                if ( !pattern.TryAdd( new PointF( x, y ), time ) )
                                {
                                    ErrorHandler.HandleException( new Exception( "Failed to add position to pattern" ) );
                                }
                            }
                            Utils.Watch.MilliSleep( 1 ); // Dont pin the CPU
                        }

                    } catch ( FormatException ex )
                    {
                        ErrorHandler.HandleException( ex );
                    } catch ( IndexOutOfRangeException ex )
                    {
                        ErrorHandler.HandleException( ex );
                    }
                }
            } catch ( IOException ex )
            {
                ErrorHandler.HandleException( ex );
            }

            return new RecoilPattern( pattern );
        }


        /// <summary>
        /// Averages multiple recoil patterns into a single pattern.
        /// </summary>
        private static RecoilPattern AveragePatterns( List<RecoilPattern> patterns )
        {
            // Use the pattern with the least positions, to avoid out of range exceptions
            // We know there is only 3 patterns, so we can hardcode it
            int length = patterns.Min( p => p.Pattern.Count );

            Dictionary<PointF, double> averagedPattern = new();

            for ( int i = 0; i < length; i++ )
            {
                float cumulX = 0;
                float cumulY = 0;
                double cumulTime = 0;

                cumulX = ( patterns[ 0 ].Pattern.ElementAt( i ).Key.X + patterns[ 1 ].Pattern.ElementAt( i ).Key.X + patterns[ 2 ].Pattern.ElementAt( i ).Key.X ) / 3;
                cumulY = ( patterns[ 0 ].Pattern.ElementAt( i ).Key.Y + patterns[ 1 ].Pattern.ElementAt( i ).Key.Y + patterns[ 2 ].Pattern.ElementAt( i ).Key.Y ) / 3;
                cumulTime = ( patterns[ 0 ].Pattern.ElementAt( i ).Value + patterns[ 1 ].Pattern.ElementAt( i ).Value + patterns[ 2 ].Pattern.ElementAt( i ).Value ) / 3;

                // Look to see if the key already exists
                if ( averagedPattern.ContainsKey( new PointF( cumulX, cumulY ) ) )
                {
                    continue;
                } else
                {
                    averagedPattern.TryAdd( new PointF( cumulX, cumulY ), cumulTime );
                }

                Utils.Watch.MilliSleep( 1 ); // Dont pin the CPU
            }


            return new RecoilPattern( averagedPattern );
        }



        /// <summary>
        /// Retrieves the recoil pattern for a specific gun.
        /// </summary>
        internal RecoilPattern GetRecoilPattern( string gunName )
        {
            if ( patternDatabase.TryGetValue( gunName, out RecoilPattern? value ) )
            {
                return value;
            }

            ErrorHandler.HandleException( new Exception( $"No recoil pattern found for gun: {gunName}" ) );

            // This line is unreachable, but the compiler doesn't know that
            return null;
        }

        /// <summary>
        /// Refactors all stored patterns based on a new game window size.
        /// </summary>
        internal void RefactorAllPatterns( ref PInvoke.RECT currentWindowRect )
        {

            float scaleX = ( float ) ( currentWindowRect.right - currentWindowRect.left ) / ( OriginalWindowRect.right - OriginalWindowRect.left );
            float scaleY = ( float ) ( currentWindowRect.bottom - currentWindowRect.top ) / ( OriginalWindowRect.bottom - OriginalWindowRect.top );

            foreach ( var gunName in patternDatabase.Keys.ToList() )
            {
                patternDatabase[ gunName ] = patternDatabase[ gunName ].RefactorPatternForWindowSize( ref scaleX, ref scaleY );

#if DEBUG
                Logger.Log( $"Refactored: {gunName} recoil Pattern" );
#endif
            }
        }


        internal RecoilPattern CurrentPattern
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
                    RecoilPatternChangedHandler( currentPattern );
                }
            }
        }


        private void RecoilPatternThread()
        {
            List<string> CurrentAddedGuns =
            [
                "BERSERKER RB3", "BLACKOUT", "BUZZSAW RT40", "CRUSADER",
                "CYCLONE", "M25 HORNET", "M49 FURY", "M67 REAVER", "WHISPER"
            ];

            int buyKey = HidInputs.VK_B;
            int escapeKey = HidInputs.VK_ESCAPE;
            int backupKey = HidInputs.VK_F1;


            // Wait for the game window to be detected
            while ( PlayerData.GetHwnd() == nint.MaxValue )
            {
                Utils.Watch.SecondsSleep( 5 );
            }


            //setup OCR
            PInvoke.RECT gameRect = PlayerData.GetRect();
            ScreenCaptureOCR screenCaptureOCR = new( ref gameRect );


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
                if ( HidInputs.IsKeyPressed( ref buyKey ) )
                {
                    HandleBuyState( escapeKey );
                    // Automatically check for weapon name change using OCR
                    string weaponName = screenCaptureOCR.PerformOCRForWeaponName();
                    if ( !CurrentAddedGuns.Contains( weaponName ) )
                    {
                        CurrentPattern = GetRecoilPattern( "BLACKOUT" );  // Default to BLACKOUT
#if DEBUG
                        Logger.Log( $"Defaulted to BLACKOUT" );
#else
                        System.Media.SystemSounds.Question.Play();
#endif
                    } else
                    {
                        CurrentPattern = GetRecoilPattern( weaponName );
#if DEBUG
                        Logger.Log( $"Current weapon: {weaponName}" );
#else
                        System.Media.SystemSounds.Beep.Play();
#endif 
                    }
                }

                // If the scan doesnt pick up the weapon name, press F1 to try again
                if ( HidInputs.IsKeyPressed( ref backupKey ) )
                {
                    string weaponName = screenCaptureOCR.PerformOCRForWeaponName();
                    if ( !CurrentAddedGuns.Contains( weaponName ) )
                    {
                        CurrentPattern = GetRecoilPattern( "BLACKOUT" );  // Default to BLACKOUT
#if DEBUG
                        Logger.Log( $"Defaulted to BLACKOUT" );
#else
                        System.Media.SystemSounds.Question.Play();
#endif
                    } else
                    {
                        CurrentPattern = GetRecoilPattern( weaponName );
#if DEBUG
                        Logger.Log( $"Current weapon: {weaponName}" );
#else
                        System.Media.SystemSounds.Beep.Play();
#endif 
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

                Utils.Watch.MilliSleep( 500 ); // Adjust interval for how often you want to check the weapon name
            }

            screenCaptureOCR.Dispose();
        }


        /// <summary>
        /// Handles the state while the player is buying (key press "B") and exits on ESC.
        /// </summary>
        private static void HandleBuyState( int escapeKey )
        {
            while ( true )
            {
                Utils.Watch.SecondsSleep( 1 );
                if ( HidInputs.IsKeyPressed( ref escapeKey ) )
                {
                    Utils.Watch.SecondsSleep( 1 ); // delay to ensure the buy menu is closed, and gun name is visible
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



        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !Disposed )
            {
                RecoilPatternSource.Cancel();
                RecoilPatternSource.Dispose();
                Disposed = true;
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
        private int shootKey = HidInputs.VK_LBUTTON; // Private internal variable for shoot key
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
                if ( HidInputs.IsKeyHeld( ref shootKey ) && !capturing )
                {
                    // Start capturing when the shoot key is held
                    StartCapture();
                } else if ( !HidInputs.IsKeyHeld( ref shootKey ) && capturing )
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
                using StreamWriter writer = new( FilesAndFolder.recoilPatternFile );

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

    }
}





