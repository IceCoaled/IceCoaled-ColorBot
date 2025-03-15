namespace SCB.DirectX
{
    using System;
    using System.Runtime.InteropServices;
    using Vortice.Direct3D11;
    using Vortice.DXGI;
    using Windows.Graphics;
    using Windows.Graphics.Capture;
    using Windows.Graphics.DirectX.Direct3D11;
    using Windows.UI;

    /// <summary>
    /// Handles window capture functionality using Direct3D 11 and Windows Graphics Capture API
    /// </summary>
    internal partial class WindowCapture : IDisposable
    {
        private bool Disposed { get; set; } = false;

        /// The handle to the window being captured
        private nint HWND { get; set; }

        /// The rectangle defining the window's dimensions
        private PInvoke.RECT WindowRect { get; set; }

        /// The size of the frame buffer for capturing frames
        private SizeInt32 SzFrameBuffer { get; set; }

        /// The pixel format we are using for capture session
        private Windows.Graphics.DirectX.DirectXPixelFormat PixelFormat { get; set; }

        /// The graphics capture session used for capturing the window.
        private GraphicsCaptureSession? GSession { get; set; } = null;

        /// The graphics capture item representing the target window.
        private GraphicsCaptureItem? GItem { get; set; } = null;

        /// The frame pool used for capturing frames
        internal Direct3D11CaptureFramePool? FramePool { get; set; }

        /// The Direct3D device used for capture
        private IDirect3DDevice? Direct3DDevice { get; init; }

        /// Event used to synchronize class resetting operations
        internal ManualResetEventSlim? ClassResetting { get; init; }


        /// <summary>
        /// Initializes a new instance of the <see cref="WindowCapture"/> class.
        /// </summary>
        /// <param name="d3dDevice">The Direct3D 11 device used for initialization.</param>
        internal WindowCapture( ref ID3D11Device? d3dDevice, ref Vortice.DXGI.Format pixelFormat )
        {
            if ( d3dDevice is null )
            {
                ErrorHandler.HandleException( new ArgumentNullException( nameof( d3dDevice ), " WindowCapture: ID3D11Device Is Null" ) );
            }

            // Get window details
            // Get the current window handle
            HWND = PlayerData.GetHwnd();

            // Get the window rectangle
            WindowRect = PlayerData.GetRect();

            // Calculate the frame buffer size
            SzFrameBuffer = new SizeInt32( WindowRect.right - WindowRect.left, WindowRect.bottom - WindowRect.top );

            // Setup class resetting manual event
            ClassResetting = new ManualResetEventSlim();
            ClassResetting.Set();

            // Set the pixel format
            PixelFormat = ( pixelFormat == Vortice.DXGI.Format.B8G8R8A8_UNorm ) ? Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized : Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8X8UIntNormalized;

            try
            {
                using var dxgiDevice2 = d3dDevice?.QueryInterfaceOrNull<IDXGIDevice2>() ?? throw new COMException( "Failed to get idxgi device interface" );
                int hResult = CreateDirect3D11DeviceFromDXGIDevice( dxgiDevice2.NativePointer, out nint idirectDevicePtr );
                if ( hResult != 0 || idirectDevicePtr == nint.Zero )
                {
                    throw new COMException( "Failed to Get idirect3d device interface pointer", hResult );
                }

                Direct3DDevice = WinRT.MarshalInspectable<IDirect3DDevice>.FromAbi( idirectDevicePtr ) ?? throw new COMException( "Failed to Get idirect3d device interface" );
            } catch ( Exception ex )
            {
                ErrorHandler.HandleException( ex );
            } finally
            {
                CreateCaptureSession();
            }

        }


        /// <summary>
        /// Destructor for the <see cref="WindowCapture"/> class.
        /// </summary>
        ~WindowCapture()
        {
            Dispose( false );
        }



        /// <summary>
        /// Starts the capture session.
        /// </summary>
        internal void StartCaptureSession()
        {
            // If the class is resetting we wait
            // This will block the calling thread
            ClassResetting?.Wait();
#if DEBUG
            Logger.Log( "Window Capture Is Starting\n" );
#endif
            try
            {
                if ( GSession is null || FramePool is null )
                {
                    throw new COMException( "Trying To Start Session With Null Session or Null Frame Pool" );
                }
                GSession!.StartCapture(); // Start the capture session
            } catch ( ExternalException ex )
            {
                ErrorHandler.HandleException( ex );
            }
        }


        /// <summary>
        /// Stops the capture session.
        /// </summary>
        internal void StopCaptureSession()
        {
            // If the class is resetting we wait
            // This will block the calling thread
            ClassResetting?.Wait();

            GSession?.Dispose(); // Dispose of the graphics session
            FramePool?.Dispose(); // Dispose of the frame pool
            GItem = null;

            // Reset and Init share the same functionality 
            // Made signal function, should change method name
            CreateCaptureSession( true );
        }


        /// <summary>
        /// Resets the capture session, reinitializing components as needed.
        /// </summary>
        private void CreateCaptureSession( bool reset = false )
        {
            if ( reset )
            {
                // Block other threads
                ClassResetting?.Reset();
                // Sleep to let other threads get signal
                Utils.Watch.SecondsSleep( 1 );
                ClassResetCleanup();
            }

            try
            {
                if ( Direct3DDevice is null )
                {
                    throw new COMException( "IDirect 3D Device is Null" );
                }

                // Create the frame pool
                FramePool = Direct3D11CaptureFramePool.CreateFreeThreaded( Direct3DDevice, PixelFormat, 1, SzFrameBuffer );
                if ( FramePool is null )
                {
                    throw new COMException( "Failed To Create Graphics Frame Pool" );
                }

                // Create the graphics capture item
                GItem = GraphicsCaptureItem.TryCreateFromWindowId( new WindowId( ( ulong ) HWND ) );
                if ( GItem is null )
                {
                    throw new COMException( "Failed To Get Graphics Item For Window" );
                }

                // Create the capture session
                GSession = FramePool!.CreateCaptureSession( GItem );
                if ( GSession is null )
                {
                    throw new COMException( "Failed To Create Graphics Sessions" );
                }
                // Set minimum update interval between screen captures
                GSession.MinUpdateInterval = TimeSpan.FromMilliseconds( 35 );
            } catch ( ExternalException ex )
            {
                ErrorHandler.HandleException( ex );
            } finally
            {
                // Unblock other threads
                if ( reset )
                {
                    ClassResetting?.Set();
                }
            }
        }



        /// <summary>
        /// Cleans up resources for resetting the class.
        /// </summary>
        private void ClassResetCleanup()
        {
            GSession?.Dispose();
            FramePool?.Dispose();
            Direct3DDevice?.Dispose();
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !Disposed )
            {
                ClassResetCleanup();
                ClassResetting?.Dispose();
            }
            Disposed = true;
        }



        /// <summary>
        /// Checks system compatibility for window capture.
        /// </summary>
        internal static void CheckForCompatability()
        {
            DialogResult? result = null;
            if ( Environment.OSVersion.Version < new Version( 10, 0, 26100, 0 ) )
            {
                result = MessageBox.Show( "Insufficient Windows Version To Run This Application", "Please Upgrade Your OS", MessageBoxButtons.YesNo, MessageBoxIcon.Error, 0, MessageBoxOptions.ServiceNotification );

                while ( result is null )
                {
                    Utils.Watch.SecondsSleep( 1 );
                }
                Environment.Exit( 0 );
            }

            if ( !GraphicsCaptureSession.IsSupported() )
            {
                result = null; // reset
                result = MessageBox.Show( "Insufficient Windows Version To Run This Application", "Please Upgrade Your OS", MessageBoxButtons.YesNo, MessageBoxIcon.Error, 0, MessageBoxOptions.ServiceNotification );

                while ( result is null )
                {
                    Utils.Watch.SecondsSleep( 1 );
                }
                Environment.Exit( 0 );
            }
        }


        [DllImport(
        "d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true )]
        [UnmanagedCallConv( CallConvs = [ typeof( System.Runtime.CompilerServices.CallConvStdcall ) ] )]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice( nint dxgiDevice, out nint graphicsDevice );
    }
}

