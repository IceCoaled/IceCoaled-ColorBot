using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using SCB;

namespace Utils
{

    /// <summary>
    /// files and folders used by the application.
    /// used for easy access to file paths and folder names.
    /// </summary>
    internal static class FilesAndFolders
    {
        //Folders
        internal const string configFolder = @"./config/";

        internal const string tessdataFolder = @"./tessdata";

        internal const string debugFolder = @"./debug/";

        internal const string enemyScansFolder = @"./debug/enemyscans/";

        internal const string recoilFolder = @"./recoil/";

        internal const string recoilPatterns = @"./recoil/patterns/";

        //Files
        internal const string recoilPatternFile = @"./recoil/RecoilPattern.txt";

        internal const string exceptionLogFile = @"./debug/exceptionLog.txt";

        internal const string tessEngineFile = @"./tessdata/eng.traineddata";

        internal const string shaderFile = @"./shaderfiles/shader.hlsl";

        internal static readonly string gameSettingsFile = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), @"Spectre\Saved\Config\WindowsClient\GameUserSettings.ini" );


        /// <summary>
        /// function to download a gun recoil patterns repo if they downt exist
        /// </summary>
        /// <param name="urlPath"> url for the.txt file containing recoil info</param>
        /// <param name="destinationPath">file destination to create and write to</param>
        /// <returns></returns>
        internal static async Task DownloadFile( string urlPath, string destinationPath )
        {
            FixUrl( ref urlPath );

            using HttpClient client = new();

            try
            {
                using HttpResponseMessage response = await client.GetAsync( urlPath, HttpCompletionOption.ResponseHeadersRead );
                if ( response.IsSuccessStatusCode )
                {
                    await using Stream urlStream = await response.Content.ReadAsStreamAsync();

                    await using FileStream fileStream = new( destinationPath, FileMode.Create, FileAccess.Write, FileShare.None );
                    await urlStream.CopyToAsync( fileStream );

#if DEBUG
                    Logger.Log( $"Downloaded {urlPath} to {destinationPath}" );
#endif

                } else
                {
                    ErrorHandler.HandleException( new Exception( $"Failed to download {urlPath}" ) );
                    // Program will exit if status code is not successful
                }
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( ex );
                // Program will exit any exception is thrown
            }
        }


        /// <summary>
        /// function to fix the url to download the file as of right now, this is github specific. 
        /// but can be modified with flags to check for other sites
        /// </summary>
        /// <param name="downloadUrl"></param>
        private static void FixUrl( ref string downloadUrl )
        {
            //change spaces to "%20"

            downloadUrl = downloadUrl.Replace( " ", "%20" );
        }



        internal static void GetInGameSettings()
        {
            /*
             * these are the lines we are looking for in the settings file
             * MouseSensitivityADSScale=1.000000
             *MouseSensitivity=0.850000
             */

            using StreamReader reader = new( FilesAndFolders.gameSettingsFile );
            string line;

            while ( true )
            {
                line = reader.ReadLine();

                if ( line == null )
                {
                    break;
                }

                line = line.Trim();

                if ( line.StartsWith( "MouseSensitivityADSScale=" ) )
                {
                    string[] split = line.Split( '=' );
                    string value = split[ 1 ];
                    float.TryParse( value, out float result );

#if DEBUG
                    Logger.Log( $"MouseSensitivityADSScale: {result}" );
#endif

                    AimBot.AdsMultiplier = result;
                } else if ( line.StartsWith( "MouseSensitivity=" ) )
                {
                    string[] split = line.Split( '=' );
                    string value = split[ 1 ];
                    float.TryParse( value, out float result );

#if DEBUG
                    Logger.Log( $"MouseSensitivity: {result}" );
#endif

                    AimBot.MouseSensitivity = result;
                }
            }


            // Check if the settings were found

            if ( AimBot.MouseSensitivity == 0 || AimBot.AdsMultiplier == 0 )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get in game settings" ) );
            }
        }


        internal static string GetFileMD5Hash( string fileName )
        {
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            using ( var stream = System.IO.File.OpenRead( fileName ) )
            {
                var hash = md5.ComputeHash( stream );
                return BitConverter.ToString( hash ).Replace( "-", "" );
            }
        }
    }




    /// <summary>
    /// Represents a collection of points used to define a Bezier curve, including the start point, 
    /// end point, and control points for the curve. This class allows for manipulation and computation 
    /// of complex Bezier curves of varying degrees, facilitating smoother and more controlled aiming functions.
    /// </summary>
    /// <remarks>
    /// The first point in the collection is the start point, and the last point is the end point. 
    /// All other points in between are treated as control points, used to influence the shape of the curve.
    /// </remarks>
    /// <example>
    /// Example usage:
    /// <code>
    /// BezierPointCollection bezierPoints = new BezierPointCollection(startPoint, endPoint, controlPoints);
    /// var curvePoints = bezierPoints.GetCurvePoints();
    /// </code>
    /// </example>
    internal class BezierPointCollection
    {
        // Properties for the points
        internal PointF Start { get; set; }
        internal PointF End { get; set; }
        internal List<PointF> ControlPoints { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierPointCollection"/> class
        /// with the specified start point, end point, and list of control points.
        /// </summary>
        /// <param name="start">The starting point of the Bezier curve.</param>
        /// <param name="end">The ending point of the Bezier curve.</param>
        /// <param name="controlPoints">A list of control points to define the curve.</param>
        internal BezierPointCollection( PointF start, PointF end, List<PointF> controlPoints )
        {
            Start = start;
            End = end;
            ControlPoints = controlPoints ?? new();
        }


        /// <summary>
        /// Specifically used for user-selected control points. It scales the control points to the game's resolution.
        /// </summary>
        /// <param name="startPos">The start position in the game (center of screen).</param>
        /// <param name="targetPos">The target position in the game.</param>
        internal List<PointF> ScaleAndCalculate( ref PointF startPos, ref PointF targetPos, int numPoints )
        {
            // Scale the control points
            ScaleControlPointsRecursive( ControlPoints, startPos, targetPos, Start, End, 0 );

            // Update the start and end points to the startPos and targetPos
            Start = startPos;
            End = targetPos;

            // Calculate the points along the curve
            return CalculateOcticBezierPoints( numPoints );
        }

        /// <summary>
        /// Recursively scales the control points of an octic Bezier curve based on the distance between user-defined points and game-defined points.
        /// </summary>
        /// <param name="controlPoints">The list of control points to be scaled.</param>
        /// <param name="startPos">The start position of the curve in the game.</param>
        /// <param name="targetPos">The target position of the curve in the game.</param>
        /// <param name="userStart">The user-defined start position used for scaling.</param>
        /// <param name="userEnd">The user-defined end position used for scaling.</param>
        /// <param name="index">The current index of the control point being processed (default is 0).</param>
        internal void ScaleControlPointsRecursive( List<PointF> controlPoints, PointF startPos,
            PointF targetPos, PointF userStart, PointF userEnd, int index = 0 )
        {
            if ( index >= controlPoints.Count )
                return; // Base case: no more points to scale

            // Calculate the distance between user start and end points
            float userDistanceX = Math.Abs( userEnd.X - userStart.X );
            float userDistanceY = Math.Abs( userEnd.Y - userStart.Y );

            // Calculate the distance between the game start and target positions
            float gameDistanceX = Math.Abs( targetPos.X - startPos.X );
            float gameDistanceY = Math.Abs( targetPos.Y - startPos.Y );

            // Calculate scaling factors, ensuring we don't divide by zero
            float scaleX = userDistanceX > 0 ? gameDistanceX / userDistanceX : 1.0f;
            float scaleY = userDistanceY > 0 ? gameDistanceY / userDistanceY : 1.0f;

            // Scale the current control point
            controlPoints[ index ] = new PointF(
                startPos.X + ( controlPoints[ index ].X - userStart.X ) * scaleX,
                startPos.Y + ( controlPoints[ index ].Y - userStart.Y ) * scaleY );

            // Recur for the next control point
            ScaleControlPointsRecursive( controlPoints, startPos, targetPos, userStart, userEnd, index + 1 );
        }




        /// <summary>
        /// Calculates the points along an octic Bezier curve using the internal start, control, and end points.
        /// </summary>
        /// <param name="numPoints">Number of points to compute along the curve.</param>
        /// <returns>A list of interpolated points on the Bezier curve.</returns>
        internal List<PointF> CalculateOcticBezierPoints( int numPoints )
        {
            // Ensure numPoints is at least 2 to create a curve
            numPoints = Math.Max( numPoints, 2 );

            List<PointF> bezierPoints = new( numPoints );

            // Create a list that includes start, control, and end points
            List<PointF> allPoints = new();
            allPoints.Capacity = ControlPoints.Count + 2;

            // Add start point
            allPoints.Add( Start );

            // Add control points
            allPoints.AddRange( ControlPoints );

            // Add end point
            allPoints.Add( End );

            // Step size to divide the curve evenly between 0 and 1
            float step = 1.0f / ( numPoints - 1 ); // Ensures that t reaches 1 exactly at the last point

            // Pre-allocated point for the result
            PointF result;

            // Compute points along the curve using the recursive function
            for ( int i = 0; i < numPoints; i++ )
            {
                float t = i * step;

                // Compute the point on the curve and add it to the result list
                result = ComputeBezierRecursive( allPoints, allPoints.Count - 1, t );
                bezierPoints.Add( result );
            }

            return bezierPoints;
        }



        /// <summary>
        /// Computes a point on an octic (8-point) Bezier curve at a given time using recursion.
        /// </summary>
        /// <param name="points">The array of control points, including start, internal, and end points.</param>
        /// <param name="n">The number of control points minus 1 (i.e., the highest index).</param>
        /// <param name="t">The interpolation factor, between 0 and 1.</param>
        /// <returns>The interpolated point at time t on the curve.</returns>
        private PointF ComputeBezierRecursive( List<PointF> points, int n, float t )
        {
            // Base case: if there's only one point, return it
            if ( n == 0 )
            {
                return points[ 0 ];
            }

            // Create a list for the next level of points
            List<PointF> nextLevel = new();
            nextLevel.Capacity = n;

            // Linear interpolation between each pair of points
            for ( int i = 0; i < n; i++ )
            {
                PointF point = new()
                {
                    X = ( 1 - t ) * points[ i ].X + t * points[ i + 1 ].X,
                    Y = ( 1 - t ) * points[ i ].Y + t * points[ i + 1 ].Y
                };
                nextLevel.Add( point );
            }

            // Recursively process the next level of points
            return ComputeBezierRecursive( nextLevel, n - 1, t );
        }
    }




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

        private static int[] permutation = { 151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142,
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
        };

        internal static void SetupPermutationTable()
        {
            for ( int i = 0; i < 256; i++ )
            {
                p[ i ] = permutation[ i ];
                p[ 256 + i ] = permutation[ i ];  // Repeat the array
            }
        }
    }






    internal static class Watch
    {

        private static Stopwatch? captureWatch;


        /// <summary>
        /// starts the stopwatch to capture the time at each screenshot.
        /// </summary>
        internal static void StartCaptureWatch()
        {
            captureWatch = Stopwatch.StartNew();
        }

        internal static void StopCaptureWatch()
        {
            captureWatch!.Stop();
        }


        /// <summary>
        /// returns the time elapsed since the stopwatch was started.
        /// </summary>
        /// <returns></returns>
        internal static double GetCaptureTime()
        {
            return captureWatch!.Elapsed.TotalMilliseconds;
        }


        //custom implementation of thread sleep to pause the program execution for a specified number of microseconds without pinning cpu.

        /// <summary>
        /// Pauses the program execution for a specified number of microseconds.
        /// </summary>
        /// <param name="microSeconds">The number of microseconds to sleep.</param>
        internal static void MicroSleep( double microSeconds )
        {
            Stopwatch sleepStopWatch = Stopwatch.StartNew();
            SpinWait spinWait = new SpinWait();
            long ticks = ( long ) ( microSeconds * Stopwatch.Frequency / 1000000 );
            while ( sleepStopWatch.ElapsedTicks < ticks )
            {
                spinWait.SpinOnce();
            }
        }

        /// <summary>
        /// Pauses the program execution for a specified number of nanoseconds.
        /// </summary>
        /// <param name="nanoSeconds">The number of nanoseconds to sleep.</param>
        internal static void NanoSleep( double nanoSeconds )
        {
            MicroSleep( nanoSeconds / 1000 );
        }

        /// <summary>
        /// Pauses the program execution for a specified number of seconds.
        /// </summary>
        /// <param name="seconds">The number of seconds to sleep.</param>
        internal static void SecondsSleep( double seconds )
        {
            MicroSleep( seconds * 1000 );
        }

        /// <summary>
        /// Pauses the program execution for a specified number of milliseconds.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to sleep.</param>
        internal static void MilliSleep( double milliseconds )
        {
            MicroSleep( milliseconds );
        }
    }



    internal static class Admin
    {
        /// <summary>
        /// Checks whether the current process is running with administrator privileges.
        /// </summary>
        /// <returns>Returns true if the process is running as an administrator, otherwise false.</returns>
        private static bool IsRunningAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal( identity );
            return principal.IsInRole( WindowsBuiltInRole.Administrator );
        }

        /// <summary>
        /// Restarts the program with administrator privileges if it is not already running as an administrator.
        /// </summary>
        private static void RestartInAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start( startInfo );
            } catch ( Exception ex )
            {
                MessageBox.Show( "Failed To Restart In Admin With Error: " + ex.Message );
            }
            Thread.Sleep( 1000 );
            Environment.Exit( 0 );
        }

        /// <summary>
        /// Checks if the program is running with administrator privileges and restarts it with admin if necessary.
        /// </summary>
        internal static void CheckAndRunAdmin()
        {
            if ( !IsRunningAdmin() )
            {
                RestartInAdmin();
            }

            MessageBox.Show( "Already Running As Admin" );
        }
    }

    internal static class DarkMode
    {
        /// <summary>
        /// Determines if the current theme is using light mode.
        /// </summary>
        /// <returns>Returns true if light mode is being used, otherwise false.</returns>
        private static bool UsingLightTheme()
        {
            var registryKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" );
            var appsUseLightTheme = registryKey?.GetValue( "AppsUseLightTheme" );

            if ( appsUseLightTheme is null )
            {
                return true;
            } else
            {
                return Convert.ToBoolean( appsUseLightTheme,
                    CultureInfo.InvariantCulture );
            }
        }

        /// <summary>
        /// Enables or disables immersive dark mode for the specified window.
        /// </summary>
        /// <param name="handle">A handle to the window.</param>
        /// <param name="enabled">True to enable dark mode, false to disable it.</param>
        private static void UseImmersiveDarkMode( IntPtr handle, bool enabled )
        {
            int attribute = 20;
            int useImmersiveDarkMode = enabled ? 1 : 0;
            WinApi.DwmSetWindowAttribute( handle, attribute, ref useImmersiveDarkMode, sizeof( int ) );
        }

        /// <summary>
        /// Sets the dark mode for a given window if the system is not using the light theme.
        /// </summary>
        /// <param name="handle">A handle to the window.</param>
        internal static void SetDarkMode( nint handle )
        {
            if ( !UsingLightTheme() )
            {
                UseImmersiveDarkMode( handle, true );
            }
        }
    }

    internal static class MouseInput
    {

        /// <summary>
        /// Checks whether a specified key is held down.
        /// </summary>
        /// <param name="aimKey">The virtual key code of the key to check.</param>
        /// <returns>Returns true if the key is being held, otherwise false.</returns>
        internal static bool IsKeyHeld( ref int Key )
        {
            return ( ( int ) WinApi.GetAsyncKeyState( Key ) & 0x8000 ) != 0;
        }


        /// <summary>
        /// Checks whether a specified key was pressed since the last call.
        /// </summary>
        /// <param name="Key">The virtual key code of the key to check.</param>
        /// <returns>Returns true if the key is being pressed, otherwise false.</returns>
        internal static bool IsKeyPressed( ref int Key )
        {
            return ( WinApi.GetAsyncKeyState( Key ) & 0x0001 ) != 0; // Low-order bit indicates if the key was pressed since last call.
        }

        /// <summary>
        /// Moves the mouse cursor relative to its current position.
        /// </summary>
        /// <param name="x">The X movement (relative).</param>
        /// <param name="y">The Y movement (relative).</param>
        internal static void MoveRelativeMouse( ref float x, ref float y )
        {
            if ( x != 0 || y != 0 )
            {
                INPUT[] tempInput = new INPUT[ 1 ];
                ref INPUT temp = ref tempInput[ 0 ];
                temp.Type = INPUT_MOUSE;
                temp.Data.Mouse.ExtraInfo = nint.Zero;
                temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE;
                temp.Data.Mouse.MouseData = 0;
                temp.Data.Mouse.Time = 0;
                temp.Data.Mouse.X = ( int ) Math.Round( x );
                temp.Data.Mouse.Y = ( int ) Math.Round( y );
                SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
            }
        }

        /// <summary>
        /// Moves the mouse cursor to an absolute position on the virtual desktop.
        /// </summary>
        /// <param name="x">The X coordinate (range [0.0, 1.0]).</param>
        /// <param name="y">The Y coordinate (range [0.0, 1.0]).</param>
        internal static void MoveAbsoluteMouse( ref float x, ref float y )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_ABSOLUTE;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;

            // Ensure x and y are clamped within the range [0.0, 1.0]
            x = Mathf.Clamp01( x );
            y = Mathf.Clamp01( y );

            // Multiply by ABSOLUTE_MOUSE_COOR_MAX (65535.0) to convert to absolute coordinates
            temp.Data.Mouse.X = ( int ) ( x * ABSOLUTE_MOUSE_COOR_MAX );
            temp.Data.Mouse.Y = ( int ) ( y * ABSOLUTE_MOUSE_COOR_MAX );

            // Send the input event
            SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
        }

        /// <summary>
        /// Simulates a mouse button press.
        /// </summary>
        /// <param name="mouseButton">The button to press.</param>
        internal static void MouseButtonPress( ref uint mouseButton )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = mouseButton;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;
            temp.Data.Mouse.X = 0;
            temp.Data.Mouse.Y = 0;
            SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
        }

        /// <summary>
        /// Simulates a mouse button release.
        /// </summary>
        /// <param name="mouseButton">The button to release.</param>
        internal static void MouseButtonRelease( ref uint mouseButton )
        {
            MouseButtonPress( ref mouseButton );
        }

        /// <summary>
        /// Simulates pressing Windows key + T to show the taskbar.
        /// </summary>
        internal static void ShowTaskbarViaShortcut()
        {
            INPUT[] inputs = new INPUT[ 4 ];

            // Press Windows Key
            inputs[ 0 ].Type = INPUT_KEYBOARD;
            inputs[ 0 ].Data.Keyboard.Vk = 0x5B; // Virtual key code for left Windows key

            // Press T Key
            inputs[ 1 ].Type = INPUT_KEYBOARD;
            inputs[ 1 ].Data.Keyboard.Vk = 0x54; // Virtual key code for T

            // Release T Key
            inputs[ 2 ].Type = INPUT_KEYBOARD;
            inputs[ 2 ].Data.Keyboard.Vk = 0x54; // Virtual key code for T
            inputs[ 2 ].Data.Keyboard.Flags = KEYEVENTF_KEYUP;

            // Release Windows Key
            inputs[ 3 ].Type = INPUT_KEYBOARD;
            inputs[ 3 ].Data.Keyboard.Vk = 0x5B; // Virtual key code for left Windows key
            inputs[ 3 ].Data.Keyboard.Flags = KEYEVENTF_KEYUP;

            SendInput( ( uint ) inputs.Length, inputs, Marshal.SizeOf( typeof( INPUT ) ) );
        }

        // Structs and constants used for mouse and keyboard input.
        [StructLayout( LayoutKind.Sequential )]
        internal struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout( LayoutKind.Explicit )]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset( 0 )]
            public HARDWAREINPUT Hardware;
            [FieldOffset( 0 )]
            public KEYBDINPUT Keyboard;
            [FieldOffset( 0 )]
            public MOUSEINPUT Mouse;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }

        [StructLayout( LayoutKind.Sequential )]
        internal struct MOUSEMOVEPOINT
        {
            public int X;
            public int Y;
            public uint Time;
            public nint ExtraInfo;
        }



        internal const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04, VK_XBUTTON1 = 0x05, VK_XBUTTON2 = 0x06;
        internal const int VK_INSERT = 0x2D, VK_DELETE = 0x2E, VK_HOME = 0x24, VK_END = 0x23, VK_PRIOR = 0x21, VK_NEXT = 0x22;
        internal const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        internal const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
        internal const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        internal const int VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SPACE = 0x20, VK_ESCAPE = 0x1B, VK_BACK = 0x08;
        internal const int VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F6 = 0x75, VK_F7 = 0x76, VK_F8 = 0x77, VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B;
        internal const int VK_NUMPAD0 = 0x60, VK_NUMPAD1 = 0x61, VK_NUMPAD2 = 0x62, VK_NUMPAD3 = 0x63, VK_NUMPAD4 = 0x64, VK_NUMPAD5 = 0x65, VK_NUMPAD6 = 0x66, VK_NUMPAD7 = 0x67, VK_NUMPAD8 = 0x68, VK_NUMPAD9 = 0x69;
        internal const int VK_MULTIPLY = 0x6A, VK_ADD = 0x6B, VK_SUBTRACT = 0x6D, VK_DECIMAL = 0x6E, VK_DIVIDE = 0x6F;
        internal const int VK_OEM_1 = 0xBA, VK_OEM_PLUS = 0xBB, VK_OEM_COMMA = 0xBC, VK_OEM_MINUS = 0xBD, VK_OEM_PERIOD = 0xBE, VK_OEM_2 = 0xBF, VK_OEM_3 = 0xC0, VK_OEM_4 = 0xDB, VK_OEM_5 = 0xDC, VK_OEM_6 = 0xDD, VK_OEM_7 = 0xDE, VK_OEM_8 = 0xDF;
        internal const int VK_OEM_102 = 0xE2, VK_OEM_CLEAR = 0xFE;
        internal const int VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45, VK_F = 0x46, VK_G = 0x47, VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F, VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54, VK_U = 0x55, VK_V = 0x56, VK_W = 0x57, VK_X = 0x58, VK_Y = 0x59, VK_Z = 0x5A;
        internal const int VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34, VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39;
        internal const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, INPUT_HARDWARE = 2;
        internal const Single ABSOLUTE_MOUSE_COOR_MAX = 65535.0f;
        internal const uint MOUSEEVENTF_MOVE = 1, // just apply X/Y (delta due to not setting absolute flag)
            MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4,
            MOUSEEVENTF_RIGHTDOWN = 8, MOUSEEVENTF_RIGHTUP = 16,
            MOUSEEVENTF_MIDDLEDOWN = 32, MOUSEEVENTF_MIDDLEUP = 64,
            MOUSEEVENTF_XBUTTONDOWN = 128, MOUSEEVENTF_XBUTTONUP = 256,
            MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000,
            KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2, MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000,
            MOUSEEVENTF_MIDDLEWDOWN = 0x0020, MOUSEEVENTF_MIDDLEWUP = 0x0040,
            KEYEVENTF_SCANCODE = 0x0008, MAPVK_VK_TO_VSC = 0, KEYEVENTF_UNICODE = 0x0004, EXTENDED_FLAG = 0x100;

        // P/Invoke for sending input and getting/setting cursor position.
        [DllImport( "user32.dll" )]
        private static extern uint SendInput( uint numberOfInputs, INPUT[] inputs, int sizeOfInputs );

        [DllImport( "user32.dll" )]
        public static extern bool GetCursorPos( ref Point lpPoint );

        [DllImport( "user32.dll" )]
        public static extern void SetCursorPos( int x, int y );

        [DllImport( "user32.dll" )]
        public static extern int GetMouseMovePointsEx( uint cbSize, ref MOUSEMOVEPOINT lppt, [Out] MOUSEMOVEPOINT[] lpptBuf, int nBufPoints, uint resolution );
    }


    internal static class UtilsThreads
    {
        internal static void UiSmartKey( NotifyIcon trayIcon, IceColorBot mainClass, CancellationTokenSource formClose )
        {
            int insert = MouseInput.VK_INSERT;
            bool isKeyPressed = false;

            while ( !formClose.Token.IsCancellationRequested )
            {
                // Check if the INSERT key is pressed
                if ( Utils.MouseInput.IsKeyPressed( ref insert ) )
                {
                    // Only toggle if the key wasn't pressed in the last loop iteration
                    if ( !isKeyPressed )
                    {
                        mainClass.Invoke( new Action( () =>
                        {
                            if ( mainClass.Visible )
                            {
                                mainClass.Hide();
                                trayIcon.Visible = true;
                                mainClass.WindowState = FormWindowState.Minimized;  // Minimize the form
                            } else
                            {
                                MouseInput.ShowTaskbarViaShortcut();
                                Thread.Sleep( 100 );
                                mainClass.Show();
                                mainClass.WindowState = FormWindowState.Normal;  // Restore the form
                                mainClass.Activate();
                                mainClass.BringToFront();
                                WinApi.SetWindowPos( mainClass.Handle, WinApi.HWND_TOP, 0, 0, 0, 0, WinApi.SWP_NOMOVE | WinApi.SWP_NOSIZE );


                                trayIcon.Visible = false;
                            }
                        } ) );

                        isKeyPressed = true;  // Mark that the key has been pressed
                    }
                } else
                {
                    isKeyPressed = false;  // Reset state when the key is released
                }

                // Small delay to prevent high CPU usage
                Thread.Sleep( 10 );
            }
        }



        internal static void SmartGameCheck( ref CancellationTokenSource formClose )
        {
            string gameSettingsMD5 = FilesAndFolders.GetFileMD5Hash( FilesAndFolders.gameSettingsFile );
#if DEBUG
            Logger.Log( $"Smart Game Check Thread Started, File Hash: {gameSettingsMD5}" );
#endif

            PInvoke.RECT rect = new();
            nint firstHwnd;
            while ( !formClose.Token.IsCancellationRequested )
            {
                firstHwnd = WinApi.FindWindow();

                if ( firstHwnd == PlayerData.GetHwnd() )
                {
                    Thread.Sleep( 1000 );

                    if ( firstHwnd != nint.MaxValue )
                    {
                        if ( WinApi.GetWindowRect( PlayerData.GetHwnd(), ref rect ) )
                        {
                            if ( PlayerData.GetRect().left != rect.left ||
                                PlayerData.GetRect().right != rect.right ||
                                PlayerData.GetRect().top != rect.top ||
                                PlayerData.GetRect().bottom != rect.bottom )
                            {
                                PlayerData.SetRect( rect );

#if DEBUG
                                Logger.Log( "Game Rect Changed" );
#endif
                            }
                        }
                    }


                    // check if player changed settings, if so, reload settings
                    string gameSettingsMD5New = FilesAndFolders.GetFileMD5Hash( FilesAndFolders.gameSettingsFile );
                    if ( gameSettingsMD5New != gameSettingsMD5 )
                    {
                        gameSettingsMD5 = gameSettingsMD5New;
                        FilesAndFolders.GetInGameSettings();
#if DEBUG
                        Logger.Log( "Game Settings Changed" );
                        Logger.Log( $"New Game Settings File Hash: {gameSettingsMD5}" );
#endif

                    }

                    continue;
                }



                if ( firstHwnd != nint.MaxValue && PlayerData.GetHwnd() == nint.MaxValue )
                {
                    PlayerData.SetHwnd( firstHwnd );

                    if ( !WinApi.GetWindowRect( PlayerData.GetHwnd(), ref rect ) )
                    {
                        ErrorHandler.HandleException( new Exception( "Error getting window rect" ) );
                    }
                    PlayerData.SetRect( rect );
#if DEBUG
                    Logger.Log( "Game is active with HWND: " + PlayerData.GetHwnd() );
#endif
                }

                Thread.Sleep( 10000 );

                if ( firstHwnd == nint.MaxValue && PlayerData.GetHwnd() != nint.MaxValue )
                {

                    PlayerData.SetHwnd( firstHwnd );
#if DEBUG
                    Logger.Log( "Game is not active" );
#endif
                }
            }
        }
    }


    /// <summary>
    /// Class for saving and loading player config files.
    /// </summary>
    internal static class PlayerConfigs
    {
        public delegate void SettingsLoadedHandler( Dictionary<string, object?>? settings );
        public static event SettingsLoadedHandler OnSettingsLoaded;



        /// <summary>
        /// Saves the current config to a .json file.
        /// </summary>
        internal static void SaveConfig( int configNum )
        {
            // Create a new json serializer options object with WriteIndented enabled
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new ColorToleranceConverter(), new BezierPointCollectionConverter(), new RangeConverter<int>() }
            };

            // Create blob of player data           
            Type? playerDataBlob = typeof( PlayerData );

            // Get all the fields in the player data blob
            Dictionary<string, object?>? blobFields = null;
            try
            {
                blobFields = playerDataBlob
                 .GetFields( BindingFlags.Static | BindingFlags.NonPublic )
                 .Where( x => x.FieldType != typeof( IntPtr ) && x.FieldType != typeof( object ) ) // Filter out unsupported types
                 .ToDictionary( x => x.Name, x => x.GetValue( null ) );

            } catch ( Exception ex )
            {
                ErrorHandler.HandleExceptionNonExit( ex );
                return;
            }

            // Serialize the blob fields to a json string
            var jsonString = JsonSerializer.Serialize( blobFields, options );

            if ( jsonString == null )
            {
                ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to serialize settings." ) );
                return;
            }

            // Write the json string to a config file
            File.WriteAllText( FilesAndFolders.configFolder + $"config{configNum}.json", jsonString );
        }



        /// <summary>
        /// Loads a config file and applies the settings to the player.
        /// </summary>
        internal static void LoadConfig( int configNum )
        {
            // Read the json string from the file
            string? jsonString = File.ReadAllText( FilesAndFolders.configFolder + $"config{configNum}.json" );

            if ( jsonString == null )
            {
                ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to read config file." ) );
                return;
            }

            // Create a json serializer options object with the custom converter
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new ColorToleranceConverter(), new BezierPointCollectionConverter(), new RangeConverter<int>() }
            };

            // Deserialize the json string to a settings object           
            Dictionary<string, object?>? settings = null;

            try
            {
                settings = JsonSerializer.Deserialize<Dictionary<string, object?>?>( jsonString, options );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleExceptionNonExit( ex );
                return;
            }

            // Get the player data blob
            Type? playerDataBlob = typeof( PlayerData );

            //saving the color tolerance and name to set after all settings are loaded
            ColorTolerance colorTolerance = new( 0, 0, 0, 0, 0, 0 );
            string colorToleranceName = "Default";


            // Loop through the settings and set the fields in the player data blob
            foreach ( var setting in settings! )
            {
                FieldInfo? field = playerDataBlob.GetField( setting.Key, BindingFlags.Static | BindingFlags.NonPublic );

                if ( field == null )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to get field." ) );
                    continue;
                }

                if ( setting.Value == null )
                {
                    continue;
                }

                if ( setting.Key == "localBezierCollection" && setting.Value is JsonElement bezierCollectionJson )
                {
                    Utils.BezierPointCollection? bezierCollection = JsonSerializer.Deserialize<Utils.BezierPointCollection>( bezierCollectionJson.GetRawText(), options );

                    if ( PlayerData.BezierControlPointsSet() )
                    {
                        PlayerData.SetBezierPoints( bezierCollection! );
                    }

                    continue;
                }

                if ( setting.Key == "localColorTolerance" && setting.Value is JsonElement colorToleranceJson )
                {
                    colorTolerance = JsonSerializer.Deserialize<ColorTolerance>( colorToleranceJson.GetRawText(), options )!;
                    continue;
                }

                // Other field handling logic for different field types
                if ( setting.Value is JsonElement jsonElement )
                {
                    object? nonJsonVar = null;

                    if ( field.FieldType == typeof( double ) )
                    {
                        nonJsonVar = jsonElement.GetDouble();
                    } else if ( field.FieldType == typeof( int ) )
                    {
                        nonJsonVar = jsonElement.GetInt32();
                    } else if ( field.FieldType == typeof( bool ) )
                    {
                        nonJsonVar = jsonElement.GetBoolean();
                    } else if ( field.FieldType == typeof( string ) )
                    {
                        nonJsonVar = jsonElement.GetString();

                        if ( setting.Key == "localColorToleranceName" && nonJsonVar is string toleranceName )
                        {
                            colorToleranceName = toleranceName;
                        }
                    }

                    field.SetValue( null, nonJsonVar );
                } else
                {
                    field.SetValue( null, setting.Value );
                }
            }

            // Set the color tolerance
            PlayerData.SetColorTolerance( colorTolerance, colorToleranceName );


            // Just for debugging purposes
