using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Color = System.Drawing.Color;


namespace SCB
{
    /// <summary>
    /// Manages DirectX 12 operations, including screen capture, GPU-based filtering, and resource management.
    /// </summary>
    internal class DirectX11 : IDisposable
    {
        // Indicates whether the object has been disposed to prevent multiple disposals.
        private bool disposed;

        // Window details
        private string? gpuVender;

        // D3D12 device and core components
        private ID3D11Device? d3d11Device;
        private ID3D11Device1? d3d11Device1;
        private ID3D11DeviceContext? d3d11Context;

        // Resources
        private ID3D11UnorderedAccessView? uav;
        private ID3D11ShaderResourceView? srv;

        // Buffers
        private ID3D11Buffer? constantBuffer;
        private ID3D11Buffer? inputBuffer;
        private ID3D11Buffer? outputBuffer;


        // Dxgi interface for desktop duplication 
        private IDXGIAdapter1? desktopAdapter;
        private IDXGIOutput? output;
        private IDXGIOutput1? output1;
        private IDXGIOutputDuplication? outputDuplication;


        // Shader management
        private PixelShaderManager? shaderManager;

        // Monitor information
        private readonly uint monitorWidth;
        private readonly uint monitorHeight;





        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        /// <param name="hWnd">The window handle for capturing screen data.</param>
        /// <param name="windowRect">The dimensions of the window to capture.</param>
        /// <param name="shaderPaths">A list of paths to the shader files to use for filtering.</param>
        internal DirectX11()
        {
            // Get the monitor information
            Screen? primaryScreen = Screen.PrimaryScreen;
            monitorHeight = ( uint ) primaryScreen!.Bounds.Height;
            monitorWidth = ( uint ) primaryScreen.Bounds.Width;

            // Initialize the DirectX 12 device and core components
            InitD3D12();

            // Create the Shader Manager to manage pixel shaders, pipleline states, and resources
            shaderManager = ErrorHandler.HandleObjCreation( new PixelShaderManager( d3d11Device!, gpuVender! ), nameof( shaderManager ) );
        }




        ~DirectX11()
        {
            Dispose( false );
        }




        /// <summary>
        /// Initializes the DirectX 12 device, command queue, swap chain, descriptor heaps, and other components.
        /// </summary>
        private void InitD3D12()
        {

            // Create featurelevel array
            FeatureLevel[] featureLevel =
            [
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0
            ];

            // Updated adapter selection code
            using IDXGIFactory4 factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            // Get the right adapter to create the device we need
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

                if ( D3D11.D3D11CreateDevice( tempAdapter, DriverType.Hardware, DeviceCreationFlags.BgraSupport, featureLevel, out ID3D11Device tempDevice ).Success )
                {
                    // Check for shader support
                    var hwOpts = tempDevice!.CheckFeatureSupport<FeatureDataD3D10XHardwareOptions>( Vortice.Direct3D11.Feature.D3D10XHardwareOptions );
                    if ( hwOpts.ComputeShadersPlusRawAndStructuredBuffersViaShader4X )
                    {
                        desktopAdapter = tempAdapter;
                        d3d11Device = tempDevice;

                        // Null original pointers for safety
                        tempAdapter = null;
                        tempDevice = null;
                        break;
                    }

                } else
                {
                    tempAdapter.Dispose();
                }
            }


#if DEBUG
            Logger.Log( $"GPU Vendor: {gpuVender}" );
#endif

            // Check if a suitable adapter was found
            if ( desktopAdapter == null || d3d11Device == null )
            {
                ErrorHandler.HandleException( new Exception( "No suitable Direct3D 12 adapter found." ) );
            }

            // Get device context
            d3d11Context = d3d11Device!.ImmediateContext;


            // Get the DXGI output
            if ( desktopAdapter.EnumOutputs( 0, out output ).Failure )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get DXGI output." ) );
            }

            // Get the DXGI output1
            output1 = output.QueryInterface<IDXGIOutput1>();
            if ( output1 == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get DXGI output1." ) );
            }

            // Dispose output, we no longer need it
            output.Dispose();

            // Get the DXGI output duplication
            outputDuplication = output1!.DuplicateOutput( d3d11Device );

            // Create input buffer
            BufferDescription inputBufferDesc = default;
            inputBufferDesc.Usage = ResourceUsage.Default;
            inputBufferDesc.BindFlags = BindFlags.ShaderResource;
            inputBufferDesc.CPUAccessFlags = CpuAccessFlags.Read | CpuAccessFlags.Write;
            inputBufferDesc.MiscFlags = ResourceOptionFlags.BufferAllowRawViews;
            inputBufferDesc.StructureByteStride = sizeof( UInt32 );
            inputBufferDesc.ByteWidth = monitorWidth * monitorHeight * sizeof( UInt32 );

