using System.Numerics;
using System.Runtime.CompilerServices;

namespace SCB
{
    internal static class Mathf
    {
        /// <summary>
        /// Converts string to ushorts then compresses the ushorts into uints
        /// This is capped at 8 utf16 letters as this is for a specific use
        /// Strings are alread utf16 so this is perfect for use
        /// We specifically pack the letters in word swapped little endian
        /// </summary>
        /// <param name="word">Input word to be coverted</param>
        /// <returns>returns compress data in uint array 4 long</returns>
        public static uint[] ConvertUtf16ToUintArray( ref string word )
        {
            ushort[] utfWord = new ushort[ 8 ];
            uint[] result = new uint[ 4 ];
            if ( word.Length > 8 )
            {
                throw new ArgumentException( "Word is too long" );
            } else
            {

                for ( int i = 0; i < 8; i++ )
                {
                    if ( i < word.Length ) // Check if within the string's length
                    {
                        utfWord[ i ] = ( ( ushort ) word[ i ] );
                    } else
                    {
                        utfWord[ i ] = 0; // Zero out remaining array values
                    }
                }
            }
            /*
             * Original:    h     a     i     r
             *UTF-16:    0068  0061  0069  0072
             *In memory: 0x00610068  0x00720069
                        [   ah    ]  [   ri    ]
            */


            result[ 0 ] = ( ( ( uint ) utfWord[ 0 ] ) | ( ( ( uint ) utfWord[ 1 ] ) << 16 ) );

            result[ 1 ] = ( ( ( uint ) utfWord[ 2 ] ) | ( ( ( uint ) utfWord[ 3 ] ) << 16 ) );

            result[ 2 ] = ( ( ( uint ) utfWord[ 4 ] ) | ( ( ( uint ) utfWord[ 5 ] ) << 16 ) );

            result[ 3 ] = ( ( ( uint ) utfWord[ 6 ] ) | ( ( ( uint ) utfWord[ 7 ] ) << 16 ) );

            return result;
        }



        /// <summary>
        /// Calculates the Euclidean distance between two points in 2D space.
        /// </summary>
        /// <typeparam name="T">A numeric type that implements INumericType.</typeparam>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>Returns the distance between <paramref name="p1"/> and <paramref name="p2"/>.</returns>
        internal static T GetDistance<T>( ref PointF p1, ref PointF p2 ) where T : struct, INumber<T>, IFloatingPoint<T>, IRootFunctions<T>, IExponentialFunctions<T>
        {
            return T.Sqrt( T.Exp2( T.CreateChecked( p2.X - p1.X ) ) + T.Exp2( T.CreateChecked( p2.Y - p1.Y ) ) );
        }




        /// <summary>
        /// Clamps the given value between 0 and 1.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Clamp01.html#:~:text=The%20random%20number%20is%20clamped%20//%20to%20between
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="value">The value to clamp.</param>
        /// <returns>Returns 0 if the value is less than 0, 1 if the value is greater than 1, otherwise the value itself.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static T Clamp01<T>( T value ) where T : struct, INumber<T>
        {
            return T.Clamp( value, T.Zero, T.One );
        }



        /// <summary>
        /// Clamps the given value between the specified minimum and maximum values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Clamp.html#:~:text=Use%20Clamp%20to%20restrict%20a%20value%20to%20a
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static T Clamp<T>( T value, T min, T max ) where T : struct, INumber<T>
        {
            return T.Clamp( value, min, max );
        }

        /// <summary>
        /// Applies a smooth step interpolation to ease transitions between two values.
        /// https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mathf.SmoothStep.html
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="edge0">The start edge.</param>
        /// <param name="edge1">The end edge.</param>
        /// <param name="t">The value to smooth (should be between 0 and 1).</param>
        /// <returns>Returns a smoother step transition.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static T SmoothStep<T>( T edge0, T edge1, T t ) where T : struct, INumber<T>
        {

            return t <= edge0 ? T.Zero : t >= edge1 ? T.One : ( t * t * ( T.CreateChecked( 3 ) - T.CreateChecked( 2 ) * t ) );
        }


        /// <summary>
        /// Performs inverse linear interpolation between two values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.InverseLerp.html#:~:text=Description.%20Determines%20where%20a%20value%20lies%20between%20two
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns>Returns the inversely scaled value.</returns>
        internal static T InverseLerp<T>( T a, T b, T t ) where T : struct, INumber<T>
        {
            return T.CreateChecked( 1 ) / ( a + ( b - a ) * t );
        }

        /// <summary>
        /// Linearly interpolates between two values, clamped between 0 and 1.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Lerp.html#:~:text=Mathf.Lerp.%20Switch%20to%20Manual.%20Declaration.%20public%20static%20float
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns>Returns the interpolated value.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static T Lerp<T>( T a, T b, T t ) where T : struct, INumber<T>
        {
            return a + ( b - a ) * t;
        }


