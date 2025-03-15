using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SCB
{

    /// <summary>
    /// Class for handling mouse input, or keyboard input. large room for expansion.
    /// </summary>
    internal static partial class HidInputs
    {
        /// <summary>
        /// Checks most significat bit to see if its zero or one
        /// </summary>
        /// <param name="Key">The virtual key code of the key to check.</param>
        /// <returns>Returns true if the key is being pressed, otherwise false.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static bool IsKeyPressed( ref int Key )
        {
            return ( GetAsyncKeyState( Key ) & 0x8000 ) != 0;
        }


        /// <summary>
        /// Moves the mouse cursor relative to its current position.
        /// This method is faster than the other one, but it requires the caller to create the INPUT array.
        /// This is done to avoid creating a new array every time the method is called.
        /// We also remove any checks for any of the data, as we assume the caller has already done them.
        /// </summary>
        /// <param name="inputs">Takes single inout and we cast to array</param>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        internal static void CustomMoveRelativeMouse( ref INPUT input )
        {
            _ = SendInput( 1u, [ input ], Marshal.SizeOf<INPUT>() );
        }

        /// <summary>
        /// Moves the mouse cursor relative to its current position.
        /// </summary>
        /// <param name="x">The X movement (relative).</param>
        /// <param name="y">The Y movement (relative).</param>
        internal static void MoveRelativeMouse( ref float x, ref float y )
        {
            if ( float.Abs( x ) <= 0.00001 || float.Abs( y ) <= 0.00001 )
            {
                INPUT[] tempInput = new INPUT[ 1 ];
                ref INPUT temp = ref tempInput[ 0 ];
                temp.Type = INPUT_MOUSE;
                temp.Data.Mouse.ExtraInfo = nint.Zero;
                temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE;
                temp.Data.Mouse.MouseData = 0;
                temp.Data.Mouse.Time = 0;
                temp.Data.Mouse.X = ( int ) Math.Round( x );
                temp.Data.Mouse.Y = ( int ) Math.Round( y );
                _ = SendInput( 1, tempInput, Marshal.SizeOf( tempInput[ 0 ] ) );
            }
        }

        /// <summary>
        /// Moves the mouse cursor to an absolute position on the virtual desktop.
        /// </summary>
        /// <param name="x">The X coordinate (range [0.0, 1.0]).</param>
        /// <param name="y">The Y coordinate (range [0.0, 1.0]).</param>
        internal static void MoveAbsoluteMouse( ref float x, ref float y )
        {
            INPUT[] tempInput = new INPUT[ 1 ];
            ref INPUT temp = ref tempInput[ 0 ];
            temp.Type = INPUT_MOUSE;
            temp.Data.Mouse.ExtraInfo = nint.Zero;
            temp.Data.Mouse.Flags = MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_ABSOLUTE;
            temp.Data.Mouse.MouseData = 0;
            temp.Data.Mouse.Time = 0;

            // Multiply by ABSOLUTE_MOUSE_COOR_MAX (65535.0) to convert to absolute coordinates
            temp.Data.Mouse.X = ( ( int ) ( Mathf.Clamp01( x ) * ABSOLUTE_MOUSE_COOR_MAX ) );
            temp.Data.Mouse.Y = ( ( int ) ( Mathf.Clamp01( y ) * ABSOLUTE_MOUSE_COOR_MAX ) );

            // Send the input event
            _ = SendInput( 1, tempInput, Marshal.SizeOf<INPUT>() );
        }

        /// <summary>
        /// Simulates a mouse button press.
        /// </summary>
        /// <param name="mouseButton">The button to press.</param>
        internal static void MouseButtonPress( ref uint mouseButton )
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
            _ = SendInput( 1, tempInput, Marshal.SizeOf<INPUT>() );
        }

        /// <summary>
        /// Simulates a mouse button release.
        /// </summary>
        /// <param name="mouseButton">The button to release.</param>
        internal static void MouseButtonRelease( ref uint mouseButton )
        {
            MouseButtonPress( ref mouseButton );
        }

        /// <summary>
        /// Simulates pressing Windows key + T to show the taskbar.
        /// </summary>
        internal static void ShowTaskbarViaShortcut()
        {
            INPUT[] inputs = new INPUT[ 4 ];

            // Press Windows Key
            inputs[ 0 ].Type = INPUT_KEYBOARD;
            inputs[ 0 ].Data.Keyboard.Vk = 0x5B; // Virtual key code for left Windows key

            // Press T Key
            inputs[ 1 ].Type = INPUT_KEYBOARD;
            inputs[ 1 ].Data.Keyboard.Vk = 0x54; // Virtual key code for T

            // Release T Key
            inputs[ 2 ].Type = INPUT_KEYBOARD;
            inputs[ 2 ].Data.Keyboard.Vk = 0x54; // Virtual key code for T
            inputs[ 2 ].Data.Keyboard.Flags = KEYEVENTF_KEYUP;

            // Release Windows Key
            inputs[ 3 ].Type = INPUT_KEYBOARD;
            inputs[ 3 ].Data.Keyboard.Vk = 0x5B; // Virtual key code for left Windows key
            inputs[ 3 ].Data.Keyboard.Flags = KEYEVENTF_KEYUP;

            _ = SendInput( ( uint ) inputs.Length, inputs, Marshal.SizeOf<INPUT>() );
        }

        // Structs and constants used for mouse and keyboard input.
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

        [StructLayout( LayoutKind.Sequential )]
        internal struct MOUSEMOVEPOINT
        {
            public int X;
            public int Y;
            public uint Time;
            public nint ExtraInfo;
        }



        internal const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04, VK_XBUTTON1 = 0x05, VK_XBUTTON2 = 0x06;
        internal const int VK_INSERT = 0x2D, VK_DELETE = 0x2E, VK_HOME = 0x24, VK_END = 0x23, VK_PRIOR = 0x21, VK_NEXT = 0x22;
        internal const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
        internal const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
        internal const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        internal const int VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SPACE = 0x20, VK_ESCAPE = 0x1B, VK_BACK = 0x08;
        internal const int VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F6 = 0x75, VK_F7 = 0x76, VK_F8 = 0x77, VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B;
        internal const int VK_NUMPAD0 = 0x60, VK_NUMPAD1 = 0x61, VK_NUMPAD2 = 0x62, VK_NUMPAD3 = 0x63, VK_NUMPAD4 = 0x64, VK_NUMPAD5 = 0x65, VK_NUMPAD6 = 0x66, VK_NUMPAD7 = 0x67, VK_NUMPAD8 = 0x68, VK_NUMPAD9 = 0x69;
        internal const int VK_MULTIPLY = 0x6A, VK_ADD = 0x6B, VK_SUBTRACT = 0x6D, VK_DECIMAL = 0x6E, VK_DIVIDE = 0x6F;
        internal const int VK_OEM_1 = 0xBA, VK_OEM_PLUS = 0xBB, VK_OEM_COMMA = 0xBC, VK_OEM_MINUS = 0xBD, VK_OEM_PERIOD = 0xBE, VK_OEM_2 = 0xBF, VK_OEM_3 = 0xC0, VK_OEM_4 = 0xDB, VK_OEM_5 = 0xDC, VK_OEM_6 = 0xDD, VK_OEM_7 = 0xDE, VK_OEM_8 = 0xDF;
        internal const int VK_OEM_102 = 0xE2, VK_OEM_CLEAR = 0xFE;
        internal const int VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45, VK_F = 0x46, VK_G = 0x47, VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F, VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54, VK_U = 0x55, VK_V = 0x56, VK_W = 0x57, VK_X = 0x58, VK_Y = 0x59, VK_Z = 0x5A;
        internal const int VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34, VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39;
        internal const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, INPUT_HARDWARE = 2;
        internal const Single ABSOLUTE_MOUSE_COOR_MAX = 65535.0f;
        internal const uint MOUSEEVENTF_MOVE = 1, // just apply X/Y (delta due to not setting absolute flag)
            MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4,
            MOUSEEVENTF_RIGHTDOWN = 8, MOUSEEVENTF_RIGHTUP = 16,
            MOUSEEVENTF_MIDDLEDOWN = 32, MOUSEEVENTF_MIDDLEUP = 64,
            MOUSEEVENTF_XBUTTONDOWN = 128, MOUSEEVENTF_XBUTTONUP = 256,
            MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000,
            KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2, MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000,
            MOUSEEVENTF_MIDDLEWDOWN = 0x0020, MOUSEEVENTF_MIDDLEWUP = 0x0040,
            KEYEVENTF_SCANCODE = 0x0008, MAPVK_VK_TO_VSC = 0, KEYEVENTF_UNICODE = 0x0004, EXTENDED_FLAG = 0x100;

        // P/Invoke for sending input and getting/setting cursor position.
        /// <summary>
        /// Sends direct input to the operating system.
        /// </summary>
        /// <param name="numberOfInputs">Number of input structs in array</param>
        /// <param name="inputs">input structs array</param>
        /// <param name="sizeOfInputs">Total size of array</param>
        /// <returns></returns>
        [DllImport( "user32.dll", SetLastError = true )]
        private static extern uint SendInput( uint numberOfInputs, INPUT[] inputs, int sizeOfInputs );

        [DllImport( "user32.dll", SetLastError = true )]
        public static extern bool GetCursorPos( ref Point lpPoint );

        [DllImport( "user32.dll", SetLastError = true )]
        public static extern void SetCursorPos( int x, int y );

        /// <summary>
        /// Determines whether a key is up or down at the time the function is called.
        /// </summary>
        /// <param name="vKey">The virtual-key code of the key to check.</param>
        /// <returns>If the key is pressed, the function returns a non-zero value.</returns>
        [DllImport( "user32.dll" )]
        internal static extern short GetAsyncKeyState( int vKey );
    }


}
