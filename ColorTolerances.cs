
namespace SCB
{
    internal class ColorTolerance( int redMin, int redMax, int greenMin, int greenMax, int blueMin, int blueMax )
    {
        // Properties for color ranges
        /// <summary>
        /// Gets the allowable red channel values.
        /// </summary>
        public IReadOnlyCollection<int> Red { get; private set; } = new HashSet<int>( Enumerable.Range( redMin, redMax - redMin + 1 ) );
        /// <summary>
        /// Gets the allowable green channel values.
        /// </summary>
        public IReadOnlyCollection<int> Green { get; private set; } = new HashSet<int>( Enumerable.Range( greenMin, greenMax - greenMin + 1 ) );
        /// <summary>
        /// Gets the allowable blue channel values.
        /// </summary>
        public IReadOnlyCollection<int> Blue { get; private set; } = new HashSet<int>( Enumerable.Range( blueMin, blueMax - blueMin + 1 ) );



        /// <summary>
        /// Determines if a specified RGB color is within the defined tolerance ranges.
        /// </summary>
        /// <param name="red">The red component of the color to check.</param>
        /// <param name="green">The green component of the color to check.</param>
        /// <param name="blue">The blue component of the color to check.</param>
        /// <returns>Returns true if the color is within the range, otherwise false.</returns>
        public bool IsColorInRange( int red, int green, int blue )
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
        public bool IsColorInRange( IEnumerable<int> color )
        {
            // Ensure the enumerable contains exactly 3 components for RGB.
            if ( color.Count() < 3 )
            {
                ErrorHandler.HandleExceptionNonExit( new ArgumentException( "Color must have 3 components." ) );
            }

            // Check if the color components are within range by delegating to the primary IsColorInRange method.
            return IsColorInRange( color.ElementAt( 0 ), color.ElementAt( 1 ), color.ElementAt( 2 ) );
        }
    }



    /// <summary>
    /// Class to handle color tolerances for color filtering.
    /// </summary>
    internal static class ColorTolerances
    {

        private static ColorTolerance? orange;
        private static ColorTolerance? red;
        private static ColorTolerance? green;
        private static ColorTolerance? cyan;
        private static ColorTolerance? yellow;
        private static ColorTolerance? purple;
        private static ColorTolerance? brownPlateCarrier;
        private static ColorTolerance? tanPlateCarrier;

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
            cyan = new ColorTolerance( 66, 100, 246, 255, 246, 255 );
            yellow = new ColorTolerance( 237, 255, 237, 255, 78, 133 );
            purple = new ColorTolerance( 194, 255, 60, 99, 144, 255 );
            tanPlateCarrier = new ColorTolerance( 180, 200, 169, 189, 156, 176 );
            brownPlateCarrier = new ColorTolerance( 131, 151, 116, 136, 101, 121 );

            // Create dictionary with color names as keys and their respective ColorTolerance objects as values
            colorDictionary = new Dictionary<string, ColorTolerance>
            {
                { "orange", orange },
                { "red", red },
                { "green", green },
                { "cyan", cyan },
                { "yellow", yellow },
                { "purple", purple },
                { "tanPlateCarrier", tanPlateCarrier },
                { "brownPlateCarrier", brownPlateCarrier }
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
    }
}
