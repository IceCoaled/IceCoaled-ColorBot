using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
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
        private nint hWnd;
        private PInvoke.RECT windowRect;

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
        private ID3D12Resource? capturedResource; // Resource for captured screen data
        private ID3D12Resource? filteredResource; // Resource for storing filtered image data

        // Fence and Synchronization
        private ID3D12Fence? fence;
        private ulong fenceValue;
        private AutoResetEvent? fenceEvent;

        // Shader and Pipeline State
        private ID3D12PipelineState? pipelineState;
        private PixelShaderManager? shaderManager;

        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        /// <param name="hWnd">The window handle for capturing screen data.</param>
        /// <param name="windowRect">The dimensions of the window to capture.</param>
        /// <param name="shaderPaths">A list of paths to the shader files to use for filtering.</param>
        internal DirectX12( nint hWnd, PInvoke.RECT windowRect, List<string> shaderPaths )
        {
            this.windowRect = windowRect;
            this.hWnd = hWnd;
            InitD3D12();
            shaderManager = new PixelShaderManager( device!, rootSignature!, shaderPaths );
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
            commandQueue = device.CreateCommandQueue( new CommandQueueDescription( CommandListType.Direct ) );

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
            commandList = device.CreateCommandList<ID3D12GraphicsCommandList2>( 0, CommandListType.Direct, commandAllocator, pipelineState );

            // Create Root Signature
            RootSignatureDescription rootSignatureDesc = new( RootSignatureFlags.AllowInputAssemblerInputLayout );
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
            commandList!.Reset( commandAllocator, pipelineState );

            // Record commands to copy the back buffer to the captured resource
            CopyBackBufferToResource( backBufferIndex );

            // Apply filtering using all shaders managed by the shader manager
            foreach ( string shaderName in shaderManager!.GetShaderNames() )
            {
                ApplyFilterProcess( shaderName );
            }

            // Map the filtered resource to access the filtered image data
            void* dataPointer = null;
            filteredResource!.Map( 0, null, dataPointer );

            // Create a Bitmap from the mapped data
            int width = ( int ) filteredResource.Description.Width;
            int height = ( int ) filteredResource.Description.Height;
            int size = ( width * height ) * ( int ) filteredResource.Description.Format.GetBitsPerPixel(); // Assuming 4 bytes per pixel (RGBA)

            Bitmap bitmap = new( width, height, PixelFormat.Format32bppArgb );
            BitmapData bmpData = bitmap.LockBits( new Rectangle( 0, 0, width, height ), ImageLockMode.WriteOnly, bitmap.PixelFormat );

            Buffer.MemoryCopy( dataPointer, bmpData.Scan0.ToPointer(), size, size );

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
            commandList!.SetGraphicsRootDescriptorTable( 0, srvHeap.GetGPUDescriptorHandleForHeapStart() );
            commandList!.SetGraphicsRootDescriptorTable( 1, uavHeap.GetGPUDescriptorHandleForHeapStart() );

            // Record commands to filter the captured resource
            commandList!.ResourceBarrierTransition( capturedResource!, ResourceStates.CopySource, ResourceStates.NonPixelShaderResource );
            commandList!.ResourceBarrierTransition( filteredResource!, ResourceStates.Common, ResourceStates.UnorderedAccess );
            commandList!.Dispatch( 1, 1, 1 );
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
            if ( disposed )
            {
                return;
            }

            if ( disposing )
            {
                // Dispose managed resources
                fenceEvent?.Dispose();
                fence?.Dispose();
                pipelineState?.Dispose();
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
                shaderManager.Dispose();
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
        /// The list of paths to shader files to be compiled and managed.
        /// </summary>
        private readonly List<string> shadersPaths;

        /// <summary>
        /// The list of compiled shaders and associated pipeline states.
        /// </summary>
        private readonly List<ShaderPipeline> shaderPipelines;

        /// <summary>
        /// Initializes a new instance of the PixelShaderManager class with the specified device, root signature, and shader file paths.
        /// </summary>
        /// <param name="device">The DirectX 12 device used for resource creation.</param>
        /// <param name="rootSignature">The root signature used in the pipeline.</param>
        /// <param name="shaderFilePaths">The list of paths to shader files to be compiled and managed.</param>
        internal PixelShaderManager( ID3D12Device2 device, ID3D12RootSignature rootSignature, List<string> shaderFilePaths )
        {
            this.device = device;
            this.rootSignature = rootSignature;
            this.shadersPaths = shaderFilePaths;
            this.shaderPipelines = new List<ShaderPipeline>();
            InitShaders();
        }

        ~PixelShaderManager()
        {
            Dispose( false );
        }

        /// <summary>
        /// Initializes and compiles the shaders from the specified file paths, creating associated pipeline states.
        /// </summary>
        private void InitShaders()
        {
            foreach ( string shaderPath in shadersPaths )
            {
                try
                {
                    string shaderCode = File.ReadAllText( shaderPath );
                    Blob compiledPixelShader = CompileShader( shaderCode );
                    ID3D12PipelineState pipelineState = CreatePipelineState( compiledPixelShader );

                    shaderPipelines.Add( new ShaderPipeline( shaderPath, compiledPixelShader, pipelineState ) );
                } catch ( Exception ex )
                {
                    ErrorHandler.HandleException( new Exception( $"Failed to read or compile shader at path: {shaderPath}", ex ) );
                }
            }
        }

        /// <summary>
        /// Compiles the provided shader code into a Blob containing the compiled bytecode.
        /// </summary>
        /// <param name="shaderCode">The shader code to compile.</param>
        /// <returns>A Blob containing the compiled shader bytecode.</returns>
        private Blob CompileShader( string shaderCode )
        {
            Blob blob;
            Blob errorBlob;

            var result = Compiler.Compile(
                shaderCode,
                "placeholder",
                string.Empty,
                string.Empty,
                out blob,
                out errorBlob
            );

            if ( errorBlob != null || result.Failure )
            {
                string? errorMessage = Marshal.PtrToStringAnsi( errorBlob!.BufferPointer );
                errorBlob.Dispose();
                ErrorHandler.HandleException( new Exception( $"Failed to compile shader: {errorMessage}" ) );
            }

            return blob;
        }

        /// <summary>
        /// Creates a pipeline state object using the provided compiled shader.
        /// </summary>
        /// <param name="shader">The compiled shader Blob.</param>
        /// <returns>An ID3D12PipelineState object representing the created pipeline state.</returns>
        private ID3D12PipelineState CreatePipelineState( Blob shader )
        {
            GraphicsPipelineStateDescription pipelineDesc = new GraphicsPipelineStateDescription
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
            var shaderPipeline = shaderPipelines.FirstOrDefault( sp => sp.ShaderName == shaderName );
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
            return shaderPipelines.First( sp => sp.ShaderName == shaderName );
        }

        /// <summary>
        /// Returns an array of all the shader names managed by the PixelShaderManager.
        /// </summary>
        /// <returns>An array of shader names.</returns>
        internal string[] GetShaderNames()
        {
            return shaderPipelines.Select( sp => sp.ShaderName ).ToArray();
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
                    shaderPipeline.PipelineState.Dispose();
                    shaderPipeline.CompiledShader.Dispose();
                }
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Represents a compiled shader and its associated pipeline state.
    /// </summary>
    internal class ShaderPipeline
    {
        /// <summary>
        /// The name of the shader.
        /// </summary>
        internal string ShaderName { get; }

        /// <summary>
        /// The compiled shader Blob.
        /// </summary>
        internal Blob CompiledShader { get; }

        /// <summary>
        /// The pipeline state associated with the shader.
        /// </summary>
        internal ID3D12PipelineState PipelineState { get; }

        /// <summary>
        /// Initializes a new instance of the ShaderPipeline class.
        /// </summary>
        /// <param name="shaderName">The name of the shader.</param>
        /// <param name="compiledShader">The compiled shader Blob.</param>
        /// <param name="pipelineState">The pipeline state associated with the shader.</param>
        internal ShaderPipeline( string shaderName, Blob compiledShader, ID3D12PipelineState pipelineState )
        {
            ShaderName = shaderName;
            CompiledShader = compiledShader;
            PipelineState = pipelineState;
        }
    }

}