#if DEBUG
            var aimSettings = PlayerData.GetAimSettings();
            var aimFov = PlayerData.GetAimFov();

            //print all player data fields
            Logger.Log( "Player Data Fields:" );
            Logger.Log( "-------------------" );
            Logger.Log( "Aim Speed: " + aimSettings.aimSpeed );
            Logger.Log( "Aim Smoothing: " + aimSettings.aimSmoothing );
            Logger.Log( "Deadzone: " + aimSettings.deadzone );
            Logger.Log( "Aim Key: " + aimSettings.aimKey );
            if ( aimSettings.prediction )
            {
                Logger.Log( "Prediction: Enabled" );
            } else
            {
                Logger.Log( "Prediction: Disabled" );
            }

            if ( aimSettings.antiRecoil )
            {
                Logger.Log( "Anti Recoil: Enabled" );
            } else
            {
                Logger.Log( "Anti Recoil: Disabled" );
            }

            Logger.Log( "Aim Fov: " + aimFov );

            Logger.Log( "-------------------" );
            Logger.Log( "Color Tolerance: " + PlayerData.GetColorToleranceName() );

            ColorTolerance debugTolerance = PlayerData.GetColorTolerance();

            Logger.Log( "Red Min: " + debugTolerance.Red.Minimum );
            Logger.Log( "Red Max: " + debugTolerance.Red.Maximum );

            Logger.Log( "Green Min: " + debugTolerance.Green.Minimum );
            Logger.Log( "Green Max: " + debugTolerance.Green.Maximum );

            Logger.Log( "Blue Min: " + debugTolerance.Blue.Minimum );
            Logger.Log( "Blue Max: " + debugTolerance.Blue.Maximum );

            Logger.Log( "-------------------" );

            if ( PlayerData.BezierControlPointsSet() )
            {
                Logger.Log( "Bezier Control Points Set" );

                Utils.BezierPointCollection debugTest = PlayerData.GetBezierPoints();

                foreach ( var point in debugTest.ControlPoints )
                {
                    Logger.Log( $"Control Point: {point.X}, {point.Y}" );
                }

                Logger.Log( $"Start Point: {debugTest.Start.X}, {debugTest.Start.Y}" );
                Logger.Log( $"End Point: {debugTest.End.X}, {debugTest.End.Y}" );
            } else
            {
                Logger.Log( "Bezier Control Points Not Set" );
            }

