#if DEBUG
//#define RENDORDOC_DEBUG
//#define CAPTURE_DEBUG
//#define DEBUG_BUFFER
#if !RENDORDOC_DEBUG
#endif
#endif

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SCB.Atomics;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using static SCB.DirectX.ShaderManager;
using static SCB.DirectX.UavBufferManager;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace SCB
{
    internal partial class DirectX11
    {
        /// <summary>
        /// New frame middle man 
        /// This means we can use the signal to get the frames more efficently 
        /// </summary>
        private NullableAtomicThreadSignal<Direct3D11CaptureFrame?>? NewFrame;

        /// <summary>
        /// For counting dropped frames if the count exceeds 50 we throw a full exception that closes app
        ///<function cref="ProcessNewFrame"></function>
        /// </summary>
        private int FrameDropCount = 0;

        /// <summary>
        /// Processes a new frame from the capture session
        /// Ensures the frame is copied to the staging buffer and handled appropriately
        /// </summary>
        private int ProcessNewFrame( out double capTime )
        {
            int frameDroppedSignal = -1;
            capTime = -10.000;
            Direct3D11CaptureFrame? newFrame = null;
            nint nativeSurface = nint.Zero;
            IDirect3DSurface? IDSurface = null;
            try
            {
                // Get new frame from frame pool 
                newFrame = NewFrame?.GetValue() ?? throw new InvalidOperationException( "Failed to get new frame" );

                // Get IDirect3DSurface ABI, this is whats wrapping the IDXGISurface interface
                IDSurface = newFrame?.Surface.As<IDirect3DSurface>() ?? throw new COMException( "Failed to get ABI for surface" );

                // Get our interop interface for communicating between WinRT and standard Com-Objects 
                IDirect3DDxgiInterfaceAccess dxgiInterfaceAccess = IDSurface.As<IDirect3DDxgiInterfaceAccess>() ?? throw new COMException( "Failed to get idxgi access interface" );

                // Get the native IDXGISurface interface pointer
                dxgiInterfaceAccess.GetInterface( typeof( IDXGISurface ).GUID, out nativeSurface );
                if ( nativeSurface == nint.Zero )
                {
                    throw new COMException( "Failed to get idxgi surface interface" );
                }

                // Query the IDXGISurface for ID3D11Texture2D interface
                using var texture2D = ( ( IDXGISurface ) nativeSurface ).QueryInterfaceOrNull<ID3D11Texture2D>() ?? throw new COMException( "Failed to query 2d texture interface" );

                // Copy texture contents to staging buffer
                d3d11Context!.CopyResource( stagingBuffer, texture2D );
                // Wait for copy
                WaitForFence();

            } catch ( Exception ex )
            {
                if ( FrameDropCount > 200 )
                {
                    // If too many frames are dropped, handle as a critical exception
                    ErrorHandler.HandleException( new Exception( $"Hit Frame Drop Cap of {FrameDropCount}, " + ex.Message, ex ) );
                } else
                {
                    // Handle non-critical frame drops and increment the drop counter
                    ErrorHandler.HandleExceptionNonExit( ex );
                    frameDroppedSignal = Interlocked.Increment( ref FrameDropCount );
                }
            } finally
            {
                if ( newFrame is not null )
                {
                    capTime = newFrame!.SystemRelativeTime.TotalMilliseconds;
                    newFrame.Dispose();
                }
                IDSurface?.Dispose();
                ( ( IDXGISurface ) nativeSurface )?.Dispose();
            }

            if ( frameDroppedSignal != -1 || capTime <= 0 )
            {
                return -1;
            }
            return 1;
        }


        /// <summary>
        /// Initializes the unordered access view list, used for dispatching compute shaders
        /// </summary>
        private void SetAllUavViews()
        {
            // Get buffer type count
            var typeCount = Enum.GetValues<BufferType>().Length + 1; //< Add one for the main uav

            // We need to initialize the list with null values
            // Otherwise we will get either a null reference exception or out of range exception
            uavBufferViews = [ .. Enumerable.Repeat<ID3D11UnorderedAccessView?>( null, typeCount ) ];

            // Set our texture buffer to the first uav buffer view
            uavBufferViews[ 0 ] = uav!; // Set the first uav buffer view to the main uav

            // We do this to make sure we have the shaders in the correct order
            // Its much more important with the uav buffer views as this dictates
            // The registers that the views are bound to in the shader
            foreach ( var view in BufferManager?.GetAllUavBufferViewsSafe()! )
            {
                switch ( view.Key )
                {
                    case BufferType.ColorRanges:
                    uavBufferViews[ 1 ] = view.Value;
                    break;
                    case BufferType.GroupData:
                    uavBufferViews[ 2 ] = view.Value;
                    break;
                    case BufferType.ScanBoxData:
                    uavBufferViews[ 3 ] = view.Value;
                    break;
                    case BufferType.DetectedPlayers:
                    uavBufferViews[ 4 ] = view.Value;
                    break;
#if DEBUG_BUFFER
                    case BufferType.DebugBuffer
                    uavBufferViews[ 6 ] = view.Value;
                    break;
#endif
                    default:
                    /// <exception cref="Exception">Unknown buffer type</exception>
                    /// This will close the application
                    ErrorHandler.HandleException( new Exception( "Unknown buffer type" ) );
                    break;
                }

            }

            // Trim the list to remove any null values
            uavBufferViews.TrimExcess();

            if ( uavBufferViews.Count != typeCount )
            {
                // If the count is not equal to the type count, throw an exception
                ErrorHandler.HandleException( new Exception( $"Failed to set all uav buffer views. Current count: {uavBufferViews.Count}, buffer type count: {typeCount}" ) );
            }
        }


        private void SetAllShaders()
        {
            // Get compute shader types count
            var typeCount = Enum.GetValues<ShaderType>().Length;

            // Initialize the dictionary, and shader type list with the type count
            computeShaders = new( typeCount );

            // We dont need to make sure the shaders are in order
            // Because we are using a dictionary to store them
            foreach ( var shader in ShaderManager!.GetAllComputeShadersSafe<ID3D11ComputeShader>()! )
            {
                computeShaders.Add( shader.Key, shader.Value );
            }

            // Trim the list to remove any null values
            computeShaders.TrimExcess();

            if ( computeShaders.Count != ( typeCount ) )
            {
                // If the count is not equal to the type count, throw an exception. This will close the application
                ErrorHandler.HandleException( new Exception( $"Failed to set all compute shaders. Current count: {computeShaders.Count}, shader type count: {typeCount}" ) );
            }
        }




        /// <summary>
        /// Our function to get the new frame each time the event triggers
        /// </summary>
        /// <param name= "framePool">Frame pool object thats being used by graphics session</param>
        private void FrameArrivedHandler( Direct3D11CaptureFramePool framePool, object _ )
        {
            // Check if dx11 class is resetting
            // Im finding we get errors if we return early from even handlers
            // This is here for now just in case
            ResettingClass.Wait();

            // Set new frame
            var nFrame = framePool?.TryGetNextFrame();
            if ( nFrame is not null )
            {
                // Check so see if we have the go ahead
                if ( NewFrame!.IsNonSignaled() )
                {
                    NewFrame?.SetSignaled( nFrame );
                } else
                {
                    nFrame.Dispose();
                }
            }
        }



        /// <summary>
        /// Resets the DirectX 11 class in the case that the user changes the outline color; the windows size or window handle changes.
        /// we do this as its just easier and faster to reset the class than updating the structured buffer.
        /// This also helps to keep our resources clean and not have to worry about memory leaks.
        /// </summary>
        private void ResetDx11()
        {

            // Clean up the resources
            Cleanup();

            // Subscribe to the player data update event
            PlayerData.OnUpdate += UserSettingsUpdated;

            // Get the monitor information
            PInvoke.RECT window = PlayerData.GetRect();
            WindowHeight = ( uint ) window.bottom - ( uint ) window.top;
            WindowWidth = ( uint ) window.right - ( uint ) window.left;

            // Reset the class
            InitD3D11();

            // Calculate dispatch sizes
            // X threads is the number of threads in a thread group for the x axis
            // this is in the shader not the actual dispatch size
            // I.E ->[numthreads( X_THREADGROUP, Y_THREADGROUP, 1 )] <-X_THREADGROUP
            int xThreads = ( gpuVender == "AMD" ) ? 16 : 8;
            XDispatch = ( ( uint ) ( ( WindowWidth + ( xThreads - 1 ) ) / xThreads ) );
            YDispatch = ( WindowHeight + 3u ) >> 2;

            // Create the window capture class
            windowCapture = new( ref d3d11Device!, ref textureDescription.Format );

            // Instantiate new frame thread signal
            NewFrame = new( default );
            NewFrame?.SetNonSignaled();

            // Subscribe to new frame event
            windowCapture.FramePool!.FrameArrived += this.FrameArrivedHandler;

            // Create the buffer manager
            BufferManager = new( ref colorToleranceManager!, ref d3d11Device!, xThreads );
            // Create the shader manager
            ShaderManager = new( ref d3d11Device!, ref colorToleranceManager!, ref BufferManager, ref gpuVender! );

            // Get the compute shaders, and uav buffer views
            SetAllShaders();
            SetAllUavViews();

        }

        /// <summary>
        /// Cleans up the resources and resets the class.
        /// </summary>
        private void Cleanup()
        {
            if ( windowCapture is not null && windowCapture.FramePool is not null )
            {
                // Unsubscribe to new frame event
                windowCapture.FramePool.FrameArrived -= this.FrameArrivedHandler;
                windowCapture.Dispose();
            }
            uavBufferViews?.Clear();
            BufferManager?.Dispose();
            ShaderManager?.Dispose();
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
#endif
#endif
        }



        /// <summary>
        /// This is our event handler for when dx11 related player data gets updated
        /// </summary>
        /// <param name="sender">N/A</param>
        /// <param name="e">This holds our custom class to retrieve update type, and updated variable</param>
        private async void UserSettingsUpdated( object sender, PlayerUpdateCallbackEventArgs e )
        {
            if ( e.Key == UpdateType.OutlineColor ||
                e.Key == UpdateType.Hwnd ||
                e.Key == UpdateType.WindowRect ||
                e.Key == UpdateType.AimFov )
            {
                // Wait in case we are in the middle of a reset
                ResettingClass?.Wait();

                // notify outside the class that we are resetting the class
                ResettingClass?.Reset();

                // Make sure we give other threads time to block
                await Utils.Watch.AsyncSecondsSleep( 1 );
                // Reset the class
                ResetDx11();

                // Set our class notification back to signal, to unblock outside threads
                ResettingClass?.Set();
            }
        }


        /// <summary>
        /// Checks if the adapter supports the nessary
        /// Support for the texture format and other features
        /// </summary>
        private void CheckFormatSupport()
        {
            var formatSupport2 = d3d11Device!.CheckFeatureFormatSupport2( textureDescription.Format );
            var formatSupport = d3d11Device!.CheckFeatureFormatSupport( textureDescription.Format );

            if ( ( formatSupport & FormatSupport.Buffer ) != 0 )
            {
#if DEBUG
                Logger.Log( "R8G8B8A8_UNorm format is supported" );
#endif

            } else
            {
                ErrorHandler.HandleException( new Exception( "R8G8B8A8_UNorm format is not supported" ) );
            }


            // Check for texture 2D support
            if ( ( formatSupport & FormatSupport.Texture2D ) != 0 )
            {
#if DEBUG
                Logger.Log( "Texture2D is supported" );
#endif
            } else
            {
                ErrorHandler.HandleException( new Exception( "Texture2D is not supported" ) );
            }


            // Check for shader support
            if ( ( formatSupport & FormatSupport.ShaderLoad ) != 0 )
            {
#if DEBUG
                Logger.Log( "Shaders are supported" );
#endif
            } else
            {
                ErrorHandler.HandleException( new Exception( "Shaders are not supported" ) );
            }


            // Check for UAV typed load support
            if ( ( formatSupport2 & FormatSupport2.UnorderedAccessViewTypedLoad ) != 0 )
            {
#if DEBUG
                Logger.Log( "UnorderedAccessViewTypedLoad is supported" );
#endif
            } else
            {
                ErrorHandler.HandleException( new Exception( "UnorderedAccessViewTypedLoad is not supported" ) );
            }
        }



        private void PrintImageToFile( ref string fileName, ref MappedSubresource mappedBuffer, bool isFiltered = false )
        {
            using Bitmap bitmap = new( ( int ) WindowWidth, ( int ) WindowHeight, BitsPerPixel == 32 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb );
            try
            {
                // Lock bitmap for operations
                var bmpData = bitmap.LockBits( new Rectangle( 0, 0, ( int ) WindowWidth, ( int ) WindowHeight ), ImageLockMode.WriteOnly, bitmap.PixelFormat );

                // Check if the row pitch matches the stride count
                if ( mappedBuffer.RowPitch != bmpData.Stride )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidDataException( "Row pitch does not match stride." ) );
                }

                // Get the size of the image data. this includes any padding. so its best to use the stride.
                int copySize = bmpData.Stride * bmpData.Height;

                unsafe
                {
                    // Copy the image data to the bitmap
                    NativeMemory.Copy( mappedBuffer.DataPointer.ToPointer(), bmpData.Scan0.ToPointer(), ( ( nuint ) copySize ) );
                }

                bitmap.UnlockBits( bmpData );
            } finally
            {
                // Unmap the filtered resource
                d3d11Context!.Unmap( stagingBuffer, 0 );
                // Print to file
                if ( isFiltered )
                {
                    // Wait for unmap
                    WaitForFence();
                    fileName = fileName.Replace( "Unfiltered", "Filtered" );
                } else
                {
                    // Wait for unmap
                    WaitForFence();
                }

                bitmap.Save( fileName, ImageFormat.Png );
            }
        }


        /// <summary>
        /// Waits for fence signal
        /// No return if the fence signal never changes
        /// </summary>
        /// <param name="fenceSignal"></param>
        private void WaitForFence()
        {
            if ( d3d11Fence is null || d3d11Context4 is null )
            {
                ErrorHandler.HandleException( new COMException( "d3d fence or context4 is null" ) );
            }

            // Push fence value ahead of current completed value
            d3d11FenceValue = d3d11Fence.CompletedValue + 1;

            // Push signal to queue
            d3d11Context4.Signal( d3d11Fence!, d3d11FenceValue );

            // Wait for signal
            while ( d3d11Fence.CompletedValue < d3d11FenceValue )
            {
                Utils.Watch.MicroSleep( 10 );
            }
        }


        [ComImport]
        [Guid( "A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1" )]
        [InterfaceType( ComInterfaceType.InterfaceIsIUnknown )]
        public interface IDirect3DDxgiInterfaceAccess
        {
            int GetInterface( [In] ref Guid iid, out IntPtr ppv );
        }
    }
}

