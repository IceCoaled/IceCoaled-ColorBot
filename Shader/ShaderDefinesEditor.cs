using System.Text.RegularExpressions;
using SCB;
using SharpGen.Runtime;
using Vortice.Direct3D;

namespace ShaderUtils
{

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
        [GeneratedRegex( @"#define\s+MAX_PLAYERS\s+uint\(\s*\d+\s*\)", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetMaxPlayers();

        [GeneratedRegex( @"#define\s+WINDOW_SIZE_X\s+uint\(\s*\d+\s*\)", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetWindowSizeX();

        [GeneratedRegex( @"#define\s+WINDOW_SIZE_Y\s+uint\(\s*\d+\s*\)", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetWindowSizeY();

        [GeneratedRegex( @"#define\s+X_THREADGROUP\s+uint\(\s*\d+\s*\)", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetXNumTreads();

        [GeneratedRegex( @"#define\s+NUM_COLOR_RANGES\s+uint\(\s*\d+\s*\)", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetNumColorRanges();

        [GeneratedRegex( @"#define\s+DEBUG$", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetReleaseMode();

        [GeneratedRegex( @"min16uint3\s+COLOR_NAME_OUTLNZ\s+\[\s*2\s*\]\s+=\s+\{\s*min16uint3\(\s*\d+,\s*\d+,\s*\d+\s*\),\s*min16uint3\(\s*\d+,\s*\d+,\s*\d+\s*\)\s*\};", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetOutlineName();

        [GeneratedRegex( @"min16uint3\s+COLOR_NAME_HAIR\s+\[\s*2\s*\]\s+=\s+\{\s*min16uint3\(\s*\d+,\s*\d+,\s*\d+\s*\),\s*min16uint3\(\s*\d+,\s*\d+,\s*\d+\s*\)\s*\};", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetHairName();

        [GeneratedRegex( @"min16uint3\s+COLOR_NAME_SKIN\s+\[\s*2\s*\]\s+=\s+\{\s*min16uint3\(\s*\d+,\s*\d+,\s*\d+\s*\),\s*min16uint3\(\s*\d+,\s*\d+,\s*\d+\s*\)\s*\};", RegexOptions.NonBacktracking | RegexOptions.Compiled, 35 )]
        private static partial Regex SetSkinName();


        /// <summary>
        /// This is only used to make things more readable.
        /// </summary>
        private enum RegexFactoryKeys
        {
            PlayerCount,
            WindowX,
            WindowY,
            Xthreads,
            NumRanges,
            ReleaseMode,
            OutlineName,
            HairName,
            SkinName,
        }


        /// <summary>
        /// Our regex factory, this way we can just run the regex's in a loop.
        /// Much easier and cleaner then calling all of them in seperate if statments.
        /// </summary>
        private readonly static Dictionary<RegexFactoryKeys, Func<Regex>> RegexFactory = new()
        {
            {RegexFactoryKeys.PlayerCount, SetMaxPlayers },
            {RegexFactoryKeys.WindowX, SetWindowSizeX },
            {RegexFactoryKeys.WindowY, SetWindowSizeY },
            {RegexFactoryKeys.Xthreads, SetXNumTreads },
            {RegexFactoryKeys.NumRanges, SetNumColorRanges },
            {RegexFactoryKeys.ReleaseMode, SetReleaseMode },
            {RegexFactoryKeys.OutlineName, SetOutlineName },
            {RegexFactoryKeys.HairName, SetHairName },
            {RegexFactoryKeys.SkinName, SetSkinName },
        };


        /// <summary>
        /// Converts the names we use for the gpu color range buffer into shorts or utf16 values.
        /// This is done so we can efficiently pack them in the gpu buffers.
        /// </summary>
        /// <param name="utf16Names">A 3x6 short matrix, passed by reference</param>
        private static void SetColorRangeNames( ref short[,] utf16Names )
        {
            string outLineColor = PlayerData.GetOutlineColor();
            const string hair = "Hair";
            const string skin = "Skin";

            for ( int o = 0, h = 0, s = 0; ( o < outLineColor.Length ) | ( h < hair.Length ) | ( s < skin.Length );
                o = ( o < outLineColor.Length ) ? ++o : o, h = ( h < hair.Length ) ? ++h : h, s = ( s < skin.Length ) ? ++s : s )
            {
                utf16Names[ 0, o ] = ( ( short ) outLineColor[ o ] );
                utf16Names[ 1, o ] = ( ( short ) hair[ o ] );
                utf16Names[ 2, o ] = ( ( short ) skin[ o ] );
            }
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
        /// <param name="numOfColorRanges">Number of color ranges we are using in the shader</param>
        /// <param name="numOfXThreads">Number of x threads being used. dont confuse this with dispatch threads, this is the specific amount of x threads the shader is told to use per group.</param>
        /// <param name="maxPlayers"> The max players set in the shader, this changing changes a lot, currently we have it set to six as there can only be 6 enemies 3 mains, 3 spectres.</param>
        internal static void EditShaderCode( int numOfColorRanges, int numOfXThreads, int maxPlayers = 6 )
        {

            string? rawShaderCode = null;

            // Read shaderdefines.hlsli
            try
            {
                rawShaderCode = File.ReadAllText( FileManager.shaderDefineFile );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to read shader code from path: {FileManager.shaderFile}", ex ) );
            }

            short[,] utf16Names = new short[ 3, 6 ];

            // Get the 16bit arrays for the color range names
            SetColorRangeNames( ref utf16Names );

            // Window height and width for calculations in the shader
            var windowRect = PlayerData.GetRect();
            var windowWidth = windowRect.right - windowRect.left;
            var windowHeight = windowRect.bottom - windowRect.top;

            // Regex replacement strings
            string[] replacementStrings =
            [
                $"#define MAX_PLAYERS uint( {maxPlayers} )",
                $"#define WINDOW_SIZE_X uint( {windowWidth} )",
                $"#define WINDOW_SIZE_Y uint( {windowHeight} )",
                $"#define X_THREADGROUP uint( {numOfXThreads} )",
                $"#define NUM_COLOR_RANGES uint( {numOfColorRanges} )",
                "//#define DEBUG",
                $"min16uint3 COLOR_NAME_OUTLNZ [ 2 ] = {{ min16uint3( {utf16Names[0,0]}, {utf16Names[0,1]}, {utf16Names[0,2]} ), min16uint3( {utf16Names[0,3]}, {utf16Names[0,4]}, {utf16Names[0,5]} ) }};",
                $"min16uint3 COLOR_NAME_HAIR [ 2 ] = {{ min16uint3( {utf16Names[1,0]}, {utf16Names[1,1]}, {utf16Names[1,2]} ), min16uint3( {utf16Names[1,3]}, {utf16Names[1,4]}, {utf16Names[1,5]} ) }};",
                $"min16uint3 COLOR_NAME_SKIN [ 2 ] = {{ min16uint3( {utf16Names[2,0]}, {utf16Names[2,1]}, {utf16Names[2,2]} ), min16uint3( {utf16Names[2,3]}, {utf16Names[2,4]}, {utf16Names[2,5]} ) }};",
            ];

            // Loop through all our regex's and change the shader code as needed before compiling it.
            ApplyRegex( ref rawShaderCode, replacementStrings );

            // Compare raw shader code to the original shader file to see if its changed
            if ( rawShaderCode != File.ReadAllText( FileManager.shaderDefineFile ) )
            {
                File.WriteAllText( FileManager.shaderDefineFile, rawShaderCode );
            }
        }
    }
}
