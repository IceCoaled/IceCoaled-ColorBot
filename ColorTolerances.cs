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
        /// Validates the input list to ensure its count matches the expected count in the stored tolerances dictionary.
        /// </summary>
        /// <param name="ctorList">The list to validate against the count of items in the internal tolerances dictionary.</param>
        /// <returns><c>true</c> if the list count matches the expected count; otherwise, <c>false</c>.</returns>
        internal virtual bool ValidateTolerances( List<ColorTolerance> ctorList )
        {
            if ( ctorList.Count != Tolerances.Values.First().Count ) //< This should always be one. So this should be safe
            {
                ErrorHandler.HandleException( new Exception( $"Feature has {ctorList.Count} elements, expected {Tolerances.Values.First().Count}" ) );
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
        /// List of all swap colors used for highlighting or other visual effects.
        /// </summary>
        internal List<Color> SwapColorsList { get; private set; }



#nullable disable //< Suppressing compiler warnings for nullable types, since we are initializing them in the constructor.
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorToleranceManager"/> class.
        /// </summary>
        internal ColorToleranceManager()
        {
            // Set up color tolerances for character outlines, features, and outfits
            if ( !InitOutlines() || !InitCharacterFeatures() )
            {
                ErrorHandler.HandleException( new Exception( "ColorToleranceManager failed to initialize" ) );
            }

            // Get all swap colors used for highlighting or other visual effects
            GetAllSwapColors();

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
                { "orange", new List<ColorTolerance> { new( 243, 255, 138, 246, 73, 110 ) } },
                { "red", new List<ColorTolerance> { new( 245, 255, 98, 132, 78, 137 ) } },
                { "green", new List<ColorTolerance> { new( 28, 112, 238, 255, 28, 100 ) } },
                { "cyan", new List<ColorTolerance> { new( 58, 112, 228, 255, 228, 255 ) } },
                { "yellow", new List<ColorTolerance> { new( 235, 255, 235, 255, 76, 135 ) } },
                { "purple", new List<ColorTolerance> { new( 192, 255, 58, 102, 142, 255 ) } }
            }, Color.Purple, "outlnz" );


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

            // Define color tolerances for hair colors
            var Hair = new ColorTolerance[ 9 ]
            {
                new(206, 216, 179, 189, 126, 136),
                new(69, 79, 76, 86, 88, 98),
                new(181, 191, 108, 118, 66, 76),
                new(121, 131, 108, 118, 82, 92),
                new(91, 101, 51, 61, 28, 38),
                new(72, 82, 31, 41, 22, 32),
                new(190, 200, 206, 216, 223, 233),
                new(105, 115, 174, 184, 132, 142),
                new(121, 131, 79, 89, 154, 164)
            };

            // Define color tolerances for skin tones
            var Skin = new ColorTolerance[ 10 ]
            {
                new(143, 153, 80, 90, 64, 74),
                new(140, 150, 84, 94, 61, 71),
                new(154, 164, 96, 106, 69, 79),
                new(176, 186, 116, 126, 88, 98),
                new(187, 197, 122, 132, 88, 98),
                new(212, 222, 153, 163, 110, 120),
                new(214, 224, 165, 175, 124, 134),
                new(204, 214, 146, 156, 124, 134),
                new(220, 230, 157, 167, 132, 142),
                new(220, 230, 168, 178, 148, 158)
            };

            // Initialize CharacterFeatures with distinct swap colors
            CharacterFeatures =
            [
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "Hair", new List<ColorTolerance>( Hair ) }
                }, Color.DeepPink, "Hair" ),
                new( new Dictionary<string, List<ColorTolerance>>
                {
                     { "Skin", new List<ColorTolerance>( Skin ) }
                }, Color.Gold, "Skin" )
            ];


            // Validate the number of elements in CharacterFeatures
            if ( CharacterFeatures.Count != 2 )
            {
                ErrorHandler.HandleException( new Exception( "CharacterFeatures has incorrect number of elements" ) );
                return false;
            }

            if ( !CharacterFeatures[ 0 ].ValidateTolerances( Hair.AsEnumerable().ToList() ) )
            {
                ErrorHandler.HandleException( new Exception( "CharacterFeatures( Hair ) has incorrect number of elements" ) );
                return false;
            }

            if ( !CharacterFeatures[ 1 ].ValidateTolerances( Skin.AsEnumerable().ToList() ) )
            {
                ErrorHandler.HandleException( new Exception( "CharacterFeatures( Skin ) has incorrect number of elements" ) );
                return false;
            }

#if DEBUG
            Logger.Log( "CharacterFeatures initialized successfully" );
#endif

            return true;
        }

        /// <summary>
        /// Gets all swap colors used for highlighting or other visual effects.
        /// </summary>  
        private void GetAllSwapColors()
        {
            SwapColorsList =
            [
                CharacterOutlines.GetSwapColor()
            ];
            foreach ( var feature in CharacterFeatures )
            {
                SwapColorsList.Add( feature.GetSwapColor() );
            }
        }
    }
}
