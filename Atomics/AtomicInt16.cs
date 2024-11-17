using System.Runtime.CompilerServices;

namespace SCB.Atomics
{

    using ASC = AtomicSupportClass;


    public unsafe class AtomicInt16( short value ) : UnsafeAtomicNumerics<short>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<short, short, short>> SignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<short>();
        public Dictionary<ASC.AtomicOps, Func<short, int, short>> SignedBitwiseOperations { get; set; } = ASC.Signed16BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<short, int, short>> SignedShiftOperations { get; set; } = ASC.Signed16ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Add( short value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Subtract( short value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Multiply( short value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Divide( short value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Modulus( short value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Max( short value )
        {
            return short.Max( ReadInt16(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Min( short value )
        {
            return short.Min( ReadInt16(), value );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short And( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Or( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Xor( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short RightShift( int vale )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public short RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private short PerformArithmeticOperation( ASC.AtomicOps op, bool overload, short value )
        {
            if ( !SignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            short result = operation( ReadInt16(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private short PerformBitwiseOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            short result = operation( ReadInt16(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private short PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            short result = operation( ReadInt16(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }


        ~AtomicInt16()
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
            if ( obj is AtomicInt16 atomic )
            {
                return ReadInt16() == atomic.ReadInt16() &&
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
        public static implicit operator AtomicInt16( short value )
        {
            return new AtomicInt16( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicInt16 atomic, short value )
        {
            return atomic.ReadInt16() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicInt16 atomic, short value )
        {
            return atomic.ReadInt16() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( short value, AtomicInt16 atomic )
        {
            return value == atomic.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( short value, AtomicInt16 atomic )
        {
            return value != atomic.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicInt16 atomic, short value )
        {
            return atomic.ReadInt16() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicInt16 atomic, short value )
        {
            return atomic.ReadInt16() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( short value, AtomicInt16 atomic )
        {
            return value > atomic.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( short value, AtomicInt16 atomic )
        {
            return value < atomic.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return atomic1.ReadInt16() > atomic2.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return atomic1.ReadInt16() < atomic2.ReadInt16();
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicInt16 atomic, short value )
        {
            return atomic.ReadInt16() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicInt16 atomic, short value )
        {
            return atomic.ReadInt16() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( short value, AtomicInt16 atomic )
        {
            return value >= atomic.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( short value, AtomicInt16 atomic )
        {
            return value <= atomic.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return atomic1.ReadInt16() >= atomic2.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return atomic1.ReadInt16() <= atomic2.ReadInt16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator +( AtomicInt16 atomic, short value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator +( short value, AtomicInt16 atomic )
        {
            return ( short ) ( value + atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator +( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() + atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator -( short value, AtomicInt16 atomic )
        {
            return ( short ) ( value - atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator -( AtomicInt16 atomic, short value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator -( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() - atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator *( AtomicInt16 atomic, short value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator *( short value, AtomicInt16 atomic )
        {
            return ( short ) ( value * atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator *( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() * atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator /( AtomicInt16 atomic, short value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator /( short value, AtomicInt16 atomic )
        {
            return ( short ) ( value / atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator /( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() / atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator %( AtomicInt16 atomic, short value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator %( short value, AtomicInt16 atomic )
        {
            return ( short ) ( value % atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator %( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() % atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator &( AtomicInt16 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator &( int value, AtomicInt16 atomic )
        {
            return ( short ) ( value & atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator &( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() & atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator |( AtomicInt16 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator |( int value, AtomicInt16 atomic )
        {
            return ( short ) ( value | atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator |( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() | atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator ^( AtomicInt16 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator ^( int value, AtomicInt16 atomic )
        {
            return ( short ) ( value ^ atomic.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator ^( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() ^ atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator ~( AtomicInt16 atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator <<( AtomicInt16 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator >>( AtomicInt16 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator <<( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() << atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static short operator >>( AtomicInt16 atomic1, AtomicInt16 atomic2 )
        {
            return ( short ) ( atomic1.ReadInt16() >> atomic2.ReadInt16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicInt16 operator ++( AtomicInt16 atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicInt16 operator --( AtomicInt16 atomic )
        {
            atomic.Decrement();
            return atomic;
        }
    }




    public unsafe class AtomicUint16( ushort value ) : UnsafeAtomicNumerics<ushort>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<ushort, ushort, ushort>> UnsignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<ushort>();
        public Dictionary<ASC.AtomicOps, Func<ushort, int, ushort>> UnsignedBitwiseOperations { get; set; } = ASC.Unsigned16BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<ushort, int, ushort>> UnsignedShiftOperations { get; set; } = ASC.Unsigned16ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Add( ushort value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Subtract( ushort value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Multiply( ushort value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Divide( ushort value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Modulus( ushort value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Max( ushort value )
        {
            return ushort.Max( ReadUint16(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Min( ushort value )
        {
            return ushort.Min( ReadUint16(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort And( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Or( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Xor( int value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ushort RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private ushort PerformArithmeticOperation( ASC.AtomicOps op, bool overload, ushort value )
        {
            if ( !UnsignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            ushort result = operation( ReadUint16(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private ushort PerformBitwiseOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !UnsignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            ushort result = operation( ReadUint16(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private ushort PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !UnsignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            ushort result = operation( ReadUint16(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        ~AtomicUint16()
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
            if ( obj is AtomicUint16 atomic )
            {
                return ReadUint16() == atomic.ReadUint16() &&
                    GetHashCode() == atomic.GetHashCode();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator AtomicUint16( ushort value )
        {
            return new AtomicUint16( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicUint16 atomic, ushort value )
        {
            return atomic.ReadUint16() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicUint16 atomic, ushort value )
        {
            return atomic.ReadUint16() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( ushort value, AtomicUint16 atomic )
        {
            return value == atomic.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( ushort value, AtomicUint16 atomic )
        {
            return value != atomic.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicUint16 atomic, ushort value )
        {
            return atomic.ReadUint16() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicUint16 atomic, ushort value )
        {
            return atomic.ReadUint16() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( ushort value, AtomicUint16 atomic )
        {
            return value > atomic.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( ushort value, AtomicUint16 atomic )
        {
            return value < atomic.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return atomic1.ReadUint16() > atomic2.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return atomic1.ReadUint16() < atomic2.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicUint16 atomic, ushort value )
        {
            return atomic.ReadUint16() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicUint16 atomic, ushort value )
        {
            return atomic.ReadUint16() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( ushort value, AtomicUint16 atomic )
        {
            return value >= atomic.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( ushort value, AtomicUint16 atomic )
        {
            return value <= atomic.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return atomic1.ReadUint16() >= atomic2.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return atomic1.ReadUint16() <= atomic2.ReadUint16();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator +( AtomicUint16 atomic, ushort value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator +( ushort value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value + atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator +( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() + atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator -( AtomicUint16 atomic, ushort value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator -( ushort value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value - atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator -( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() - atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator *( AtomicUint16 atomic, ushort value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator *( ushort value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value * atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator *( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() * atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator /( AtomicUint16 atomic, ushort value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator /( ushort value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value / atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator /( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() / atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator %( AtomicUint16 atomic, ushort value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator %( ushort value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value % atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator %( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() % atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator &( AtomicUint16 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator &( int value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value & atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator &( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() & atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator |( AtomicUint16 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator |( int value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value | atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator |( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() | atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator ^( AtomicUint16 atomic, int value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator ^( int value, AtomicUint16 atomic )
        {
            return ( ushort ) ( value ^ atomic.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator ^( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() ^ atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator ~( AtomicUint16 atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator <<( AtomicUint16 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator >>( AtomicUint16 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator <<( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() << atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ushort operator >>( AtomicUint16 atomic1, AtomicUint16 atomic2 )
        {
            return ( ushort ) ( atomic1.ReadUint16() >> atomic2.ReadUint16() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicUint16 operator ++( AtomicUint16 atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicUint16 operator --( AtomicUint16 atomic )
        {
            atomic.Decrement();
            return atomic;
        }
    }
}
