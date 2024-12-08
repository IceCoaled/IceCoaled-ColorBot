#if DEBUG
//#define RENDORDOC_DEBUG
#endif


using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;
using Color = System.Drawing.Color;




namespace SCB
{
    /// <summary>
    /// Manages DirectX 12 operations, including screen capture, GPU-based filtering, and resource management.
    /// </summary>
    internal partial class DirectX11 : IDisposable
    {
        // Indicates whether the object has been disposed to prevent multiple disposals.
        private bool disposed;

        // RendorDoc API
#if  RENDORDOC_DEBUG
        private readonly RenderDocApi? renderDocApi;
#endif

        // Window details
        private string? gpuVender;

        // D3D12 device and core components
        private IDXGIAdapter1? desktopAdapter;
        private ID3D11Device? d3d11Device;
        private ID3D11DeviceContext? d3d11Context;

        // Resources
        private ID3D11UnorderedAccessView? uav;

        // Shader resources
        private ID3D11ComputeShader? computeShader;

        // Buffers
        private ID3D11Texture2D? uaBuffer;
        private ID3D11Texture2D? stagingBuffer;


        // Window Capture
        private WindowCapture? windowCapture;


        // Debug info queue
        private ID3D11InfoQueue? infoQueue;
        private int strideMismatchCount;
        private int failedCaptureCount;
        private readonly nint lastMappedValue;
        // Shader management
        private ConstantBufferManager? BufferManager { get; set; }
        internal ManualResetEventSlim ResettingClass { get; private set; }

        // Monitor information
        private uint WindowWidth { get; set; }
        private uint WindowHeight { get; set; }

        private int BitsPerPixel { get; set; }



