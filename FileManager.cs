namespace SCB
{
    internal class FileManager
    {
        //Folders
        internal const string configFolder = @"./config/";

        internal const string tessdataFolder = @"./tessdata";

        internal const string debugFolder = @"./debug/";

        internal const string enemyScansFolder = @"./debug/enemyscans/";

        internal const string recoilFolder = @"./recoil";

        internal const string recoilPatterns = @"./recoil/patterns";

        internal const string shaderFolder = @"./shaders";

        //Files
        internal const string recoilPatternFile = @"./recoil/RecoilPattern.txt";

        internal const string exceptionLogFile = @"./debug/exceptionLog.txt";

        internal const string d3d11LogFile = @"./debug/d3d11Log.txt";

        internal const string tessEngineFile = @"./tessdata/eng.traineddata";

        internal const string shaderFile = @"./shaders/GenericShader.hlsl";

        internal static readonly string gameSettingsFile = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), @"Spectre\Saved\Config\WindowsClient\GameUserSettings.ini" );


        /// <summary>
        /// function to download a gun recoil patterns repo if they downt exist
        /// </summary>
        /// <param name="urlPath"> url for the.txt file containing recoil info</param>
        /// <param name="destinationPath">file destination to create and write to</param>
        /// <returns></returns>
        internal async Task DownloadFile( string urlPath, string destinationPath )
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
        private void FixUrl( ref string downloadUrl )
        {
            //change spaces to "%20"

            downloadUrl = downloadUrl.Replace( " ", "%20" );
        }



        internal (float mouseSens, float adsScale) GetInGameSettings()
        {
            /*
            * these are the lines we are looking for in the settings file
            * MouseSensitivityADSScale
            * MouseSensitivity
            */

            float AdsScale = 0f;
            float mouseSens = 0f;

            using StreamReader reader = new( FileManager.gameSettingsFile );
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

                    AdsScale = result;
                } else if ( line.StartsWith( "MouseSensitivity=" ) )
                {
                    string[] split = line.Split( '=' );
                    string value = split[ 1 ];
                    float.TryParse( value, out float result );

#if DEBUG
                    Logger.Log( $"MouseSensitivity: {result}" );
#endif

                    mouseSens = result;
                }
            }


            // Check if the settings were found

            if ( AdsScale == 0.0 || mouseSens == 0.0 )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get in game settings" ) );
            }

            return (mouseSens, AdsScale);
        }


        internal string GetFileMD5Hash( string fileName )
        {
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            using ( var stream = System.IO.File.OpenRead( fileName ) )
            {
                var hash = md5.ComputeHash( stream );
                return BitConverter.ToString( hash ).Replace( "-", "" );
            }
        }


        internal void Initialize()
        {
            //create any folders that don't exist from FileManager
            if ( !Directory.Exists( tessdataFolder ) )
            {
                Directory.CreateDirectory( tessdataFolder );

                //if this folder doesnt exist, then download the tessdata folder from the github repo
                string url = "https://github.com/IceCoaled/IceCoaled-ColorBot/tree/master/tessdata/eng.traineddata";
                string path = tessEngineFile;

                Task.Run( () => DownloadFile( url, path ) );
            }

            if ( !Directory.Exists( shaderFolder ) )
            {
                Directory.CreateDirectory( shaderFolder );

                // If the generic shader file doesn't exist, download it from the github repo
                string url = "https://github.com/IceCoaled/IceCoaled-ColorBot/tree/master/GenericShader.hlsl";
                string path = shaderFile;

                Task.Run( () => DownloadFile( url, path ) );

                // Wait until the file is downloaded
                while ( !File.Exists( path ) )
                {
                    Thread.Sleep( 1000 );
                }
            }

            if ( !Directory.Exists( configFolder ) )
            {
                Directory.CreateDirectory( configFolder );
            }

            if ( !Directory.Exists( debugFolder ) )
            {
                Directory.CreateDirectory( debugFolder );
            }

            if ( !File.Exists( d3d11LogFile ) )
            {
                File.Create( d3d11LogFile ).Close();
            }

            if ( !File.Exists( exceptionLogFile ) )
            {
                File.Create( exceptionLogFile ).Close();
            }

            if ( !Directory.Exists( recoilFolder ) )
            {
                Directory.CreateDirectory( recoilFolder );

                Directory.CreateDirectory( recoilPatterns );

                //if this folder doesnt exist, then download the recoil folder from the github repo
                string url = "https://github.com/IceCoaled/IceCoaled-ColorBot/tree/master/recoilpatterns";
                string path = recoilPatterns;

                List<string> CurrentAddedGuns = new()
                {
                    "BERSERKER RB3", "BLACKOUT", "BUZZSAW RT40", "CRUSADER",
                    "CYCLONE", "M25 HORNET", "M49 FURY", "M67 REAVER", "WHISPER"
                };

                foreach ( string gun in CurrentAddedGuns )
                {
                    string gunFolder = path + gun;
                    if ( !Directory.Exists( gunFolder ) )
                    {
                        Directory.CreateDirectory( gunFolder );
                    }

                    for ( int i = 1; i < 4; i++ )
                    {

                        string gunFile = $"{gunFolder}/{gun}-{i}.txt";
                        string gunFileUrl = $"{url}/{gun}/{gun}-{i}.txt";

                        Task.Run( () => DownloadFile( gunFileUrl, gunFile ) );

                        //wait till task is done
                        while ( !File.Exists( gunFile ) )
                        {
                            Thread.Sleep( 1000 );
                        }
                    }
                }
            }

#if DEBUG
            if ( !Directory.Exists( FileManager.enemyScansFolder ) )
            {
                Directory.CreateDirectory( FileManager.enemyScansFolder );
            }
#endif

            //get the in game settings
            GetInGameSettings();
        }
    }
}