        /// <summary>
        /// Performs cosine-based ease-in-out interpolation for smoother transitions.
        /// https://docs.unity3d.com/ScriptReference/AnimationCurve.EaseInOut.html#:~:text=Declaration.%20public%20static%20AnimationCurve%20EaseInOut%20(float%20timeStart,%20float
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns>Returns the eased value.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static T EaseInOut<T>( T a, T b, T t ) where T : struct, INumber<T>, ITrigonometricFunctions<T>
        {
            return a + ( b - a ) * ( T.CreateChecked( 0.5 ) * ( T.CreateChecked( 1 ) - T.CreateChecked( T.Cos( T.Pi * t ) ) ) );
        }



        /// <summary>
        /// Predicts the future position of a moving object using its current and previous positions, and the time difference.
        /// https://docs.unity.cn/Packages/com.unity.physics@1.2/api/Unity.Physics.GraphicsIntegration.GraphicalSmoothingUtility.Extrapolate.html#:~:text=Extrapolate(in%20RigidTransform,%20in%20PhysicsVelocity,%20in%20PhysicsMass,%20float)%20Compute
        /// </summary>
        /// <param name="currentPos">The current position of the object.</param>
        /// <param name="previousPos">The previous position of the object.</param>
        /// <param name="deltaTime">The time difference between the two positions.</param>
        /// <param name="extrapolationTime">The time into the future for which to predict the position.</param>
        /// <returns>Returns the predicted future position.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static PointF MotionExtrapolation( ref PointF currentPos, ref PointF previousPos, float deltaTime, float extrapolationTime )
        {
            // Calculate velocity (change in position over time)
            // Extrapolate future position assuming constant velocity
            return new PointF( ( currentPos.X + ( ( ( currentPos.X - previousPos.X ) / deltaTime ) * extrapolationTime ) ),
                ( currentPos.Y + ( ( ( currentPos.Y - previousPos.Y ) / deltaTime ) * extrapolationTime ) ) );
        }

        ///------------Perlin Noise------------///

        /// <summary>
        /// Applies a fade function to smooth out a value.
        /// https://thebookofshaders.com/11/
        /// </summary>
        /// <param name="t">The value to fade (should be between 0 and 1).</param>
        /// <returns>Returns the smoothed value using a quintic fade curve.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static double Fade( double t )
        {
            return t * t * t * ( t * ( t * 6 - 15 ) + 10 ); // Quintic curve for smoother transitions
        }

        /// <summary>
        /// Computes gradient based on hash and coordinates for Perlin noise.
        /// https://mrl.cs.nyu.edu/~perlin/noise/
        /// </summary>
        /// <param name="hash">The hash value for determining gradient direction.</param>
        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <returns>Returns the dot product of the gradient vector and the distance vector.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static double Grad( int hash, double x, double y )
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h is 12 or 14 ? x : 0;
            return ( ( h & 1 ) == 0 ? u : -u ) + ( ( h & 2 ) == 0 ? v : -v );
        }



        /// <summary>
        /// Generates 2D Perlin noise at a given point.
        /// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
        /// https://flafla2.github.io/2014/08/09/perlinnoise.html
        /// </summary>
        /// <param name="x">The x-coordinate of the point.</param>
        /// <param name="y">The y-coordinate of the point.</param>
        /// <returns>Returns the Perlin noise value at the point.</returns>
        internal static double PerlinNoise( double x, double y )
        {
            int X = ( int ) Math.Floor( x ) & 255;
            int Y = ( int ) Math.Floor( y ) & 255;
            x -= Math.Floor( x );
            y -= Math.Floor( y );
            double u = Fade( x );
            double v = Fade( y );
            int A = p[ X ] + Y;
            int AA = p[ A ];
            int AB = p[ A + 1 ];
            int B = p[ X + 1 ] + Y;
            int BA = p[ B ];
            int BB = p[ B + 1 ];

            return Lerp(
                Lerp( Grad( p[ AA ], x, y ), Grad( p[ BA ], x - 1, y ), u ),
                Lerp( Grad( p[ AB ], x, y - 1 ), Grad( p[ BB ], x - 1, y - 1 ), u ),
                v
            );
        }


        // Permutation table for Perlin noise
        private static readonly int[] p = new int[ 512 ];

        private static readonly int[] permutation = [ 151, 160, 137, 91, 90, 15,
            131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142,
            8, 99, 37, 240, 21, 10, 23, 190, 6, 148, 247, 120, 234, 75, 0, 26,
            197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237,
            149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134,
            139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133,
            230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25,
            63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200,
            196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3,
            64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255,
            82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42,
            223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153,
            101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79,
            113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242,
            193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249,
            14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204,
            176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222,
            114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
        ];

        internal static void SetupPermutationTable()
        {
            for ( int i = 0; i < 256; i++ )
            {
                p[ i ] = permutation[ i ];
                p[ 256 + i ] = permutation[ i ];  // Repeat the array
            }
        }
    }

}
