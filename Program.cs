using Recoil;
using Utils;

namespace SCB
{


    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //create any folders that don't exist from FilesAndFolders
            if ( !Directory.Exists( FilesAndFolders.tessdataFolder ) )
            {
                Directory.CreateDirectory( FilesAndFolders.tessdataFolder );

                //if this folder doesnt exist, then download the tessdata folder from the github repo
                string url = "https://github.com/tesseract-ocr/tessdata/blob/main/eng.traineddata";
                string path = FilesAndFolders.tessEngineFile;

                Task.Run( () => DownloadFile( url, path ) );
            }



            if ( !Directory.Exists( FilesAndFolders.configFolder ) )
            {
                Directory.CreateDirectory( FilesAndFolders.configFolder );
            }

            if ( !Directory.Exists( FilesAndFolders.debugFolder ) )
            {
                Directory.CreateDirectory( FilesAndFolders.debugFolder );
            }

            if ( !Directory.Exists( FilesAndFolders.recoilFolder ) )
            {
                Directory.CreateDirectory( FilesAndFolders.recoilFolder );

                Directory.CreateDirectory( FilesAndFolders.recoilPatterns );

                //if this folder doesnt exist, then download the recoil folder from the github repo
                string url = "https://github.com/IceCoaled/IceCoaled-ColorBot/tree/master/recoilpatterns";
                string path = FilesAndFolders.recoilPatterns;

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
            if ( !Directory.Exists( FilesAndFolders.enemyScansFolder ) )
            {
                Directory.CreateDirectory( FilesAndFolders.enemyScansFolder );
            }
#endif


            // setup misc classes
            ColorTolerances.SetupColorTolerances();
#if DEBUG
            Logger.Start();
            Logger.Log( "Logger Initialized" );
            Task.Run( () => RecoilPatternProcessor.RecoilPatternThread() );
#else
            Task.Run( () => RecoilPatternProcessor.RecoilPatternThread() );
#endif

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run( new IceColorBot() );
        }



        /// <summary>
        /// function to download a gun recoil patterns repo if they downt exist
        /// </summary>
        /// <param name="urlPath"> url for the.txt file containing recoil info</param>
        /// <param name="destinationPath">file destination to create and write to</param>
        /// <returns></returns>
        private static async Task DownloadFile( string urlPath, string destinationPath )
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
                    ErrorHandler.PrintToStatusBar( $"Downloaded {urlPath} to {destinationPath}" );

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
    }
}