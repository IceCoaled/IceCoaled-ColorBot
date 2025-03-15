#if DEBUG
//#define RENDORDOC_DEBUG
//#define CAPTURE_DEBUG
//#define DEBUG_BUFFER
#if !RENDORDOC_DEBUG
using Vortice.Direct3D11.Debug;
#endif
#endif

using System.Runtime.InteropServices;
using SCB.DirectX;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;




namespace SCB
{
    /// <summary>
    /// Manages DirectX 12 operations, including screen capture, GPU-based filtering, and resource management.
    /// </summary>
    internal partial class DirectX11 : IDisposable
    {
        // Indicates whether the object has been disposed to prevent multiple disposals.
        private bool disposed = false;

        // Color tolerance manager
        private ColorToleranceManager? colorToleranceManager;

        // RendorDoc API
#if  RENDORDOC_DEBUG
        private readonly RenderDocApi? renderDocApi;
#endif

        // gpu vendor
        private string? gpuVender;

        // D3D11 device and core components
        private ID3D11Device? d3d11Device;
        private ID3D11DeviceContext? d3d11Context;
        private IDXGIAdapter1? desktopAdapter;
        private ID3D11DeviceContext4? d3d11Context4;
        private ID3D11Fence? d3d11Fence;

        // Fence value
        private ulong d3d11FenceValue;

        // views
        private ID3D11UnorderedAccessView? uav;

        // Buffers
        private ID3D11Texture2D? uaBuffer;
        private ID3D11Texture2D? stagingBuffer;

        // Texture description
        private Texture2DDescription textureDescription;

        // Window Capture
        internal WindowCapture? windowCapture;

#if DEBUG
#if !RENDORDOC_DEBUG
        // Debug info queue
        private ID3D11InfoQueue? infoQueue;
        private Thread? DebugLogger;
        private CancellationTokenSource? DebugCancellation;
#endif
#endif
        // Shader management
        private ShaderManager? ShaderManager;
        private UavBufferManager? BufferManager;
        private List<ID3D11UnorderedAccessView?>? uavBufferViews;
        private Dictionary<ShaderManager.ShaderType, ID3D11ComputeShader?>? computeShaders;


        // class reset event
        internal ManualResetEventSlim ResettingClass { get; private set; }
        /// <summary>
        /// When the task thread starts the cleaning process
        /// It will call <function cref="ManualResetEventSlim.Reset"></function>
        /// The main thread will wait if needed for the signal to be set
        /// </summary>
        private ManualResetEventSlim CleaningBuffersSignal { get; set; }

        // Window information
        private PInvoke.RECT Window; //< cant use proper because i want to reference the window rect.
        private uint WindowWidth { get; set; }
        private uint WindowHeight { get; set; }
        private int BitsPerPixel { get; set; }
        private uint XDispatch { get; set; }
        private uint YDispatch { get; set; }


        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        internal DirectX11( ref ColorToleranceManager? toleranceManager, [Optional] RenderDocApi? rDocApi )
        {
            // Set the manual reset event, so it doesnt block anything
            ResettingClass = new();
            ResettingClass.Set();
            // Set the cleaning buffers signal
            CleaningBuffersSignal = new();
            CleaningBuffersSignal.Set();

            // Set our new frame thread signal
            NewFrame = new( default );
            NewFrame.SetNonSignaled();

            // Set initial fence value
            d3d11FenceValue = 0ul;

            // Set the render doc api
#if RENDORDOC_DEBUG

            renderDocApi = rDocApi;
            if ( renderDocApi is null )
            {
                ErrorHandler.HandleException( new Exception( "RenderDoc API is null" ) );
            }
#endif

            // Set tolerance manager
            colorToleranceManager = toleranceManager;
            if ( colorToleranceManager is null )
            {
                ErrorHandler.HandleException( new Exception( "Color tolerance manager is null" ) );
            }


            // Get the monitor information
            Window = PlayerData.GetRect();
            WindowHeight = ( uint ) Window.bottom - ( uint ) Window.top;
            WindowWidth = ( uint ) Window.right - ( uint ) Window.left;

            // Get the bits per pixel
            BitsPerPixel = Screen.PrimaryScreen!.BitsPerPixel;

            // Initialize the DirectX 12 device and core components
            InitD3D11();

            // Calculate dispatch sizes
            // X threads is the number of threads in a thread group for the x axis
            // this is in the shader not the actual dispatch size
            // I.E ->[numthreads( X_THREADGROUP, Y_THREADGROUP, 1 )] <-X_THREADGROUP
            int xThreads = ( gpuVender == "AMD" ) ? 16 : 8;
            XDispatch = ( ( uint ) ( ( WindowWidth + ( xThreads - 1 ) ) / xThreads ) );
            YDispatch = ( WindowHeight + 3u ) >> 2; //< its always 4 Y threads per group so we can just divide by 4

            // Create the window capture class
            windowCapture = new( ref d3d11Device!, ref textureDescription.Format );

            // Subscribe to new frame event
            windowCapture.FramePool!.FrameArrived += this.FrameArrivedHandler;

            // Create the buffer manager
            BufferManager = new( ref colorToleranceManager!, ref d3d11Device!, xThreads );
            // Create the shader manager
            ShaderManager = new( ref d3d11Device!, ref colorToleranceManager!, ref BufferManager, ref gpuVender! );

            // Get the compute shaders, and uav buffer views
            SetAllShaders();
            SetAllUavViews();

            // Subscribe to the player data update event
            PlayerData.OnUpdate += UserSettingsUpdated;
        }


