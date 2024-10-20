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

                Task.Run( () => FilesAndFolders.DownloadFile( url, path ) );
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

                        Task.Run( () => FilesAndFolders.DownloadFile( gunFileUrl, gunFile ) );

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

            //get the in game settings
            FilesAndFolders.GetInGameSettings();


            // setup misc classes
            Utils.Mathf.SetupPermutationTable();
            ColorTolerances.SetupColorTolerances();
#if DEBUG
            Logger.Start();
            Logger.Log( "Logger Initialized" );
#endif          

            // start the recoil pattern thread
            Task.Run( () => RecoilPatternProcessor.RecoilPatternThread() );


            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run( new IceColorBot() );
        }
    }
}