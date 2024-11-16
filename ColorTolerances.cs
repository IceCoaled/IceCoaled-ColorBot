namespace SCB
{

    /// <summary>The Range class.</summary>
    /// <typeparam name="T">Generic parameter.</typeparam>
    /// <param name="maximum">The maximum value of the range.</param>
    /// <param name="minimum">The minimum value of the range.</param>
    internal sealed class Range<T>( T minimum, T maximum ) where T : IComparable<T>
    {
        /// <summary>Minimum value of the range.</summary>
        internal T Minimum { get; private set; } = minimum;

        /// <summary>Maximum value of the range.</summary>
        internal T Maximum { get; private set; } = maximum;

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
    /// You could add much greater flexibility by making this generic, and adding a typical contstructor.
    /// This way you could get the type of T and set the ranges accordingly.
    /// </summary>
    /// <param name="redMin">Minimum red rgb value</param>
    /// <param name="redMax">Maximum red rgb value</param>
    /// <param name="greenMin">minimum green rgb value</param>
    /// <param name="greenMax">Maximum green rgb value</param>
    /// <param name="blueMin">minimum blue rgb value</param>
    /// <param name="blueMax">Maximum blue rgb value</param>
    internal sealed class ColorTolerance( int redMin, int redMax, int greenMin, int greenMax, int blueMin, int blueMax )
    {
        internal Range<int>? Red { get; private set; } = new Range<int>( redMin, redMax );
        internal Range<int>? Green { get; private set; } = new Range<int>( greenMin, greenMax );
        internal Range<int>? Blue { get; private set; } = new Range<int>( blueMin, blueMax );


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
            return Red!.Contains( red ) && Green!.Contains( green ) && Blue!.Contains( blue );
        }

        /// <summary>
        /// Overload of the IsColorInRange method to check if a specified Color object is within the defined tolerance ranges.
        /// </summary>
        /// <param name="color">Color object</param>
        /// <returns>Returns true if the color is within the range, otherwise false.</returns>
        internal bool IsColorInRange( Color color )
        {
            return IsColorInRange( color.R, color.G, color.B );
        }


        /// <summary>
        /// Validates if the color tolerance is set correctly.
        /// I.E the maximum must be greater than the minimum for all the color components.
        /// </summary>
        /// <return>Return true if the range is valid else false</return>
        internal bool ValidateColorTolerance()
        {
            return Red!.IsValid() && Green!.IsValid() && Blue!.IsValid();
        }
    }


    /// <summary>
    /// Represents a class for managing color tolerances, selected colors, and swap colors for various features.
    /// ive made this class abstract so that it can be inherited by other classes, and the methods can be overridden(futureproofed).
    /// </summary>
    internal class ToleranceBase
    {
        /// <summary>
        /// Gets the dictionary containing color tolerances for different color names.
        /// </summary>
        internal Dictionary<string, List<ColorTolerance>> Tolerances { get; private set; }

        /// <summary>
        /// Gets or sets the swap color used for highlighting or other visual effects.
        /// </summary>
        internal Color SwapColor { get; set; }

        /// <summary>
        /// Gets or sets the currently selected color name.
        /// </summary>
        internal string Selected { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToleranceBase"/> class with color tolerances and associated names.
        /// </summary>
        /// <param name="tolerances">A list of color tolerances for each color.</param>
        /// <param name="toleranceNames">A list of color names corresponding to each color tolerance.</param>
        /// <param name="swapColor">The swap color to be used for highlighting or swapping purposes.</param>
        /// <param name="selectedColor">The initial selected color name.</param>
        internal ToleranceBase( List<ColorTolerance> tolerances, List<string> toleranceNames, Color swapColor, string selectedColor )
        {
            Tolerances = [];
            for ( int i = 0; i < tolerances.Count; i++ )
            {
                Tolerances.TryAdd( toleranceNames[ i ], [ tolerances[ i ] ] );
            }
            SwapColor = swapColor;
            Selected = selectedColor;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToleranceBase"/> class with a dictionary of color tolerances.
        /// </summary>
        /// <param name="toleranceInputs">A dictionary of color names and their corresponding color tolerances.</param>
        /// <param name="swapColor">The swap color to be used for highlighting or swapping purposes.</param>
        /// <param name="selectedColor">The initial selected color name.</param>
        internal ToleranceBase( Dictionary<string, List<ColorTolerance>> toleranceInputs, Color swapColor, string selectedColor )
        {
            Tolerances = new Dictionary<string, List<ColorTolerance>>( toleranceInputs );
            SwapColor = swapColor;
            Selected = selectedColor;
        }

        /// <summary>
        /// Gets the color tolerance associated with the specified color name.
        /// </summary>
        /// <param name="color">The name of the color to retrieve the tolerance for.</param>
        /// <returns>The <see cref="ColorTolerance"/> associated with the specified color name, or throws an exception if not found.</returns>
        internal virtual ColorTolerance GetColorTolerance( string color )
        {
            if ( Tolerances.TryGetValue( color, out List<ColorTolerance>? value ) )
            {
                return value[ 0 ];
            }
            ErrorHandler.HandleException( new Exception( $"Color {color} not found" ) );
            return default;
        }

        /// <summary>
        /// Gets the color tolerance for the currently selected color.
        /// </summary>
        /// <returns>The <see cref="ColorTolerance"/> for the selected color, or throws an exception if not found.</returns>
        internal virtual ColorTolerance GetColorTolerance()
        {
            if ( Tolerances.TryGetValue( Selected, out List<ColorTolerance>? value ) )
            {
                return value[ 0 ];
            }
            ErrorHandler.HandleException( new Exception( "No color tolerance selected" ) );
            return default;
        }

        /// <summary>
        /// Sets the currently selected color by its name.
        /// </summary>
        /// <param name="color">The name of the color to select.</param>
        internal virtual void SetSelected( string color )
        {
            if ( Tolerances.ContainsKey( color ) )
            {
                Selected = color;
            } else
            {
                ErrorHandler.HandleException( new Exception( $"Color {color} not found" ) );
            }
        }

        /// <summary>
        /// Gets the name of a color given its color tolerance.
        /// </summary>
        /// <param name="colorTolerance">The color tolerance to find the associated color name for.</param>
        /// <returns>The name of the color, or throws an exception if not found.</returns>
        internal virtual string GetColorName( ColorTolerance colorTolerance )
        {
            if ( Tolerances.Any( x => x.Value[ 0 ] == colorTolerance ) )
            {
                return Tolerances.First( x => x.Value[ 0 ] == colorTolerance ).Key;
            }

            ErrorHandler.HandleException( new Exception( "Color not found" ) );
            return default;
        }

        /// <summary>
        /// Validates the input dictionary to ensure it matches the stored tolerances dictionary in count.
        /// </summary>
        /// <param name="ctorDictionary">The dictionary to validate against the internal tolerances dictionary.</param>
        /// <returns><c>true</c> if the dictionary matches the count; otherwise, <c>false</c>.</returns>
        internal virtual bool ValidateTolerances( ref Dictionary<string, List<ColorTolerance>> ctorDictionary )
        {
            if ( ctorDictionary.Count != Tolerances.Count )
            {
                ErrorHandler.HandleException( new Exception( $"Feature has {ctorDictionary.Count} elements, expected {Tolerances.Count}" ) );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates the input list to ensure its count matches the expected count in the stored tolerances dictionary.
        /// </summary>
        /// <param name="ctorList">The list to validate against the count of items in the internal tolerances dictionary.</param>
        /// <returns><c>true</c> if the list count matches the expected count; otherwise, <c>false</c>.</returns>
        internal virtual bool ValidateTolerances( List<ColorTolerance> ctorList )
        {
            if ( ctorList.Count != Tolerances.Count )
            {
                ErrorHandler.HandleException( new Exception( $"Feature has {ctorList.Count} elements, expected {Tolerances.Count}" ) );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the swap color used for highlighting or other visual effects.
        /// </summary>
        /// <returns>The current swap color.</returns>
        internal virtual Color GetSwapColor() => SwapColor;

        /// <summary>
        /// Sets the swap color used for highlighting or other visual effects.
        /// </summary>
        /// <param name="color">The color to set as the swap color.</param>
        internal virtual void SetSwapColor( Color color ) => SwapColor = color;

        /// <summary>
        /// Gets the currently selected color name.
        /// </summary>
        /// <returns>The name of the currently selected color.</returns>
        internal virtual string GetSelected() => Selected;

        /// <summary>
        /// Get the Count of the Dictionary
        /// </summary>
        internal virtual int Count => Tolerances.Count;
    }




    /// <summary>
    /// Manages color tolerances for character outlines, features, and outfits.
    /// </summary>
    internal sealed class ColorToleranceManager
    {
        /// <summary>
        /// Stores the color tolerances for character outlines.
        /// </summary>
        internal ToleranceBase CharacterOutlines { get; private set; }

        /// <summary>
        /// Stores a list of color tolerances for various character features.
        /// </summary>
        internal List<ToleranceBase> CharacterFeatures { get; private set; }

        /// <summary>
        /// Stores a list of color tolerances for different character outfits.
        /// </summary>
        internal List<ToleranceBase> OutfitColors { get; private set; }



#nullable disable //< Suppressing compiler warnings for nullable types, since we are initializing them in the constructor.
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorToleranceManager"/> class.
        /// </summary>
        internal ColorToleranceManager()
        {
            // Set up color tolerances for character outlines, features, and outfits
            if ( !InitOutlines() || !InitCharacterFeatures() || !InitCharacterOutfits() )
            {
                ErrorHandler.HandleException( new Exception( "ColorToleranceManager failed to initialize" ) );
            }

            // Register the OutlineUpdateHandler to handle outline color updates
            PlayerData.OnUpdate += OutlineUpdateHandler;

#if DEBUG
            Logger.Log( "ColorToleranceManager initialized successfully" );
#endif
        }
#nullable enable


        ~ColorToleranceManager()
        {
            // Unregister the OutlineUpdateHandler
            PlayerData.OnUpdate -= OutlineUpdateHandler;
        }


        private void OutlineUpdateHandler( object sender, PlayerUpdateCallbackEventArgs e )
        {
            if ( e.Key == UpdateType.OutlineColor )
            {
                CharacterOutlines.SetSelected( e.UpdatedVar );
            }
        }



        /// <summary>
        /// Initializes color tolerances for character outlines.
        /// </summary>
        /// <returns>True if initialization is successful; otherwise, false.</returns>
        private bool InitOutlines()
        {

            // Define color tolerances for various outline colors
            CharacterOutlines = new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
            {
                { "orange", new List<ColorTolerance> { new( 244, 255, 140, 244, 75, 108 ) } },
                { "red", new List<ColorTolerance> { new( 247, 255, 100, 130, 80, 135 ) } },
                { "green", new List<ColorTolerance> { new( 30, 110, 240, 255, 30, 97 ) } },
                { "cyan", new List<ColorTolerance> { new( 60, 110, 230, 255, 230, 255 ) } },
                { "yellow", new List<ColorTolerance> { new( 237, 255, 237, 255, 78, 133 ) } },
                { "purple", new List<ColorTolerance> { new( 194, 255, 60, 99, 144, 255 ) } }
            }, Color.Purple, "orange" );


            // Validate the number of elements in CharacterOutlines
            if ( CharacterOutlines.Count != 6 )
            {
                ErrorHandler.HandleException( new Exception( "CharacterOutlines has incorrect number of elements" ) );
                return false;
            }

#if DEBUG
            Logger.Log( "CharacterOutlines initialized successfully" );
#endif

            return true;
        }


        /// <summary>
        /// Initializes color tolerances for character features (e.g., hair, eyes, lips).
        /// </summary>
        /// <returns>True if initialization is successful; otherwise, false.</returns>
        private bool InitCharacterFeatures()
        {

            // Define color tolerances for different character features
            var Hair = new ColorTolerance[ 8 ]
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

            var Eyes = new ColorTolerance[ 8 ]
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

            var EyeBrows = new ColorTolerance[ 10 ]
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

            var Lips = new ColorTolerance[ 10 ]
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

            var SkinTones = new ColorTolerance[ 10 ]
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

            // Initialize CharacterFeatures with distinct swap colors
            CharacterFeatures =
            [
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "Hair", new List<ColorTolerance>( Hair ) }
                }, Color.SaddleBrown, "Hair" ),
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "Eyes", new List<ColorTolerance>( Eyes ) }
                }, Color.Teal, "Eyes" ),
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "EyeBrows", new List<ColorTolerance>( EyeBrows ) }
                }, Color.Tomato, "EyeBrows" ),
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "Lips", new List<ColorTolerance>( Lips ) }
                }, Color.DeepPink, "Lips" ),
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "SkinTones", new List<ColorTolerance>( SkinTones ) }
                }, Color.BurlyWood, "SkinTones" )
            ];


            // Validate the number of elements in CharacterFeatures
            if ( CharacterFeatures.Count != 5 )
            {
                ErrorHandler.HandleException( new Exception( "CharacterFeatures has incorrect number of elements" ) );
                return false;
            }