            inputBuffer = ErrorHandler.HandleObjCreation( d3d11Device.CreateBuffer( inputBufferDesc ), nameof( inputBuffer ) );

            // Create output buffer
            BufferDescription outputBufferDesc = default;
            outputBufferDesc.Usage = ResourceUsage.Default;
            outputBufferDesc.BindFlags = BindFlags.UnorderedAccess | BindFlags.ShaderResource;
            outputBufferDesc.CPUAccessFlags = CpuAccessFlags.Read | CpuAccessFlags.Write;
            outputBufferDesc.MiscFlags = ResourceOptionFlags.BufferAllowRawViews;
            outputBufferDesc.StructureByteStride = sizeof( UInt32 );
            outputBufferDesc.ByteWidth = monitorWidth * monitorHeight * sizeof( UInt32 );

            outputBuffer = ErrorHandler.HandleObjCreation( d3d11Device.CreateBuffer( outputBufferDesc ), nameof( outputBuffer ) );


            // Create shader resource view
            ShaderResourceViewDescription srvDesc = default;
            srvDesc.Format = Format.R8G8B8A8_UNorm;
            srvDesc.ViewDimension = ShaderResourceViewDimension.Buffer;
            srvDesc.Buffer.FirstElement = 0;
            srvDesc.Buffer.NumElements = monitorWidth * monitorHeight * sizeof( UInt32 );
            srvDesc.Buffer.ElementWidth = sizeof( UInt32 );
            srvDesc.Texture2D.MostDetailedMip = 0;
            srvDesc.Texture2D.MipLevels = 1;
            srvDesc.BufferEx.Flags = BufferExtendedShaderResourceViewFlags.None;

            srv = ErrorHandler.HandleObjCreation( d3d11Device.CreateShaderResourceView( inputBuffer, srvDesc ), nameof( srv ) );

            // Create unordered access view
            UnorderedAccessViewDescription uavDesc = default;
            uavDesc.Format = Format.R8G8B8A8_UNorm;
            uavDesc.ViewDimension = UnorderedAccessViewDimension.Buffer;
            uavDesc.Buffer.FirstElement = 0;
            uavDesc.Buffer.NumElements = monitorWidth * monitorHeight * sizeof( UInt32 );
            uavDesc.Buffer.Flags = BufferUnorderedAccessViewFlags.None;

            uav = ErrorHandler.HandleObjCreation( d3d11Device.CreateUnorderedAccessView( outputBuffer, uavDesc ), nameof( uav ) );


#if DEBUG
            Logger.Log( "D3D12 Initialized" );
#endif
        }








        /// <summary>
        /// Processes the current frame and returns the filtered result as a Bitmap.
        /// </summary>
        /// <returns>The filtered frame as a Bitmap.</returns>
        internal unsafe void ProcessFrameAsBitmap( ref Bitmap? bitmap )
        {

            // Get the next frame
            if ( outputDuplication!.AcquireNextFrame( 10, out OutduplFrameInfo frameInfo, out IDXGIResource? desktopResource ).Failure )
            {
                ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to acquire next frame." ) );
                return;
            }

            using ( desktopResource )
            {

                // desktopResource doesnt have a shared handle, so we just query for the d3d11 texture
                ID3D11Texture2D? d3d11Texture = desktopResource.QueryInterface<ID3D11Texture2D>();
                if ( d3d11Texture == null )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to get D3D11 texture." ) );
                    return;
                }

                // Verify the texture size
                var desc = d3d11Texture.Description;
                if ( desc.Width != monitorWidth || desc.Height != monitorHeight )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( "Texture size does not match monitor size." ) );
                    return;
                }


#if DEBUG
                // Map the d3d11 texture to access the texture data
                if ( d3d11Context!.Map( d3d11Texture, 0, MapMode.Read, 0, out MappedSubresource mappedResource ).Failure )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to map d3d11 texture." ) );
                    return;
                }

                // Create a Bitmap from the mapped data
                PixelFormat bitmapFormat = desc.Format.GetBitsPerPixel() == 32 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
                bitmap = new( ( int ) desc.Width, ( int ) desc.Height, bitmapFormat );
                BitmapData bmpData = bitmap.LockBits( new Rectangle( 0, 0, ( int ) desc.Width, ( int ) desc.Height ), ImageLockMode.WriteOnly, bitmap.PixelFormat );
                Buffer.MemoryCopy( mappedResource.DataPointer.ToPointer(), bmpData.Scan0.ToPointer(), bmpData.Stride * bmpData.Height, bmpData.Stride * bmpData.Height );

                bitmap.UnlockBits( bmpData );

                // Unmap the d3d11 texture
                d3d11Context!.Unmap( d3d11Texture, 0 );

                //Save the bitmap to file
                string randomNum = new Random().Next( 0, 1000 ).ToString();
                bitmap.Save( $"{Utils.FilesAndFolders.enemyScansFolder}d3d11Resource.{randomNum}.bmp" );
