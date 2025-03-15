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

        internal virtual void AssignCustomTolerance( string toleranceName, ColorTolerance customTolerance )
        {
            foreach ( var pair in Tolerances )
            {
                if ( toleranceName == pair.Key )
                {
                    pair.Value[ 0 ] = customTolerance; //< This is [0] because this is specifically for outline colors, which is 1 tolerance per key.
                }
            }
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
        internal List<ToleranceBase> CharacterFeatures { get; }

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
            // instantiate character features
            CharacterFeatures = [];
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
                foreach ( var outlineName in CharacterOutlines.Tolerances.Keys )
                {
                    if ( e.UpdatedVar == outlineName )
                    {
                        CharacterOutlines.SetSelected( e.UpdatedVar );
                        return;
                    }
                }

                int red = 0;
                int green = 0;
                int blue = 0;


                string[] customColor = e.UpdatedVar.ToString().Split( ',' );
                foreach ( var value in customColor )
                {
                    string rgbName = value.Split( '=' )[ 0 ];
                    string rgbValue = value.Split( '=' )[ 1 ];

                    if ( rgbName.Contains( 'R' ) )
                    {
                        red = Convert.ToInt32( rgbValue );

                    } else if ( rgbName.Contains( 'G' ) )
                    {
                        green = Convert.ToInt32( rgbValue );
                    } else if ( rgbName.Contains( 'B' ) ) //< This wont get hit, but its here for safety.
                    {
                        blue = Convert.ToInt32( rgbValue );
                    }
                }
                CharacterOutlines.AssignCustomTolerance( "custom", CustomColorToleranceGenerator( red, green, blue ) );
                CharacterOutlines.SetSelected( "custom" );
            }
        }



        /// <summary>
        /// Initializes color tolerances for character outlines.
        /// We have a custom slot but realistically we would need a really solid algorithm,
        /// To determine the proper color thresholds for it, just part of the POC
        /// </summary>
        /// <returns>True if initialization is successful; otherwise, false.</returns>
        private bool InitOutlines()
        {

            // Define color tolerances for various outline colors
            CharacterOutlines = new ToleranceBase( new Dictionary<string, List<ColorTolerance>>
            {
                { "orange", new List<ColorTolerance> { new( 235, 255, 138, 255, 116, 167 ), new( 230, 255, 178, 255, 70, 111 ), new( 200, 233, 150, 201, 70, 111 ), new( 160, 200, 130, 170, 74, 95 ) } },
                { "red", new List<ColorTolerance> { new( 238, 255, 90, 130, 80, 135 ), new( 218, 255, 0, 50, 0, 50 ), new( 182, 220, 90, 108, 95, 121 ), new( 170, 211, 0, 40, 0, 40 ), new( 232, 255, 80, 100, 78, 98 ) } },
                { "green", new List<ColorTolerance> { new( 90, 190, 210, 255, 60, 100 ) } },
                { "cyan", new List<ColorTolerance> { new( 58, 112, 208, 255, 208, 255 ) } },
                { "yellow", new List<ColorTolerance> { new( 210, 255, 210, 255, 115, 220 ) } },
                { "purple", new List<ColorTolerance> { new( 192, 255, 55, 125, 166, 255 ), new( 223, 255, 126, 168, 230, 255 ), new( 238, 255, 45, 65, 238, 255 ), new( 140, 170, 30, 75, 135, 165 ) } },
                { "custom", new List<ColorTolerance> { new(0,0,0,0,0,0 ) } },
            }, Color.Purple, "orange" );//< We set default color selection to avoid any bugs off rip.


            // Validate the number of elements in CharacterOutlines
            if ( CharacterOutlines.Count != 7 )
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
            CharacterFeatures.Add( new ToleranceBase( new Dictionary<String, List<ColorTolerance>>
            {
                { "gold", new List<ColorTolerance> { new( 220, 240, 190, 208, 129, 146 ), new( 200, 212, 162, 176, 117, 130 ) } },
                { "orange", new List<ColorTolerance> { new( 190, 210, 96, 120, 45, 68 ) } },
                { "peach", new List<ColorTolerance> { new( 150, 200, 119, 148, 73, 107 ) } },
                { "oak", new List<ColorTolerance> { new( 119, 129, 40, 59, 15, 40 ) } },
                { "brown", new List<ColorTolerance> { new( 70, 86, 35, 45, 40, 45 ), new( 74, 83, 37, 43, 28, 33 ), new( 85, 90, 17, 22, 0, 5 ) } },
                { "grey", new List<ColorTolerance> { new( 190, 208, 207, 222, 211, 230 ), new( 160, 180, 182, 202, 200, 226 ) } },
                { "green", new List<ColorTolerance> { new( 80, 112, 132, 165, 90, 130 ) } },
                { "purple", new List<ColorTolerance> { new( 118, 135, 47, 89, 140, 173 ) } },
            }, Color.FromArgb( 223, 242, 164 ), "hair" ) );


            if ( CharacterFeatures[ 0 ].Count != 8 )
            {
                ErrorHandler.HandleException( new Exception( "Character Hair has incorrect number of elements" ) );
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


        private static ColorTolerance CustomColorToleranceGenerator( int red, int green, int blue )
        {
            static (int min, int max) GetToleranceRange( int rgb )
            {
                int range = rgb switch
                {
                    >= 200 => ( int ) ( rgb * 0.20f ),
                    >= 10 => ( int ) ( rgb * 0.15f ),
                    _ => ( int ) ( rgb * 0.10f ),
                };

                int min = int.Max( 0, rgb - range );
                int max = int.Min( 255, rgb + range );
                return (min, max);
            }

            var redMinMax = GetToleranceRange( red );
            var greenMinMax = GetToleranceRange( green );
            var blueMinMax = GetToleranceRange( blue );

            return new( redMinMax.min, redMinMax.max, greenMinMax.min, greenMinMax.max, blueMinMax.min, blueMinMax.max );
        }
    }
}