        ~DirectX11()
        {
            Dispose( false );
        }


        /// <summary>
        /// Initializes the DirectX 12 device, command queue, swap chain, descriptor heaps, and other components.
        /// </summary>
        private void InitD3D11()
        {
            var featureLvls = new FeatureLevel[ 2 ];

            // Set the feature levels we want to use
            // We need a minimum of 12_0 to use the necessary features for this application.
            // 12 is fairly common on modern gpus so most people should be able to run this application.
            featureLvls[ 0 ] = FeatureLevel.Level_12_0;
            featureLvls[ 1 ] = FeatureLevel.Level_12_1;


            // Updated adapter selection code
            using IDXGIFactory4 factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            SharpGenException deviceException; // Bullshit compiler warning, cant use variables that arent instantiated.

            // Get the right adapter to create the device we need
            for ( uint i = 0; factory.EnumAdapters1( i, out desktopAdapter ).Success; ++i )
            {
                AdapterDescription1 desc = desktopAdapter!.Description1;
                gpuVender = desc.VendorId switch
                {
                    0x10DE => "NVIDIA",
                    0x1002 => "AMD",
                    0x8086 => "Intel",
                    _ => "Unknown"
                };

                if ( gpuVender == "Unknown" )
                {
                    desktopAdapter.Dispose();
                    continue;
                }


                if ( ( desc.Flags & AdapterFlags.None ) == 0 )
                {
#if DEBUG
                    DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.Debug | DeviceCreationFlags.BgraSupport;
#else
                    DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.BgraSupport;
#endif

                    deviceException = new( D3D11.D3D11CreateDevice( desktopAdapter, DriverType.Unknown, deviceCreationFlags, featureLvls, out d3d11Device, out FeatureLevel selectedLvl, out d3d11Context ) );

                    if ( deviceException.ResultCode == 0x0 )
                    {
                        // Check if the adapter supports the necessary features
                        // If this fails we will exit the program
                        var hwOpts = d3d11Device!.CheckFeatureSupport<FeatureDataD3D10XHardwareOptions>( Vortice.Direct3D11.Feature.D3D10XHardwareOptions );
                        if ( hwOpts.ComputeShadersPlusRawAndStructuredBuffersViaShader4X )
                        {

#if DEBUG
                            Logger.Log( $"Selected Feature Level: {selectedLvl}" );
                            Logger.Log( $"Selected GPU Vendor: {gpuVender}" );
                            Logger.Log( $"Selected Adapter: {desc.Description}" );
                            Logger.Log( $"Selected Adapter Vendor ID: {desc.VendorId}" );
                            Logger.Log( $"Selected Adapter Device ID: {desc.DeviceId}" );
                            Logger.Log( $"Selected Adapter Subsystem ID: {desc.SubsystemId}" );
                            Logger.Log( $"Selected Adapter Revision: {desc.Revision}" );
                            Logger.Log( $"Selected Adapter Dedicated Video Memory: {desc.DedicatedVideoMemory}" );
                            Logger.Log( $"Selected Adapter Dedicated System Memory: {desc.DedicatedSystemMemory}" );
                            Logger.Log( $"Selected Adapter Shared System Memory: {desc.SharedSystemMemory}" );
#endif
                        } else
                        {
                            ErrorHandler.HandleException( new Exception( "Compute Shaders, raw buffers, and structured buffers are not supported" ) );
                        }
                        break;
                    }
                } else
                {
                    desktopAdapter.Dispose();
                    continue;
                }
            }



            // Check if a suitable adapter was found
            if ( desktopAdapter is null || d3d11Device is null )
            {
                ErrorHandler.HandleException( new Exception( "No suitable Direct3D 12 adapter found" ) );
            }

            // Query higher level interfaces   
            d3d11Context4 = d3d11Context?.QueryInterfaceOrNull<ID3D11DeviceContext4>();
            if ( d3d11Context4 is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to query device context 4 interface" ) );
            }

            using ID3D11Device5? d3d11Device5 = d3d11Context4?.Device.QueryInterfaceOrNull<ID3D11Device5>();
            if ( d3d11Device5 is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to query device 5 interface" ) );
            }

#if DEBUG
#if !RENDORDOC_DEBUG
            // Setup the debug layer
            SetupDebugLayer();
#endif
#endif

            textureDescription = new()
            {
                Width = WindowWidth,
                Height = WindowHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = BitsPerPixel == 32 ? Format.B8G8R8A8_UNorm : Format.Unknown,
                SampleDescription = new SampleDescription( 1, 0 ),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.UnorderedAccess,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };


            // If any of the checks fails
            // We will exit the program
            CheckFormatSupport();

            uaBuffer = d3d11Device?.CreateTexture2D( textureDescription );
            if ( uaBuffer is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create output buffer" ) );
            }

            // Create unordered access view
            var uavDesc = new UnorderedAccessViewDescription()
            {
                Format = textureDescription.Format,
                ViewDimension = UnorderedAccessViewDimension.Texture2D,
                Texture2D = new Texture2DUnorderedAccessView()
                {
                    MipSlice = 0
                }
            };

            uav = d3d11Device?.CreateUnorderedAccessView( uaBuffer, uavDesc );
            if ( uav is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create unordered access view" ) );
            }

            // Create the staging buffer
            var stagingDesc = textureDescription;

            if ( stagingDesc.Width == WindowWidth && stagingDesc.Height == WindowHeight )
            {
                stagingDesc.Usage = ResourceUsage.Staging;
                stagingDesc.BindFlags = BindFlags.None;
                stagingDesc.CPUAccessFlags = CpuAccessFlags.Write | CpuAccessFlags.Read;
            } else
            {
                ErrorHandler.HandleException( new OutOfMemoryException( "Failed to copy texture description to staging description" ) );
            }


            stagingBuffer = d3d11Device?.CreateTexture2D( stagingDesc );
            if ( stagingBuffer is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create staging buffer" ) );
            }


            d3d11Fence = d3d11Device5?.CreateFence( 1 );
            if ( d3d11Fence is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create fence" ) );
            }



#if DEBUG
            // Set all the debug names for the objects
            d3d11Fence.DebugName = "IceFence";
            uaBuffer.DebugName = "IceUaBuffer";
            stagingBuffer.DebugName = "IceStagingBuffer";
            uav.DebugName = "IceUav";
            d3d11Context4!.DebugName = "IceContext4";
            d3d11Device5!.DebugName = "IceDevice5";
            d3d11Context!.DebugName = "IceContext";
            d3d11Device!.DebugName = "IceDevice";
            Logger.Log( "D3D11 Initialized" );
#endif
        }




#if DEBUG
#if !RENDORDOC_DEBUG
        private void SetupDebugLayer()
        {

            // Get the debug layer interfaces
            using var d3d11Debug = d3d11Device?.QueryInterface<ID3D11Debug>();
            if ( d3d11Debug is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get D3D11 Debug Interface." ) );
            } else
            {
                // Set the debug layer options
                d3d11Debug!.ReportLiveDeviceObjects( ReportLiveDeviceObjectFlags.Detail );
            }
            infoQueue = d3d11Device?.QueryInterface<ID3D11InfoQueue>();
            if ( infoQueue is null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get D3D11 Info Queue." ) );
            } else
            {
                // Enable the debug layer
                infoQueue.MuteDebugOutput = false;
                infoQueue.MessageCountLimit = 1000;

                // Configure the info queue
                infoQueue!.SetBreakOnSeverity( MessageSeverity.Corruption, true );
                infoQueue.SetBreakOnSeverity( MessageSeverity.Error, true );
                infoQueue.SetBreakOnSeverity( MessageSeverity.Warning, true );
                infoQueue.SetBreakOnSeverity( MessageSeverity.Message, true );

                // let the debug logger run in the background till the class is disposed
                DebugCancellation = new();
                DebugLogger = new( LogDebug );
                DebugLogger.Start();
            }
        }


