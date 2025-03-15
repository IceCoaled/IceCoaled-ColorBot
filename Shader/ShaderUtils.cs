#if DEBUG
//#define DEBUG_BUFFER
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Mathematics;

namespace SCB.DirectX
{
    using shaderConsts = ShaderUtilities.ShaderConsts;

    /// <summary>
    /// Our Include file handler for d3d compiler to process any files we have include with our main shader.
    /// </summary>
    /// <param name="folderPath"></param>
    public partial class ShaderIncludehandler( string folderPath ) : CallbackBase, Include
    {
        private string Directory { get; } = folderPath;
        public Stream Open( IncludeType type, string fileName, Stream? parentStream )
        {
            return type switch
            {
                IncludeType.Local => File.OpenRead( Path.Combine( Directory, fileName ) ),
                IncludeType.System => File.OpenRead( fileName ),
                _ => throw new NotImplementedException(),
            };
        }
        public void Close( Stream stream ) => stream?.Close();
    }



    /// <summary>
    /// Static class for anything directly related to the shader files.
    /// </summary>
    public static partial class ShaderUtilities
    {
        /// <summary>
        /// Regex's for editing the constants that are changable via user selection
        /// </summary>      
        [GeneratedRegex( @"static\s+const\s+uint2\s+SCAN_FOV\s+=\s+uint2\(\s+\d+,\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetScanFov();

        [GeneratedRegex( @"static\s+const\s+uint\s+MAX_PLAYERS\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetMaxPlayers();

        [GeneratedRegex( @"static\s+const\s+uint\s+WINDOW_SIZE_X\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetWindowSizeX();

        [GeneratedRegex( @"static\s+const\s+uint\s+WINDOW_SIZE_Y\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetWindowSizeY();

        [GeneratedRegex( @"static\s+const\s+uint\s+X_THREADGROUP\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetXNumTreads();

        [GeneratedRegex( @"static\s+const\s+uint\s+NUM_COLOR_RANGES\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetNumColorRanges();

        [GeneratedRegex( @"#define\s+DEBUG$", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetReleaseMode();

        [GeneratedRegex( @"static\s+const\s+uint4\s+COLOR_NAME_OUTLNZ\s+=\s+uint4\(\s+\d+,\s+\d+,\s+\d+,\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetOutlineName();

        [GeneratedRegex( @"static\s+const\s+uint4\s+COLOR_NAME_HAIR\s+=\s+uint4\(\s+\d+,\s+\d+,\s+\d+,\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetHairName();

        [GeneratedRegex( @"static\s+const\s+uint\s+MAX_SCAN_BOX_BUFFER_SIZE\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetMaxScanBoxBufferSize();

        [GeneratedRegex( @"static\s+const\s+uint\s+MAX_SCAN_BOX_GROUPS\s+=\s+uint\(\s+\d+\s+\);", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetMaxScanBoxGroups();




        public record ShaderConsts
        {
            public const int MAX_THREAD_GROUP_SIZE = ( 16 * 4 );
            public const int MAX_PLAYER_DETEC_STRUCTS = 1;
            public const int MAX_PLAYERS = 6;
            public const int MAX_RANGES = 12;
            public const int MAX_SCAN_BOX_SIDE = 700;
            public const int ACTUAL_PIXELS_AT_MAX_SCAN_BOX_SIDE = 704;
            public const int CLASSIFICATIONS_PER_UINT = 16;
            public readonly static int MAX_SCAN_BOX_BUFFER_SIZE = ( ( int ) MathF.Ceiling( ( ( float ) ( MAX_SCAN_BOX_SIDE * MAX_SCAN_BOX_SIDE + CLASSIFICATIONS_PER_UINT - 1 ) / CLASSIFICATIONS_PER_UINT ) ) );
            public static int MAX_SCAN_GROUPS( int xThreadSize ) => ( ( int ) MathF.Ceiling( ( ( float ) ( ( ( MAX_SCAN_BOX_SIDE + ( xThreadSize - 1 ) ) / xThreadSize ) * ( ( MAX_SCAN_BOX_SIDE + 3 ) >> 2 ) ) ) ) );
        }



        /// <summary>
        /// This is only used to make things more readable.
        /// </summary>
        private enum RegexFactoryKeys
        {
            ScanFov,
            PlayerCount,
            WindowX,
            WindowY,
            Xthreads,
            NumRanges,
            ReleaseMode,
            OutlineName,
            HairName,
            MaxSzScanBox,
            MaxScanBoxGroups,
        }


        /// <summary>
        /// Our regex factory, this way we can just run the regex's in a loop.
        /// Much easier and cleaner then calling all of them in seperate if statments.
        /// </summary>
        private readonly static Dictionary<RegexFactoryKeys, Func<Regex>> RegexFactory = new()
        {
            {RegexFactoryKeys.ScanFov, SetScanFov },
            {RegexFactoryKeys.PlayerCount, SetMaxPlayers },
            {RegexFactoryKeys.WindowX, SetWindowSizeX },
            {RegexFactoryKeys.WindowY, SetWindowSizeY },
            {RegexFactoryKeys.Xthreads, SetXNumTreads },
            {RegexFactoryKeys.NumRanges, SetNumColorRanges },
            {RegexFactoryKeys.ReleaseMode, SetReleaseMode },
            {RegexFactoryKeys.OutlineName, SetOutlineName },
            {RegexFactoryKeys.HairName, SetHairName },
            {RegexFactoryKeys.MaxSzScanBox, SetMaxScanBoxBufferSize },
            {RegexFactoryKeys.MaxScanBoxGroups, SetMaxScanBoxGroups },
        };


        /// <summary>
        /// Converts the names we use for the gpu color range buffer into shorts or utf16 values.
        /// This is done so we can efficiently pack them in the gpu buffers.
        /// </summary>
        /// <param name="utf16Names">A 3x6 short matrix, passed by reference</param>
        private static void SetColorRangeNames( ref ColorToleranceManager toleranceManager, out uint[] outlineOut, out uint[] hairOut )
        {
            string outLineColor = PlayerData.GetOutlineColor();
            string hair = toleranceManager.CharacterFeatures[ 0 ].GetSelected();// If you where to have more character features we need to code this

            outlineOut = Mathf.ConvertUtf16ToUintArray( ref outLineColor );
            hairOut = Mathf.ConvertUtf16ToUintArray( ref hair );
        }



        /// <summary>
        /// This is our looped function to call all our regex's for the shader defines file.
        /// </summary>
        /// <param name="rawShaderCode">Raw shader code from shaderdefines.hlsli, passed by reference since it gets modified a lot here.</param>
        /// <param name="replacements">our replacment strings for the regex's</param>
        private static void ApplyRegex( ref string rawShaderCode, string[] replacements )
        {
            foreach ( var regex in RegexFactory )
            {
#if DEBUG                
                if ( regex.Key == RegexFactoryKeys.ReleaseMode )
                {
                    continue;
                }
#endif
                if ( regex.Value().IsMatch( rawShaderCode ) )
                {
                    rawShaderCode = regex.Value().Replace( rawShaderCode, replacements[ ( ( int ) regex.Key ) ] );
                }
            }
        }


        /// <summary>
        /// Our main function to edit shaderdefines.hlsli.
        /// </summary>
        /// <param name="toleranceManager">Reference to color tolerance manager</param>
        /// <param name="numOfColorRanges">Number of color ranges we are using in the shader</param>
        /// <param name="numOfXThreads">Number of x threads being used. dont confuse this with dispatch threads, this is the specific amount of x threads the shader is told to use per group.</param>
        /// <param name="maxPlayers"> The max players set in the shader, this changing changes a lot, currently we have it set to six as there can only be 6 enemies 3 mains, 3 spectres.</param>
        internal static void EditShaderCode( ref ShaderPaths shaderPaths, ref ColorToleranceManager toleranceManager, int numOfColorRanges, int numOfXThreads, int maxPlayers = shaderConsts.MAX_PLAYERS )
        {

            string? rawShaderCode = null;

            // Read shaderdefines.hlsli
            try
            {
                rawShaderCode = File.ReadAllText( shaderPaths.GetShaderDefines() );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to read defines code", ex ) );
            }

            // Get the 16bit arrays for the color range names
            SetColorRangeNames( ref toleranceManager, out uint[]? outLineName, out uint[]? hairName );
            if ( outLineName is null || hairName is null )
            {
                ErrorHandler.HandleException( new InvalidDataException( "Failed To Convert Color Tolerance Names to UTF16" ) );
            }

            // Window height and width for calculations in the shader
            var windowRect = PlayerData.GetRect();
            var windowWidth = windowRect.right - windowRect.left;
            var windowHeight = windowRect.bottom - windowRect.top;

            // Get scan fov
            var scanFov = PlayerData.GetFov();

            // Regex replacement strings
            string[] replacementStrings =
            [
                $"static const uint2 SCAN_FOV = uint2( {int.Min(windowWidth, scanFov)}, {int.Min(windowHeight, scanFov)} );",
                $"static const uint MAX_PLAYERS = uint( {maxPlayers} );",
                $"static const uint WINDOW_SIZE_X = uint( {windowWidth} );",
                $"static const uint WINDOW_SIZE_Y = uint( {windowHeight} );",
                $"static const uint X_THREADGROUP = uint( {numOfXThreads} );",
                $"static const uint NUM_COLOR_RANGES = uint( {numOfColorRanges} );",
                "//#define DEBUG",
                $"static const uint4 COLOR_NAME_OUTLNZ = uint4( {outLineName[0]}, {outLineName[1]}, {outLineName[2]}, {outLineName[3]} );",
                $"static const uint4 COLOR_NAME_HAIR = uint4( {hairName[0]}, {hairName[1]}, {hairName[2]}, {hairName[3]} );",
                $"static const uint MAX_SCAN_BOX_BUFFER_SIZE = uint( {shaderConsts.MAX_SCAN_BOX_BUFFER_SIZE} );",
                $"static const uint MAX_SCAN_BOX_GROUPS = uint( {shaderConsts.MAX_SCAN_GROUPS(numOfXThreads)} );",
            ];

            // Loop through all our regex's and change the shader code as needed before compiling it.
            ApplyRegex( ref rawShaderCode, replacementStrings );

            // Write the new shader code to the file
            File.WriteAllText( shaderPaths.GetShaderDefines(), rawShaderCode );

        }
    }


    ///-----------Gpu-Buffer-Structs---------------///
    ///<summary>
    /// ALL MAIN STRUCTS USED IN THE GPU BUFFERS
    /// MUST CONTAIN A SAFETY CHECK VARIABLE
    /// IT MUST BE THE LAST VARIABLE IN THE STRUCT
    /// THIS IS TO MAKE SURE WE CAN VERIFY MEMORY ALIGNMENT
    /// WHILE DEBUGGING
    ///</summary>

    /// <summary>
    /// Vectorized uint2 struct. We cant use this in the 
    /// Gpu buffers because of alignment issues.
    /// I made this just to mess around, its not actually used.
    /// </summary>
    //[StructLayout( LayoutKind.Sequential, Pack = 16, Size = 32 )]
    //struct Uint2
    //{
    //    public Vector<UInt32> X;
    //    public Vector<UInt32> Y;

    //    /// <summary>
    //    /// Constructor for Uint2, using uints.
    //    /// </summary>
    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public Uint2( uint x, uint y )
    //    {
    //        X = new( x );
    //        Y = new( y );
    //    }

    //    /// <summary>
    //    /// Constructor for Uint2, using vector2.
    //    /// </summary>
    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public Uint2( Vector<UInt32> x, Vector<UInt32> y )
    //    {
    //        X = x;
    //        Y = y;
    //    }

    //    // Overides

    //    public override readonly string ToString()
    //    {
    //        return $"X: {X}, Y: {Y}";
    //    }

    //    public override readonly bool Equals( object? obj )
    //    {
    //        return obj is Uint2 u2 && u2 == this;
    //    }

    //    public override readonly int GetHashCode()
    //    {
    //        return X.GetHashCode() ^ Y.GetHashCode();
    //    }


    //    // Forwarded Operator overloads
    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator +( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X + right.X, left.Y + right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator +( Uint2 left, uint right )
    //    {
    //        return new( new Vector<UInt32>( left.X.ToScalar() + right ), new Vector<UInt32>( left.Y.ToScalar() + right ) );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator -( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X - right.X, left.Y - right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator -( Uint2 left, uint right )
    //    {
    //        return new( new Vector<UInt32>( left.X.ToScalar() - right ), new Vector<UInt32>( left.Y.ToScalar() - right ) );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static bool operator ==( Uint2 left, Uint2 right )
    //    {
    //        return ( left.X == right.X & left.Y == right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static bool operator !=( Uint2 left, Uint2 right )
    //    {
    //        return ( left.X != right.X | left.Y != right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static bool operator <( Uint2 left, Uint2 right )
    //    {
    //        return ( Vector.LessThan( left.X, right.X ).ToScalar() ^ Vector.LessThan( left.Y, right.Y ).ToScalar() ) == 0;
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static bool operator >( Uint2 left, Uint2 right )
    //    {
    //        return ( Vector.GreaterThan( left.X, right.X ).ToScalar() ^ Vector.GreaterThan( left.Y, right.Y ).ToScalar() ) == 0;
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static bool operator <=( Uint2 left, Uint2 right )
    //    {
    //        return ( Vector.LessThanOrEqual( left.X, right.X ).ToScalar() ^ Vector.LessThanOrEqual( left.Y, right.Y ).ToScalar() ) == 0;
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static bool operator >=( Uint2 left, Uint2 right )
    //    {
    //        return ( Vector.GreaterThanOrEqual( left.X, right.X ).ToScalar() ^ Vector.GreaterThanOrEqual( left.Y, right.Y ).ToScalar() ) == 0;
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator *( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X * right.X, left.Y * right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator *( Uint2 left, uint right )
    //    {
    //        return new( left.X * right, left.Y * right );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator /( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X / right.X, left.Y / right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator /( Uint2 left, uint right )
    //    {
    //        return new( left.X / right, left.Y / right );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator &( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X & right.X, left.Y & right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator &( Uint2 left, uint right )
    //    {
    //        return new( new Vector<UInt32>( left.X.ToScalar() & right ), new Vector<UInt32>( left.Y.ToScalar() & right ) );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator |( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X | right.X, left.Y | right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator |( Uint2 left, uint right )
    //    {
    //        return new( new Vector<UInt32>( left.X.ToScalar() | right ), new Vector<UInt32>( left.Y.ToScalar() | right ) );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator ^( Uint2 left, Uint2 right )
    //    {
    //        return new( left.X ^ right.X, left.Y ^ right.Y );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator ^( Uint2 left, uint right )
    //    {
    //        return new( new Vector<UInt32>( left.X.ToScalar() ^ right ), new Vector<UInt32>( left.Y.ToScalar() ^ right ) );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator >>( Uint2 left, int right )
    //    {
    //        return new( left.X >> right, left.Y >> right );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator <<( Uint2 left, int right )
    //    {
    //        return new( left.X << right, left.Y << right );
    //    }

    //    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    //    public static Uint2 operator ~( Uint2 left )
    //    {
    //        return new( ~left.X, ~left.Y );
    //    }
    //}



    ///<summary>
    ///  Vectorized Uint4x2 struct. We cant use this in the
    ///  Gpu buffers because of alignment issues.
    ///  I made this just to mess around, its not actually used.
    ///</summary>
    //[StructLayout( LayoutKind.Sequential, Pack = 16, Size = 128 )]
    //struct Uint4x2
    //{
    //    public Uint2 MM00;
    //    public Uint2 MM10;
    //    public Uint2 MM20;
    //    public Uint2 MM30;


    //    /// <summary>
    //    /// Constructor for uint4x2, using uint2's.
    //    /// </summary>
    //    public Uint4x2( Uint2 mm00, Uint2 mm10, Uint2 mm20, Uint2 mm30 )
    //    {
    //        MM00 = mm00;
    //        MM10 = mm10;
    //        MM20 = mm20;
    //        MM30 = mm30;
    //    }

    //    /// <summary>
    //    /// Constructor for uint4x2, using uints.
    //    /// </summary>
    //    public Uint4x2( uint mm00_x, uint mm00_y,
    //            uint mm10_x, uint mm10_y,
    //            uint mm20_x, uint mm20_y,
    //            uint mm30_x, uint mm30_y )
    //    {
    //        MM00 = new Uint2( mm00_x, mm00_y );
    //        MM10 = new Uint2( mm10_x, mm10_y );
    //        MM20 = new Uint2( mm20_x, mm20_y );
    //        MM30 = new Uint2( mm30_x, mm30_y );
    //    }


    //    /// <summary>
    //    /// Default constructor
    //    /// </summary>
    //    public Uint4x2()
    //    {
    //        MM00 = new Uint2( 0, 0 );
    //        MM10 = new Uint2( 0, 0 );
    //        MM20 = new Uint2( 0, 0 );
    //        MM30 = new Uint2( 0, 0 );
    //    }
    //}


    /// <summary>
    /// Non vectorized uint4x2 struct. We use this in the Gpu buffers.
    /// </summary>
    [StructLayout( LayoutKind.Sequential, Pack = 8, Size = 32 )]
    struct UInt4x2
    {
        private UInt2 X;
        private UInt2 Y;
        private UInt2 Z;
        private UInt2 W;


        public UInt4x2( UInt2 x, UInt2 y, UInt2 z, UInt2 w )
        {
            X = x;
            Y = y;
            W = z;
            Z = w;
        }

        public UInt4x2(
            uint xX, uint xY,
            uint yX, uint yY,
            uint zX, uint zY,
            uint wX, uint wY )
        {
            X = new UInt2( xX, xY );
            Y = new UInt2( yX, yY );
            W = new UInt2( zX, zY );
            Z = new UInt2( wX, wY );
        }

        public UInt4x2()
        {
            X = new UInt2( 0, 0 );
            Y = new UInt2( 0, 0 );
            Z = new UInt2( 0, 0 );
            W = new UInt2( 0, 0 );
        }

        public UInt2 x
        {
            readonly get => X;
            set => X = value;
        }

        public UInt2 y
        {
            readonly get => Y;
            set => Y = value;
        }

        public UInt2 z
        {
            readonly get => Z;
            set => Z = value;
        }

        public UInt2 w
        {
            readonly get => W;
            set => W = value;
        }

        public uint x_x
        {
            readonly get => X[ 0 ];
            set => X[ 0 ] = value;
        }

        public uint x_y
        {
            readonly get => X[ 1 ];
            set => X[ 1 ] = value;
        }

        public uint y_x
        {
            readonly get => Y[ 0 ];
            set => Y[ 0 ] = value;
        }

        public uint y_y
        {
            readonly get => Y[ 1 ];
            set => Y[ 1 ] = value;
        }

        public uint z_x
        {
            readonly get => Z[ 0 ];
            set => Z[ 0 ] = value;
        }

        public uint z_y
        {
            readonly get => Z[ 1 ];
            set => Z[ 1 ] = value;
        }

        public uint w_x
        {
            readonly get => W[ 0 ];
            set => W[ 0 ] = value;
        }

        public uint w_y
        {
            readonly get => W[ 1 ];
            set => W[ 1 ] = value;
        }

        public UInt2 _m00
        {
            readonly get => X;
            set => X = value;
        }

        public UInt2 _m10
        {
            readonly get => Y;
            set => Y = value;
        }
        public UInt2 _m20
        {
            readonly get => Z;
            set => Z = value;
        }
        public UInt2 _m30
        {
            readonly get => W;
            set => W = value;
        }

        public uint _m00_10
        {
            readonly get => X[ 0 ];
            set => X[ 0 ] = value;
        }
        public uint _m00_01
        {
            readonly get => X[ 1 ];
            set => X[ 1 ] = value;
        }
        public uint _m10_10
        {
            readonly get => Y[ 0 ];
            set => Y[ 0 ] = value;
        }
        public uint _m10_01
        {
            readonly get => Y[ 1 ];
            set => Y[ 1 ] = value;
        }
        public uint _m20_10
        {
            readonly get => Z[ 0 ];
            set => Z[ 0 ] = value;
        }
        public uint _m20_01
        {
            readonly get => Z[ 1 ];
            set => Z[ 1 ] = value;
        }
        public uint _m30_10
        {
            readonly get => W[ 0 ];
            set => W[ 0 ] = value;
        }
        public uint _m30_01
        {
            readonly get => W[ 1 ];
            set => W[ 1 ] = value;
        }

        public readonly uint this[ int keyX, int keyY ]
        {
            get
            {
                return (keyX, keyY) switch
                {
                    (0, 0 ) => X[ 0 ],
                    (0, 1 ) => X[ 1 ],
                    (1, 0 ) => Y[ 0 ],
                    (1, 1 ) => Y[ 1 ],
                    (2, 0 ) => Z[ 0 ],
                    (2, 1 ) => Z[ 1 ],
                    (3, 0 ) => W[ 0 ],
                    (3, 1 ) => W[ 1 ],
                    _ => throw new IndexOutOfRangeException()
                };
            }
        }

        public readonly UInt2 this[ int key ] => key switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => new UInt2( 0, 0 ),
        };

        public override readonly string ToString()
        {
            return $"X: {X}, Y: {Y}, Z: {Z}, W: {W}";
        }

        public override readonly bool Equals( object? obj )
        {
            return obj is UInt4x2 u4x2 && u4x2 == this;
        }

        public override readonly int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( UInt4x2 left, UInt4x2 right )
        {
            return ( left._m00 == right._m00 & left._m10 == right._m10 & left._m20 == right._m20 & left._m30 == right._m30 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( UInt4x2 left, UInt4x2 right )
        {
            return ( left._m00 != right._m00 | left._m10 != right._m10 | left._m20 != right._m20 | left._m30 != right._m30 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( UInt4x2 left, uint right )
        {
            return ( left._m00.X == right & left._m00.Y == right & left._m10.X == right & left._m10.Y == right &
                left._m20.X == right & left._m20.Y == right & left._m30.X == right & left._m30.Y == right );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( UInt4x2 left, uint right )
        {
            return ( left._m00.X != right | left._m00.Y != right | left._m10.X != right | left._m10.Y != right |
                left._m20.X != right | left._m20.Y != right | left._m30.X != right | left._m30.Y != right );
        }
    }





    /// <summary>
    ///  Copy of float4 struct in hlsl, we use this for normalized brga values
    /// </summary>
    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 16 )]
    struct Float4
    {

        public float X;
        public float Y;
        public float Z;
        public float W;

        /// <summary>
        /// Constructor for float4 using floats.
        /// </summary>
        public Float4( float x, float y, float z, float w )
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        ///  UNorm constructor for color values, using uints.
        /// </summary>
        public Float4( uint x, uint y, uint z, uint w )
        {
            X = x / 255.0f;
            Y = y / 255.0f;
            Z = z / 255.0f;
            W = w / 255.0f;
        }

        /// <summary>
        /// SNorm constructor for color values, using ints.
        /// </summary>
        public Float4( int x, int y, int z, int w )
        {
            X = x / 255.0f;
            Y = y / 255.0f;
            Z = z / 255.0f;
            W = w / 255.0f;
        }
    }


    /// <summary>
    /// Range struct holds a min and max value to create the range.
    /// </summary>
    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 8 )]
    struct Range
    {
        public float Minimum;
        public float Maximum;


        public Range( float min, float max )
        {
            Minimum = min;
            Maximum = max;
        }

        /// <summary>
        /// UNorm constructor for color range
        /// </summary>
        public Range( uint min, uint max )
        {
            Minimum = min / 255.0f;
            Maximum = max / 255.0f;
        }

        /// <summary>
        /// SNorm constructor for color range
        /// </summary>
        public Range( int min, int max )
        {
            Minimum = min / 255.0f;
            Maximum = max / 255.0f;
        }
    }


    /// <summary>
    /// Color Range struct
    ///  Holds bgr ranges for color tolerances
    /// </summary>
    /// <param name="redRange">Red color tolerance</param>
    /// <param name="greenRange">Green color tolerance</param>
    /// <param name="blueRange">Blue color tolerance</param>
    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 24 )]
    struct ColorRange( Range redRange, Range greenRange, Range blueRange )
    {
        public Range RedRange = redRange;
        public Range GreenRange = greenRange;
        public Range BlueRange = blueRange;
    }


    /// <summary>
    /// Main color range struct we use in uav buffer - ColorRangeBuffer
    /// This struct is declared at a size of 384 to keep gpu cache line intact for the buffer
    /// </summary>
    [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4, Size = 384 )]
    struct ColorRanges
    {
        [MarshalAs( UnmanagedType.ByValArray, SizeConst = shaderConsts.MAX_RANGES )]
        public ColorRange[] Ranges;

        public Float4 SwapColor;

        public uint NumOfRanges;

        private readonly UInt2 padding0 = new();

        public UInt4 Name;

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 3 )]
        private readonly UInt4[] Padding1 = new UInt4[ 3 ];

        public readonly uint SafetyCheck = uint.MaxValue;

        /// <summary>
        /// Specific constructor for buffer manager.
        /// this is used for when we make the our color ranges array to be serialized in memory.
        /// </summary>
        /// <param name="numOfRanges">Number of ranges in struct</param>
        /// <param name="ranges">Input color ranges</param>
        /// <param name="swapColor">The color that will be used to swap if a pixel is within the range</param>
        /// <param name="name">Name of color tolerance</param>
        public ColorRanges( uint numOfRanges, ColorRange[] ranges, Float4 swapColor, string name )
        {
            // Set the ranges
            Ranges = new ColorRange[ shaderConsts.MAX_RANGES ];
            Array.Copy( ranges, Ranges, Math.Min( ranges.Length, shaderConsts.MAX_RANGES ) );

            // Set the name
            Name = new( Mathf.ConvertUtf16ToUintArray( ref name ) );

            // Set the number of ranges
            NumOfRanges = numOfRanges;
            SwapColor = swapColor;
        }
    }


    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 16 )]
    struct BndBoxMM
    {
        public UInt2 bbMin;
        public UInt2 bbMax;

        public BndBoxMM( UInt2 min, UInt2 max )
        {
            bbMin = min;
            bbMax = max;
        }

        public BndBoxMM( uint minX, uint minY, uint maxX, uint maxY )
        {
            bbMin = new UInt2( minX, minY );
            bbMax = new UInt2( maxX, maxY );
        }

        public BndBoxMM()
        {
            bbMin = new UInt2( 0, 0 );
            bbMax = new UInt2( 0, 0 );
        }
    }



    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 40 )]
    struct TargetFinder()
    {
        public UInt2 rightLowestPoint = new( 0, 0 );
        public UInt2 leftLowestPoint = new( 0, 0 );
        public UInt2 ySearchPlaneLane = new( 0, 0 );
        public int leftReductiondegree = 0;
        public int DegHairToRightLow = 0;
        public int DegHairToLeftLow = 0;
        public int distance = 0;
    }


    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 48 )]
    struct PlayerPosition()
    {
        public UInt2 HeadPosition = new( 0, 0 );
        public UInt2 TorsoPosition = new( 0, 0 );
        public UInt4x2 BoundingBox = new( new( 0, 0 ), new( 0, 0 ), new( 0, 0 ), new( 0, 0 ) );
    }

    /// <summary>
    /// Main detected players struct we use in uav buffer - PlayerPositionBuffer
    /// This struct is declared at a size of 384 to keep gpu cache line intact for the buffer
    /// </summary>
    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 384 )]
    struct DetectedPlayers()
    {
        [MarshalAs( UnmanagedType.ByValArray, SizeConst = shaderConsts.MAX_PLAYERS )]
        public PlayerPosition[] PlayerPositions = new PlayerPosition[ shaderConsts.MAX_PLAYERS ];

        public BndBoxMM globalBBmerge = new();

        public TargetFinder globals = new();

        public uint DetectedPlayerCount = 0;

        public uint centroidMergeFlag = 0;

        private readonly UInt4 Padding1 = new( 0, 0, 0, 0 );

        private readonly UInt3 padding0 = new( 0, 0, 0 );

        public readonly uint SafetyCheck = uint.MaxValue;
    }




    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 32 )]
    struct HairCentroid()
    {
        public UInt4 outlinePos = new( 0, 0, 0, 0 );

        public uint clusterSize = 0;

        public uint allowance = 0;

        private readonly UInt2 padding0 = new( 0, 0 );
    };



    /// <summary>
    /// Main struct we use in uav buffer - GroupDetailsBuffer
    /// </summary>
    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 128 )]
    struct GroupData()
    {
        public HairCentroid hairCentroid = new();

        public uint hasCluster = 0;

        private readonly UInt3 padding0 = new();

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 4 )]
        private readonly UInt4[] StatusPxlType = new UInt4[ 4 ];

        private readonly UInt3 padding2 = new();

        public readonly uint SafetyCheck = uint.MaxValue;

    }


#if DEBUG_BUFFER

    [StructLayout( LayoutKind.Sequential, Pack = 4, Size = 256 )]
    struct DebugBuffer()
    {
        [MarshalAs( UnmanagedType.ByValArray, SizeConst = shaderConsts.MAX_PLAYERS )]
        public HairCentroid[] hairCentroids = new HairCentroid[ shaderConsts.MAX_PLAYERS ];

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 3 )]
        private readonly UInt4[] padding1 = new UInt4[ 3 ];

        private readonly UInt2 padding0 = new( 0, 0 );

        public uint hasCluster = 0;

        public uint SafetyCheck = uint.MaxValue;
    }

    //[StructLayout( LayoutKind.Sequential, Pack = 4, Size = 32 )]
    //struct DebugBuffer()
    //{
    //    public UInt4 outlinePos = new( 0, 0, 0, 0 );
    //    public uint clusterSize = 0;
    //    private readonly UInt2 padding0 = new( 0, 0 );
    //    public readonly uint SafetyCheck = uint.MaxValue;
    //};
#endif

}
