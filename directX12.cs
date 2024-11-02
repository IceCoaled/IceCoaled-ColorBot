using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Color = System.Drawing.Color;
using Feature = Vortice.Direct3D12.Feature;


namespace SCB
{
    /// <summary>
    /// Manages DirectX 12 operations, including screen capture, GPU-based filtering, and resource management.
    /// </summary>
    internal unsafe class DirectX12 : IDisposable
    {
        // Indicates whether the object has been disposed to prevent multiple disposals.
        private bool disposed;

        // Window details
        private readonly nint hWnd;
        private readonly PInvoke.RECT windowRect;
        private string? gpuVender;

        // Devices and Core Pipeline Components
        private ID3D12Device2? device;
        private ID3D12CommandQueue? commandQueue;
        private ID3D12GraphicsCommandList2? commandList;
        private ID3D12CommandAllocator? commandAllocator;

        // Swap Chain and Render Targets
        private IDXGISwapChain3? swapChain;
        private ID3D12Resource[] renderTargets;

        // Descriptor Heaps
        private ID3D12DescriptorHeap? rtvHeap; // Render Target View Heap
        private ID3D12DescriptorHeap? srvHeap; // Shader Resource View Heap
        private ID3D12DescriptorHeap? uavHeap; // Unordered Access View Heap

        // Root Signature
        private ID3D12RootSignature? rootSignature;

        // Staging Buffers and Resources
        private readonly ID3D12Resource? capturedResource; // Resource for captured screen data
        private readonly ID3D12Resource? filteredResource; // Resource for storing filtered image data

        // Fence and Synchronization
        private ID3D12Fence? fence;
        private ulong fenceValue;
        private AutoResetEvent? fenceEvent;

        // Shader and Pipeline State
        private readonly ID3D12PipelineState? initialPipelineState;
        private readonly PixelShaderManager? shaderManager;



        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        /// <param name="hWnd">The window handle for capturing screen data.</param>
        /// <param name="windowRect">The dimensions of the window to capture.</param>
        /// <param name="shaderPaths">A list of paths to the shader files to use for filtering.</param>
        internal DirectX12( nint hWnd, PInvoke.RECT windowRect )
        {
            this.windowRect = windowRect;
            this.hWnd = hWnd;
            GetGpuVendorName();
            InitD3D12();
            shaderManager = new PixelShaderManager( device!, rootSignature!, gpuVender! );
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
            IDXGIAdapter1? adapter = null;
            for ( uint i = 0; factory.EnumAdapters1( i, out IDXGIAdapter1 tempAdapter ).Success; i++ )
            {
                AdapterDescription1 desc = tempAdapter.Description1;

                // Skip the adapter if it is a software adapter
                if ( ( desc.Flags & AdapterFlags.Software ) != 0 )
                {
                    tempAdapter.Dispose();
                    continue;
                }

                if ( D3D12.D3D12CreateDevice( tempAdapter, FeatureLevel.Level_12_0, out ID3D12Device? featureCheckDevice ).Success )
                {
                    // Check for Mesh Shader support of at least Tier 1, this means minimum Direct3D 12 Ultimate support (12.2)
                    var featureData = new FeatureDataD3D12Options7();
                    if ( featureCheckDevice!.CheckFeatureSupport( Feature.Options7, ref featureData ) &&
                        featureData.MeshShaderTier >= MeshShaderTier.Tier1 )
                    {
                        adapter = tempAdapter;
                        featureCheckDevice.Dispose();
                        break;
                    }

                    featureCheckDevice.Dispose();
                } else
                {
                    tempAdapter.Dispose();
                }
            }

            if ( adapter == null )
            {
                ErrorHandler.HandleException( new Exception( "No suitable Direct3D 12 adapter found." ) );
            }

            var result = D3D12.D3D12CreateDevice( adapter, FeatureLevel.Level_12_0, out ID3D12Device2? tempDevice );
            if ( result.Failure )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create D3D12 device." ) );
            }

            device = tempDevice;

            // Create Command Queue
            commandQueue = device!.CreateCommandQueue( new CommandQueueDescription( CommandListType.Direct ) );

            // Define the swap chain description
            SwapChainDescription1 swapChainDesc = new()
            {
                BufferCount = 2,
                Width = ( uint ) ( this.windowRect.right - this.windowRect.left ), // Set to the appropriate width of the window
                Height = ( uint ) ( this.windowRect.bottom - this.windowRect.top ), // Set to the appropriate height of the window
                Format = Format.R8G8B8A8_UNorm,
                BufferUsage = Usage.RenderTargetOutput,
                SwapEffect = SwapEffect.FlipDiscard,
                SampleDescription = new SampleDescription( 1, 0 )
            };

