using System.Collections.Immutable;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Debug;
using Vortice.DXGI;





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
        private ID3D11DeviceContext? d3d11Context;

        // Resources
        private ID3D11UnorderedAccessView? uav;
        private ID3D11ShaderResourceView? srv;

        // Shader resources
        string? rawShaderCode;
        private Blob? shaderBlob;
        private ID3D11ComputeShader? computeShader;

        // Buffers
        private ID3D11Texture2D? inputBuffer;
        private ID3D11Texture2D? outputBuffer;
        private ID3D11Texture2D? stagingBuffer;


        // Dxgi interface for desktop duplication 
        private IDXGIAdapter1? desktopAdapter;
        private IDXGIOutput? output;
        private IDXGIOutput1? output1;
        private IDXGIOutputDuplication? outputDuplication;

        // Debug info queue
        private ID3D11InfoQueue? infoQueue;
        private ID3D11Debug d3d11Debug;

        // Shader management
        private readonly ConstantBufferManager? bufferManager;

        // Monitor information
        private readonly uint monitorWidth;
        private readonly uint monitorHeight;





        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        internal DirectX11( ref ColorToleranceManager toleranceManager )
        {
            // Get the monitor information
            Screen? primaryScreen = Screen.PrimaryScreen;
            monitorHeight = ( uint ) primaryScreen!.Bounds.Height;
            monitorWidth = ( uint ) primaryScreen.Bounds.Width;

            // Initialize the DirectX 12 device and core components
            InitD3D12();

            // Create the Shader 
            ReadShaderCodeFromFile();
            EditShaderCodeVariables();
            CompileAndCreateShaderFromFile();

            // Create the constant buffer manager
            bufferManager = new( ref toleranceManager, ref d3d11Device! );
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

            var featureLvls = new FeatureLevel[ 5 ];

            // Set the feature levels we want to use
            featureLvls[ 0 ] = FeatureLevel.Level_11_0;
            featureLvls[ 1 ] = FeatureLevel.Level_11_1;
            featureLvls[ 2 ] = FeatureLevel.Level_12_0;
            featureLvls[ 3 ] = FeatureLevel.Level_12_1;
            featureLvls[ 4 ] = FeatureLevel.Level_12_2;


            // Updated adapter selection code
            using IDXGIFactory4 factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            // Get the right adapter to create the device we need
            for ( uint i = 0; factory.EnumAdapters1( i, out IDXGIAdapter1 tempAdapter1 ).Success; i++ )
            {
                AdapterDescription1 desc = tempAdapter1.Description1;
                gpuVender = desc.VendorId switch
                {
                    0x10DE => "NVIDIA",
                    0x1002 => "AMD",
                    0x8086 => "Intel",
                    _ => "Unknown"
                };


                if ( gpuVender == "Unknown" )
                {
                    tempAdapter1.Dispose();
                    continue;
                }

                // Skip the adapter if it is a software adapter
                if ( ( desc.Flags & AdapterFlags.Software ) != 0 )
                {
                    tempAdapter1.Dispose();
                    continue;
                }


#if DEBUG
                DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug;
#else
                DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.BgraSupport;
#endif

                if ( D3D11.D3D11CreateDevice( tempAdapter1, DriverType.Unknown, deviceCreationFlags, featureLvls, out ID3D11Device? tempDevice, out FeatureLevel selectedLvl, out ID3D11DeviceContext? tempContext ).Success )
                {
                    // Check if the adapter supports the necessary features. if any fail, the program will exit.
                    // we are able to do such a harsh check because by checking for the tempAdapter1 to be hardware, and the gpuVender to be known, we can be sure that the adapter is a hardware adapter.
                    // that means if we are here the adapter is a hardware adapter and we can check for the necessary features. if not supported this application cant run.
                    CheckFeatureSupport( ref tempDevice, ref desc, ref selectedLvl );

                    // Set the device and context
                    tempDevice!.AddRef();
                    d3d11Device = tempDevice;
                    tempContext.AddRef();
                    d3d11Context = tempContext;
                    tempAdapter1.AddRef();
                    desktopAdapter = tempAdapter1;


                    // Set the device debug name
                    d3d11Device!.DebugName = "IceDevice";

                    // Null original pointers for safety
                    tempAdapter1.Release();
                    tempDevice.Release();
                    tempContext.Release();
                    break;

                }
            }



            // Check if a suitable adapter was found
            if ( desktopAdapter == null || d3d11Device == null )
            {
                ErrorHandler.HandleException( new Exception( "No suitable Direct3D 12 adapter found." ) );
            }

#if DEBUG
            // Setup the debug layer
            SetupDebugLayer();
#endif

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

            // Get the DXGI output duplication
            outputDuplication = output1!.DuplicateOutput( d3d11Device );

            // Create the 2d textures for the input, output, and staging buffers
            inputBuffer = ErrorHandler.HandleObjCreation( d3d11Device.CreateTexture2D( CreateTextureDescription( BufferType.Input ) ), nameof( inputBuffer ) );
            outputBuffer = ErrorHandler.HandleObjCreation( d3d11Device.CreateTexture2D( CreateTextureDescription( BufferType.Output ) ), nameof( outputBuffer ) );
            stagingBuffer = ErrorHandler.HandleObjCreation( d3d11Device.CreateTexture2D( CreateTextureDescription( BufferType.Staging ) ), nameof( stagingBuffer ) );


            // Create shader resource view
            ShaderResourceViewDescription srvDesc = default;
            srvDesc.Format = Format.B8G8R8A8_UNorm;
            srvDesc.ViewDimension = ShaderResourceViewDimension.Texture2D;
            srvDesc.Texture2D = new Texture2DShaderResourceView
            {
                MipLevels = 1,
                MostDetailedMip = 0
            };

            srv = ErrorHandler.HandleObjCreation( d3d11Device.CreateShaderResourceView( inputBuffer, srvDesc ), nameof( srv ) );

            // Create unordered access view
            UnorderedAccessViewDescription uavDesc = default;
            uavDesc.Format = Format.B8G8R8A8_UNorm;
            uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2D;
            uavDesc.Texture2D = new Texture2DUnorderedAccessView
            {
                MipSlice = 0
            };

            uav = ErrorHandler.HandleObjCreation( d3d11Device.CreateUnorderedAccessView( outputBuffer, uavDesc ), nameof( uav ) );


#if DEBUG
            Logger.Log( "D3D11 Initialized" );
#endif
        }


