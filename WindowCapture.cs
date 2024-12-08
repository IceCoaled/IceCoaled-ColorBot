namespace SCB
{
    using System;
    using System.Runtime.InteropServices;
    using Vortice.Win32;

    internal partial class WindowCapture : IDisposable
    {
        private bool disposed = false;

        // Device Contexts
        private IntPtr HdcScreen { get; set; }
        private IntPtr HdcMemory { get; set; }
        private IntPtr HBitmap { get; set; }
        private IntPtr OldBitmap { get; set; }

        // Bitmap Info 
        private BITMAPINFO bmi = new();

        // Window Information
        private nint HWnd { get; set; }
        private int Width { get; set; }
        private int Height { get; set; }

        // GetDIBits fail count
        private int GetDIBitsFailCount { get; set; }


        internal WindowCapture( int windowWidth, int windowHeight )
        {
            // Set window dimensions
            Width = windowWidth;
            Height = windowHeight;

            var monitor = Screen.PrimaryScreen;

            // Setup the bmiHeader for copying the captured image
            bmi = new()
            {
                bmiHeader = new()
                {
                    BitCount = ( short ) monitor!.BitsPerPixel,
                    Compression = ( int ) ColorModes.BI_RGB_COMPRESSION,
                    Height = -Height, //< negative height to copy the image from top to bottom
                    Width = Width,
                    PlaneCount = 1,
                    SizeInBytes = Marshal.SizeOf<BitmapInfoHeader>(),
                    ColorUsedCount = 0,
                    ColorImportantCount = 0,
                    SizeImage = ( Height * Width * ( monitor!.BitsPerPixel / 8 ) ),
                },
            };


            // Get window handle
            HWnd = PlayerData.GetHwnd();

            // Setup the window details
            SetupWindowDetails();
        }


        ~WindowCapture()
        {
            Dispose( false );
        }


        // Gets the windows grahpics context, the window dimensions and creates a compatible DC and bitmap
        private void SetupWindowDetails()
        {
            // Get the device context of the window
            HdcScreen = GetWindowDC( HWnd );
            if ( HdcScreen == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new InvalidOperationException( "Unable to get the device context of the window." ) );
            }

            // Create a compatible DC and bitmap
            HdcMemory = CreateCompatibleDC( HdcScreen );
            if ( HdcMemory == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new InvalidOperationException( "Unable to create a compatible device context." ) );
            }

            HBitmap = CreateCompatibleBitmap( HdcScreen, Width, Height );
            if ( HBitmap == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new InvalidOperationException( "Unable to create a compatible bitmap." ) );
            }

            // Select the bitmap into the memory DC
            OldBitmap = SelectObject( HdcMemory, HBitmap );
        }


        // Take a screenshot of the given window and return as a Bitmap
        public void CaptureWindow( ref nint mappedResource )
        {
            // Perform a BitBlt to copy the window content to the memory DC
            if ( !BitBlt( HdcMemory, 0, 0, Width, Height, HdcScreen, 0, 0, SRCCOPY ) )
                throw new InvalidOperationException( "BitBlt operation failed." );

            // Copy the bitmap to the d3d11 mapped resource
            int scanLines = GetDIBits( HdcMemory, HBitmap, 0, ( uint ) Height, mappedResource, ref bmi, ( uint ) ColorModes.DIB_RGB_COLORS );
            if ( scanLines == 0 )
            {
                string hexResult = GetLastError().ToString( "X" );
                GetDIBitsFailCount++;
                if ( GetDIBitsFailCount > 50 )
                {
                    ErrorHandler.HandleException( new InvalidOperationException( $"GetDIBits operation failed with error: {hexResult}, and exceeded max error count of 50 " ) );
                } else
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( $"GetDIBits operation failed with error: {hexResult} " ) );
                }
            }
        }


        // Cleanup resources
        private void Cleanup()
        {
            // Release resources                      
            _ = SafeRelease<bool>( DeleteObject, HBitmap );
            _ = SafeRelease<IntPtr>( SelectObject, HdcMemory, OldBitmap );
            _ = SafeRelease<bool>( DeleteDC, HdcMemory );
            _ = SafeRelease<bool>( ReleaseDC, HWnd, HdcScreen );
        }

        private static T SafeRelease<T>( Delegate releaseFunc, params object[] args )
        {
            if ( args.Length > 0 && args[ 0 ] is nint resource && resource != IntPtr.Zero )
            {
                T? result = ( T? ) releaseFunc.DynamicInvoke( args ); // Invoke the delegate with the given arguments
                args[ 0 ] = IntPtr.Zero; // Set the first argument (resource) to zero
                return result!;
            }
            return default!;
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                Cleanup();
            }
            disposed = true;
        }

        private enum ColorModes
        {
            DIB_RGB_COLORS = 0x00,
            DIB_PAL_COLORS = 0x01,
            DIB_PAL_INDICES = 0x02,

            // Compression mode for bitmap header
            BI_RGB_COMPRESSION = DIB_RGB_COLORS
        }


        // P/Invoke declarations
        [DllImport( "user32.dll" )]
        private static extern IntPtr GetWindowDC( IntPtr hWnd );

        [DllImport( "user32.dll" )]
        private static extern bool ReleaseDC( IntPtr hWnd, IntPtr hDC );

        [DllImport( "gdi32.dll" )]
        private static extern IntPtr CreateCompatibleDC( IntPtr hdc );

        [DllImport( "gdi32.dll" )]
        private static extern IntPtr CreateCompatibleBitmap( IntPtr hdc, int nWidth, int nHeight );

        [DllImport( "gdi32.dll" )]
        private static extern IntPtr SelectObject( IntPtr hdc, IntPtr hgdiobj );

        [DllImport( "gdi32.dll" )]
        private static extern bool BitBlt( IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop );

        [DllImport( "gdi32.dll" )]
        private static extern bool DeleteObject( IntPtr hObject );

        [DllImport( "gdi32.dll" )]
        private static extern bool DeleteDC( IntPtr hdc );

        [DllImport( "gdi32.dll", SetLastError = true )]
        private static extern int GetDIBits( IntPtr hdc, IntPtr hBitmap, uint uStartScan, uint cScanLines,
        IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage );

        [DllImport( "Kernel32.dll" )]
        private static extern long GetLastError();


        private const int SRCCOPY = 0x00CC0020;


        [StructLayout( LayoutKind.Sequential )]
        private struct BITMAPINFO
        {
            public BitmapInfoHeader bmiHeader;
            public RGBQUAD[] bmiColors;
        }

        [StructLayout( LayoutKind.Explicit )]
        private struct RGBQUAD
        {
            [FieldOffset( 0 )]
            public byte rgbBlue;
            [FieldOffset( 1 )]
            public byte rgbGreen;
            [FieldOffset( 2 )]
            public byte rgbRed;
            [FieldOffset( 3 )]
            public byte rgbReserved;
        }
    }

}