#endif


                try
                {
                    // Copy the d3d11 texture to the d3d11 captured resource
                    d3d11Context!.CopyResource( inputBuffer, d3d11Texture );

                    // Flush context to submit the copy command
                    d3d11Context!.Flush();


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
                if ( d3d11Context.Map( outputBuffer, 0, MapMode.Read, 0, out MappedSubresource filterImage ).Failure )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to map filtered resource." ) );
                    return;
                }
                try
                {
                    // Get image details
                    var fiDesc = outputBuffer!.Description;
                    bitmap = new( ( int ) monitorWidth, ( int ) monitorHeight, fiDesc.StructureByteStride == 4 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb );
                    bmpData = bitmap.LockBits( new Rectangle( 0, 0, ( int ) monitorWidth, ( int ) monitorHeight ), ImageLockMode.WriteOnly, bitmap.PixelFormat );
                    Buffer.MemoryCopy( dataPointer, bmpData.Scan0.ToPointer(), bmpData.Stride * bmpData.Height, bmpData.Stride * bmpData.Height );

                    bitmap.UnlockBits( bmpData );
                } finally
                {
                    // Unmap the filtered resource
                    d3d11Context.Unmap( outputBuffer, 0 );
                    dataPointer = null;
                }
            }
        }




        /// <summary>
        /// Applies the filtering process to the captured resource using the specified shader.
        /// </summary>
        /// <param name="shaderName">The name of the shader to use for filtering.</param>
        private unsafe void ApplyFilterProcess( string shaderName )
        {

            // Calculate the thread group size and dispatch the shader
            // nvidia and intel will both dynamicaly use 8, 16, 32 wavefonts, amd will dynamically use 32, or 64 wavefronts.
            uint threadGroupSize = gpuVender == "AMD" ? 64u : 32u;
            uint groupCountX = ( monitorWidth + threadGroupSize - 1 ) / threadGroupSize;

            // Set the shader, uav, srv, and constant buffer
            d3d11Context!.CSSetConstantBuffer( 0, shaderManager!.GetShaderPipeLine( sp => sp!.Shader!.ShaderName == shaderName )!.ConstantBuffer );
            d3d11Context.CSSetShader( shaderManager!.GetShaderPipeLine( sp => sp!.Shader!.ShaderName == shaderName )!.Shader!.CompiledShader, null, 0 );
            d3d11Context.CSSetShaderResources( 0, 1, [ srv! ] );
            d3d11Context.CSSetUnorderedAccessViews( 0, 1, [ uav! ] );

            // Dispatch the shader
            d3d11Context.Dispatch( groupCountX, monitorHeight, 1 );

            // Unset the shader, uav, srv, and constant buffer for the next shader
            d3d11Context.CSSetShader( null, null, 0 );
            d3d11Context.CSSetUnorderedAccessViews( 0, 0, [] );
            d3d11Context.CSSetShaderResources( 0, 0, [] );
            d3d11Context.CSSetConstantBuffers( 0, 0, null );

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
                // Dispose managed resources
                inputBuffer?.Dispose();
                outputBuffer?.Dispose();
                constantBuffer?.Dispose();
                srv?.Dispose();
                uav?.Dispose();
                shaderManager?.Dispose();
                desktopAdapter?.Dispose();
                output?.Dispose();
                output1?.Dispose();
                outputDuplication?.Dispose();
                d3d11Context?.Dispose();
                d3d11Device?.Dispose();
            }
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
        private ID3D11Device device;

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
        internal PixelShaderManager( ID3D11Device device, string gpuVendor )
        {
            this.device = device;
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
                Shader? shader = ErrorHandler.HandleObjCreation( new Shader( shaderName, shaderCode, toleranceDetails, swapColor, gpuVendor, ref device ), $"{shaderName}" + nameof( shader ) );

                // Create the constant buffer for the shader
                var constantBuffer = CreateConstantBuffer( ref shader );

                // Create the shader pipeline, and add it to the list
                shaderPipelines.Add( new ShaderPipeline( shader, constantBuffer ) );
            }
        }




        /// <summary>
        /// Creates a constant buffer for the specified shader.
        /// </summary>
        /// <param name="shader"></param>
        /// <returns>Buffer resource</returns>
        private unsafe ID3D11Buffer CreateConstantBuffer( ref Shader? shader )
        {
            // Setup the color ranges for the constant buffer
            shader!.SetupColorRanges( out ColorRanges colorRanges );
            if ( colorRanges.NumOfRanges == 0 )
            {
                ErrorHandler.HandleException( new Exception( "No color ranges found." ) );
            }

            // Get size of the color ranges struct
            int szColorRanges = Marshal.SizeOf( typeof( ColorRanges ) );
            // align the size
            szColorRanges = ( szColorRanges + 255 ) & ~255;

            // Create constant buffer
            BufferDescription constantBufferDesc = default;
            constantBufferDesc.Usage = ResourceUsage.Default;
            constantBufferDesc.BindFlags = BindFlags.ConstantBuffer;
            constantBufferDesc.CPUAccessFlags = CpuAccessFlags.Write;
            constantBufferDesc.MiscFlags = ResourceOptionFlags.None;
            constantBufferDesc.StructureByteStride = 0;
            constantBufferDesc.ByteWidth = ( uint ) szColorRanges;

            var constBuffer = ErrorHandler.HandleObjCreation( device.CreateBuffer( constantBufferDesc ), $"{shader.ShaderName} const buffer" );

            // Map the constant buffer
            if ( device!.ImmediateContext.Map( constBuffer, 0, MapMode.WriteDiscard, 0, out MappedSubresource mappedResource ).Failure )
            {
                ErrorHandler.HandleException( new Exception( "Failed to map constant buffer." ) );
            }

            // Copy the color ranges to the constant buffer
            Buffer.MemoryCopy( &colorRanges, mappedResource.DataPointer.ToPointer(), szColorRanges, szColorRanges );

            // Unmap the constant buffer
            device!.ImmediateContext.Unmap( constBuffer, 0 );

            return constBuffer!;
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
        internal ID3D11Buffer? ConstantBuffer { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ShaderPipeline class.
        /// </summary>
        /// <param name="shader">The name of the shader.</param>
        /// <param name="pipelineState">The pipeline state associated with the shader.</param>
        /// 
        internal ShaderPipeline( Shader? shader, ID3D11Buffer? constantBuffer )
        {
            Shader = shader!;
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
                Shader!.Dispose();
                ConstantBuffer!.Dispose();
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
        private Blob? compiledShaderBlob;

        ///<summary>
        /// compiled shader
        ///</summary>
        private ID3D11ComputeShader? compiledShader;

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
        internal Shader( string shaderName, string shaderCode, List<Tuple<string, ColorTolerance[]>?> colorTolerance, Color swapColor, string gpuVendor, ref ID3D11Device device )
        {
            ShaderName = shaderName;
            ShaderCode = shaderCode;
            ToleranceDetails = colorTolerance;
            SwapColor = swapColor;
            GpuVendor = gpuVendor;

            EditShaderCodeVariables();

            CompileShaderFromFile();

            CreateComputeShader( ref device! );
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
        private void CompileShaderFromFile()
        {
            var result = Compiler.Compile(
                ShaderCode!,
                "main",
                string.Empty, //< use this to look for the shader in the same directory as the executable.
                "cs_4_0", //< we can use 5_0 but we are not using any 5_0 features.
                out Blob blob,
                out Blob errorBlob
            );

            if ( errorBlob != null || result.Failure )
            {
                string? errorMessage = Marshal.PtrToStringAnsi( errorBlob!.BufferPointer );
                errorBlob.Dispose();
                ErrorHandler.HandleException( new Exception( $"Failed to compile shader: {errorMessage}" ) );
            }

            CompiledShaderBlob = blob;
        }


        unsafe void CreateComputeShader( ref ID3D11Device device )
        {
            // Create the shader
            compiledShader = ErrorHandler.HandleObjCreation( device.CreateComputeShader( compiledShaderBlob! ), nameof( compiledShader ) );
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
            Screen? primary = Screen.PrimaryScreen;
            int width = primary!.Bounds.Width;
            int height = primary.Bounds.Height;


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
        internal ref Blob? RefCompiledShaderBlob() => ref compiledShaderBlob;

        /// <summary>
        /// Gets or sets the compiled shader Blob.
        /// </summary>
        internal Blob? CompiledShaderBlob
        {
            get => compiledShaderBlob;
            set => compiledShaderBlob = value;
        }

        /// <summary>
        /// Gets a reference to the compiled shader.
        /// </summary>  
        internal ref ID3D11ComputeShader? RefCompiledShader() => ref compiledShader;

        /// <summary>
        /// Gets or sets the compiled shader.
        /// </summary>
        internal ID3D11ComputeShader? CompiledShader
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
                compiledShaderBlob?.Dispose();
                compiledShader?.Dispose();
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
