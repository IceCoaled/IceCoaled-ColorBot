using System.Text.RegularExpressions;
using SCB;

namespace ShaderUtils
{
    public static partial class ShaderUtilities
    {
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


        private static void SetColorRangeNames( ref short[,] utf16Names )
        {
            string outLineColor = "cyan";
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

        internal static void EditShaderCode( int numOfColorRanges, int numOfXThreads, int maxPlayers = 6 )
        {

            string? rawShaderCode = null;

            try
            {
                rawShaderCode = File.ReadAllText( FileManager.shaderDefineFile );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to read shader code from path: {FileManager.shaderFile}", ex ) );
            }

            short[,] utf16Names = new short[ 3, 6 ];

            SetColorRangeNames( ref utf16Names );

            var windowRect = PlayerData.GetRect();
            var windowWidth = windowRect.right - windowRect.left;
            var windowHeight = windowRect.bottom - windowRect.top;

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

            ApplyRegex( ref rawShaderCode, replacementStrings );

            // Compare raw shader code to the shader file to see if its changed
            if ( rawShaderCode != File.ReadAllText( FileManager.shaderDefineFile ) )
            {
                File.WriteAllText( FileManager.shaderDefineFile, rawShaderCode );
            }
        }
    }
}
