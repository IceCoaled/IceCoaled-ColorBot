using System.Runtime.InteropServices;



namespace SCB
{
    public class RenderDocApi
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

            if ( Directory.Exists( renderDocCaptureFolder ) )
            {
                Directory.CreateDirectory( renderDocCaptureFolder );
            }

            SetCaptureFilePathTemplate( renderDocCaptureFolder + @"\\capture" );
        }



        // Example Function: Start Frame Capture
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



        private int GetApiVersionFromJson( string dirPath )
        {
            // We specifically look for the line "api_version": "1.3.131",

            // Concatenate the path
            string jsonPath = Path.GetFullPath( RendorDocJson, dirPath );

            if ( !File.Exists( jsonPath ) )
            {
                return 0;
            }

            // Read all lines
            string[] lines = File.ReadAllLines( jsonPath );

            int versionNum = -1;

            foreach ( string line in lines )
            {
                // Find the api_version line
                if ( line.Contains( "api_version" ) )
                {
                    string[] split = line.Split( '"' );
                    string version = split[ 3 ][ ..^1 ];
                    version = version.Replace( ".", "0" );

                    // remove last 2 characters and return as a whole number
                    versionNum = int.Parse( version[ ..^1 ] );
                }
            }

            // Now we need to convert it to ta valid version number. I.E.
            // if we are 1.3.131 we need to conver it to 10300, 1.2.xxx to 10200, 1.4.1 to 10401, 1.4.2 to 10402

            if ( versionNum != -1 )
            {
                int major = versionNum / 10000;
                int minor = ( versionNum - major * 10000 ) / 100;
                return major * 10000 + minor * 100;
            }

            return versionNum;
        }

        // LoadLibrary Helper
        [DllImport( "kernel32.dll", CharSet = CharSet.Auto )]
        private static extern IntPtr GetModuleHandle( string lpModuleName );

        // GetProcAddress Helper
        [DllImport( "kernel32.dll", CharSet = CharSet.Ansi )]
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


        // Capture Options Enum
        public enum CaptureOption
        {
            AllowVSync,
            AllowFullscreen,
            ApiValidation,
            CaptureCallstacks,
            CaptureCallstacksOnlyActions,
            DelayForDebugger,
            VerifyBufferAccess,
            HookIntoChildren,
            RefAllResources,
            CaptureAllCmdLists,
            DebugOutputMute,
            AllowUnsupportedVendorExtensions,
            SoftMemoryLimit,
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

