using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PInvoke;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11on12;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using Color = System.Drawing.Color;
using Feature = Vortice.Direct3D12.Feature;


namespace SCB
{
    /// <summary>
    /// Manages DirectX 12 operations, including screen capture, GPU-based filtering, and resource management.
    /// </summary>
    internal class DirectX12 : IDisposable
    {
        // Indicates whether the object has been disposed to prevent multiple disposals.
        private bool disposed;

        // Window details
        private string? gpuVender;

        // Devices and Core Pipeline Components
        private ID3D12Device2? d3d12Device;
        private ID3D12CommandQueue? commandQueue;
        private ID3D12GraphicsCommandList2? commandList;
        private ID3D12CommandAllocator? commandAllocator;

        // Dxgi interface 
        private IDXGIAdapter1? desktopAdapter;
        private ID3D11On12Device? d3d11On12Device;
        private ID3D11Device? d3d11Device;
        private IDXGIOutput? output;
        private IDXGIOutput1? output1;
        private IDXGIOutputDuplication? outputDuplication;

        // Descriptor Heaps
        private ID3D12DescriptorHeap? rtvHeap; // Render Target View Heap
        private ID3D12DescriptorHeap? srvHeap; // Shader Resource View Heap
        private ID3D12DescriptorHeap? uavHeap; // Unordered Access View Heap

        // Root Signature
        private ID3D12RootSignature? rootSignature;

        // Staging Buffers and Resources
        private ID3D12Resource? capturedResource; // Resource for captured screen data
        private ID3D12Resource? filteredResource; // Resource for storing filtered image data

        // Fence and Synchronization
        private ID3D12Fence? fence;
        private ulong fenceValue;
        private AutoResetEvent? fenceEvent;

        // Shader and Pipeline State
        private ID3D12PipelineState? initialPipelineState;
        private PixelShaderManager? shaderManager;

        // Debug layer
        ID3D12Debug1? debugDevice;
        ID3D12InfoQueue1? infoQueue;

        // Monitor information
        private RECT monitorRect;





        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        /// <param name="hWnd">The window handle for capturing screen data.</param>
        /// <param name="windowRect">The dimensions of the window to capture.</param>
        /// <param name="shaderPaths">A list of paths to the shader files to use for filtering.</param>
        internal DirectX12()
        {
            // Get the monitor information
            Screen primaryScreen = Screen.PrimaryScreen;
            monitorRect = new RECT
            {
                left = primaryScreen!.Bounds.Left,
                top = primaryScreen!.Bounds.Top,
                right = primaryScreen!.Bounds.Right,
                bottom = primaryScreen!.Bounds.Bottom
            };

            // Initialize the DirectX 12 device and core components
            InitD3D12();

            // Create the Shader Manager to manage pixel shaders, pipleline states, and resources
            shaderManager = ErrorHandler.HandleObjCreation( new PixelShaderManager( d3d12Device!, rootSignature!, gpuVender! ), nameof( shaderManager ) );
        }




        ~DirectX12()
        {
            Dispose( false );
        }




        /// <summary>
        /// Initializes the DirectX 12 device, command queue, swap chain, descriptor heaps, and other components.
        /// </summary>
        private void InitD3D12()
        {
            // Updated adapter selection code
            using IDXGIFactory4 factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            // Get the first hardware adapter that supports Direct3D 12
            desktopAdapter = null;
            for ( uint i = 0; factory.EnumAdapters1( i, out IDXGIAdapter1 tempAdapter ).Success; i++ )
            {
                AdapterDescription1 desc = tempAdapter.Description1;
                gpuVender = desc.VendorId switch
                {
                    0x10DE => "NVIDIA",
                    0x1002 => "AMD",
                    0x8086 => "Intel",
                    _ => "Unknown"
                };



                if ( gpuVender == "Unknown" )
                {
                    tempAdapter.Dispose();
                    continue;
                }

                // Skip the adapter if it is a software adapter
                if ( ( desc.Flags & AdapterFlags.Software ) != 0 )
                {
                    tempAdapter.Dispose();
                    continue;
                }

                if ( D3D12.D3D12CreateDevice( tempAdapter, FeatureLevel.Level_12_0, out ID3D12Device2? featureCheckDevice ).Success )
                {
                    // Check for Mesh Shader support of at least Tier 1, this means minimum Direct3D 12 Ultimate support (12.2)
                    var featureData = new FeatureDataD3D12Options7();
                    if ( featureCheckDevice!.CheckFeatureSupport( Feature.Options7, ref featureData ) &&
                        featureData.MeshShaderTier >= MeshShaderTier.Tier1 )
                    {
                        desktopAdapter = tempAdapter;
                        featureCheckDevice.Dispose();
                        break;
                    }

                    featureCheckDevice.Dispose();
                } else
                {
                    tempAdapter.Dispose();
                }
            }


#if DEBUG
            Logger.Log( $"GPU Vendor: {gpuVender}" );
#endif

            // Check if a suitable adapter was found
            if ( desktopAdapter == null )
            {
                ErrorHandler.HandleException( new Exception( "No suitable Direct3D 12 adapter found." ) );
            }

#if DEBUG
            // Enable the D3D12 debug interface, before creating the device
            // Optional: Enable GPU-based validation if available
            if ( D3D12.D3D12GetDebugInterface<ID3D12Debug1>( out var localDevice ).Success )
            {
                localDevice?.EnableDebugLayer();
                localDevice?.SetEnableGPUBasedValidation( true );
                localDevice?.SetEnableSynchronizedCommandQueueValidation( true );
                Logger.Log( "D3D12 Debug Layer enabled." );
                Logger.Log( "D3D12 GPU-Based Validation enabled." );
                debugDevice = localDevice;
            } else
            {
                ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to enable D3D12 Debug Layer." ) );
            }
#endif


            // Create the device
            if ( !D3D12.D3D12CreateDevice( desktopAdapter, FeatureLevel.Level_12_0, out ID3D12Device2? tempDevice ).Success )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create D3D12 device." ) );
            }
            d3d12Device = tempDevice;
            tempDevice = null;

#if DEBUG 
            // Setup the debug layer
            SetupDebugLayer();
#endif

