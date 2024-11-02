using System.Reflection;

namespace SCB
{

    /// <summary>The Range class.</summary>
    /// <typeparam name="T">Generic parameter.</typeparam>
    internal class Range<T> where T : IComparable<T>
    {
        /// <summary>Minimum value of the range.</summary>
        internal T Minimum { get; set; }

        /// <summary>Maximum value of the range.</summary>
        internal T Maximum { get; set; }

        /// <summary>Presents the Range in readable format.</summary>
        /// <returns>String representation of the Range</returns>
        public override string ToString()
        {
            return string.Format( "[{0} - {1}]", this.Minimum, this.Maximum );
        }

        /// <summary>Determines if the range is valid.</summary>
        /// <returns>True if range is valid, else false</returns>
        internal bool IsValid()
        {
            return this.Minimum.CompareTo( this.Maximum ) <= 0;
        }

        /// <summary>Determines if the provided value is inside the range.</summary>
        /// <param name="value">The value to test</param>
        /// <returns>True if the value is inside Range, else false</returns>
        internal bool Contains( T value )
        {
            return ( this.Minimum.CompareTo( value ) <= 0 ) && ( value.CompareTo( this.Maximum ) <= 0 );
        }

        /// <summary>Determines if this Range is inside the bounds of another range.</summary>
        /// <param name="Range">The parent range to test on</param>
        /// <returns>True if range is inclusive, else false</returns>
        internal bool IsInsideRange( Range<T> range )
        {
            return this.IsValid() && range.IsValid() && range.Contains( this.Minimum ) && range.Contains( this.Maximum );
        }

        /// <summary>Determines if another range is inside the bounds of this range.</summary>
        /// <param name="Range">The child range to test</param>
        /// <returns>True if range is inside, else false</returns>
        internal bool ContainsRange( Range<T> range )
        {
            return this.IsValid() && range.IsValid() && this.Contains( range.Minimum ) && this.Contains( range.Maximum );
        }
    }



    /// <summary>
    /// Color tolerance class to handle color tolerances for color filtering.
    /// </summary>
    /// <param name="redMin">Minimum red rgb value</param>
    /// <param name="redMax">Maximum red rgb value</param>
    /// <param name="greenMin">minimum green rgb value</param>
    /// <param name="greenMax">Maximum green rgb value</param>
    /// <param name="blueMin">minimum blue rgb value</param>
    /// <param name="blueMax">Maximum blue rgb value</param>
    internal class ColorTolerance( int redMin, int redMax, int greenMin, int greenMax, int blueMin, int blueMax ) : IDisposable
    {
        private bool disposed = false;
        internal Range<int>? Red { get; private set; } = new Range<int> { Minimum = redMin, Maximum = redMax };
        internal Range<int>? Green { get; private set; } = new Range<int> { Minimum = greenMin, Maximum = greenMax };
        internal Range<int>? Blue { get; private set; } = new Range<int> { Minimum = blueMin, Maximum = blueMax };


        ~ColorTolerance()
        {
            Dispose( false );
        }

        /// <summary>
        /// Determines if a specified RGB color is within the defined tolerance ranges.
        /// </summary>
        /// <param name="red">The red component of the color to check.</param>
        /// <param name="green">The green component of the color to check.</param>
        /// <param name="blue">The blue component of the color to check.</param>
        /// <returns>Returns true if the color is within the range, otherwise false.</returns>
        internal bool IsColorInRange( int red, int green, int blue )
        {
            // Check if all components (red, green, and blue) are within their respective ranges.
            return Red.Contains( red ) && Green.Contains( green ) && Blue.Contains( blue );
        }

        /// <summary>
        /// Determines if a specified RGB color, represented as an <see cref="IEnumerable{T}"/>, is within the defined tolerance ranges.
        /// </summary>
        /// <param name="color">An enumerable representing the RGB components of the color (must contain 3 values).</param>
        /// <returns>Returns true if the color is within the range, otherwise false.</returns>
        /// <exception cref="ArgumentException">Thrown when the color enumerable does not contain exactly 3 components.</exception>
        internal bool IsColorInRange( IEnumerable<int> color )
        {
            // Ensure the enumerable contains exactly 3 components for RGB.
            if ( color.Count() < 3 )
            {
                ErrorHandler.HandleExceptionNonExit( new ArgumentException( "Color must have 3 components." ) );
            }

            // Check if the color components are within range by delegating to the primary IsColorInRange method.
            return IsColorInRange( color.ElementAt( 0 ), color.ElementAt( 1 ), color.ElementAt( 2 ) );
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !disposed )
            {
                Red = null;
                Green = null;
                Blue = null;
            }

