using System.Runtime.InteropServices;

namespace SCB
{
    internal static class MouseInput
    {

        public static Point GetLastMousePos()
        {
            Point pos = new Point();
            if ( !GetCursorPos( ref pos ) )
            {
                pos.X = -1;
                pos.Y = -1;
                return pos;
            }

            return pos;
        }


        public static void MoveRelativeMouse( int x, int y )
        {
            if ( x != 0 || y != 0 )
            {
                INPUT[] tempInput = new INPUT[ 1 ];
                ref INPUT temp = ref tempInput[ 0 ];
                temp.Type = INPUT_MOUSE;
                temp.Data.Mouse.ExtraInfo = nint.Zero;
                temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE;
                temp.Data.Mouse.MouseData = 0;
                temp.Data.Mouse.Time = 0;
                temp.Data.Mouse.X = x;
                temp.Data.Mouse.Y = y;
                uint result = SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
            }
        }


        /// <summary>
        /// Move the mouse cursor to an absolute position on the virtual desktop
        /// </summary>
        /// <param name="x">X coordinate in range of [0.0, 1.0]. 0.0 for left. 1.0 for far right</param>
        /// <param name="y">Y coordinate in range of [0.0, 1.0]. 0.0 for top. 1.0 for bottom</param>
        public static void MoveAbsoluteMouse( double x, double y )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_ABSOLUTE;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;
            temp.Data.Mouse.X = ( int ) ( x * ABSOLUTE_MOUSE_COOR_MAX );
            temp.Data.Mouse.Y = ( int ) ( y * ABSOLUTE_MOUSE_COOR_MAX );
            uint result = SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
        }


        public static void MouseButtonPress( uint mouseButton )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = mouseButton;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;
            temp.Data.Mouse.X = 0;
            temp.Data.Mouse.Y = 0;
            uint result = SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
        }

        public static void MouseButtonRelease( uint mouseButton )
        {
            MouseButtonPress( mouseButton );
        }


        [StructLayout( LayoutKind.Sequential )]
        internal struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }



        [StructLayout( LayoutKind.Explicit )]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset( 0 )]
            public HARDWAREINPUT Hardware;
            [FieldOffset( 0 )]
            public KEYBDINPUT Keyboard;
            [FieldOffset( 0 )]
            public MOUSEINPUT Mouse;
        }


        [StructLayout( LayoutKind.Sequential )]
        internal struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }


        [StructLayout( LayoutKind.Sequential )]
        internal struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }


        [StructLayout( LayoutKind.Sequential )]
        internal struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public nint ExtraInfo;
        }


        internal const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, INPUT_HARDWARE = 2;
        internal const double ABSOLUTE_MOUSE_COOR_MAX = 65535.0;
        internal const uint MOUSEEVENTF_MOVE = 1, // just apply X/Y (delta due to not setting absolute flag)
            MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4,
            MOUSEEVENTF_RIGHTDOWN = 8, MOUSEEVENTF_RIGHTUP = 16,
            MOUSEEVENTF_MIDDLEDOWN = 32, MOUSEEVENTF_MIDDLEUP = 64,
            MOUSEEVENTF_XBUTTONDOWN = 128, MOUSEEVENTF_XBUTTONUP = 256,
            MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000,
            KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2, MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000,
            MOUSEEVENTF_MIDDLEWDOWN = 0x0020, MOUSEEVENTF_MIDDLEWUP = 0x0040,
            KEYEVENTF_SCANCODE = 0x0008, MAPVK_VK_TO_VSC = 0, KEYEVENTF_UNICODE = 0x0004, EXTENDED_FLAG = 0x100;


        [DllImport( "user32.dll" )]
        private static extern uint SendInput( uint numberOfInputs, INPUT[] inputs, int sizeOfInputs );

        [DllImport( "user32.dll" )]
        public static extern bool GetCursorPos( ref Point lpPoint );


    }
}