#if DEBUG
            Logger.Log( "CharacterFeatures initialized successfully" );
#endif

            return true;
        }

        /// <summary>
        /// Initializes color tolerances for various character outfits.
        /// </summary>
        /// <returns>True if initialization is successful; otherwise, false.</returns>
        private bool InitCharacterOutfits()
        {

            // Define color tolerances for different outfits
            OutfitColors =
            [
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "bloodlust", new List<ColorTolerance> { new( 0, 34, 0, 38, 0, 40 ), new( 3, 43, 6, 46, 10, 50 ), new( 0, 27, 0, 28, 0, 30 ) } }
                }, Color.Firebrick, "bloodlust" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "elegantOperative", new List<ColorTolerance> { new( 0, 23, 0, 24, 0, 22 ), new( 26, 66, 30, 70, 38, 78 ), new( 25, 65, 30, 70, 40, 80 ) } }
                    }, Color.DarkSlateGray, "elegantOperative" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "prince", new List<ColorTolerance> { new( 0, 20, 0, 20, 0, 20 ), new( 2, 42, 4, 44, 8, 48 ), new( 35, 75, 37, 77, 48, 88 ) } }
                }, Color.BlueViolet, "prince" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "starbright", new List<ColorTolerance> { new( 0, 21, 0, 21, 0, 21 ), new( 3, 43, 5, 45, 9, 49 ), new( 0, 27, 0, 28, 0, 29 ) } }
                }, Color.Gold, "starbright" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "generic", new List<ColorTolerance> { new( 0, 20, 0, 20, 0, 20 ), new( 0, 20, 0, 20, 0, 20 ), new( 0, 20, 0, 20, 0, 20 ) } } //< this covers honorIntBattle, popstar, zeroFour, risingStarAlpha, risingStarBeta
                }, Color.Silver, "generic" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "default1", new List<ColorTolerance> { new( 9, 49, 6, 46, 3, 43 ), new( 11, 51, 30, 70, 48, 88 ), new( 91, 131, 135, 175, 127, 167 ) } }
                }, Color.LightSalmon, "default1" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "default2", new List<ColorTolerance> { new( 8, 48, 10, 50, 8, 48 ), new( 34, 74, 62, 102, 65, 105 ), new( 104, 144, 114, 154, 109, 149 ), new( 25, 65, 60, 100, 60, 100 ) } }
                }, Color.SteelBlue, "default2" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "default3", new List<ColorTolerance> { new( 0, 35, 0, 35, 0, 33 ), new( 7, 47, 24, 64, 24, 64 ), new( 98, 138, 86, 126, 61, 101 ) } }
                }, Color.MediumSlateBlue, "default3" ),
                new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
                {
                    { "halloween", new List<ColorTolerance> { new( 0, 34, 0, 38, 0, 40 ), new( 3, 43, 6, 46, 10, 50 ), new( 0, 27, 0, 28, 0, 30 ) } }
                }, Color.OrangeRed, "halloween" )
            ];


            // Validate the number of elements in OutfitColors
            if ( OutfitColors.Count != 9 )
            {
                ErrorHandler.HandleException( new Exception( "OutfitColors has incorrect number of elements" ) );
                return false;
            }

#if DEBUG
            Logger.Log( "OutfitColors initialized successfully" );
#endif

            return true;
        }
    }
}