            // Create Swap Chain
            using ( IDXGISwapChain1 swapChain1 = factory.CreateSwapChainForHwnd( commandQueue, hWnd, swapChainDesc ) )
            {
                // Get the IDXGISwapChain3 for future use
                swapChain = swapChain1.QueryInterface<IDXGISwapChain3>();
            }

            // Create Render Target View Descriptor Heap
            rtvHeap = device.CreateDescriptorHeap( new DescriptorHeapDescription
            {
                Type = DescriptorHeapType.RenderTargetView,
                DescriptorCount = 2,
                Flags = DescriptorHeapFlags.None
            } );

            // Create Render Target Views
            renderTargets = new ID3D12Resource[ 2 ];
            CpuDescriptorHandle rtvHandle = rtvHeap.GetCPUDescriptorHandleForHeapStart();
            uint rtvDescriptorSize = device.GetDescriptorHandleIncrementSize( DescriptorHeapType.RenderTargetView );

            for ( uint i = 0; i < 2; i++ )
            {
                renderTargets[ i ] = swapChain.GetBuffer<ID3D12Resource>( i );
                device.CreateRenderTargetView( renderTargets[ i ], null, rtvHandle );

                // Move the handle to the next descriptor
                rtvHandle.Ptr += rtvDescriptorSize;
            }

            // Create Shader Resource and Unordered Access Descriptor Heaps
            srvHeap = device.CreateDescriptorHeap( new DescriptorHeapDescription
            {
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible
            } );

            uavHeap = device.CreateDescriptorHeap( new DescriptorHeapDescription
            {
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible
            } );

            // Create Command Allocator and Command List
            commandAllocator = device.CreateCommandAllocator( CommandListType.Direct );
            commandList = device.CreateCommandList<ID3D12GraphicsCommandList2>( 0, CommandListType.Direct, commandAllocator, initialPipelineState );

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
            rootSignature = device.CreateRootSignature( rootSignatureDesc, RootSignatureVersion.Version12 );

            // Create Fence for Synchronization
            fence = device.CreateFence( 0, FenceFlags.None );
            fenceValue = 1;
            fenceEvent = new AutoResetEvent( false );
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
        internal unsafe Bitmap ProcessFrameAsBitmap()
        {
            // Wait for the previous frame to complete
            WaitForPreviousFrame();

            // Get the current back buffer index
            uint backBufferIndex = swapChain!.CurrentBackBufferIndex;

            // Reset the command allocator and command list
            commandAllocator!.Reset();
            commandList!.Reset( commandAllocator, initialPipelineState );

            // Record commands to copy the back buffer to the captured resource
            CopyBackBufferToResource( backBufferIndex );

            // Apply filtering using all shaders managed by the shader manager
            foreach ( string shaderName in shaderManager!.GetShaderNames() )
            {
                ApplyFilterProcess( shaderName );
            }

            // Map the filtered resource to access the filtered image data
            void* dataPointer = null;
            filteredResource!.Map( 0, null, &dataPointer );

            // Create a Bitmap from the mapped data
            int width = ( int ) filteredResource.Description.Width;
            int height = ( int ) filteredResource.Description.Height;

            Bitmap bitmap = new( width, height, PixelFormat.Format32bppArgb );
            BitmapData bmpData = bitmap.LockBits( new Rectangle( 0, 0, width, height ), ImageLockMode.WriteOnly, bitmap.PixelFormat );
            IntPtr bmpDataPointer = bmpData.Scan0;

            Unsafe.Copy( dataPointer, ref bmpDataPointer );

            bitmap.UnlockBits( bmpData );

            // Unmap the filtered resource
            filteredResource!.Unmap( 0, null );

            return bitmap;
        }




        /// <summary>
        /// Copies the current back buffer to the captured resource for further processing.
        /// </summary>
        /// <param name="backBufferIndex">The index of the back buffer to copy.</param>
        private void CopyBackBufferToResource( uint backBufferIndex )
        {
            commandList!.ResourceBarrierTransition( renderTargets[ backBufferIndex ], ResourceStates.Present, ResourceStates.CopySource );
            commandList!.CopyResource( capturedResource!, renderTargets[ backBufferIndex ] );
            commandList!.ResourceBarrierTransition( renderTargets[ backBufferIndex ], ResourceStates.CopySource, ResourceStates.Present );
        }