            // Create d3d11 device
            d3d11Device = D3D11.D3D11CreateDevice( DriverType.Hardware, DeviceCreationFlags.BgraSupport, FeatureLevel.Level_11_1 );
            if ( d3d11Device == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create D3D11 device." ) );
            }

            // Get the DXGI output
            if ( !desktopAdapter.EnumOutputs( 0, out output ).Success )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get DXGI output." ) );
            }

            // Get the DXGI output1
            output1 = output.QueryInterface<IDXGIOutput1>();
            if ( output1 == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get DXGI output1." ) );
            }


            // Dispose output
            output.Dispose();

            // Create the captured and filtered resources
            capturedResource = ErrorHandler.HandleObjCreation( d3d12Device.CreateCommittedResource( new HeapProperties( HeapType.Default ), HeapFlags.None,
                ResourceDescription.Texture2D( Format.R8G8B8A8_UNorm, ( uint ) ( monitorRect.right - monitorRect.left ), ( uint ) ( monitorRect.bottom - monitorRect.top ), 1, 1, 1, 0, Vortice.Direct3D12.ResourceFlags.AllowUnorderedAccess ), ResourceStates.CopySource ), nameof( capturedResource ) );

            filteredResource = ErrorHandler.HandleObjCreation( d3d12Device.CreateCommittedResource( new HeapProperties( HeapType.Default ), HeapFlags.None,
                ResourceDescription.Texture2D( Format.R8G8B8A8_UNorm, ( uint ) ( monitorRect.right - monitorRect.left ), ( uint ) ( monitorRect.bottom - monitorRect.top ), 1, 1, 1, 0, Vortice.Direct3D12.ResourceFlags.AllowUnorderedAccess ), ResourceStates.Common ), nameof( filteredResource ) );


            // Create Command Queue
            commandQueue = ErrorHandler.HandleObjCreation( d3d12Device!.CreateCommandQueue( new CommandQueueDescription( CommandListType.Direct ) ), nameof( commandQueue ) );


            // Create Render Target View Descriptor Heap
            rtvHeap = ErrorHandler.HandleObjCreation( d3d12Device.CreateDescriptorHeap( new DescriptorHeapDescription
            {
                Type = DescriptorHeapType.RenderTargetView,
                DescriptorCount = 2,
                Flags = DescriptorHeapFlags.None
            } ), nameof( rtvHeap ) );


            // Create Shader Resource and Unordered Access Descriptor Heaps
            srvHeap = ErrorHandler.HandleObjCreation( d3d12Device.CreateDescriptorHeap( new DescriptorHeapDescription
            {
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible
            } ), nameof( srvHeap ) );

            uavHeap = ErrorHandler.HandleObjCreation( d3d12Device.CreateDescriptorHeap( new DescriptorHeapDescription
            {
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible
            } ), nameof( uavHeap ) );

            // Create Command Allocator and Command List
            commandAllocator = ErrorHandler.HandleObjCreation( d3d12Device.CreateCommandAllocator( CommandListType.Direct ), nameof( commandAllocator ) );
            commandList = ErrorHandler.HandleObjCreation( d3d12Device.CreateCommandList<ID3D12GraphicsCommandList2>( 0, CommandListType.Direct, commandAllocator!, initialPipelineState ), nameof( commandList ) );

            // Create Root Signature
            RootDescriptor rootDescriptor = new( 0, 0 ); // Register b0, Space 0
            RootParameter[] rootParameters =
            [
                // Constant buffer view at register b0, space 0, visible to all shaders
                new(RootParameterType.ConstantBufferView, rootDescriptor, ShaderVisibility.All),

                // Descriptor table for SRV at t0
                new(new RootDescriptorTable(new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0)), ShaderVisibility.Pixel),