#if DEBUG
        private void SetupDebugLayer()
        {

            // Get the debug layer interfaces
            d3d11Debug = d3d11Device!.QueryInterface<ID3D11Debug>();
            infoQueue = d3d11Device.QueryInterface<ID3D11InfoQueue>();

            // Set the debug layer options
            d3d11Debug!.ReportLiveDeviceObjects( ReportLiveDeviceObjectFlags.Detail );

            // Configure the info queue
            infoQueue!.SetBreakOnSeverity( MessageSeverity.Corruption, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Error, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Warning, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Info, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Message, true );

            infoQueue.SetBreakOnCategory( MessageCategory.ApplicationDefined, true );
            infoQueue.SetBreakOnCategory( MessageCategory.Miscellaneous, true );
            infoQueue.SetBreakOnCategory( MessageCategory.Initialization, true );
            infoQueue.SetBreakOnCategory( MessageCategory.Cleanup, true );
            infoQueue.SetBreakOnCategory( MessageCategory.Compilation, true );
            infoQueue.SetBreakOnCategory( MessageCategory.Execution, true );

            // Need this for right now to see if we can catch the error
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchBoundResourceMapped, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchindirectInvalidArgBuffer, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchindirectOffsetOverflow, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchindirectOffsetUnaligned, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchindirectUnsupported, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchThreadgroupcountOverflow, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchThreadgroupcountZero, true );
            infoQueue.SetBreakOnID( MessageId.DeviceDispatchUnsupported, true );




            infoQueue.MuteDebugOutput = false;
            infoQueue.MessageCountLimit = 1000;

            // let the debug logger run in the background till the class is disposed
            _ = LogDebug().ConfigureAwait( false );
        }


        private async Task LogDebug()
        {
            while ( !disposed )
            {
                var msgCount = infoQueue!.NumStoredMessages;
                if ( msgCount > 0 )
                {
                    for ( ulong i = 0; i < msgCount; i++ )
                    {
                        var message = infoQueue.GetMessage( i );
                        string currentTnD = DateTime.Now.ToString( "MM/dd/yyyy HH:mm:ss" );

                        string logMsg = $"\n {currentTnD} \n Severity: {message.Severity} \n ID: {message.Id} \n Category: {message.Category} \n Description: {message.Description}";
                        await File.WriteAllTextAsync( FileManager.d3d11LogFile, logMsg, Encoding.Unicode );
                    }

                    if ( infoQueue.NumStoredMessages == msgCount )
                    {
                        infoQueue.ClearStoredMessages();
                    }
                }

                await Task.Delay( 50 );
            }
        }