        /// <summary>
        /// Applies the filtering process to the captured resource using the specified shader.
        /// </summary>
        /// <param name="shaderName">The name of the shader to use for filtering.</param>
        private void ApplyFilterProcess( string shaderName )
        {
            shaderManager!.SetPipelineState( commandList!, shaderName );
            commandList!.SetGraphicsRootSignature( rootSignature );

            // Set the descriptor heaps
            ID3D12DescriptorHeap[] heaps = { srvHeap!, uavHeap! };
            commandList!.SetDescriptorHeaps( heaps );

            // Set the shader resource view and unordered access view
            commandList!.SetGraphicsRootDescriptorTable( 0, srvHeap!.GetGPUDescriptorHandleForHeapStart() );
            commandList!.SetGraphicsRootDescriptorTable( 1, uavHeap!.GetGPUDescriptorHandleForHeapStart() );

            var shaderPipeline = shaderManager!.GetShaderPipeLine( shaderName );
            if ( shaderPipeline.ConstantBuffer != null )
            {
                commandList!.SetGraphicsRootConstantBufferView( 2, shaderPipeline.ConstantBuffer!.GPUVirtualAddress );
            }

            // Record commands to filter the captured resource
            commandList!.ResourceBarrierTransition( capturedResource!, ResourceStates.CopySource, ResourceStates.NonPixelShaderResource );
            commandList!.ResourceBarrierTransition( filteredResource!, ResourceStates.Common, ResourceStates.UnorderedAccess );


            // Calculate the thread group size and dispatch the shader
            // nvidia and intel will both dynamicaly use 8, 16, 32 wavefonts, amd will dynamically use 32, or 64 wavefronts.
            uint threadGroupSize = gpuVender == "AMD" ? 64u : 32u;
            uint groupCountX = ( uint ) ( ( ( windowRect.right - windowRect.left ) + threadGroupSize - 1 ) / threadGroupSize );


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



        /// <summary>
        /// Grabs the GPU's vendor name from the adapter description.
        /// </summary>
        private void GetGpuVendorName()
        {
            using IDXGIDevice idxgiDevice = this.device!.QueryInterface<IDXGIDevice>();
            using IDXGIAdapter adapter = idxgiDevice.GetAdapter();
            AdapterDescription adapterDesc = adapter.Description;

            gpuVender = adapterDesc.VendorId switch
            {
                0x10DE => "NVIDIA",
                0x1002 => "AMD",
                0x8086 => "Intel",
                _ => "Unknown"
            };
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
                device?.Dispose();
                shaderManager!.Dispose();
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
    internal unsafe class PixelShaderManager : IDisposable
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
            ReadShaderCode( out string shaderCode );

            using var outfits = new OutfitColorTolerances();
            using var features = new CharacterFeatureTolerances();

            // We know there will be 3 shaders currently, character features, outfits, and outlines. so this will be hardcoded for now.
            for ( int i = 0; i < 3; i++ )
            {
                string shaderName = i == 0 ? "features" : i == 1 ? "outfits" : "outlines";

                Color swapColor = i == 0 ? features.GetSwapColor() : i == 1 ? outfits.GetSwapColor() : ColorTolerances.GetSwapColor();

                List<Tuple<string, ColorTolerance[]>?> toleranceDetails = shaderName.CompareTo( "features" ) == 0
                ? features.GetCharacterFeatures()
                : shaderName.CompareTo( "outfits" ) == 0
                    ? outfits.GetOutfits()
                    : new List<Tuple<string, ColorTolerance[]>?>
                    {
                        new( "outlines", new[] { ColorTolerances.GetColorTolerance() })
                    };



                Shader tempShader = new( shaderName, shaderCode, toleranceDetails, swapColor, gpuVendor );
                ID3D12Resource constantBuffer = CreateConstantBuffer( ref tempShader );
                ID3D12PipelineState pipelineState = CreatePipelineState( ref tempShader.RefCompiledShader()! );

                shaderPipelines.Add( new ShaderPipeline( tempShader, pipelineState, constantBuffer ) );
            }
        }

        private ID3D12Resource CreateConstantBuffer( ref Shader shader )
        {

            shader.SetupColorRanges( out ColorRanges colorRanges );
            if ( colorRanges.NumOfRanges == 0 )
            {
                ErrorHandler.HandleException( new Exception( "No color ranges found." ) );
            }

            int constBufferSize = Marshal.SizeOf<ColorRanges>();

            var constantBuffer = device.CreateCommittedResource( new HeapProperties( HeapType.Upload ), HeapFlags.None,
               ResourceDescription.Buffer( ( ulong ) constBufferSize, ResourceFlags.None ), ResourceStates.GenericRead );

            Vortice.Direct3D12.Range writeRange = new( 0, 0 );
            void* mappedData = null;
            constantBuffer.Map( 0, writeRange, &mappedData ).CheckError();
            if ( mappedData == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to map constant buffer." ) );
            }

            // Copy the color ranges to the constant buffer            
            Unsafe.Copy( mappedData, ref colorRanges );

            // Unmap the constant buffer
            constantBuffer.Unmap( 0, writeRange );


            DescriptorHeapDescription cbvHeapDesc = new()
            {
                Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                DescriptorCount = 1,
                Flags = DescriptorHeapFlags.ShaderVisible
            };

            var cbvHeap = device.CreateDescriptorHeap( cbvHeapDesc );
            if ( cbvHeap == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create constant buffer descriptor heap." ) );
            }

            ConstantBufferViewDescription cbvDesc = new()
            {
                BufferLocation = constantBuffer.GPUVirtualAddress,
                SizeInBytes = ( uint ) ( ( constBufferSize + 255 ) & ~255 )
            };

            device.CreateConstantBufferView( cbvDesc, cbvHeap.GetCPUDescriptorHandleForHeapStart() );

            return constantBuffer;
        }


        /// <summary>
        /// Creates a pipeline state object using the provided compiled shader.
        /// </summary>
        /// <param name="shader">The compiled shader Blob.</param>
        /// <returns>An ID3D12PipelineState object representing the created pipeline state.</returns>
        private ID3D12PipelineState CreatePipelineState( ref Blob shader )
        {
            GraphicsPipelineStateDescription pipelineDesc = new()
            {
                RootSignature = rootSignature,
                PixelShader = shader.AsBytes().AsMemory(),
                VertexShader = ReadOnlyMemory<byte>.Empty, // Placeholder, if needed
                RenderTargetFormats = new[] { Format.R8G8B8A8_UNorm },
                SampleDescription = new SampleDescription( 1, 0 ),
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RasterizerState = RasterizerDescription.CullNone,
                BlendState = BlendDescription.Opaque,
                DepthStencilState = DepthStencilDescription.None
            };

            return device.CreateGraphicsPipelineState( pipelineDesc );
        }

        /// <summary>
        /// Sets the pipeline state for the specified shader on the provided command list.
        /// </summary>
        /// <param name="commandList">The command list on which to set the pipeline state.</param>
        /// <param name="shaderName">The name of the shader whose pipeline state is to be set.</param>
        internal void SetPipelineState( ID3D12GraphicsCommandList commandList, string shaderName )
        {
            var shaderPipeline = shaderPipelines.Find( sp => sp.Shader.ShaderName == shaderName );
            if ( shaderPipeline == null )
            {
                ErrorHandler.HandleExceptionNonExit( new ArgumentException( $"Shader '{shaderName}' not found." ) );
                return;
            }

            commandList.SetPipelineState( shaderPipeline.PipelineState );
        }

        /// <summary>
        /// Retrieves the shader pipeline associated with the given shader name.
        /// </summary>
        /// <param name="shaderName">The name of the shader whose pipeline is to be retrieved.</param>
        /// <returns>The ShaderPipeline object associated with the specified shader name.</returns>
        internal ShaderPipeline GetShaderPipeLine( string shaderName )
        {
            return shaderPipelines.First( sp => sp.Shader.ShaderName == shaderName );
        }

        /// <summary>
        /// Returns an array of all the shader names managed by the PixelShaderManager.
        /// </summary>
        /// <returns>An array of shader names.</returns>
        internal string[] GetShaderNames()
        {
            return shaderPipelines.Select( sp => sp.Shader.ShaderName ).ToArray();
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
        internal Shader Shader { get; private set; }


        /// <summary>
        /// The constant buffer associated with the shader it holds the color tolerance values.
        /// </summary>
        internal ID3D12Resource ConstantBuffer { get; private set; }

        /// <summary>
        /// The pipeline state associated with the shader.
        /// </summary>
        internal ID3D12PipelineState PipelineState { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ShaderPipeline class.
        /// </summary>
        /// <param name="shader">The name of the shader.</param>
        /// <param name="pipelineState">The pipeline state associated with the shader.</param>
        /// 
        internal ShaderPipeline( Shader shader, ID3D12PipelineState pipelineState, ID3D12Resource constantBuffer )
        {
            Shader = shader;
            PipelineState = pipelineState;
            ConstantBuffer = constantBuffer;
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
                PipelineState.Dispose();
                Shader.Dispose();
                ConstantBuffer.Dispose();
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


}