                // Descriptor table for UAV at u0
                new(new RootDescriptorTable(new DescriptorRange(DescriptorRangeType.UnorderedAccessView, 1, 0)), ShaderVisibility.Pixel)
            ];

            StaticSamplerDescription[] staticSamplers = new StaticSamplerDescription[]
            {
                new( ( uint) ShaderVisibility.All, 0)
            };

            RootSignatureDescription rootSignatureDesc = new(
                RootSignatureFlags.AllowInputAssemblerInputLayout,
                rootParameters,
                staticSamplers
            );

            rootSignature = ErrorHandler.HandleObjCreation( d3d12Device.CreateRootSignature( rootSignatureDesc, RootSignatureVersion.Version1 ), nameof( rootSignature ) );

            // Create Fence for Synchronization
            fence = ErrorHandler.HandleObjCreation( d3d12Device.CreateFence( 0, Vortice.Direct3D12.FenceFlags.None ), nameof( fence ) );
            fenceValue = 1;
            fenceEvent = new AutoResetEvent( false );


            // Get desktop duplication interface
            outputDuplication = ErrorHandler.HandleObjCreation( output1!.DuplicateOutput( d3d11Device ), nameof( outputDuplication ) );

            // Set root signature and descriptor heaps
            commandList!.SetGraphicsRootSignature( rootSignature );
        }


        private void SetupDebugLayer()
        {
            // Get debug info queue
            infoQueue = d3d12Device!.QueryInterface<ID3D12InfoQueue1>();
            if ( infoQueue == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get debug info queue interface." ) );
            }

            // Set break on severity
            infoQueue.SetBreakOnSeverity( MessageSeverity.Corruption, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Error, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Warning, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Message, true );

            // Optional: Mute or unmute debug output
            infoQueue.MuteDebugOutput = false;

            //Setup Message Callback
            Vortice.Direct3D12.Debug.ID3D12InfoQueue1.MessageCallback messageCallback = ( cat, severity, id, description ) =>
            {
                var currentTimeAndDate = DateTime.Now.ToString( "MM/dd/yyyy HH:mm:ss" );
                var message = $"[{currentTimeAndDate}] \n Severity Level : {severity} \n Error Catagory: {cat} \n Error Id: {id} \n Error Description : {description}";
                File.AppendAllText( Utils.FilesAndFolders.d3d12LogFile, message );
            };

            // Create log file if it does not exist
            if ( !File.Exists( Utils.FilesAndFolders.d3d12LogFile ) )
            {
                File.Create( Utils.FilesAndFolders.d3d12LogFile ).Close();
            }

            // Register the callback
            infoQueue.RegisterMessageCallback( messageCallback, MessageCallbackFlags.FlagNone );


#if DEBUG
            Logger.Log( "D3D12 Debug Setup" );
#endif
        }



        /// <summary>
        /// Waits for the GPU to complete processing the previous frame before proceeding.
        /// </summary>
        private void WaitForPreviousFrame()
        {
            // Signal the fence to mark the end of the current frame
            ulong fenceValueForSignal = fenceValue;
            commandQueue!.Signal( fence, fenceValueForSignal );
            fenceValue++;

            // Wait until the previous frame is finished
            if ( fence!.CompletedValue < fenceValueForSignal )
            {
                fenceEvent!.WaitOne();
            }
        }



        /// <summary>
        /// Processes the current frame and returns the filtered result as a Bitmap.
        /// </summary>
        /// <returns>The filtered frame as a Bitmap.</returns>
        internal unsafe void ProcessFrameAsBitmap( ref Bitmap? bitmap )
        {
            // Wait for the previous frame to complete
            WaitForPreviousFrame();

            // Get the next frame
            if ( !outputDuplication!.AcquireNextFrame( 10, out OutduplFrameInfo frameInfo, out IDXGIResource? desktopResource ).Success )
            {
                ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to acquire next frame." ) );
                return;
            }

            using ( desktopResource )
            {

                // Get the texture from the shared handle
                using ID3D12Resource? texture = desktopResource!.QueryInterface<ID3D12Resource>();

                // Get the texture description
                ResourceDescription textureDesc = texture!.Description;

                // Check if the texture is valid
                if ( textureDesc.Dimension != Vortice.Direct3D12.ResourceDimension.Texture2D ||
                    textureDesc.Width != ( ulong ) ( monitorRect.right - monitorRect.left ) ||
                    textureDesc.Height != ( uint ) ( monitorRect.bottom - monitorRect.top ) )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( "Invalid texture." ) );
                    return;
                }

                try
                {
                    // Reset the command allocator and command list, copy the desktop texture to the captured resource
                    commandAllocator!.Reset();
                    commandList!.Reset( commandAllocator, initialPipelineState );
                    commandList!.CopyResource( capturedResource!, texture! );
                } finally
                {
                    // Release the frame (release as early as possible)
                    outputDuplication!.ReleaseFrame();
                }

                // Apply filtering using all shaders managed by the shader manager
                foreach ( string shaderName in shaderManager!.GetShaderNames() )
                {
                    ApplyFilterProcess( shaderName );
                }

                // Map the filtered resource to access the filtered image data
                void* dataPointer = null;
                if ( !filteredResource!.Map( 0, dataPointer ).Success )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to map filtered resource." ) );
                    return;
                }
                try
                {
                    // Create a Bitmap from the mapped data
                    var filterDesc = filteredResource!.Description; // changed to a chached value because vortices uses function calls to get structs
                    int width = ( int ) filterDesc.Width;
                    int height = ( int ) filterDesc.Height;
                    var bitmapFormat = filterDesc.Format.GetBitsPerPixel() == 32 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
                    long bitmapSize = ( width * height * filterDesc.Format.GetBitsPerPixel() ) / 0x08;

                    // Create a new bitmap and copy the data from the filtered resource
                    bitmap = new( width, height, bitmapFormat );
                    BitmapData bmpData = bitmap.LockBits( new Rectangle( 0, 0, width, height ), ImageLockMode.WriteOnly, bitmap.PixelFormat );
                    Buffer.MemoryCopy( dataPointer, bmpData.Scan0.ToPointer(), bitmapSize, bitmapSize );

                    bitmap.UnlockBits( bmpData );
                } finally
                {
                    // Unmap the filtered resource
                    filteredResource!.Unmap( 0, null );
                    dataPointer = null;
                }
            }
        }




        /// <summary>
        /// Applies the filtering process to the captured resource using the specified shader.
        /// </summary>
        /// <param name="shaderName">The name of the shader to use for filtering.</param>
        private void ApplyFilterProcess( string shaderName )
        {
            // Set the pipeline state for the shader
            shaderManager!.SetPipelineState( commandList!, shaderName );

            // Set the descriptor heaps
            ID3D12DescriptorHeap[] heaps = { srvHeap!, uavHeap! };
            commandList!.SetDescriptorHeaps( heaps );

            // Set the shader resource view and unordered access view
            commandList!.SetGraphicsRootDescriptorTable( 0, srvHeap!.GetGPUDescriptorHandleForHeapStart() );
            commandList!.SetGraphicsRootDescriptorTable( 1, uavHeap!.GetGPUDescriptorHandleForHeapStart() );

            // Set the constant buffer for the shader
            var shaderPipeline = shaderManager!.GetShaderPipeLine( sp => sp?.Shader!.ShaderName == shaderName );
            shaderPipeline?.ConstantBuffer?.Let( _ => commandList!.SetGraphicsRootConstantBufferView( 2, shaderPipeline.ConstantBuffer!.GPUVirtualAddress ) );

            // Record commands to filter the captured resource
            commandList!.ResourceBarrierTransition( capturedResource!, ResourceStates.CopySource, ResourceStates.NonPixelShaderResource );
            commandList!.ResourceBarrierTransition( filteredResource!, ResourceStates.Common, ResourceStates.UnorderedAccess );


            // Calculate the thread group size and dispatch the shader
            // nvidia and intel will both dynamicaly use 8, 16, 32 wavefonts, amd will dynamically use 32, or 64 wavefronts.
            uint threadGroupSize = gpuVender == "AMD" ? 64u : 32u;
            uint groupCountX = ( uint ) ( ( ( monitorRect.right - monitorRect.left ) + threadGroupSize - 1 ) / threadGroupSize );


            commandList!.Dispatch( groupCountX, 1, 1 );
            commandList!.ResourceBarrierTransition( filteredResource!, ResourceStates.UnorderedAccess, ResourceStates.CopySource );

            // Close the command list and execute it
            commandList!.Close();
            commandQueue!.ExecuteCommandList( commandList );

            // Signal the fence value
            commandQueue!.Signal( fence, fenceValue );
            fenceValue++;

            // Wait until the command queue is finished
            WaitForPreviousFrame();
        }


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
                fenceEvent?.Dispose();
                fence?.Dispose();
                initialPipelineState?.Dispose();
                filteredResource?.Dispose();
                capturedResource?.Dispose();
                rootSignature?.Dispose();
                uavHeap?.Dispose();
                srvHeap?.Dispose();
                rtvHeap?.Dispose();
                commandAllocator?.Dispose();
                commandList?.Dispose();
                commandQueue?.Dispose();
                d3d12Device?.Dispose();
                shaderManager?.Dispose();
                desktopAdapter?.Dispose();
                d3d11Device?.Dispose();
                output?.Dispose();
                output1?.Dispose();
                outputDuplication?.Dispose();

