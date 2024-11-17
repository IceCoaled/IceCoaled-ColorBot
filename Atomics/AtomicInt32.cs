using System.Runtime.CompilerServices;

namespace SCB.Atomics
{
    using ASC = AtomicSupportClass;

    public unsafe class AtomicInt32( int value ) : UnsafeAtomicNumerics<int>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<int, int, int>> SignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<int>();
        public Dictionary<ASC.AtomicOps, Func<int, int, int>> SignedBitwiseOperations { get; set; } = ASC.Signed32BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<int, int, int>> SignedShiftOperations { get; set; } = ASC.Signed32ShiftOperations();


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Add( int value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Subtract( int value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Multiply( int value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Divide( int value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Modulus( int value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Max( int value )
        {
            return int.Max( ReadInt32(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Min( int value )
        {
            return int.Min( ReadInt32(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int And( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Or( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Xor( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private int PerformArithmeticOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            int result = operation( ReadInt32(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private int PerformBitwiseOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            int result = operation( ReadInt32(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private int PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            int result = operation( ReadInt32(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }



        ~AtomicInt32()
        {
            Dispose( false );
        }

        protected override void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                base.Dispose( disposing );
            }
            disposed = true;
        }

        // Object overrides

        public override bool Equals( object? obj )
        {
            if ( obj is AtomicInt32 atomic )
            {
                return ReadInt32() == atomic.ReadInt32() &&
                    GetHashCode() == atomic.GetHashCode();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        // Overload operators
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator AtomicInt32( int value )
        {
            return new AtomicInt32( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicInt32 atomic, int value )
        {
            return atomic.ReadInt32() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicInt32 atomic, int value )
        {
            return atomic.ReadInt32() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( int value, AtomicInt32 atomic )
        {
            return atomic.ReadInt32() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( int value, AtomicInt32 atomic )
        {
            return atomic.ReadInt32() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicInt32 atomic, int value )
        {
            return atomic.ReadInt32() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicInt32 atomic, int value )
        {
            return atomic.ReadInt32() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( int value, AtomicInt32 atomic )
        {
            return value > atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( int value, AtomicInt32 atomic )
        {
            return value < atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.ReadInt32() > atomic2.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.ReadInt32() < atomic2.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicInt32 atomic, int value )
        {
            return atomic.ReadInt32() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicInt32 atomic, int value )
        {
            return atomic.ReadInt32() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( int value, AtomicInt32 atomic )
        {
            return value >= atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( int value, AtomicInt32 atomic )
        {
            return value <= atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.ReadInt32() >= atomic2.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.ReadInt32() <= atomic2.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator +( AtomicInt32 atomic, int value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator +( int value, AtomicInt32 atomic )
        {
            return value + atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator +( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Add, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator -( AtomicInt32 atomic, int value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator -( int value, AtomicInt32 atomic )
        {
            return value - atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator -( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator *( AtomicInt32 atomic, int value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator *( int value, AtomicInt32 atomic )
        {
            return value * atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator *( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator /( AtomicInt32 atomic, int value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator /( int value, AtomicInt32 atomic )
        {
            return value / atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator /( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator %( AtomicInt32 atomic, int value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator %( int value, AtomicInt32 atomic )
        {
            return value % atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator %( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator &( AtomicInt32 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator &( int value, AtomicInt32 atomic )
        {
            return value & atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator &( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.And, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator |( AtomicInt32 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator |( int value, AtomicInt32 atomic )
        {
            return value | atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator |( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Or, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator ^( AtomicInt32 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator ^( int value, AtomicInt32 atomic )
        {
            return value ^ atomic.ReadInt32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator ^( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator ~( AtomicInt32 atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator <<( AtomicInt32 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator >>( AtomicInt32 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator <<( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, atomic2.ReadInt32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int operator >>( AtomicInt32 atomic1, AtomicInt32 atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.RightShift, true, atomic2.ReadInt32() );
        }



        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicInt32 operator ++( AtomicInt32 atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicInt32 operator --( AtomicInt32 atomic )
        {
            atomic.Decrement();
            return atomic;
        }
    }





    public unsafe class AtomicUint32( uint value ) : UnsafeAtomicNumerics<uint>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<uint, uint, uint>> UnsignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<uint>();
        public Dictionary<ASC.AtomicOps, Func<uint, uint, uint>> UnsignedBitwiseOperations { get; set; } = ASC.Unsigned32BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<uint, int, uint>> UnsignedShiftOperations { get; set; } = ASC.Unsigned32ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Add( uint value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Subtract( uint value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Multiply( uint value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Divide( uint value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Modulus( uint value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Max( uint value )
        {
            return uint.Max( ReadUint32(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Min( uint value )
        {
            return uint.Min( ReadUint32(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint And( uint value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Or( uint value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Xor( uint value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public uint RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private uint PerformArithmeticOperation( ASC.AtomicOps op, bool overload, uint value )
        {
            if ( !UnsignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            uint result = operation( ReadUint32(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private uint PerformBitwiseOperation( ASC.AtomicOps op, bool overload, uint value )
        {
            if ( !UnsignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            uint result = operation( ReadUint32(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private uint PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !UnsignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            uint result = operation( ReadUint32(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        ~AtomicUint32()
        {
            Dispose( false );
        }

        protected override void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                base.Dispose( disposing );
            }
            disposed = true;
        }

        // Object overrides

        public override bool Equals( object? obj )
        {
            if ( obj is AtomicUint32 atomic )
            {
                return ReadUint32() == atomic.ReadUint32() &&
                    GetHashCode() == atomic.GetHashCode();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        // Overload operators
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator AtomicUint32( uint value )
        {
            return new AtomicUint32( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicUint32 atomic, uint value )
        {
            return atomic.ReadUint32() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicUint32 atomic, uint value )
        {
            return atomic.ReadUint32() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( uint value, AtomicUint32 atomic )
        {
            return atomic.ReadUint32() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( uint value, AtomicUint32 atomic )
        {
            return atomic.ReadUint32() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicUint32 atomic, uint value )
        {
            return atomic.ReadUint32() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicUint32 atomic, uint value )
        {
            return atomic.ReadUint32() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( uint value, AtomicUint32 atomic )
        {
            return value > atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( uint value, AtomicUint32 atomic )
        {
            return value < atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.ReadUint32() > atomic2.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.ReadUint32() < atomic2.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicUint32 atomic, uint value )
        {
            return atomic.ReadUint32() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicUint32 atomic, uint value )
        {
            return atomic.ReadUint32() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( uint value, AtomicUint32 atomic )
        {
            return value >= atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( uint value, AtomicUint32 atomic )
        {
            return value <= atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator +( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator +( uint value, AtomicUint32 atomic )
        {
            return value + atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator +( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Add, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator -( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator -( uint value, AtomicUint32 atomic )
        {
            return value - atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator -( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator *( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator *( uint value, AtomicUint32 atomic )
        {
            return value * atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator *( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator /( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator /( uint value, AtomicUint32 atomic )
        {
            return value / atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator /( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator %( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator %( uint value, AtomicUint32 atomic )
        {
            return value % atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator %( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator &( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator &( uint value, AtomicUint32 atomic )
        {
            return value & atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator &( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.And, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator |( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator |( uint value, AtomicUint32 atomic )
        {
            return value | atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator |( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Or, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator ^( AtomicUint32 atomic, uint value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator ^( uint value, AtomicUint32 atomic )
        {
            return value ^ atomic.ReadUint32();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator ^( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator ~( AtomicUint32 atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator <<( AtomicUint32 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator >>( AtomicUint32 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator <<( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, ( int ) atomic2.ReadUint32() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static uint operator >>( AtomicUint32 atomic1, AtomicUint32 atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.RightShift, true, ( int ) atomic2.ReadUint32() );
        }
    }
}