        private void LogDebug()
        {

            int yieldCount = 0;
            while ( !disposed || DebugCancellation!.IsCancellationRequested )
            {
                // Double-check infoQueue before waiting
                if ( infoQueue is null )
                {
                    // Handle the null case (log, wait, retry, etc.)
                    ErrorHandler.HandleExceptionNonExit( new NullReferenceException( "infoQueue is null before waiting" ) );
                    continue;
                }


                while ( !ResettingClass.IsSet || infoQueue is null )
                {
                    Utils.Watch.SecondsSleep( 1 );

                    if ( ++yieldCount == 250 )
                    {
                        ErrorHandler.HandleException( new InvalidOperationException( " dx11 debug task hung" ) );
                    }
                }

                yieldCount = 0;

                try
                {
                    ulong msgCount = 0;
                    if ( infoQueue is not null )
                    {
                        Utils.Watch.MilliSleep( 15 );
                        msgCount = infoQueue.NumStoredMessages;
                    } else
                    {
                        continue;
                    }


                    if ( msgCount > 0 )
                    {
                        for ( ulong i = 0; i < msgCount; i++ )
                        {
                            var message = infoQueue!.GetMessage( i );
                            string currentTnD = DateTime.Now.ToString( "MM/dd/yyyy HH:mm:ss" );

                            string logMsg = $"\n\n {currentTnD} \n Severity: {message.Severity} \n ID: {message.Id} \n Category: {message.Category} \n Description: {message.Description}";
                            File.AppendAllText( FileManager.d3d11LogFile, logMsg, System.Text.Encoding.Unicode ); // Not sure why the fuck its requiring the use of unicode but whatever.
                        }

                        if ( infoQueue!.NumStoredMessages == msgCount )
                        {
                            infoQueue.ClearStoredMessages();
                        }
                    }
                } catch ( SharpGenException? ex )
                {
                    // If the exception is a null exception we will yield as the class is resetting
                    if ( ex.InnerException is NullReferenceException )
                    {
                        while ( !ResettingClass.IsSet )
                        {
                            Utils.Watch.MilliSleep( 1000 );
                        }
                    }
                }
                Utils.Watch.MilliSleep( 10 );
            }
        }
#endif
#endif

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Releases unmanaged resources and optionally releases managed resources if disposing is true.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !disposed )
            {
                if ( windowCapture is not null && windowCapture.FramePool is not null )
                {
                    // Subscribe to new frame event
                    windowCapture.FramePool.FrameArrived -= this.FrameArrivedHandler;
                    windowCapture.Dispose();
                }
                PlayerData.OnUpdate -= UserSettingsUpdated;
                uavBufferViews?.Clear();
                computeShaders?.Clear();
                BufferManager?.Dispose();
                ShaderManager?.Dispose();
                ResettingClass?.Dispose();
                CleaningBuffersSignal.Dispose();
                NewFrame?.Dispose();
                uaBuffer?.Dispose();
                stagingBuffer?.Dispose();
                uav?.Dispose();
                d3d11Fence?.Dispose();
                d3d11Context4?.Dispose();
                d3d11Device?.Dispose();
                d3d11Context?.Dispose();
                desktopAdapter?.Dispose();

#if DEBUG
#if !RENDORDOC_DEBUG
                infoQueue?.Dispose();
                DebugCancellation?.Cancel();
                DebugCancellation?.Dispose();
#endif
#endif
            }
            disposed = true;
        }
    }



}
