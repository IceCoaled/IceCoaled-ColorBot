
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
    internal class ColorTolerance( int redMin, int redMax, int greenMin, int greenMax, int blueMin, int blueMax )
    {
        internal Range<int> Red { get; private set; } = new Range<int> { Minimum = redMin, Maximum = redMax };
        internal Range<int> Green { get; private set; } = new Range<int> { Minimum = greenMin, Maximum = greenMax };
        internal Range<int> Blue { get; private set; } = new Range<int> { Minimum = blueMin, Maximum = blueMax };


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
    }
}