            disposed = true;
        }
    }



    /// <summary>
    /// Class to handle color tolerances for enemy outlines specifically(user selected)
    /// </summary>
    internal static class ColorTolerances
    {

        private static ColorTolerance? orange;
        private static ColorTolerance? red;
        private static ColorTolerance? green;
        private static ColorTolerance? cyan;
        private static ColorTolerance? yellow;
        private static ColorTolerance? purple;

        private static Dictionary<string, ColorTolerance>? colorDictionary;

        private static ColorTolerance? selected;
        readonly private static object selectedLock = new();


        /// <summary>
        /// Sets up the color tolerances for the different color groups.
        /// </summary>
        internal static void SetupColorTolerances()
        {
            orange = new ColorTolerance( 244, 255, 140, 244, 75, 108 );
            red = new ColorTolerance( 247, 255, 100, 130, 80, 135 );
            green = new ColorTolerance( 30, 110, 240, 255, 30, 97 );
            cyan = new ColorTolerance( 60, 110, 230, 255, 230, 255 );
            yellow = new ColorTolerance( 237, 255, 237, 255, 78, 133 );
            purple = new ColorTolerance( 194, 255, 60, 99, 144, 255 );

            // Create dictionary with color names as keys and their respective ColorTolerance objects as values
            colorDictionary = new Dictionary<string, ColorTolerance>
            {
                { "orange", orange },
                { "red", red },
                { "green", green },
                { "cyan", cyan },
                { "yellow", yellow },
                { "purple", purple },
            };

#if DEBUG
            Logger.Log( "Color tolerances set successfully" );
#endif
        }

        /// <summary>
        /// Gets the color tolerance based on the color name.
        /// </summary>
        /// <param name="color">The name of the color.</param>
        /// <returns>The color tolerance ranges.</returns>
        /// <exception cref="Exception">Thrown when the color is not found.</exception>
        internal static ColorTolerance GetColorTolerance( string color )
        {
            if ( colorDictionary != null && colorDictionary.TryGetValue( color, out ColorTolerance? value ) )
            {
                return value;
            }
            ErrorHandler.HandleException( new Exception( $"Color {color} not found" ) );

            // This line is unreachable, but the compiler doesn't know that
            return null;
        }

        /// <summary>
        /// Gets the currently selected color tolerance.
        /// </summary>
        /// <returns>The selected color tolerance.</returns>
        /// <exception cref="Exception">Thrown when the selected color is null.</exception>
        internal static ColorTolerance GetColorTolerance()
        {
            lock ( selectedLock )
            {
                if ( selected != null )
                {
                    return selected;
                }
                ErrorHandler.HandleException( new Exception( "No color tolerance selected" ) );
            }

            // This line is unreachable, but the compiler doesn't know that
            return null;
        }

        /// <summary>
        /// Sets the selected color tolerance by color name.
        /// </summary>
        /// <param name="color">The name of the color to select.</param>
        /// <exception cref="Exception">Thrown when the color is not found.</exception>
        internal static void SetColorTolerance( string color )
        {
            lock ( selectedLock )
            {
                if ( colorDictionary != null && colorDictionary.ContainsKey( color ) )
                {
                    selected = colorDictionary[ color ];
                } else
                {
                    ErrorHandler.HandleException( new Exception( $"Color {color} not found" ) );
                }
            }
        }

        /// <summary>
        /// Gets the color name for the given color tolerance.
        /// </summary>
        /// <param name="colorTolerance">The color tolerance to get the name for.</param>
        /// <returns>The name of the color, or "Color not found".</returns>
        internal static string GetColorName( ColorTolerance colorTolerance )
        {
            lock ( selectedLock )
            {
                if ( colorDictionary != null )
                {
                    foreach ( var item in colorDictionary )
                    {
                        if ( item.Value == colorTolerance )
                        {
                            return item.Key;
                        }
                    }
                }
                ErrorHandler.HandleException( new Exception( "Color not found" ) );
            }

            // This line is unreachable, but the compiler doesn't know that
            return null;
        }


        /// <summary>
        /// Returns the swap color for if an outline is found
        /// </summary>
        /// <returns></returns>
        internal static Color GetSwapColor() => Color.Purple;
    }




    /// <summary>
    /// This class holds the color tolerances for different character features, such as skin tones, hair, eyes, eyebrows, and lips.
    /// It also provides methods to access these features, perform setup, and check correctness.
    /// </summary>
    internal class CharacterFeatureTolerances : IDisposable
    {
        private bool Disposed = false;

        /// <summary>
        /// Dictionary to validate if all the expected values for each feature are set correctly.
        /// </summary>
        private readonly Dictionary<string, int> featureTCount;

        /// <summary>
        /// Gets the list of character features with their respective color tolerances.
        /// </summary>
        internal List<Tuple<string, ColorTolerance[]>?> CharacterFeatures { get; private set; }

        /// <summary>
        /// Gets the swap color used to highlight features.
        /// </summary>
        internal Color SwapColor { get; private set; } = Color.LawnGreen;

        /// <summary>
        /// Color tolerances for character skin tones.
        /// </summary>
        private ColorTolerance[]? SkinTones { get; set; }

        /// <summary>
        /// Color tolerances for character hair.
        /// </summary>
        private ColorTolerance[]? Hair { get; set; }

        /// <summary>
        /// Color tolerances for character eyes.
        /// </summary>
        private ColorTolerance[]? Eyes { get; set; }

        /// <summary>
        /// Color tolerances for character eyebrows.
        /// </summary>
        private ColorTolerance[]? EyeBrows { get; set; }

        /// <summary>
        /// Color tolerances for character lips.
        /// </summary>
        private ColorTolerance[]? Lips { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterFeatureTolerances"/> class.
        /// Sets up the color tolerances for each feature and validates them.
        /// </summary>
        internal CharacterFeatureTolerances()
        {
            // Setup the expected counts for each feature
            featureTCount = new Dictionary<string, int>
            {
                { "SkinTones", 10 },
                { "Hair", 8 },
                { "Eyes", 8 },
                { "EyeBrows", 10 },
                { "Lips", 10 }
            };

            // Setup the color tolerances
            SetupHair();
            SetupEyes();
            SetupEyeBrows();
            SetupLips();
            SetupSkinTones();

            // Validate if all the values were set correctly
            if ( CheckFeatureTCount() )
            {
#if DEBUG
                Logger.Log( "CharacterFeatureTolerances set successfully" );
#endif
            }

            // Setup the character features tuple
            SetupCharacterFeatures();
        }

        /// <summary>
        /// Destructor to clean up resources.
        /// </summary>
        ~CharacterFeatureTolerances()
        {
            Dispose( false );
        }

        /// <summary>
        /// Sets up the character features as tuples containing feature name and color tolerances.
        /// </summary>
        private void SetupCharacterFeatures()
        {
            CharacterFeatures = new List<Tuple<string, ColorTolerance[]>?>
            {
                new("SkinTones", SkinTones!),
                new("Hair", Hair!),
                new("Eyes", Eyes!),
                new("EyeBrows", EyeBrows!),
                new("Lips", Lips!)
            };
        }

        /// <summary>
        /// Sets up the color tolerances for hair.
        /// </summary>
        private void SetupHair()
        {
            Hair = new ColorTolerance[ 8 ]
            {
            new(13, 53, 49, 89, 61, 101),
            new(50, 90, 78, 118, 70, 110),
            new(16, 56, 54, 94, 58, 98),
            new(25, 65, 57, 97, 57, 97),
            new(33, 73, 61, 101, 56, 96),
            new(35, 75, 64, 104, 62, 102),
            new(18, 58, 43, 83, 57, 97),
            new(14, 54, 32, 72, 45, 85)
            };
        }

        /// <summary>
        /// Sets up the color tolerances for eyes.
        /// </summary>
        private void SetupEyes()
        {
            Eyes = new ColorTolerance[ 8 ]
            {
            new(0, 38, 18, 58, 25, 65),
            new(29, 69, 20, 60, 15, 55),
            new(48, 88, 73, 113, 79, 119),
            new(65, 105, 81, 121, 86, 126),
            new(75, 115, 85, 125, 85, 125),
            new(68, 108, 74, 114, 38, 78),
            new(84, 124, 77, 117, 50, 90),
            new(71, 111, 61, 101, 36, 76)
            };
        }

        /// <summary>
        /// Sets up the color tolerances for eyebrows.
        /// </summary>
        private void SetupEyeBrows()
        {
            EyeBrows = new ColorTolerance[ 10 ]
            {
            new(16, 56, 37, 77, 45, 85),
            new(38, 78, 30, 70, 25, 65),
            new(53, 93, 44, 84, 37, 77),
            new(90, 130, 74, 114, 55, 95),
            new(110, 150, 89, 129, 63, 103),
            new(38, 78, 28, 68, 19, 59),
            new(80, 120, 39, 79, 22, 62),
            new(105, 145, 76, 116, 68, 108),
            new(135, 175, 129, 169, 135, 175),
            new(43, 83, 37, 77, 30, 70)
            };
        }

        /// <summary>
        /// Sets up the color tolerances for lips.
        /// </summary>
        private void SetupLips()
        {
            Lips = new ColorTolerance[ 10 ]
            {
            new(22, 62, 42, 82, 52, 92),
            new(50, 90, 44, 84, 49, 89),
            new(79, 119, 66, 106, 64, 104),
            new(67, 107, 54, 94, 53, 93),
            new(65, 105, 50, 90, 45, 85),
            new(80, 120, 55, 95, 38, 78),
            new(104, 144, 60, 100, 46, 86),
            new(104, 144, 58, 98, 43, 83),
            new(104, 144, 68, 108, 56, 96),
            new(23, 63, 18, 58, 13, 53)
            };
        }

        /// <summary>
        /// Sets up the color tolerances for skin tones.
        /// </summary>
        private void SetupSkinTones()
        {
            SkinTones = new ColorTolerance[ 10 ]
            {
            new(17, 57, 40, 80, 70, 110),
            new(68, 108, 54, 94, 58, 98),
            new(61, 101, 48, 88, 40, 80),
            new(66, 106, 49, 89, 36, 76),
            new(63, 103, 61, 101, 54, 94),
            new(84, 124, 76, 116, 69, 109),
            new(74, 114, 69, 109, 69, 109),
            new(80, 120, 72, 112, 69, 109),
            new(92, 132, 79, 119, 67, 107),
            new(14, 54, 20, 60, 25, 65)
            };
        }

        /// <summary>
        /// Checks if all the color tolerances were set correctly.
        /// </summary>
        /// <returns>True if setup is correct; otherwise, false.</returns>
        private bool CheckFeatureTCount()
        {
            foreach ( var feature in featureTCount )
            {
                var field = this.GetType().GetField( feature.Key, BindingFlags.NonPublic | BindingFlags.Instance );
                if ( field == null )
                {
                    ErrorHandler.HandleException( new Exception( $"Field {feature.Key} not found" ) );
                    return false;
                }

                if ( field.GetValue( this ) is not Array array )
                {
                    ErrorHandler.HandleException( new Exception( $"Field {feature.Key} is not an array" ) );
                    return false;
                }

                if ( array.Length != feature.Value )
                {
                    ErrorHandler.HandleException( new Exception( $"Field {feature.Key} has {array.Length} elements, expected {feature.Value}" ) );
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the swap color used for highlighting features.
        /// </summary>
        /// <returns>The swap color.</returns>
        internal Color GetSwapColor() => SwapColor;

        /// <summary>
        /// Gets the color tolerances for a specific feature.
        /// </summary>
        /// <param name="feature">The feature name.</param>
        /// <returns>An array of <see cref="ColorTolerance"/> for the feature.</returns>
        internal ColorTolerance[] GetColorTolerances( string feature )
        {
            foreach ( var tCheck in CharacterFeatures )
            {
                if ( tCheck!.Item1 == feature )
                {
                    return tCheck.Item2;
                }
            }
            ErrorHandler.HandleException( new Exception( $"Feature {feature} not found" ) );
            return Array.Empty<ColorTolerance>();
        }

        /// <summary>
        /// Gets a tuple containing the swap color and color tolerances for a specific feature.
        /// </summary>
        /// <param name="feature">The feature name.</param>
        /// <returns>A tuple containing the swap color and an array of color tolerances.</returns>
        internal Tuple<Color, ColorTolerance[]> GetSwapToleranceTuple( string feature )
        {
            foreach ( var tCheck in CharacterFeatures )
            {
                if ( tCheck!.Item1 == feature )
                {
                    return new Tuple<Color, ColorTolerance[]>( SwapColor, tCheck.Item2 );
                }
            }
            ErrorHandler.HandleException( new Exception( $"Feature {feature} not found" ) );
            return null!;
        }

        /// <summary>
        /// Gets the list of character features with their color tolerances.
        /// </summary>
        /// <returns>A list of character feature tuples.</returns>
        internal List<Tuple<string, ColorTolerance[]>?> GetCharacterFeatures() => CharacterFeatures;

        /// <summary>
        /// Disposes of the class and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Releases resources used by the class.
        /// </summary>
        /// <param name="disposing">Whether to release managed resources.</param>
        protected virtual void Dispose( bool disposing )
        {
            if ( disposing && !Disposed )
            {
                SkinTones = null;
                Hair = null;
                Eyes = null;
                EyeBrows = null;
                Lips = null;

                CharacterFeatures.Clear();
            }

            Disposed = true;
        }
    }


    /// <summary>
    /// This class holds the color tolerances for different character outfits.
    /// It provides methods to set up these tolerances, access the outfits, and perform validation.
    /// </summary>
    internal class OutfitColorTolerances : IDisposable
    {
        private bool Disposed = false;

        /// <summary>
        /// Dictionary for validating that all outfit color tolerances have been set correctly.
        /// </summary>
        internal Dictionary<string, int> OutfitTCount { get; private set; }

        /// <summary>
        /// Gets the list of outfits with their respective color tolerances.
        /// </summary>
        internal List<Tuple<string, ColorTolerance[]>?> Outfits { get; private set; }

        /// <summary>
        /// Gets or sets the swap color used for highlighting outfits.
        /// </summary>
        private Color SwapColor { get; set; } = Color.HotPink;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutfitColorTolerances"/> class.
        /// Sets up outfit color tolerances and validates them.
        /// </summary>
        internal OutfitColorTolerances()
        {
            // Setup the expected counts for each outfit
            OutfitTCount = new Dictionary<string, int>
        {
            { "bloodlust", 3 },
            { "elegantOperative", 3 },
            { "honorInBattle", 3 },
            { "popstar", 3 },
            { "zeroFour", 3 },
            { "starbright", 3 },
            { "risingStarAlpha", 3 },
            { "risingStarBeta", 3 },
            { "default1", 3 },
            { "default2", 4 },
            { "default3", 3 },
            { "halloween", 3 }
        };

            // Setup outfit color tolerances
            SetupOutfits();

            // Validate if all outfit color tolerances were set correctly
            if ( CheckOutfitTCount() )
            {
#if DEBUG
                Logger.Log( "OutfitColorTolerances set successfully" );
#endif
            }
        }

        /// <summary>
        /// Destructor to clean up resources.
        /// </summary>
        ~OutfitColorTolerances()
        {
            Dispose( false );
        }

        /// <summary>
        /// Sets up the outfit color tolerances.
        /// </summary>
        private void SetupOutfits()
        {
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "bloodlust", [ new ColorTolerance( 0, 34, 0, 38, 0, 40 ), new ColorTolerance( 3, 43, 6, 46, 10, 50 ), new ColorTolerance( 0, 27, 0, 28, 0, 30 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "elegantOperative", [ new ColorTolerance( 0, 23, 0, 24, 0, 22 ), new ColorTolerance( 26, 66, 30, 70, 38, 78 ), new ColorTolerance( 25, 65, 30, 70, 40, 80 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "prince", [ new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 2, 42, 4, 44, 8, 48 ), new ColorTolerance( 35, 75, 37, 77, 48, 88 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "honorInBattle", [ new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 0, 28, 0, 29, 0, 31 ), new ColorTolerance( 0, 20, 0, 20, 0, 20 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "popstar", [ new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 0, 23, 0, 23, 0, 24 ), new ColorTolerance( 0, 20, 0, 20, 0, 20 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "zeroFour", [ new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 0, 21, 0, 21, 0, 21 ), new ColorTolerance( 0, 20, 0, 20, 0, 20 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "starbright", [ new ColorTolerance( 0, 21, 0, 21, 0, 21 ), new ColorTolerance( 3, 43, 5, 45, 9, 49 ), new ColorTolerance( 0, 27, 0, 28, 0, 29 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "risingStarAlpha", [ new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 0, 20, 0, 20, 0, 20 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "risingStarBeta", [ new ColorTolerance( 0, 20, 0, 20, 0, 20 ), new ColorTolerance( 0, 22, 0, 22, 0, 22 ), new ColorTolerance( 0, 20, 0, 20, 0, 20 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "default1", [ new ColorTolerance( 9, 49, 6, 46, 3, 43 ), new ColorTolerance( 11, 51, 30, 70, 48, 88 ), new ColorTolerance( 91, 131, 135, 175, 127, 167 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "default2", [ new ColorTolerance( 8, 48, 10, 50, 8, 48 ), new ColorTolerance( 34, 74, 62, 102, 65, 105 ), new ColorTolerance( 104, 144, 114, 154, 109, 149 ), new ColorTolerance( 25, 65, 60, 100, 60, 100 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "default3", [ new ColorTolerance( 0, 35, 0, 35, 0, 33 ), new ColorTolerance( 7, 47, 24, 64, 24, 64 ), new ColorTolerance( 98, 138, 86, 126, 61, 101 ) ] ) );
            Outfits.Add( new Tuple<string, ColorTolerance[]>( "halloween", [ new ColorTolerance( 0, 34, 0, 38, 0, 40 ), new ColorTolerance( 3, 43, 6, 46, 10, 50 ), new ColorTolerance( 0, 27, 0, 28, 0, 30 ) ] ) );
        }


        /// <summary>
        /// Gets the swap color used for highlighting outfits.
        /// </summary>
        /// <returns>The swap color.</returns>
        internal Color GetSwapColor() => SwapColor;

        /// <summary>
        /// Gets the color tolerances for a specific outfit.
        /// </summary>
        /// <param name="outfit">The name of the outfit.</param>
        /// <returns>An array of <see cref="ColorTolerance"/> for the specified outfit.</returns>
        internal ColorTolerance[] GetColorTolerances( string outfit )
        {
            foreach ( var tCheck in Outfits )
            {
                if ( tCheck!.Item1 == outfit )
                {
                    return tCheck.Item2;
                }
            }
            ErrorHandler.HandleException( new Exception( $"Outfit {outfit} not found" ) );
            return Array.Empty<ColorTolerance>();
        }

        /// <summary>
        /// Gets a tuple containing the swap color and color tolerances for a specific outfit.
        /// </summary>
        /// <param name="outfit">The name of the outfit.</param>
        /// <returns>A tuple containing the swap color and an array of color tolerances.</returns>
        internal Tuple<Color, ColorTolerance[]> GetSwapToleranceTuple( string outfit )
        {
            foreach ( var tCheck in Outfits )
            {
                if ( tCheck!.Item1 == outfit )
                {
                    return new Tuple<Color, ColorTolerance[]>( SwapColor, tCheck.Item2 );
                }
            }
            ErrorHandler.HandleException( new Exception( $"Outfit {outfit} not found" ) );
            return null!;
        }

        /// <summary>
        /// Gets the list of outfits with their color tolerances.
        /// </summary>
        /// <returns>A list of outfit tuples.</returns>
        internal List<Tuple<string, ColorTolerance[]>?> GetOutfits() => Outfits;

        /// <summary>
        /// Validates if all the outfit color tolerances have been set correctly.
        /// </summary>
        /// <returns>True if all outfit tolerances are set correctly; otherwise, false.</returns>
        private bool CheckOutfitTCount()
        {
            foreach ( var tCheck in Outfits.Zip( OutfitTCount, Tuple.Create ) )
            {
                if ( tCheck.Item1.Item1 == tCheck.Item2.Key && tCheck.Item1.Item2.Length == tCheck.Item2.Value )
                {
                    continue;
                } else
                {
                    ErrorHandler.HandleException( new Exception( $"Outfit {tCheck.Item1.Item1} has {tCheck.Item1.Item2.Length} elements, expected {tCheck.Item2.Value}" ) );
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Disposes of the class and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Releases resources used by the class.
        /// </summary>
        /// <param name="disposing">Whether to release managed resources.</param>
        protected virtual void Dispose( bool disposing )
        {
            if ( disposing && !Disposed )
            {
                Outfits.Clear();
            }

            Disposed = true;
        }
    }
}