#if DEBUG
                IDXGIDebug1? liveReport = null;
                if ( DXGI.DXGIGetDebugInterface1( out liveReport ).Success )
                {
                    Guid liveReportAll = new( 0x35cdd7fc, 0x13b2, 0x421d, 0xa5, 0xd7, 0x7e, 0x44, 0x51, 0x28, 0x7d, 0x64 );
                    var file = File.Open( Utils.FilesAndFolders.d3d12LogFile, FileMode.OpenOrCreate, FileAccess.ReadWrite )!;
                    file?.Let( _ =>
                    {
                        using StreamWriter sw = new( file );
                        sw.WriteLine( "-----Live Report-----" );

                        var dwConsoleOut = Console.Out;
                        try
                        {
                            // Set the main output to the file
                            Console.SetOut( sw );

                            // Report live objects
                            liveReport?.ReportLiveObjects( liveReportAll, ReportLiveObjectFlags.All );
                        } finally
                        {
                            // Reset the output to the console
                            Console.SetOut( dwConsoleOut );
                        }

                        sw.WriteLine( "-----End Live Report-----" );

                        file.Close();
                        sw.Close();
                    }
                    );
                    liveReport?.Dispose();
                }

                infoQueue?.Dispose();
                debugDevice?.Dispose();
#endif
            }

            // Dispose unmanaged resources
            disposed = true;
        }
    }




    /// <summary>
    /// Manages pixel shaders for DirectX 12, including shader compilation, pipeline creation, and resource management.
    /// </summary>
    /// <remarks>
    /// This class is responsible for compiling pixel shaders from given file paths, creating graphics pipeline states
    /// for those shaders, and setting up the necessary resources to manage shader execution. It is also capable of
    /// disposing all managed and unmanaged resources used by the shaders and pipeline states.
    /// </remarks>
    internal class PixelShaderManager : IDisposable
    {
        /// <summary>
        /// Indicates whether the object has been disposed to prevent multiple disposals.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// The DirectX 12 device used for resource creation.
        /// </summary>
        private readonly ID3D12Device2 device;

        /// <summary>
        /// The root signature used in the pipeline.
        /// </summary>
        private readonly ID3D12RootSignature rootSignature;

        /// <summary>
        /// The list of compiled shaders and associated pipeline states.
        /// </summary>
        private readonly List<ShaderPipeline> shaderPipelines;

        private readonly string gpuVendor;


        /// <summary>
        /// Initializes a new instance of the PixelShaderManager class with the specified device, root signature, and shader file paths.
        /// </summary>
        /// <param name="device">The DirectX 12 device used for resource creation.</param>
        /// <param name="rootSignature">The root signature used in the pipeline.</param>
        /// <param name="shaderFilePaths">The list of paths to shader files to be compiled and managed.</param>
        internal PixelShaderManager( ID3D12Device2 device, ID3D12RootSignature rootSignature, string gpuVendor )
        {
            this.device = device;
            this.rootSignature = rootSignature;
            this.shaderPipelines = new();
            this.gpuVendor = gpuVendor;
            InitShaders();
        }

        ~PixelShaderManager()
        {
            Dispose( false );
        }


        /// <summary>
        /// Reads the shader code from the specified file path.
        /// There is only one shader. it has been made to be modular and generic so that we can add in the color tolerance values.
        /// </summary>
        /// <param name="shaderCode"></param>
        private static void ReadShaderCode( out string shaderCode )
        {
            shaderCode = string.Empty;
            try
            {
                shaderCode = File.ReadAllText( Utils.FilesAndFolders.shaderFile );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to read shader code from path: {Utils.FilesAndFolders.shaderFile}", ex ) );
            }

            if ( shaderCode == string.Empty )
            {
                ErrorHandler.HandleException( new Exception( $"Shader code is empty." ) );
            }
        }

        /// <summary>
        /// Initializes and compiles the shaders from the specified file paths, creating associated pipeline states.
        /// </summary>
        private void InitShaders()
        {
            // Read the raw shader code from the file
            ReadShaderCode( out string shaderCode );

            // instantiate the color tolerances classes
            using var outfits = new OutfitColorTolerances();
            using var features = new CharacterFeatureTolerances();

            // We know there will be 3 shaders currently, character features, outfits, and outlines. so this will be hardcoded for now.
            for ( int i = 0; i < 3; i++ )
            {
                // Get the shader name based on the index
                string shaderName = i == 0 ? "features" : i == 1 ? "outfits" : "outlines";

                // Get the swap color based on the index
                Color swapColor = i == 0 ? features.GetSwapColor() : i == 1 ? outfits.GetSwapColor() : ColorTolerances.GetSwapColor();

                // Get the color tolerance details based on the shader name
                List<Tuple<string, ColorTolerance[]>?> toleranceDetails = shaderName.CompareTo( "features" ) == 0
                ? features.GetCharacterFeatures()
                : shaderName.CompareTo( "outfits" ) == 0
                    ? outfits.GetOutfits()
                    : new List<Tuple<string, ColorTolerance[]>?>
                    {
                        new( "outlines", new[] { ColorTolerances.GetColorTolerance() })
                    };

                // Create the shader 
                Shader? shader = ErrorHandler.HandleObjCreation( new Shader( shaderName, shaderCode, toleranceDetails, swapColor, gpuVendor ), $"{shaderName}" + nameof( shader ) );

                // Create the constant buffer for the shader
                var constantBuffer = CreateConstantBuffer( ref shader );

                // Create the pipeline state for the shader
                ID3D12PipelineState? pipelineState = ErrorHandler.HandleObjCreation( CreatePipelineState( ref shader!.RefCompiledShader()! ), nameof( pipelineState ) );


                // Create the shader pipeline, and add it to the list
                shaderPipelines.Add( new ShaderPipeline( shader, pipelineState, constantBuffer ) );
            }
        }


        /// <summary>
        /// Creates a constant buffer for the specified shader.
        /// </summary>
        /// <param name="shader"></param>
        /// <returns>Buffer resource</returns>
        private unsafe (ID3D12Resource?, ID3D12DescriptorHeap?) CreateConstantBuffer( ref Shader? shader )
        {
            // Setup the color ranges for the constant buffer
            shader!.SetupColorRanges( out ColorRanges colorRanges );
            if ( colorRanges.NumOfRanges == 0 )
            {
                ErrorHandler.HandleException( new Exception( "No color ranges found." ) );
            }

            int constBufferSize = Marshal.SizeOf<ColorRanges>();

            // Create a constant buffer for the color ranges
            ID3D12Resource? constantBuffer = ErrorHandler.HandleObjCreation( device.CreateCommittedResource( new HeapProperties( HeapType.Upload ), HeapFlags.None,
               ResourceDescription.Buffer( ( ulong ) constBufferSize, Vortice.Direct3D12.ResourceFlags.None ), ResourceStates.GenericRead ), nameof( constantBuffer ) );

            // Map the constant buffer
            void* mappedData = null;
            if ( !constantBuffer!.Map( 0, mappedData ).Success )
            {
                ErrorHandler.HandleException( new Exception( "Failed to map constant buffer." ) );
                return default;
            }

            // Copy the color ranges to the constant buffer
            Buffer.MemoryCopy( &colorRanges, mappedData, constBufferSize, constBufferSize );

            // Unmap the constant buffer
            constantBuffer.Unmap( 0 );
            mappedData = null;

            // Create a descriptor heap for the constant buffer
            DescriptorHeapDescription cbvHeapDesc = new()
            {
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible
            };

            // Create the descriptor heap
            ID3D12DescriptorHeap? cbvHeap = ErrorHandler.HandleObjCreation( device.CreateDescriptorHeap( cbvHeapDesc ), nameof( cbvHeap ) );

            // Create a constant buffer view description
            ConstantBufferViewDescription cbvDesc = new()
            {
                BufferLocation = constantBuffer.GPUVirtualAddress,
                SizeInBytes = ( uint ) ( ( constBufferSize + 255 ) & ~255 )
            };

            // Create a constant buffer view
            device.CreateConstantBufferView( cbvDesc, cbvHeap!.GetCPUDescriptorHandleForHeapStart() );

            return (constantBuffer, cbvHeap);
        }


        /// <summary>
        /// Creates a pipeline state object using the provided compiled shader.
        /// </summary>
        /// <param name="shader">The compiled shader Blob.</param>
        /// <returns>An ID3D12PipelineState object representing the created pipeline state.</returns>
        private ID3D12PipelineState? CreatePipelineState( ref Blob shader )
        {
            // Create the pipeline state description
            GraphicsPipelineStateDescription pipelineDesc = new()
            {
                RootSignature = rootSignature,
                PixelShader = shader.AsBytes().AsMemory(),
                VertexShader = ReadOnlyMemory<byte>.Empty, // Placeholder, if needed
                RenderTargetFormats = new[] { Format.R8G8B8A8_UNorm },
                SampleDescription = new SampleDescription( 1, 0 ),
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RasterizerState = Vortice.Direct3D12.RasterizerDescription.CullNone,
                BlendState = Vortice.Direct3D12.BlendDescription.Opaque,
                DepthStencilState = Vortice.Direct3D12.DepthStencilDescription.None
            };

            // Return the created pipeline state
            return device.CreateGraphicsPipelineState( pipelineDesc );
        }

        /// <summary>
        /// Sets the pipeline state for the specified shader on the provided command list.
        /// </summary>
        /// <param name="commandList">The command list on which to set the pipeline state.</param>
        /// <param name="shaderName">The name of the shader whose pipeline state is to be set.</param>
        internal void SetPipelineState( ID3D12GraphicsCommandList commandList, string shaderName )
        {

            commandList.SetPipelineState( GetShaderPipeLine( sp => sp?.Shader!.ShaderName == shaderName )!.PipelineState );
        }

        /// <summary>
        /// Retrieves the shader pipeline associated with the given shader name.
        /// </summary>
        /// <param name="shaderName">The name of the shader whose pipeline is to be retrieved.</param>
        /// <returns>The ShaderPipeline object associated with the specified shader name.</returns>
        internal ShaderPipeline? GetShaderPipeLine( Predicate<ShaderPipeline?> match )
        {
            return shaderPipelines.Find( match )!;
        }

        /// <summary>
        /// Returns an array of all the shader names managed by the PixelShaderManager.
        /// </summary>
        /// <returns>An array of shader names.</returns>
        internal string[] GetShaderNames()
        {
            return shaderPipelines.Select( sp => sp.Shader!.ShaderName ).ToArray();
        }


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
            if ( disposing && !disposed )
            {
                foreach ( var shaderPipeline in shaderPipelines )
                {
                    shaderPipeline.Dispose();
                }
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Represents a compiled shader and its associated pipeline state.
    /// </summary>
    internal class ShaderPipeline : IDisposable
    {
        private bool disposed;
        /// <summary>
        /// The shader class that holds the shader name and compiled shader Blob.
        /// Also the color tolerance values for the shader.
        /// </summary>
        internal Shader? Shader { get; private set; }

        /// <summary>
        /// The constant buffer associated with the shader it holds the color tolerance values.
        /// </summary>
        internal ID3D12Resource? ConstantBuffer { get; private set; }

        /// <summary>
        /// The descriptor heap for the constant buffer.
        /// </summary>
        internal ID3D12DescriptorHeap? ConstantBufferHeap { get; private set; }

        /// <summary>
        /// The pipeline state associated with the shader.
        /// </summary>
        internal ID3D12PipelineState? PipelineState { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ShaderPipeline class.
        /// </summary>
        /// <param name="shader">The name of the shader.</param>
        /// <param name="pipelineState">The pipeline state associated with the shader.</param>
        /// 
        internal ShaderPipeline( Shader? shader, ID3D12PipelineState? pipelineState, (ID3D12Resource?, ID3D12DescriptorHeap?) constantBuffer )
        {
            Shader = shader!;
            PipelineState = pipelineState!;
            ConstantBuffer = constantBuffer.Item1!;
            ConstantBufferHeap = constantBuffer.Item2!;
        }

        ~ShaderPipeline()
        {
            Dispose( false );
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !disposed )
            {
                PipelineState!.Dispose();
                Shader!.Dispose();
                ConstantBuffer!.Dispose();
                ConstantBufferHeap!.Dispose();
            }

            disposed = true;
        }
    }




    /// <summary>
    /// This is the shader class that holds the shader name and compiled shader Blob.
    /// This class compiles and holds the shader code, name, path, and compiled shader Blob.
    /// With the shader code available, we can make quick changes to the code with the color tolerance values and recompile.
    /// </summary>
    internal partial class Shader : IDisposable
    {
        private bool disposed;

        /// <summary>
        /// Gets the name of the shader.
        /// </summary>
        internal string ShaderName { get; private set; }

        /// <summary>
        /// Gets or sets the shader code.
        /// </summary>
        internal string? ShaderCode { get; private set; }

        /// <summary>
        /// Compiled shader Blob.
        /// </summary>
        private Blob? compiledShader;

        /// <summary>
        /// Gets the list of tuples representing tolerance details.
        /// Each tuple contains a string name and an array of <see cref="ColorTolerance"/>.
        /// </summary>
        internal List<Tuple<string, ColorTolerance[]>?> ToleranceDetails { get; private set; }

        /// <summary>
        /// Gets or sets the swap color for the shader.
        /// </summary>
        private Color SwapColor { get; set; }

        /// <summary>
        /// Gets or sets the GPU vendor name (e.g., AMD, NVIDIA).
        /// </summary>
        private string GpuVendor { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Shader"/> class.
        /// </summary>
        /// <param name="shaderName">The name of the shader.</param>
        /// <param name="shaderCode">The shader code in string format.</param>
        /// <param name="colorTolerance">The color tolerance details for the shader.</param>
        /// <param name="swapColor">The color to be swapped.</param>
        /// <param name="gpuVendor">The GPU vendor name.</param>
        internal Shader( string shaderName, string shaderCode, List<Tuple<string, ColorTolerance[]>?> colorTolerance, Color swapColor, string gpuVendor )
        {
            ShaderName = shaderName;
            ShaderCode = shaderCode;
            ToleranceDetails = colorTolerance;
            SwapColor = swapColor;
            GpuVendor = gpuVendor;

            EditShaderCodeVariables();

            CompileShader();
        }

        /// <summary>
        /// Sets up the color ranges for the constant buffer.
        /// </summary>
        /// <param name="colorRanges">The <see cref="ColorRanges"/> structure to be set up.</param>
        internal void SetupColorRanges( out ColorRanges colorRanges )
        {
            List<ColorRange> ranges = []; // this should never get to 45 so we will hardcode 45 as our max in the HLSL shader structs.

            foreach ( var tuple in ToleranceDetails )
            {
                foreach ( var colorTolerance in tuple!.Item2 )
                {
                    ranges.Add( new ColorRange(
                        new Range( ( uint ) colorTolerance.Red!.Minimum, ( uint ) colorTolerance.Red.Maximum ),
                        new Range( ( uint ) colorTolerance.Green!.Minimum, ( uint ) colorTolerance.Green.Maximum ),
                        new Range( ( uint ) colorTolerance.Blue!.Minimum, ( uint ) colorTolerance.Blue.Maximum ) ) );
                }
            }

            colorRanges = new ColorRanges( ( uint ) ranges.Count, [ .. ranges ], new Uint4( ( uint ) SwapColor.R, ( uint ) SwapColor.G, ( uint ) SwapColor.B, ( uint ) SwapColor.A ) );
        }

        /// <summary>
        /// Compiles the shader code.
        /// </summary>
        private void CompileShader()
        {
            var result = Compiler.Compile(
                ShaderCode!,
                "main",
                string.Empty,
                string.Empty,
                out Blob blob,
                out Blob errorBlob
            );

            if ( errorBlob != null || result.Failure )
            {
                string? errorMessage = Marshal.PtrToStringAnsi( errorBlob!.BufferPointer );
                errorBlob.Dispose();
                ErrorHandler.HandleException( new Exception( $"Failed to compile shader: {errorMessage}" ) );
            }

            CompiledShader = blob;
        }

        /// <summary>
        /// Edits the shader code variables such as GPU vendor, screen width, and screen height.
        /// </summary>
        private void EditShaderCodeVariables()
        {
            // Edit GPU vendor-specific code
            if ( GpuVendor == "AMD" )
            {
                ShaderCode = ShaderCode!.Replace( "//#define AMD 64", "#define AMD 64" );

                // Make sure NVIDIA is commented out.
                if ( !ShaderCode!.Contains( "//#define NVIDIA 32" ) )
                {
                    ShaderCode = ShaderCode!.Replace( "#define NVIDIA 32", "//#define NVIDIA 32" );
                }
            } else
            {
                // NVIDIA should be commented out by default, so we are just making sure it is not commented out.
                // Intel follows the same wavefront protocol as NVIDIA.             
                ShaderCode = ShaderCode!.Replace( "//#define NVIDIA 32", "#define NVIDIA 32" );

                // Make sure AMD is commented out.
                if ( !ShaderCode!.Contains( "//#define AMD 64" ) )
                {
                    ShaderCode = ShaderCode!.Replace( "#define AMD 64", "//#define AMD 64" );
                }
            }

            // Get the screen resolution and set the screen width and height in the shader code.
            var screenRect = PlayerData.GetRect();
            uint width = ( uint ) ( screenRect.right - screenRect.left );
            uint height = ( uint ) ( screenRect.bottom - screenRect.top );

            // Use regex to replace any existing SCREEN_WIDTH and SCREEN_HEIGHT definitions.
            ShaderCode = Regex.Replace( ShaderCode!, @"#define SCREEN_WIDTH \d+", $"#define SCREEN_WIDTH {width}" );
            ShaderCode = Regex.Replace( ShaderCode!, @"#define SCREEN_HEIGHT \d+", $"#define SCREEN_HEIGHT {height}" );

            // Double check that the edits were made.
            if ( !ShaderCode!.Contains( $"#define SCREEN_WIDTH {width}" ) || !ShaderCode!.Contains( $"#define SCREEN_HEIGHT {height}" ) )
            {
                ErrorHandler.HandleException( new Exception( "Failed to edit the shader code." ) );
            }

            if ( ShaderCode!.Contains( $"//#define {GpuVendor} 64" ) )
            {
                ErrorHandler.HandleException( new Exception( "Failed to edit the shader code." ) );
            }
        }

        /// <summary>
        /// Gets a reference to the compiled shader Blob.
        /// </summary>
        /// <returns>A reference to the compiled shader Blob.</returns>
        internal ref Blob? RefCompiledShader() => ref compiledShader;

        /// <summary>
        /// Gets or sets the compiled shader Blob.
        /// </summary>
        internal Blob? CompiledShader
        {
            get => compiledShader;
            set => compiledShader = value;
        }

        ~Shader()
        {
            Dispose( false );
        }

        /// <summary>
        /// Disposes the shader and its resources.
        /// </summary>
        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Releases resources used by the <see cref="Shader"/> class.
        /// </summary>
        /// <param name="disposing">Indicates whether managed resources should also be disposed.</param>
        protected virtual void Dispose( bool disposing )
        {
            if ( disposing && !disposed )
            {
                CompiledShader!.Dispose();
            }
            disposed = true;
        }
    }

    /// <summary>
    /// These structs are used for setting up constant buffers for the shaders.
    /// </summary>

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct Uint4( uint x, uint y, uint z, uint w )
    {
        public uint X = x;
        public uint Y = y;
        public uint Z = z;
        public uint W = w;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct Range( uint minimum, uint maximum )
    {
        public uint Minimum = minimum;
        public uint Maximum = maximum;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct ColorRange( Range redRange, Range greenRange, Range blueRange )
    {
        public Range RedRange = redRange;
        public Range GreenRange = greenRange;
        public Range BlueRange = blueRange;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct ColorRanges( uint numOfRanges, ColorRange[] ranges, Uint4 swapColor )
    {
        public uint NumOfRanges = numOfRanges;

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 45 )] // Set SizeConst based on your max range count
        public ColorRange[] Ranges = ranges;

        public Uint4 SwapColor = swapColor;
    }

    /// <summary>
    /// Nullable extensions for easy null checking with pushing an action based on the object being not null
    /// </summary>
    public static class NullableExtensions
    {
        public static void Let<T>( this T? obj, Action<T> action ) where T : class
        {
            if ( obj != null )
            {
                action( obj );
            }
        }
    }
}
