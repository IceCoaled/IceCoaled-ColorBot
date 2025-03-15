using System.Runtime.InteropServices;



namespace SCB.DirectX
{
    public partial class RenderDocApi
    {
        // Path to RenderDoc
        private readonly string RendorDocFilePath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles ), "RenderDoc" );

        private const string RendorDocJson = "renderdoc.json";

        private const string RenderDocDll = "renderdoc.dll";

        // API Struct Pointer
        private readonly IntPtr apiPointer;

        // Delegates for API functions
        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        private delegate int GetApiDelegate( int version, out IntPtr outApiPointers );

        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        private delegate void StartFrameCaptureDelegate( IntPtr device, IntPtr windowHandle );

        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        private delegate uint EndFrameCaptureDelegate( IntPtr device, IntPtr windowHandle );

        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        private delegate void SetCaptureFilePathTemplateDelegate( IntPtr path );


        private RenderDocApiStruct RDocApi { get; set; }

        public RenderDocApi()
        {
            // We are either using  renderdoc to launch the application or we are letting it attach at the start.
            // This way we can let it do its own setup and we can use it to capture frames.

            if ( !Directory.Exists( RendorDocFilePath ) )
            {
                ErrorHandler.HandleException( new Exception( "Failed to find RenderDoc path." ) );
            }


            IntPtr moduleHandle = GetModuleHandle( RenderDocDll );
            if ( moduleHandle == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "Failed to find RenderDoc DLL." ) );
            }

            // Get API version
            int version = GetApiVersionFromJson( RendorDocFilePath );
            if ( version == -1 )
            {
                ErrorHandler.HandleException( new Exception( "Failed to find RenderDoc API version." ) );
            }

            // Get RENDERDOC_GetAPI function
            IntPtr getApiFuncPtr = GetProcAddress( moduleHandle, "RENDERDOC_GetAPI" );
            if ( getApiFuncPtr == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "Failed to find RENDERDOC_GetAPI function." ) );
            }

            var getApi = Marshal.GetDelegateForFunctionPointer<GetApiDelegate>( getApiFuncPtr );

            // Initialize API
            if ( getApi( version, out apiPointer ) == 0 || apiPointer == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "Failed to initialize RenderDoc API." ) );
            }

            // Marshal the API struct
            RDocApi = Marshal.PtrToStructure<RenderDocApiStruct>( apiPointer );

            // Add RednorDoc capture folder to our debug folder
            string renderDocCaptureFolder = FileManager.debugFolder + @"RendorDocCaptures";

            if ( !Directory.Exists( renderDocCaptureFolder ) )
            {
                Directory.CreateDirectory( renderDocCaptureFolder );
            }

            SetCaptureFilePathTemplate( renderDocCaptureFolder + @"\\capture" );
        }



        /// <summary>
        ///  Our custom StartFrameCapture render doc api call.
        ///  Both params should be marked optional as you just choose one.
        /// </summary>
        /// <param name="device"> pointer to our d3d device</param>
        /// <param name="windowHandle">handle to a window(HWND)</param>
        public void StartFrameCapture( IntPtr device, IntPtr windowHandle )
        {
            if ( RDocApi.StartFrameCapture == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "StartFrameCapture function not found." ) );
            }

            // Get the function delegate
            var startFrameCapture = Marshal.GetDelegateForFunctionPointer<StartFrameCaptureDelegate>( RDocApi.StartFrameCapture );

            // Call the function
            startFrameCapture( device, windowHandle );
        }


        /// <summary>
        ///  Our custom EtartFrameCapture render doc api call.
        ///  Both params should be marked optional as you just choose one.
        /// </summary>
        /// <param name="device"> pointer to our d3d device</param>
        /// <param name="windowHandle">handle to a window(HWND)</param>
        /// <returns>Returns 1 if most recent caputure was discared, 0 if there was an error and no capture at all</returns>
        public uint EndFrameCapture( IntPtr device, IntPtr windowHandle )
        {
            if ( RDocApi.EndFrameCapture == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "EndFrameCapture function not found." ) );
            }

            // Get the function delegate
            var endFrameCapture = Marshal.GetDelegateForFunctionPointer<EndFrameCaptureDelegate>( RDocApi.EndFrameCapture );

            // Call the function
            return endFrameCapture( device, windowHandle );
        }


        /// <summary>
        /// Custom Path output for the frame capture files.
        /// We use this so we know they are going to our own debug folder.
        /// </summary>
        /// <param name="path"></param>
        public void SetCaptureFilePathTemplate( string path )
        {
            if ( RDocApi.SetCaptureFilePathTemplate == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "SetCaptureFilePathTemplate function not found." ) );
            }

            // Get the function delegate
            var setCaptureFilePathTemplate =
                Marshal.GetDelegateForFunctionPointer<SetCaptureFilePathTemplateDelegate>( RDocApi.SetCaptureFilePathTemplate );

            // Convert the string to a pointer
            IntPtr pathPtr = Marshal.StringToHGlobalAnsi( path );

            // Call the function
            setCaptureFilePathTemplate( pathPtr );

            // Free the pointer
            Marshal.FreeHGlobal( pathPtr );
        }



        /// <summary>
        /// Gets the current api version number from the renderdoc setting file.
        /// </summary>
        /// <param name="dirPath">Path to renderdoc folder</param>
        /// <returns>Returns the api number with the minor normalized and </returns>
        private static int GetApiVersionFromJson( string dirPath )
        {

            // Concatenate the path
            string jsonPath = Path.GetFullPath( RendorDocJson, dirPath );

            if ( !File.Exists( jsonPath ) )
            {
                return 0;
            }

            // Read all lines
            string[] lines = File.ReadAllLines( jsonPath );

            int versionNum = -1;

            lines.Where( line => line.Contains( "api_version" ) ).ToList().ForEach( line =>
            {
                string[] split = line.Split( '"' );
                string version = split[ 3 ][ ..^1 ];
                version = version.Replace( ".", "0" );
                // remove last 2 characters and get the rest of the line
                versionNum = int.Parse( version[ ..^1 ] );
            } );


            // Now we pack the version number.
            if ( versionNum != -1 )
            {
                int major = versionNum / 10000;
                int minor = ( versionNum - major * 10000 ) / 100;
                return major * 10000 + minor * 100;
            }
            return versionNum;
        }

        // LoadLibrary Helper
        [DllImport( "kernel32.dll", SetLastError = true )]
        private static extern IntPtr GetModuleHandle( string lpModuleName );

        // GetProcAddress Helper
        [DllImport( "kernel32.dll", SetLastError = true )]
        private static extern IntPtr GetProcAddress( IntPtr hModule, string procName );


        private enum RENDERDOC_Version // current accepted versions of RenderDoc api, this is only here for reference
        {
            eRENDERDOC_API_Version_1_0_0 = 10000,    // RENDERDOC_API_1_0_0 = 1 00 00
            eRENDERDOC_API_Version_1_0_1 = 10001,    // RENDERDOC_API_1_0_1 = 1 00 01
            eRENDERDOC_API_Version_1_0_2 = 10002,    // RENDERDOC_API_1_0_2 = 1 00 02
            eRENDERDOC_API_Version_1_1_0 = 10100,    // RENDERDOC_API_1_1_0 = 1 01 00
            eRENDERDOC_API_Version_1_1_1 = 10101,    // RENDERDOC_API_1_1_1 = 1 01 01
            eRENDERDOC_API_Version_1_1_2 = 10102,    // RENDERDOC_API_1_1_2 = 1 01 02
            eRENDERDOC_API_Version_1_2_0 = 10200,    // RENDERDOC_API_1_2_0 = 1 02 00
            eRENDERDOC_API_Version_1_3_0 = 10300,    // RENDERDOC_API_1_3_0 = 1 03 00
            eRENDERDOC_API_Version_1_4_0 = 10400,    // RENDERDOC_API_1_4_0 = 1 04 00
            eRENDERDOC_API_Version_1_4_1 = 10401,    // RENDERDOC_API_1_4_1 = 1 04 01
            eRENDERDOC_API_Version_1_4_2 = 10402,    // RENDERDOC_API_1_4_2 = 1 04 02
            eRENDERDOC_API_Version_1_5_0 = 10500,    // RENDERDOC_API_1_5_0 = 1 05 00
            eRENDERDOC_API_Version_1_6_0 = 10600,    // RENDERDOC_API_1_6_0 = 1 06 00
        }

        // API Struct
        [StructLayout( LayoutKind.Sequential )]
        private struct RenderDocApiStruct
        {
            public IntPtr GetApiVersion;
            public IntPtr SetCaptureOptionU32;
            public IntPtr SetCaptureOptionF32;
            public IntPtr GetCaptureOptionU32;
            public IntPtr GetCaptureOptionF32;
            public IntPtr SetFocusToggleKeys;
            public IntPtr SetCaptureKeys;
            public IntPtr GetOverlayBits;
            public IntPtr MaskOverlayBits;
            public IntPtr RemoveHooks;
            public IntPtr UnloadCrashHandler;
            public IntPtr SetCaptureFilePathTemplate;
            public IntPtr GetCaptureFilePathTemplate;
            public IntPtr GetNumCaptures;
            public IntPtr GetCapture;
            public IntPtr TriggerCapture;
            public IntPtr IsTargetControlConnected;
            public IntPtr LaunchReplayUI;
            public IntPtr SetActiveWindow;
            public IntPtr StartFrameCapture;
            public IntPtr IsFrameCapturing;
            public IntPtr EndFrameCapture;
            public IntPtr TriggerMultiFrameCapture;
            public IntPtr SetCaptureFileComments;
            public IntPtr DiscardFrameCapture;
            public IntPtr ShowReplayUI;
            public IntPtr SetCaptureTitle;
        }
    }
}

