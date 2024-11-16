namespace SCB
{
    internal static class Mathf
    {

        /// <summary>
        /// Calculates the Euclidean distance between two points in 2D space.
        /// </summary>
        /// <typeparam name="T">A numeric type that implements INumericType.</typeparam>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>Returns the distance between <paramref name="p1"/> and <paramref name="p2"/>.</returns>
        internal static T GetDistance<T>( ref PointF p1, ref PointF p2 ) where T : struct, IComparable<T>
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            double distanceSquared = dx * dx + dy * dy;
            double distance = Math.Sqrt( distanceSquared );

            // Convert the result to the desired type
            if ( typeof( T ) == typeof( int ) )
            {
                return ( T ) ( object ) ( int ) Math.Round( distance );
            } else if ( typeof( T ) == typeof( float ) )
            {
                return ( T ) ( object ) ( float ) distance;
            } else if ( typeof( T ) == typeof( double ) )
            {
                return ( T ) ( object ) distance;
            } else
            {
                ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( $"Unsupported type {typeof( T )}." ) );
            }

            return default;
        }




        /// <summary>
        /// Clamps the given value between 0 and 1.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Clamp01.html#:~:text=The%20random%20number%20is%20clamped%20//%20to%20between
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="value">The value to clamp.</param>
        /// <returns>Returns 0 if the value is less than 0, 1 if the value is greater than 1, otherwise the value itself.</returns>
        internal static T Clamp01<T>( T value ) where T : struct, IComparable<T>
        {
            T zero = ( T ) Convert.ChangeType( 0, typeof( T ) );
            T one = ( T ) Convert.ChangeType( 1, typeof( T ) );

            if ( value.CompareTo( zero ) < 0 )
            {
                return zero;
            } else if ( value.CompareTo( one ) > 0 )
            {
                return one;
            } else
            {
                return value;
            }
        }



        /// <summary>
        /// Clamps the given value between the specified minimum and maximum values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.Clamp.html#:~:text=Use%20Clamp%20to%20restrict%20a%20value%20to%20a
        /// </summary>
        public static dynamic Clamp( dynamic value, dynamic min, dynamic max )
        {
            if ( value < min )
                return min;
            if ( value > max )
                return max;
            return value;
        }

        /// <summary>
        /// Applies a fade function to smooth out a value.
        /// https://thebookofshaders.com/11/
        /// </summary>
        /// <param name="t">The value to fade (should be between 0 and 1).</param>
        /// <returns>Returns the smoothed value using a quintic fade curve.</returns>
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
        internal static double Grad( int hash, double x, double y )
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h is 12 or 14 ? x : 0;
            return ( ( h & 1 ) == 0 ? u : -u ) + ( ( h & 2 ) == 0 ? v : -v );
        }

        /// <summary>
        /// Applies a smoother step interpolation to ease transitions between two values.
        /// https://docs.unity3d.com/ScriptReference/Mathf.SmoothStep.html#:~:text=Description.%20Interpolates%20between%20min%20and%20max%20with%20smoothing
        /// </summary>
        /// <typeparam name="T">A numeric type (int, float, double).</typeparam>
        /// <param name="edge0">The start edge.</param>
        /// <param name="edge1">The end edge.</param>
        /// <param name="t">The value to smooth (should be between 0 and 1).</param>
        /// <returns>Returns a smoother step transition.</returns>
        internal static T SmootherStep<T>( T edge0, T edge1, T t ) where T : struct, IComparable<T>
        {

            t = Clamp01( ( T ) Convert.ChangeType( ( ( dynamic ) t - ( dynamic ) edge0 ) / ( ( dynamic ) edge1 - ( dynamic ) edge0 ), typeof( T ) ) );

            return ( T ) Convert.ChangeType( ( dynamic ) t * ( dynamic ) t * ( dynamic ) t * ( ( dynamic ) t * ( ( dynamic ) t * 6 - 15 ) + 10 ), typeof( T ) );
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
        internal static T InverseLerp<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( 1.0 / ( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * Clamp01( t ) ), typeof( T ) );
        }



        /// <summary>
        /// Performs inverse linear interpolation between two values unclamped.
        /// </summary>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns></returns>
        internal static T LerpUnclamped<T>( float a, float b, float t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * t, typeof( T ) );
        }


        /// <summary>
        /// Performs inverse linear interpolation between two values. unclamped.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor (typically between 0 and 1).</param>
        /// <returns></returns>
        internal static T InverseLerpUnclamped<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( 1.0 / ( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * t ), typeof( T ) );
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
        internal static T Lerp<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * Clamp01( t ), typeof( T ) );
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
        internal static T EaseInOut<T>( T a, T b, T t ) where T : struct, IComparable<T>
        {
            return ( T ) Convert.ChangeType( ( dynamic ) a + ( ( dynamic ) b - ( dynamic ) a ) * ( 0.5 * ( 1 - Math.Cos( Math.PI * ( double ) ( dynamic ) t ) ) ), typeof( T ) );
        }


        /// <summary>
        /// Smoothly transitions a value towards a target over time, using smoothing parameters for gradual movement.
        /// https://docs.unity3d.com/ScriptReference/Mathf.SmoothDamp.html#:~:text=Description.%20Gradually%20moves%20the%20current%20value%20towards%20a
        /// </summary>
        /// <param name="current">The current value that will be updated towards the target.</param>
        /// <param name="target">The target value that the current value should move towards.</param>
        /// <param name="currentVelocity">A reference to the current velocity, modified each call for damping effect.</param>
        /// <param name="smoothTime">The time in seconds it takes to reach the target value. Smaller values result in faster response.</param>
        /// <param name="deltaTime">The time since the last update (typically the frame duration).</param>
        /// <param name="maxSpeed">The maximum speed at which the value can move towards the target. Optional, with a default of <see cref="float.PositiveInfinity"/>.</param>
        /// <remarks>
        /// This function is particularly useful for smoothing movements, applying a spring-like effect, where the transition towards the target becomes progressively slower as it approaches.
        /// Internally, it prevents overshooting by clamping the result based on <paramref name="maxSpeed"/> and adjusting <paramref name="currentVelocity"/> over time.
        /// </remarks>
        /// <example>
        /// Example usage:
        /// <code>
        /// float currentValue = 0f;
        /// float targetValue = 10f;
        /// float velocity = 0f;
        /// float deltaTime = 0.02f; // Assuming a 50 FPS game loop
        /// float smoothTime = 0.5f;
        /// SmoothDamp(ref currentValue, targetValue, ref velocity, smoothTime, deltaTime);
        /// </code>
        /// </example>
        internal static void SmoothDamp( ref float current, float target, ref float currentVelocity, float smoothTime, float deltaTime, float maxSpeed = float.PositiveInfinity )
        {
            // Ensure smoothTime doesn't go too low to prevent erratic behavior, but allow fast response
            smoothTime = Mathf.Clamp( smoothTime, 0.01f, 1.0f );  // Set minimum smoothTime for stability

            // Adjusting the smoothing effect
            float num = 2f / smoothTime;  // The smaller smoothTime, the faster this reacts
            float num2 = num * deltaTime;
            float num3 = 1f / ( 1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2 );

            // Calculate the current difference between the target and the current value
            float num4 = current - target;
            float maxDelta = maxSpeed * smoothTime;

            // Clamp to prevent overshooting
            num4 = Mathf.Clamp( num4, -maxDelta, maxDelta );
            target = current - num4;

            // Compute velocity adjustments
            float num6 = ( currentVelocity + num * num4 ) * deltaTime;
            currentVelocity = ( currentVelocity - num * num6 ) * num3;

            // Update current to new smooth position
            current = target + ( num4 + num6 ) * num3;
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

        /// <summary>
        /// Predicts the future position of a moving object using its current and previous positions, and the time difference.
        /// https://docs.unity.cn/Packages/com.unity.physics@1.2/api/Unity.Physics.GraphicsIntegration.GraphicalSmoothingUtility.Extrapolate.html#:~:text=Extrapolate(in%20RigidTransform,%20in%20PhysicsVelocity,%20in%20PhysicsMass,%20float)%20Compute
        /// </summary>
        /// <param name="currentPos">The current position of the object.</param>
        /// <param name="previousPos">The previous position of the object.</param>
        /// <param name="deltaTime">The time difference between the two positions.</param>
        /// <param name="extrapolationTime">The time into the future for which to predict the position.</param>
        /// <returns>Returns the predicted future position.</returns>
        internal static PointF MotionExtrapolation( PointF currentPos, PointF previousPos, float deltaTime, float extrapolationTime )
        {
            // Calculate velocity (change in position over time)
            float velocityX = ( currentPos.X - previousPos.X ) / deltaTime;
            float velocityY = ( currentPos.Y - previousPos.Y ) / deltaTime;

            // Extrapolate future position assuming constant velocity
            float futurePosX = currentPos.X + ( velocityX * extrapolationTime );
            float futurePosY = currentPos.Y + ( velocityY * extrapolationTime );

            return new PointF( futurePosX, futurePosY );
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