#endif


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
                // Get texture interface from the desktop resource
                ID3D11Texture2D? desktopSurface = ErrorHandler.HandleObjCreation( desktopResource.QueryInterfaceOrNull<ID3D11Texture2D>(), nameof( desktopSurface ) );

                try
                {
                    // Copy the d3d11 texture to the d3d11 captured resource
                    d3d11Context!.CopyResource( inputBuffer!, desktopSurface );

                    // Flush context to submit the copy command
                    d3d11Context!.Flush();

                    // Dispose the desktop surface
                    desktopSurface!.Dispose();

                } finally
                {
                    // Release the frame (release as early as possible)
                    outputDuplication!.ReleaseFrame();
                }

                // Apply filtering using all shaders managed by the shader manager
                foreach ( string shaderName in shaderManager!.GetShaderNames() )
                {
                    // if index is the last name, we pass true to the lastIteration parameter.
                    bool lastIteration = shaderManager.GetShaderNames().ToList().IndexOf( shaderName ) == shaderManager.GetShaderNames().Length - 1;

                    ApplyFilterProcess( shaderName, lastIteration );
                }

                // Copy the filtered resource to the staging buffer
                d3d11Context!.CopyResource( stagingBuffer, outputBuffer );


                // Map the filtered resource to access the filtered image data
                if ( d3d11Context.Map( stagingBuffer, 0, MapMode.Read, 0, out MappedSubresource filterImage ).Failure )
                {
                    ErrorHandler.HandleException( new Exception( "Failed to map filtered resource." ) );
                    return;
                }
                try
                {
                    // Get image details
                    var fiDesc = outputBuffer!.Description;
                    bitmap = new( ( int ) fiDesc.Width, ( int ) fiDesc.Height, fiDesc.Format == Format.R8G8B8A8_UNorm ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb );

                    // Lock bitmap for operations
                    var bmpData = bitmap.LockBits( new Rectangle( 0, 0, ( int ) monitorWidth, ( int ) monitorHeight ), ImageLockMode.WriteOnly, bitmap.PixelFormat );

                    // Get the size of the image data. this includes any padding. so its best to use the stride.
                    int copySize = bmpData.Stride * bmpData.Height;

                    // Copy the image data to the bitmap
                    Buffer.MemoryCopy( filterImage.DataPointer.ToPointer(), bmpData.Scan0.ToPointer(), copySize, copySize );

                    GC.KeepAlive( bitmap );
                    bitmap.UnlockBits( bmpData );
                } finally
                {
                    GC.KeepAlive( filterImage );
                    // Unmap the filtered resource
                    d3d11Context.Unmap( outputBuffer, 0 );
                }
            }


        }




        /// <summary>
        /// Applies the filtering process to the captured resource using the specified shader.
        /// </summary>
        /// <param name="shaderName">The name of the shader to use for filtering.</param>
        private unsafe void ApplyFilterProcess( string shaderName, bool lastIteration )
        {

#if DEBUG
            // Get the shader code for debugging purposes
            _ = shaderManager!.GetShaderPipeLine( sp => sp!.Shader!.ShaderName == shaderName )!.Shader!.ShaderCode;
            Logger.Log( $"Shader Name: {shaderName}" );
#endif

            try
            {
                // Set the shader, uav, srv, and constant buffer
                d3d11Context!.CSSetConstantBuffer( 0, shaderManager!.GetShaderPipeLine( sp => sp!.Shader!.ShaderName == shaderName )!.ConstantBuffer );
                d3d11Context.CSSetShader( shaderManager!.GetShaderPipeLine( sp => sp!.Shader!.ShaderName == shaderName )!.Shader!.CompiledShader!, null, 0 );
                d3d11Context.CSSetShaderResource( 0, srv! );
                d3d11Context.CSSetUnorderedAccessView( 0, uav! );


                // Dispatch the shader
                if ( gpuVender == "AMD" )
                {
                    d3d11Context.Dispatch( 16, 4, 1 );
                } else
                {
                    d3d11Context.Dispatch( 8, 4, 1 );
                }
            } catch ( Exception ex )
            {
                // Wait for a bit before logging the exception im hoping this lets d3d debug catch up and give us a better error message.
                Utils.Watch.SecondsSleep( 10 );

                ErrorHandler.HandleException( new Exception( $"Failed to dispatch shader: {shaderName}", ex ) );
            } finally
            {
                // Unset the shader, uav, srv, and constant buffer for the next shader
                d3d11Context!.CSUnsetUnorderedAccessView( 0 );
                d3d11Context.CSUnsetShaderResource( 0 );
                d3d11Context.CSSetShader( null, null, 0 ); //< unbind the shader i didnt see and option to UnsetShader with vortice, so we will just set it to null.
                d3d11Context.CSUnsetConstantBuffer( 0 );

                if ( !lastIteration )
                {
                    // Copy the output buffer to the input buffer for the next shader
                    d3d11Context.CopyResource( inputBuffer, outputBuffer );
                }
            }
        }


        private void CheckFeatureSupport( ref ID3D11Device? device, ref AdapterDescription1 desc, ref FeatureLevel selectedLvl )
        {
            var formatSupport2 = device!.CheckFeatureFormatSupport2( Format.R8G8B8A8_UNorm );
            var formatSupport = device.CheckFeatureFormatSupport( Format.R8G8B8A8_UNorm );

            // Check to make sure the adapter supports the necessary features for this application                  
            if ( ( formatSupport & FormatSupport.Texture2D ) != 0 )
            {
                Logger.Log( "Texture2D is supported" );
            } else
            {
                ErrorHandler.HandleException( new Exception( "Texture2D is not supported" ) );
            }

            if ( ( formatSupport & FormatSupport.ShaderLoad ) != 0 )
            {
                Logger.Log( "Shaders are supported" );
            } else
            {
                ErrorHandler.HandleException( new Exception( "Shaders are not supported" ) );
            }

            if ( ( formatSupport2 & FormatSupport2.UnorderedAccessViewTypedLoad ) != 0 )
            {
                Logger.Log( "UnorderedAccessViewTypedLoad is supported" );
            } else
            {
                ErrorHandler.HandleException( new Exception( "UnorderedAccessViewTypedLoad is not supported" ) );
            }

            if ( ( formatSupport2 & FormatSupport2.UnorderedAccessViewTypedStore ) != 0 )
            {
                Logger.Log( "UnorderedAccessViewTypedStore is supported" );
            } else
            {
                ErrorHandler.HandleException( new Exception( "UnorderedAccessViewTypedStore is not supported" ) );
            }

            // Check for shader support
            var hwOpts = device.CheckFeatureSupport<FeatureDataD3D10XHardwareOptions>( Vortice.Direct3D11.Feature.D3D10XHardwareOptions );
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
            }


            // If any of the features are not supported, this function wont return and the program will exit.
        }


        private Texture2DDescription CreateTextureDescription( BufferType bType )
        {
            Texture2DDescription text2Decep = default;
            text2Decep.Usage = bType == BufferType.Input || bType == BufferType.Output ? ResourceUsage.Default : ResourceUsage.Staging;
            text2Decep.BindFlags = bType == BufferType.Input ? BindFlags.ShaderResource : bType == BufferType.Output ? BindFlags.UnorderedAccess | BindFlags.ShaderResource : BindFlags.None;
            text2Decep.CPUAccessFlags = bType == BufferType.Input || bType == BufferType.Output ? CpuAccessFlags.None : CpuAccessFlags.Read;
            text2Decep.MiscFlags = ResourceOptionFlags.None;
            text2Decep.MipLevels = 1;
            text2Decep.ArraySize = 1;
            text2Decep.Width = monitorWidth;
            text2Decep.Height = monitorHeight;
            text2Decep.Format = Format.B8G8R8A8_UNorm;
            text2Decep.SampleDescription = new SampleDescription( 1, 0 );


            return text2Decep;
        }

        private void ReadShaderCodeFromFile()
        {
            try
            {
                rawShaderCode = File.ReadAllText( FileManager.shaderFile );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to read shader code from path: {FileManager.shaderFile}", ex ) );
            }

            if ( rawShaderCode == string.Empty )
            {
                ErrorHandler.HandleException( new Exception( $"Shader code is empty." ) );
            }
        }


        /// <summary>
        /// Compiles the shader code.
        /// </summary>
        private unsafe void CompileAndCreateShaderFromFile()
        {
#if DEBUG
            ShaderFlags flags = ShaderFlags.Debug | ShaderFlags.SkipOptimization | ShaderFlags.EnableStrictness;
#else
            ShaderFlags flags = ShaderFlags.OptimizationLevel3 | ShaderFlags.EnableStrictness;
#endif

            // Allocate memory for the shader code to get a pointer to the string.
            var ansiPtr = Marshal.StringToHGlobalAnsi( rawShaderCode! );

            // Compile the shader
            var result = Compiler.Compile(
                ansiPtr.ToPointer(),
                ( nuint ) rawShaderCode!.Length,
                string.Empty, //< use this to look for the shader in the same directory as the executable.
                null,
                null,
                "main",
                "cs_5_0", //< we use 5_0 for constant buffer support. this allows upto 15 constant buffers per shader.
                flags,
                EffectFlags.None,
                out Blob blob,
                out Blob errorBlob
            );

            // Free the memory allocated for the shader code
            Marshal.FreeHGlobal( ansiPtr );


            // Check for compilation errors
            if ( errorBlob != null || result.Failure )
            {
                string? errorMessage = Marshal.PtrToStringAnsi( errorBlob!.BufferPointer );
                errorBlob.Dispose();
                ErrorHandler.HandleException( new Exception( $"Failed to compile shader: {errorMessage}" ) );
            }

            shaderBlob = blob;

            // Create the shader
            computeShader = ErrorHandler.HandleObjCreation( d3d11Device!.CreateComputeShader( blob! ), nameof( computeShader ) );
        }

        /// <summary>
        /// Edits the shader code variables such as GPU vendor, screen width, and screen height.
        /// </summary>
        private void EditShaderCodeVariables()
        {

            if ( gpuVender == "AMD" )
            {
                if ( rawShaderCode!.Contains( "//#define AMD" ) )
                {
                    rawShaderCode = rawShaderCode.Replace( "//#define AMD", "#define AMD" );
                }
            } else
            {
                if ( !rawShaderCode!.Contains( "//#define AMD" ) )
                {
                    rawShaderCode = rawShaderCode.Replace( "#define AMD", "//#define AMD" );
                }
            }
        }

        private enum BufferType
        {
            Input,
            Output,
            Staging
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
                d3d11Context?.Dispose();
                d3d11Device?.Dispose();
                inputBuffer?.Dispose();
                outputBuffer?.Dispose();
                stagingBuffer?.Dispose();
                srv?.Dispose();
                uav?.Dispose();
                computeShader?.Dispose();
                shaderBlob?.Dispose();
                bufferManager?.Dispose();
                desktopAdapter?.Dispose();
                output?.Dispose();
                output1?.Dispose();
                outputDuplication?.Dispose();
#if DEBUG
                d3d11Debug?.Dispose();
                infoQueue?.Dispose();
#endif
            }
            disposed = true;
        }
    }






    internal class ConstantBufferManager : IDisposable
    {
        private bool disposed;

        private List<Tuple<string, List<ColorRange>, Color>> Ranges { get; set; }

        public Dictionary<string, ID3D11Buffer?> ConstantBuffers { get; private set; }



        public ConstantBufferManager( ref ColorToleranceManager colorManager, ref ID3D11Device d3d11Device )
        {
            CreateColorRanges( ref colorManager );

            CreateBuffers( ref d3d11Device );
        }

        private unsafe void CreateBuffers( ref ID3D11Device d3d11Device )
        {
            ConstantBuffers = [];

            foreach ( var colorRange in Ranges )
            {
                ColorRanges temp = new( ( uint ) colorRange.Item2.Count, [ .. colorRange.Item2 ], new Uint4( colorRange.Item3.R, colorRange.Item3.G, colorRange.Item3.B, colorRange.Item3.A ) );

                int szStruct = Marshal.SizeOf( typeof( ColorRanges ) );

                // We need to Align the size of the buffer
                int alignedSize = ( szStruct + 255 ) & ~( 255 );

                // Allocate memory for the constant buffer to initialize from
                IntPtr ptr = Marshal.AllocHGlobal( alignedSize );
                if ( ptr == IntPtr.Zero )
                {
                    ErrorHandler.HandleException( new Exception( "Failed to allocate memory for the constant buffer." ) );
                }

                // Copy the data to the allocated memory
                Marshal.StructureToPtr( temp, ptr, true );

                // Check for max uint value at the start of the struct
                if ( Marshal.ReadInt32( ptr ) != int.MaxValue )
                {
                    ErrorHandler.HandleException( new Exception( "Failed to copy color range struct to memory." ) );
                }

                //Create the constant buffer
                ID3D11Buffer? constantBuffer = ErrorHandler.HandleObjCreation( d3d11Device.CreateBuffer( new BufferDescription
                {
                    Usage = ResourceUsage.Dynamic,
                    ByteWidth = ( uint ) alignedSize,
                    BindFlags = BindFlags.ConstantBuffer,
                    CPUAccessFlags = CpuAccessFlags.None,
                    MiscFlags = ResourceOptionFlags.BufferStructured,
                    StructureByteStride = 0
                }, new SubresourceData
                {
                    DataPointer = ptr,
                    SlicePitch = 0,
                    RowPitch = 0
                } ), nameof( constantBuffer ) );

                // Add the constant buffer to the list
                ConstantBuffers.Add( colorRange.Item1, constantBuffer );

                // Free the allocated memory
                Marshal.FreeHGlobal( ptr );
            }


            if ( ConstantBuffers.Count != Ranges.Count )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create all constant buffers." ) );
            }
        }



        private void CreateColorRanges( ref ColorToleranceManager colorManager )
        {

            // Get the color ranges from the color manager
            var characterFeatureColors = colorManager.CharacterFeatures;
            var charaterOutfitColors = colorManager.OutfitColors;
            var outlineColors = colorManager.CharacterOutlines;
            ToleranceBase? nullDummy = null;

            // Initialize colorRanges
            var colorRanges = new List<Tuple<string, List<ColorRange>, Color>>();

            // Parse the character feature colors, outfit colors, and outlines
            ParseTolerances( ref characterFeatureColors, ref outlineColors, ref colorRanges );
            ParseTolerances( ref charaterOutfitColors, ref nullDummy, ref colorRanges );

            Ranges = colorRanges;
        }

        private void ParseTolerances( ref List<ToleranceBase>? tolerances, ref ToleranceBase? outlines, ref List<Tuple<string, List<ColorRange>, Color>> colorRanges )
        {
            if ( tolerances != null )
            {
                foreach ( var tolerance in tolerances )
                {
                    foreach ( var tBase in tolerance.Tolerances )
                    {
                        var name = tBase.Key;
                        var ranges = new List<ColorRange>();
                        var swap = tolerance.SwapColor;

                        foreach ( var range in tBase.Value )
                        {
                            ranges.Add( new ColorRange(
                                new Range( ( uint ) range.Red!.Minimum, ( uint ) range.Red.Maximum ),
                                new Range( ( uint ) range.Green!.Minimum, ( uint ) range.Green.Maximum ),
                                new Range( ( uint ) range.Blue!.Minimum, ( uint ) range.Blue.Maximum ) ) );
                        }

                        colorRanges.Add( new Tuple<string, List<ColorRange>, Color>( name, ranges, swap ) );
                    }
                }
            }

            if ( outlines != null )
            {
                foreach ( var tBase in outlines.Tolerances )
                {
                    var name = tBase.Key;
                    var ranges = new List<ColorRange>();
                    var swap = outlines.SwapColor;

                    foreach ( var range in tBase.Value )
                    {
                        ranges.Add( new ColorRange(
                            new Range( ( uint ) range.Red!.Minimum, ( uint ) range.Red.Maximum ),
                            new Range( ( uint ) range.Green!.Minimum, ( uint ) range.Green.Maximum ),
                            new Range( ( uint ) range.Blue!.Minimum, ( uint ) range.Blue.Maximum ) ) );
                    }

                    colorRanges.Add( new Tuple<string, List<ColorRange>, Color>( name, ranges, swap ) );
                }
            }
        }

        ~ConstantBufferManager()
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
                // Dispose managed resources
                foreach ( var kvp in ConstantBuffers.Values )
                {
                    kvp?.Dispose();
                }
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
    struct ColorRanges
    {
        public uint safetyCheck = int.MaxValue;

        public uint NumOfRanges { private set; get; }

        public Uint4 SwapColor { private set; get; }

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 12 )] // Set SizeConst based on your max range count
        public ColorRange[] Ranges;

        public ColorRanges( uint numOfRanges, ColorRange[] ranges, Uint4 swapColor )
        {

            Ranges = new ColorRange[ 12 ];
            ranges.CopyTo( Ranges, 0 );

            NumOfRanges = numOfRanges;
            SwapColor = swapColor;
        }
    }
}