        /// <summary>
        /// Initializes a new instance of the DirectX12 class.
        /// </summary>
        internal DirectX11( ref ColorToleranceManager toleranceManager, [Optional] RenderDocApi rDocApi )
        {
            // Set the manual reset event, so it doesnt block anything
            ResettingClass = new();

            // Set the render doc api
#if RENDORDOC_DEBUG
            if ( rDocApi != null )
            {
                renderDocApi = rDocApi;
            }
#endif

            // Set last mapped value to zero to start
            // this ensures that the first time we map the buffer we dont get a false positive
            lastMappedValue = 0;

            // Get the monitor information
            PInvoke.RECT window = PlayerData.GetRect();
            WindowHeight = ( uint ) window.bottom - ( uint ) window.top;
            WindowWidth = ( uint ) window.right - ( uint ) window.left;

            // Get the bits per pixel
            BitsPerPixel = Screen.PrimaryScreen!.BitsPerPixel;

            // Create the window capture class
            windowCapture = new( ( int ) WindowWidth, ( int ) WindowHeight );

            // Initialize the DirectX 12 device and core components
            InitD3D12();

            // Create the constant buffer manager
            BufferManager = new( ref toleranceManager, ref d3d11Device! );

            // Create the Shader
            CompileAndCreateShaderFromFile();

            // Subscribe to the player data update event
            PlayerData.OnUpdate += OutLineColorUpdated;
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

            var featureLvls = new FeatureLevel[ 3 ];

            // Set the feature levels we want to use
            // We need a minimum of 12_0 to use the necessary features for this application.
            // 12 is fairly common on modern gpus so most people should be able to run this application.
            featureLvls[ 0 ] = FeatureLevel.Level_12_0;
            featureLvls[ 1 ] = FeatureLevel.Level_12_1;
            featureLvls[ 2 ] = FeatureLevel.Level_12_2;



            // Updated adapter selection code
            using IDXGIFactory4 factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            SharpGenException deviceException = new(); // Bullshit compiler warning, cant use variables that arent instantiated.

            // Get the right adapter to create the device we need
            for ( uint i = 0; factory.EnumAdapters1( i, out desktopAdapter ).Success; i++ )
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

                // Skip the adapter if it is a software adapter
                if ( ( desc.Flags & AdapterFlags.Software ) != 0 )
                {
                    desktopAdapter.Dispose();
                    continue;
                }


#if DEBUG
                DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.Debug;
#else
                DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.None;
#endif

                deviceException = new( D3D11.D3D11CreateDevice( desktopAdapter, DriverType.Unknown, deviceCreationFlags, featureLvls, out d3d11Device, out FeatureLevel selectedLvl, out d3d11Context ) );

                if ( deviceException.ResultCode == 0x0 )
                {
                    // Check if the adapter supports the necessary features. if any fail, the program will exit.
                    // we are able to do such a harsh check because by checking for the tempAdapter1 to be hardware, and the gpuVender to be known, we can be sure that the adapter is a hardware adapter.
                    // that means if we are here the adapter is a hardware adapter and we can check for the necessary features. if not supported this application cant run.
                    CheckFeatureSupport( ref d3d11Device, ref desc, ref selectedLvl );

                    // Set the device debug name
                    d3d11Device!.DebugName = "IceDevice";

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

            Texture2DDescription textureDesc = new()
            {
                Width = WindowWidth,
                Height = WindowHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = BitsPerPixel == 32 ? Format.B8G8R8A8_UNorm : Format.B8G8R8X8_UNorm,
                SampleDescription = new SampleDescription( 1, 0 ),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            uaBuffer = d3d11Device.CreateTexture2D( textureDesc );
            if ( uaBuffer == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create output buffer." ) );
            }

            // Create unordered access view
            var uavDesc = new UnorderedAccessViewDescription()
            {
                Format = textureDesc.Format,
                ViewDimension = UnorderedAccessViewDimension.Texture2D,
                Texture2D = new Texture2DUnorderedAccessView()
                {
                    MipSlice = 0
                }
            };

            uav = d3d11Device.CreateUnorderedAccessView( uaBuffer, uavDesc );
            if ( uav == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create unordered access view." ) );
            }

            // Create the staging buffer
            textureDesc.BindFlags = BindFlags.None;
            textureDesc.CPUAccessFlags = CpuAccessFlags.Read | CpuAccessFlags.Write;
            textureDesc.Usage = ResourceUsage.Staging;
            textureDesc.MiscFlags = ResourceOptionFlags.None;

            stagingBuffer = d3d11Device.CreateTexture2D( textureDesc );
            if ( stagingBuffer == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to create staging buffer." ) );
            }


#if DEBUG
            Logger.Log( "D3D11 Initialized" );
        }
#endif



#if DEBUG
        private void SetupDebugLayer()
        {

            // Get the debug layer interfaces
            using var d3d11Debug = d3d11Device!.QueryInterface<ID3D11Debug>();
            if ( d3d11Debug == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get D3D11 Debug Interface." ) );
            }
            infoQueue = d3d11Device.QueryInterface<ID3D11InfoQueue>();
            if ( infoQueue == null )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get D3D11 Info Queue." ) );
            }

            // Enable the debug layer
            infoQueue.MuteDebugOutput = false;
            infoQueue.MessageCountLimit = 1000;

            // Set the debug layer options
            d3d11Debug!.ReportLiveDeviceObjects( ReportLiveDeviceObjectFlags.Detail );

            // Configure the info queue
            infoQueue!.SetBreakOnSeverity( MessageSeverity.Corruption, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Error, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Warning, true );
            infoQueue.SetBreakOnSeverity( MessageSeverity.Message, true );


            // let the debug logger run in the background till the class is disposed
            Task.Run( async () => await LogDebug() );
        }


        private async Task LogDebug()
        {
            while ( !disposed )
            {
                while ( ResettingClass.Wait( 100 ) )
                {
                    await Task.Yield();
                }

                try
                {
                    ulong msgCount = infoQueue!.NumStoredMessages;

                    if ( msgCount > 0 )
                    {
                        for ( ulong i = 0; i < msgCount; i++ )
                        {
                            var message = infoQueue!.GetMessage( i );
                            string currentTnD = DateTime.Now.ToString( "MM/dd/yyyy HH:mm:ss" );

                            string logMsg = $"\n\n {currentTnD} \n Severity: {message.Severity} \n ID: {message.Id} \n Category: {message.Category} \n Description: {message.Description}";
                            await File.AppendAllTextAsync( FileManager.d3d11LogFile, logMsg, Encoding.Unicode ); // Not sure why the fuck its requiring the use of unicode but whatever.
                        }

                        if ( infoQueue!.NumStoredMessages == msgCount )
                        {
                            infoQueue.ClearStoredMessages();
                        }
                    }
                } catch ( SharpGenException? ex )
                {
                    // If the exception is a null exception we will yield as the class is resetting
                    if ( ex.InnerException!.InnerException is NullReferenceException )
                    {
                        while ( ResettingClass.Wait( 100 ) )
                        {
                            await Task.Yield();
                        }
                    }
                }
                await Task.Delay( 10 );
            }
        }

#endif

        /// <summary>
        /// Processes the current frame and returns the filtered result as a Bitmap.
        /// </summary>
        /// <returns>The filtered frame as a Bitmap.</returns>
        internal unsafe void ProcessFrameAsBitmap( ref Bitmap? bitmap )
        {
            // check if device was removed
            if ( d3d11Device!.DeviceRemovedReason.Failure )
            {
                ErrorHandler.HandleExceptionNonExit( new Exception( $"Device removed reason: {d3d11Device!.DeviceRemovedReason}" ) );
                // Reset the class
                ResetDx11();
                return;
            }

            try
            {
                // Map Staging buffer
                d3d11Context!.Map( stagingBuffer!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None, out MappedSubresource mappedResource );

                // Capture window and copy to the mapped resource
                windowCapture!.CaptureWindow( ref mappedResource.DataPointer );

                // Check if the capture failed
                if ( Marshal.ReadInt64( mappedResource.DataPointer ) == mappedResource.DataPointer )
                {
                    failedCaptureCount++;
                    if ( failedCaptureCount > 50 )
                    {
                        ErrorHandler.HandleException( new Exception( $"Failed to capture window: {failedCaptureCount}, exceeded limit of 50" ) );
                    } else
                    {
                        ErrorHandler.HandleExceptionNonExit( new Exception( "Failed to capture window." ) );
                    }
                }

                // Unmap the staging buffer
                d3d11Context.Unmap( stagingBuffer, 0 );
            } finally
            {
                // Copy the d3d11 texture to the d3d11 captured resource
                d3d11Context!.CopyResource( uaBuffer!, stagingBuffer );

                // Flush context to submit the copy command
                d3d11Context!.Flush();

                // Apply the filtering process via our shader and uav buffer
                ApplyFilterProcess();

                // Copy the filtered resource to the staging buffer
                d3d11Context!.CopyResource( stagingBuffer, uaBuffer );

                // Flush context to submit the copy command
                d3d11Context!.Flush();
            }

            // Map the filtered resource to access the filtered image data
            SharpGenException dupException = new( d3d11Context.Map( stagingBuffer, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out MappedSubresource filterImage ) );

            if ( dupException.ResultCode != 0x0 )
            {
                ErrorHandler.HandleExceptionNonExit( dupException );
                return;
            }

            try
            {
                // if the bitmap is null, create a new one
                bitmap ??= new( ( int ) WindowWidth, ( int ) WindowHeight, BitsPerPixel == 32 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb );

                // Lock bitmap for operations
                var bmpData = bitmap.LockBits( new Rectangle( 0, 0, ( int ) WindowWidth, ( int ) WindowHeight ), ImageLockMode.WriteOnly, bitmap.PixelFormat );

                // Check if the row pitch matches the stride count
                if ( filterImage.RowPitch != bmpData.Stride )
                {
                    // This should never happen, but just in case.
                    strideMismatchCount++;

                    if ( strideMismatchCount > 50 )
                    {
                        ErrorHandler.HandleException( new Exception( $"Row pitch does not match stride count: {strideMismatchCount}, exceeded limit of 50 " ) );
                    } else
                    {
                        ErrorHandler.HandleExceptionNonExit( new Exception( "Row pitch does not match stride." ) );
                    }
                }

                // Get the size of the image data. this includes any padding. so its best to use the stride.
                int copySize = bmpData.Stride * bmpData.Height;

                // Copy the image data to the bitmap
                Buffer.MemoryCopy( filterImage.DataPointer.ToPointer(), bmpData.Scan0.ToPointer(), copySize, copySize );

                bitmap.UnlockBits( bmpData );
            } finally
            {
                // Unmap the filtered resource
                d3d11Context.Unmap( stagingBuffer, 0 );

                // Flush context to submit the unmap command
                d3d11Context.Flush();
            }
        }




        /// <summary>
        /// Applies the filtering process to the captured resource using the specified shader.
        /// </summary>
        private unsafe void ApplyFilterProcess()
        {
#if RENDORDOC_DEBUG
            renderDocApi?.StartFrameCapture( d3d11Device!.NativePointer, IntPtr.Zero );
#endif
            try
            {
                // Set the shader, uav, and constant buffer
                d3d11Context!.CSSetShader( computeShader, null, 0 );
                d3d11Context.CSSetUnorderedAccessViews( 0, [ uav!, BufferManager!.ColorBufferView!, BufferManager.DetectedPlayersView! ] );

                // Dispatch the shader
                d3d11Context.Dispatch( gpuVender == "AMD" ? ( WindowWidth / 64u ) : ( WindowWidth / 32u ), ( WindowHeight / 1u ), 1u );
            } catch ( SharpGenException ex )
            {
                if ( d3d11Device!.DeviceRemovedReason.Failure )
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( $"Device removed reason: {d3d11Device!.DeviceRemovedReason}", ex ) );

                    // Reset the class
                    ResetDx11();
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new Exception( $"Failed to dispatch shader", ex ) );
                }
            } finally
            {
                // Unset the shader, uav, srv, and constant buffer for the next shader
                d3d11Context!.CSUnsetUnorderedAccessViews( 0, 3 );
                d3d11Context.CSSetShader( null, null, 0 ); //< unbind the shader i didnt see and option to UnsetShader with vortice, so we will just set it to null.

#if RENDORDOC_DEBUG
                // Flush commands for debugging
                d3d11Context.Flush();

                renderDocApi?.EndFrameCapture( d3d11Device!.NativePointer, IntPtr.Zero );
#endif
            }
        }



        /// <summary>
        /// Checks if the adapter supports the necessary features for this application.
        /// </summary>
        /// <param name="device">Id3d11Device</param>
        /// <param name="desc">Apadper description for debug print</param>
        /// <param name="selectedLvl">d3d11 feature level selcted by the adapter, this is for debug print</param>
        private void CheckFeatureSupport( ref ID3D11Device? device, ref AdapterDescription1 desc, ref FeatureLevel selectedLvl )
        {
            var formatSupport2 = device!.CheckFeatureFormatSupport2( Format.B8G8R8A8_UNorm );
            var formatSupport = device.CheckFeatureFormatSupport( Format.B8G8R8A8_UNorm );

            // Check to make sure the adapter supports the necessary features for this application                  
            if ( ( formatSupport & FormatSupport.Texture2D ) != 0 )
            {
#if DEBUG
                Logger.Log( "Texture2D is supported" );
#endif
            } else
            {
                ErrorHandler.HandleException( new Exception( "Texture2D is not supported" ) );
            }

            if ( ( formatSupport & FormatSupport.ShaderLoad ) != 0 )
            {
#if DEBUG
                Logger.Log( "Shaders are supported" );
#endif
            } else
            {
                ErrorHandler.HandleException( new Exception( "Shaders are not supported" ) );
            }

            if ( ( formatSupport2 & FormatSupport2.UnorderedAccessViewTypedLoad ) != 0 )
            {
#if DEBUG
                Logger.Log( "UnorderedAccessViewTypedLoad is supported" );
#endif
            } else
            {
                ErrorHandler.HandleException( new Exception( "UnorderedAccessViewTypedLoad is not supported" ) );
            }

            if ( ( formatSupport2 & FormatSupport2.UnorderedAccessViewTypedStore ) != 0 )
            {
#if DEBUG
                Logger.Log( "UnorderedAccessViewTypedStore is supported" );
#endif
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


        /// <summary>
        /// Compiles the shader from a hlsl file, then creates the shader.
        /// </summary>
        private unsafe void CompileAndCreateShaderFromFile()
        {
            // Edit the variables and rewrite the file if needed.
            EditShaderCodeVariables();

#if DEBUG
            var compileFlags = ShaderFlags.Debug | ShaderFlags.SkipOptimization | ShaderFlags.EnableStrictness;
#else
            var compileFlags = ShaderFlags.OptimizationLevel3 | ShaderFlags.EnableStrictness
#endif

            // Read the shader code from the file
            string rawShaderCode = File.ReadAllText( FileManager.shaderFile );
            IntPtr ansiiShaderCode = Marshal.StringToHGlobalAnsi( rawShaderCode );

            // Compile the shader
            SharpGenException? compileException = new
            (
            Compiler.Compile(
            ansiiShaderCode.ToPointer(),
            ( nuint ) rawShaderCode.Length,
            string.Empty,
            null,
            null,
            "main",
            "cs_5_0",
            compileFlags,
            EffectFlags.None,
            out Blob? shaderBlob,
            out Blob? errorBlob ) );

            // Check if the shader compiled successfully
            if ( shaderBlob == null ||
                compileException.ResultCode != 0x0 )
            {
                string? error = Marshal.PtrToStringAnsi( errorBlob!.BufferPointer );
                if ( error != null )
                {
                    //Add error indicator to the start of the error message
                    error = compileException.Message + error.Aggregate( " , Blob Error: ", ( acc, c ) => acc + c );

                    // Create a new exception with the error message
                    // This will still have all the same information as the compile exception, just with the added blob error.
                    compileException = new( error, compileException );
                    goto Cleanup;
                }
            }

            // Free the shader code memory
            Marshal.FreeHGlobal( ansiiShaderCode );


            // Create the shader
            computeShader = d3d11Device!.CreateComputeShader( shaderBlob!.BufferPointer.ToPointer(), shaderBlob!.BufferSize, null );
            if ( computeShader == null )
            {
                compileException = new( "Failed to create compute shader." );
                goto Cleanup;
            }

Cleanup:
// Dispose the shader and error blobs
            shaderBlob?.Dispose();
            errorBlob?.Dispose();

            if ( compileException.ResultCode != 0x0 )
            {
                ErrorHandler.HandleException( compileException );
            }
        }

        /// <summary>
        /// Edits the shader code variables such as GPU vendor, screen width, and screen height, byte width, and number of color ranges.
        /// </summary>
        private void EditShaderCodeVariables()
        {
            string rawShaderCode = " ";

            try
            {
                rawShaderCode = File.ReadAllText( FileManager.shaderFile );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to read shader code from path: {FileManager.shaderFile}", ex ) );
            }

            SetDispatchXGroupSize( gpuVender == "AMD" ? 64 : 32, ref rawShaderCode );
            SetNumColorRanges( BufferManager!.NumOfRanges, ref rawShaderCode );

#if !DEBUG
            UnCommentDebug( ref rawShaderCode );
#endif

            // Compare raw shader code to the shader file to see if its changed
            if ( rawShaderCode != File.ReadAllText( FileManager.shaderFile ) )
            {
                File.WriteAllText( FileManager.shaderFile, rawShaderCode );
            }
        }


        private void SetDispatchXGroupSize( int threadGroupsize, ref string shaderCode )
        {
            string pattern = @"#define\s*X_THREADGROUP\s*((int)\d+)";
            shaderCode = Regex.Replace( shaderCode, pattern, $"#define X_THREADGROUP ((int){threadGroupsize})" );
        }

        private void SetNumColorRanges( int numColorRanges, ref string shaderCode )
        {
            string pattern = @"#define\s*NUM_COLOR_RANGES\s*\d+";
            shaderCode = Regex.Replace( shaderCode, pattern, $"#define NUM_COLOR_RANGES {numColorRanges}" );
        }

        private void UnCommentDebug( ref string shaderCode )
        {
            string pattern = @"#define\s*DEBUG\s*";
            shaderCode = Regex.Replace( shaderCode, pattern, "//#define DEBUG" );
        }


        private void OutLineColorUpdated( object sender, PlayerUpdateCallbackEventArgs e )
        {
            if ( e.Key == UpdateType.OutlineColor ||
                e.Key == UpdateType.Hwnd ||
                e.Key == UpdateType.WindowRect )
            {
                // notify outside the class that we are resetting the class
                ResettingClass.Reset();

                // Reset the class
                ResetDx11();
            }
        }


        /// <summary>
        /// Resets the DirectX 11 class in the case that the user changes the outline color; the windows size or window handle changes.
        /// we do this as its just easier and faster to reset the class than updating the structured buffer.
        /// This also helps to keep our resources clean and not have to worry about memory leaks.
        /// </summary>
        private void ResetDx11()
        {
            ColorToleranceManager? colorManager = BufferManager!.ColorManager ?? null;

            // Clean up the resources
            Cleanup();

            // Get window information just in case it changed
            PInvoke.RECT window = PlayerData.GetRect();
            WindowHeight = ( uint ) window.bottom - ( uint ) window.top;
            WindowWidth = ( uint ) window.right - ( uint ) window.left;

            // Create the window capture class
            windowCapture = new( ( int ) WindowWidth, ( int ) WindowHeight );

            // Reset the class
            InitD3D12();

            // Create the constant buffer manager
            BufferManager = new( ref colorManager!, ref d3d11Device! );

            // Create the Shader
            CompileAndCreateShaderFromFile();
        }

        /// <summary>
        /// Cleans up the resources and resets the class.
        /// </summary>
        private void Cleanup()
        {
            // We dont fully dispose the class here, we just clean up the resources and reset the class.
            // Other wise this classes pointer would be junk and we would have to recreate the class.
            PlayerData.OnUpdate -= OutLineColorUpdated;

            windowCapture?.Dispose();
            BufferManager?.Dispose();
            d3d11Context?.Dispose();
            d3d11Device?.Dispose();
            uaBuffer?.Dispose();
            stagingBuffer?.Dispose();
            uav?.Dispose();
            computeShader?.Dispose();
            desktopAdapter?.Dispose();
#if DEBUG
            infoQueue?.Dispose();
#endif
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

                PlayerData.OnUpdate -= OutLineColorUpdated;

                // Dispose managed resources
                ResettingClass?.Dispose();
                BufferManager?.Dispose(); //< dispose this first to release the device pointer
                d3d11Context?.Dispose();
                d3d11Device?.Dispose();
                uaBuffer?.Dispose();
                stagingBuffer?.Dispose();
                uav?.Dispose();
                computeShader?.Dispose();
                desktopAdapter?.Dispose();
                windowCapture?.Dispose();

#if DEBUG
                infoQueue?.Dispose();
#endif
            }
            disposed = true;
        }



    }



    internal partial class ConstantBufferManager : IDisposable
    {
        private bool disposed;

        internal ColorToleranceManager? ColorManager { get; set; }

        private ID3D11Device? D3D11Device { get; set; }

        private List<Tuple<string, List<ColorRange>, Color>>? Ranges { get; set; }

        internal ID3D11Buffer? ColorBuffer { get; private set; }

        internal ID3D11Buffer? DetectedPlayersBuffer { get; private set; }

        internal ID3D11UnorderedAccessView? ColorBufferView { get; private set; }

        internal ID3D11UnorderedAccessView? DetectedPlayersView { get; private set; }

        internal int NumOfRanges => Ranges!.Count;


        /// <summary>
        /// Initializes a new instance of the ConstantBufferManager class.
        /// </summary>
        /// <param name="colorManager">Color tolerance manager</param>
        /// <param name="d3d11Device">d3d11 device</param>
        internal ConstantBufferManager( ref ColorToleranceManager colorManager, ref ID3D11Device d3d11Device )
        {
            // Set the color manager and d3d11 device
            ColorManager = colorManager;
            D3D11Device = d3d11Device;

            // Create the color range buffer and view
            CreateColorRanges( ref colorManager );

            // Create the detected players buffer and view
            CreateBuffer( BufferType.DetectedPlayers );
            SetupUAViewForBuffer( BufferType.DetectedPlayers );
        }


        ~ConstantBufferManager()
        {
            Dispose( false );
        }


        /// <summary>
        /// Serialize structures to memory.
        /// </summary>
        /// <param name="numOfStructs">Number of structures to serialize</param>
        /// <param name="structFactory">Local func to intialize the struct to be serialized</param>
        /// <param name="allocHeader">Pointer to original allocation</param>
        /// <param name="allocSize">Total size of allocation</param>
        /// <param name="strideSize">Structured stride size</param>
        private unsafe void SerializeStructure<T>( int numOfStructs, Func<int, T> structFactory, out IntPtr allocHeader, out int allocSize, out int strideSize ) where T : struct
        {
            strideSize = Marshal.SizeOf<T>();
            allocSize = strideSize * numOfStructs;
            IntPtr structPtr;
            allocHeader = structPtr = Marshal.AllocHGlobal( allocSize );

            // Add structures to the memory
            for ( int i = 0; i < numOfStructs; i++ )
            {
                // Create a struct to be serialized        
                T temp = structFactory( i );

                // Serialize the struct to the memory
                Marshal.StructureToPtr( temp, structPtr, false );

                // Check the safety check value is correct
                nint safetyCheck = structPtr + Marshal.OffsetOf<T>( "SafetyCheck" ).ToInt32();
                if ( Marshal.ReadInt32( safetyCheck ) != int.MaxValue )
                {
                    Marshal.FreeHGlobal( allocHeader );
                    ErrorHandler.HandleException( new Exception( $"Failed to serialize: {nameof( T )} " ) );
                }
                // Move the pointer to the next struct
                structPtr += strideSize;
            }
        }


        /// <summary>
        /// Creates the UA buffer for the color ranges.
        /// </summary>
        private unsafe void CreateBuffer( BufferType bufferType )
        {
            int szAlloc = 0;
            int szStride = 0;
            IntPtr allocHeader = IntPtr.Zero; //< Fucking dumb ass compiler again, cant use variables that arent instantiated.

            switch ( bufferType )
            {
                case BufferType.ColorRanges:
                {
                    ColorRanges factory( int i )
                    {
                        return new ColorRanges( ( uint ) Ranges![ i ].Item2.Count,
                            [ .. Ranges[ i ].Item2 ], new Float4( Ranges[ i ].Item3.B, Ranges[ i ].Item3.G, Ranges[ i ].Item3.R, Ranges[ i ].Item3.A ),
                            Ranges[ i ].Item1 );
                    }
                    SerializeStructure( NumOfRanges, factory, out allocHeader, out szAlloc, out szStride );
                }
                break;
                case BufferType.DetectedPlayers:
                {
                    DetectedPlayers factory( int _ )
                    {
                        return new DetectedPlayers(); //< We only need 1 struct for this buffer
                    }
                    SerializeStructure( 1, factory, out allocHeader, out szAlloc, out szStride );
                }
                break;
                default:
                ErrorHandler.HandleException( new Exception( "Buffer type to serialize not detected" ) );
                return;
            }

            // Check if the allocation header is null
            if ( allocHeader == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new Exception( "Failed to serialize buffer." ) );
            }

            // Create the buffer description
            BufferDescription bufferDesc = new()
            {
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
                CPUAccessFlags = bufferType == BufferType.ColorRanges ? CpuAccessFlags.None : CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.BufferStructured,
                StructureByteStride = ( uint ) szStride,
                ByteWidth = ( uint ) szAlloc,
            };

            // Set the subresource data
            SubresourceData subRData = new( allocHeader.ToPointer() );

            // Create the buffer
            ID3D11Buffer? buffer = bufferType switch
            {
                BufferType.ColorRanges => ColorBuffer = D3D11Device!.CreateBuffer( bufferDesc, subRData ),
                BufferType.DetectedPlayers => DetectedPlayersBuffer = D3D11Device!.CreateBuffer( bufferDesc, subRData ),
                _ => null
            };

            if ( buffer == null )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to create buffer: {bufferType}" ) );
            }

            // Free the memory
            Marshal.FreeHGlobal( allocHeader );
        }


        /// <summary>
        /// Setup the unordered access view for the buffer.
        /// </summary>
        private void SetupUAViewForBuffer( BufferType bufferType )
        {
            var description = new UnorderedAccessViewDescription
            {
                Format = Format.Unknown,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Buffer = new BufferUnorderedAccessView()
                {
                    FirstElement = 0,
                    NumElements = bufferType == BufferType.ColorRanges ? ( uint ) NumOfRanges : 1U,
                    Flags = BufferUnorderedAccessViewFlags.None
                }
            };

            ID3D11UnorderedAccessView? uav = bufferType switch
            {
                BufferType.ColorRanges => ColorBufferView = D3D11Device!.CreateUnorderedAccessView( ColorBuffer, description ),
                BufferType.DetectedPlayers => DetectedPlayersView = D3D11Device!.CreateUnorderedAccessView( DetectedPlayersBuffer, description ),
                _ => null
            };

            if ( uav == null )
            {
                ErrorHandler.HandleException( new Exception( $"Failed to create unordered access view: {bufferType}" ) );
            }
        }


        /// <summary>
        /// Setup for character feature colors, outfit colors.
        /// </summary>
        /// <param name="colorManager"></param>
        private void CreateColorRanges( ref ColorToleranceManager colorManager )
        {

            // Get the color ranges from the color manager
            var characterFeatureColors = colorManager.CharacterFeatures;

            // Initialize colorRanges
            Ranges = [];

            // Parse the character feature colors, outfit colors, and outlines
            ParseTolerances( ref characterFeatureColors );
            OutlineColor();
        }



        /// <summary>
        /// Parses the color tolerances from the color manager.
        /// </summary>
        /// <param name="tolerances"></param>
        private void ParseTolerances( ref List<ToleranceBase>? tolerances )
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

                        Ranges!.Add( new Tuple<string, List<ColorRange>, Color>( name, ranges, swap ) );
                    }
                }
            }
        }

        /// <summary>
        /// Setup for the outline color.
        /// </summary>
        private void OutlineColor()
        {
            var selected = ColorManager!.CharacterOutlines.GetSelected();
            var outline = ColorManager.CharacterOutlines.GetColorTolerance( selected );
            var swap = ColorManager.CharacterOutlines.SwapColor;

            var Range = new List<ColorRange>
            {
                new(
                new Range( ( uint ) outline.Red!.Minimum, ( uint ) outline.Red.Maximum ),
                new Range( ( uint ) outline.Green!.Minimum, ( uint ) outline.Green.Maximum ),
                new Range( ( uint ) outline.Blue!.Minimum, ( uint ) outline.Blue.Maximum ) )
            };


            // Add the current outline color to the ranges
            Ranges!.Add( new Tuple<string, List<ColorRange>, Color>( selected, Range, swap ) );

            // Create the buffer and view
            CreateBuffer( BufferType.ColorRanges );
            SetupUAViewForBuffer( BufferType.ColorRanges );
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
                ColorBuffer?.Dispose();
                DetectedPlayersBuffer?.Dispose();
                ColorBufferView?.Dispose();
                DetectedPlayersView?.Dispose();
                D3D11Device?.Release();
            }
            disposed = true;
        }

        internal enum BufferType
        {
            ColorRanges,
            DetectedPlayers,
        }
    }




    /// <summary>
    /// These structs are used for setting up buffers for the shaders.
    /// </summary>
    /// 

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct Int2( int x, int y )
    {
        public int X = x;
        public int Y = y;
    }


    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct Float4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Float4( float x, float y, float z, float w )
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Float4( uint x, uint y, uint z, uint w )
        {
            X = x / 255.0f;
            Y = y / 255.0f;
            Z = z / 255.0f;
            W = w / 255.0f;
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct Range
    {
        public float Minimum;
        public float Maximum;


        public Range( float min, float max )
        {
            Minimum = min;
            Maximum = max;
        }

        public Range( uint min, uint max )
        {
            Minimum = min / 255.0f;
            Maximum = max / 255.0f;
        }
    }


    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct ColorRange( Range redRange, Range greenRange, Range blueRange )
    {
        public Range RedRange = redRange;
        public Range GreenRange = greenRange;
        public Range BlueRange = blueRange;
    }

    [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4 )]
    struct ColorRanges
    {
        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 12 )]
        public ColorRange[] Ranges;

        public uint NumOfRanges;

        public Float4 SwapColor;

        public readonly int SafetyCheck = int.MaxValue;

        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 6 )]
        public char[] Name;

        public ColorRanges( uint numOfRanges, ColorRange[] ranges, Float4 swapColor, string name )
        {
            // Set the ranges
            Ranges = new ColorRange[ 12 ];
            Array.Copy( ranges, Ranges, Math.Min( ranges.Length, 12 ) );

            // Set the name
            Name = new char[ 6 ];
            Array.Copy( name.PadRight( 6 ).ToCharArray(), Name, Math.Min( name.Length, 6 ) );

            // Set the number of ranges
            NumOfRanges = numOfRanges;
            SwapColor = swapColor;
        }
    }


    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct PlayerPosition
    {
        public Int2 HeadPosition;
        public Int2 TorsoPosition;
        public UInt4 BoundingBox;
    }



    [StructLayout( LayoutKind.Sequential, Pack = 4 )]
    struct DetectedPlayers
    {
        [MarshalAs( UnmanagedType.ByValArray, SizeConst = 6 )] //< change for your max player count
        public PlayerPosition[] PlayerPositions;

        public int DetectedPlayerCount;

        public readonly int SafetyCheck = int.MaxValue;

        /// <summary>
        /// Regular constructor for the detected players struct. 
        /// We wont be using this constructor, but its here for reference.
        /// </summary>
        public DetectedPlayers( int playerCount, PlayerPosition[] playerPositions )
        {
            DetectedPlayerCount = playerCount;
            PlayerPositions = new PlayerPosition[ 6 ];
            Array.Copy( playerPositions, PlayerPositions, Math.Min( playerPositions.Length, 6 ) );
        }


        /// <summary>
        /// Constructor to instantiate the values so we can fill out our shader buffer  
        /// </summary>
        public DetectedPlayers()
        {
            DetectedPlayerCount = 0;
            PlayerPositions = new PlayerPosition[ 6 ];

            // Set all the player position variables to 0
            for ( int i = 0; i < 6; i++ )
            {
                PlayerPositions[ i ].HeadPosition = new Int2( 0, 0 );
                PlayerPositions[ i ].TorsoPosition = new Int2( 0, 0 );
                PlayerPositions[ i ].BoundingBox = new UInt4( 0, 0, 0, 0 );
            }
        }
    }

}