#endif

            // Invoke the settings loaded event, to set the main form controls
            OnSettingsLoaded?.Invoke( settings );
        }



        /// <summary>
        /// Custom JSON converter for serializing and deserializing BezierPointCollection objects.
        /// </summary>
        public class BezierPointCollectionConverter : JsonConverter<Utils.BezierPointCollection>
        {
            /// <summary>
            /// Reads and converts the JSON to a BezierPointCollection object.
            /// </summary>
            /// <param name="reader">The Utf8JsonReader to read the JSON from.</param>
            /// <param name="typeToConvert">The type of object being converted.</param>
            /// <param name="options">Options for customizing JSON serialization.</param>
            /// <returns>A BezierPointCollection object.</returns>
            public override Utils.BezierPointCollection Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
            {
                // Deserialize JSON into a list of PointF objects representing the bezier points.
                List<PointF>? points = JsonSerializer.Deserialize<List<PointF>>( ref reader, options );

                // Error handling if points are null or insufficient to form a Bezier curve.
                if ( points == null || points.Count < 2 )
                {
                    ErrorHandler.HandleException( new JsonException( "Invalid BezierPointCollection data." ) );
                }

                // The first and last points represent the start and end points of the curve.
                PointF start = points.First();
                PointF end = points.Last();

                // All intermediate points represent control points for the Bezier curve.
                var controlPoints = points.Skip( 1 ).Take( points.Count - 2 ).ToList();

                return new Utils.BezierPointCollection( start, end, controlPoints );
            }

            /// <summary>
            /// Writes the BezierPointCollection object to JSON format.
            /// </summary>
            /// <param name="writer">The Utf8JsonWriter to write the JSON to.</param>
            /// <param name="value">The BezierPointCollection object to serialize.</param>
            /// <param name="options">Options for customizing JSON serialization.</param>
            public override void Write( Utf8JsonWriter writer, Utils.BezierPointCollection value, JsonSerializerOptions options )
            {
                // Combine start, control, and end points into a single list for serialization.
                List<PointF>? allPoints = new()
                { value.Start };
                allPoints.AddRange( value.ControlPoints );
                allPoints.Add( value.End );

                // Serialize the points list as JSON.
                JsonSerializer.Serialize( writer, allPoints, options );
            }
        }

        /// <summary>
        /// Custom JSON converter for serializing and deserializing ColorTolerance objects.
        /// </summary>
        public class ColorToleranceConverter : JsonConverter<ColorTolerance>
        {
            /// <summary>
            /// Reads and converts the JSON to a ColorTolerance object.
            /// </summary>
            /// <param name="reader">The Utf8JsonReader to read the JSON from.</param>
            /// <param name="typeToConvert">The type of object being converted.</param>
            /// <param name="options">Options for customizing JSON serialization.</param>
            /// <returns>A ColorTolerance object.</returns>
            public override ColorTolerance Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
            {
                try
                {
                    // Deserialize JSON into a dictionary with color ranges
                    Dictionary<string, Dictionary<string, int>>? colorData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>( ref reader, options );

                    // Error handling if the deserialized data is invalid or missing required fields
                    if ( colorData == null || !colorData.ContainsKey( "Red" ) || !colorData.ContainsKey( "Green" ) || !colorData.ContainsKey( "Blue" ) )
                    {
                        ErrorHandler.HandleException( new JsonException( "Invalid ColorTolerance data. Missing 'Red', 'Green', or 'Blue' fields." ) );
                        return null; // Return null to handle gracefully
                    }

                    // Extract the min/max values for each RGB color channel
                    var redRange = new Range<int>
                    {
                        Minimum = colorData[ "Red" ][ "Min" ],
                        Maximum = colorData[ "Red" ][ "Max" ]
                    };

                    var greenRange = new Range<int>
                    {
                        Minimum = colorData[ "Green" ][ "Min" ],
                        Maximum = colorData[ "Green" ][ "Max" ]
                    };

                    var blueRange = new Range<int>
                    {
                        Minimum = colorData[ "Blue" ][ "Min" ],
                        Maximum = colorData[ "Blue" ][ "Max" ]
                    };

                    return new ColorTolerance( redRange.Minimum, redRange.Maximum, greenRange.Minimum, greenRange.Maximum, blueRange.Minimum, blueRange.Maximum );
                } catch ( Exception ex )
                {
                    // Handle exceptions during the deserialization process
                    ErrorHandler.HandleException( ex );
                    return null; // Return a null or default value to prevent crashing
                }
            }

            /// <summary>
            /// Writes the ColorTolerance object to JSON format.
            /// </summary>
            /// <param name="writer">The Utf8JsonWriter to write the JSON to.</param>
            /// <param name="value">The ColorTolerance object to serialize.</param>
            /// <param="options">Options for customizing JSON serialization.</param>
            public override void Write( Utf8JsonWriter writer, ColorTolerance value, JsonSerializerOptions options )
            {
                try
                {
                    // Prepare the color channel ranges for serialization
                    Dictionary<string, Dictionary<string, int>> colorData = new()
            {
                { "Red", new Dictionary<string, int> { { "Min", value.Red.Minimum }, { "Max", value.Red.Maximum } } },
                { "Green", new Dictionary<string, int> { { "Min", value.Green.Minimum }, { "Max", value.Green.Maximum } } },
                { "Blue", new Dictionary<string, int> { { "Min", value.Blue.Minimum }, { "Max", value.Blue.Maximum } } }
            };

                    // Serialize the color tolerance ranges as JSON
                    JsonSerializer.Serialize( writer, colorData, options );
                } catch ( Exception ex )
                {
                    // Handle exceptions during the serialization process
                    ErrorHandler.HandleException( ex );
                }
            }
        }



        /// <summary>
        /// Custom JSON converter for serializing and deserializing Range objects.
        /// </summary>
        /// <typeparam name="T">The numeric type for the range (e.g., int, float).</typeparam>
        public class RangeConverter<T> : JsonConverter<Range<T>> where T : IComparable<T>
        {
            /// <summary>
            /// Reads and converts the JSON to a Range object.
            /// </summary>
            /// <param name="reader">The Utf8JsonReader to read the JSON from.</param>
            /// <param name="typeToConvert">The type of object being converted.</param>
            /// <param name="options">Options for customizing JSON serialization.</param>
            /// <returns>A Range object.</returns>
            public override Range<T> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
            {
                try
                {
                    // Deserialize JSON into a dictionary with min/max values
                    Dictionary<string, T>? rangeData = JsonSerializer.Deserialize<Dictionary<string, T>>( ref reader, options );

                    // Error handling if the deserialized data is invalid or missing required fields
                    if ( rangeData == null || !rangeData.ContainsKey( "Minimum" ) || !rangeData.ContainsKey( "Maximum" ) )
                    {
                        ErrorHandler.HandleException( new JsonException( "Invalid Range data. Missing required 'Minimum' or 'Maximum' values." ) );
                        return null; // Return null to handle gracefully
                    }

                    // Extract the minimum and maximum values
                    T min = rangeData[ "Minimum" ];
                    T max = rangeData[ "Maximum" ];

                    // Create and return the Range object
                    Range<T> range = new Range<T> { Minimum = min, Maximum = max };

                    // Validate that the range is valid (minimum <= maximum)
                    if ( !range.IsValid() )
                    {
                        ErrorHandler.HandleException( new JsonException( "Invalid Range. 'Minimum' value is greater than 'Maximum'." ) );
                        return null;
                    }

                    return range;
                } catch ( Exception ex )
                {
                    // Handle exceptions during the deserialization process
                    ErrorHandler.HandleException( ex );
                    return null; // Return a null or default value to prevent crashing
                }
            }

            /// <summary>
            /// Writes the Range object to JSON format.
            /// </summary>
            /// <param name="writer">The Utf8JsonWriter to write the JSON to.</param>
            /// <param name="value">The Range object to serialize.</param>
            /// <param name="options">Options for customizing JSON serialization.</param>
            public override void Write( Utf8JsonWriter writer, Range<T> value, JsonSerializerOptions options )
            {
                try
                {
                    // Prepare the min/max values for serialization
                    Dictionary<string, T> rangeData = new()
            {
                { "Minimum", value.Minimum },
                { "Maximum", value.Maximum }
            };

                    // Serialize the Range as JSON
                    JsonSerializer.Serialize( writer, rangeData, options );
                } catch ( Exception ex )
                {
                    // Handle exceptions during the serialization process
                    ErrorHandler.HandleException( ex );
                }
            }
        }



    }
}
