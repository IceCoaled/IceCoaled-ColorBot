using System.Runtime.CompilerServices;

namespace SCB.Atomics
{

    using ASC = AtomicSupportClass;


    sealed unsafe partial class AtomicByte( byte value ) : UnsafeAtomicNumerics<byte>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<byte, byte, byte>> UnsignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<byte>();
        public Dictionary<ASC.AtomicOps, Func<byte, int, byte>> UnsignedBitwiseOperations { get; set; } = ASC.Unsigned8BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<byte, int, byte>> UnsignedShiftOperations { get; set; } = ASC.Unsigned8ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Add( byte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Subtract( byte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Multiply( byte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Divide( byte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Modulus( byte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Max( byte value )
        {
            return byte.Max( ReadByte(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Min( byte value )
        {
            return byte.Min( ReadByte(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte And( byte value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Or( byte value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Xor( byte value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public byte RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private byte PerformArithmeticOperation( ASC.AtomicOps op, bool overload, byte value )
        {
            if ( !UnsignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            byte result = operation( ReadByte(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private byte PerformBitwiseOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !UnsignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            byte result = operation( ReadByte(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private byte PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !UnsignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            byte result = operation( ReadByte(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        ~AtomicByte()
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
            if ( obj is AtomicByte atomic )
            {
                return ReadByte() == atomic.ReadByte() &&
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
        public static implicit operator AtomicByte( byte value )
        {
            return new AtomicByte( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicByte atomic, byte value )
        {
            return atomic.ReadByte() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicByte atomic, byte value )
        {
            return atomic.ReadByte() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( byte value, AtomicByte atomic )
        {
            return value != atomic.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( byte value, AtomicByte atomic )
        {
            return value == atomic.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicByte atomic, byte value )
        {
            return atomic.ReadByte() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicByte atomic, byte value )
        {
            return atomic.ReadByte() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( byte value, AtomicByte atomic )
        {
            return value > atomic.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( byte value, AtomicByte atomic )
        {
            return value < atomic.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.ReadByte() < atomic2.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.ReadByte() > atomic2.ReadByte();
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicByte atomic, byte value )
        {
            return atomic.ReadByte() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicByte atomic, byte value )
        {
            return atomic.ReadByte() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( byte value, AtomicByte atomic )
        {
            return value >= atomic.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( byte value, AtomicByte atomic )
        {
            return value <= atomic.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.ReadByte() >= atomic2.ReadByte();
        }
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.ReadByte() <= atomic2.ReadByte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator +( AtomicByte atomic, byte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator +( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value + atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator +( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Add, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator -( AtomicByte atomic, byte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator -( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value - atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator -( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator *( AtomicByte atomic, byte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator *( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value * atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator *( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator /( AtomicByte atomic, byte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator /( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value / atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator /( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator %( AtomicByte atomic, byte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator %( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value % atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator %( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator &( AtomicByte atomic, byte value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator &( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value & atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator &( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.And, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator |( AtomicByte atomic, byte value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator |( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value | atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator |( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Or, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator ^( AtomicByte atomic, byte value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator ^( byte value, AtomicByte atomic )
        {
            return ( byte ) ( value ^ atomic.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator ^( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator ~( AtomicByte atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator <<( AtomicByte atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator >>( AtomicByte atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator <<( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, atomic2.ReadByte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static byte operator >>( AtomicByte atomic1, AtomicByte atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.RightShift, true, atomic2.ReadByte() );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicByte operator ++( AtomicByte atomic )
        {
            return atomic.Increment();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicByte operator --( AtomicByte atomic )
        {
            return atomic.Decrement();
        }
    }


    sealed unsafe partial class AtomicSbyte( sbyte value ) : UnsafeAtomicNumerics<sbyte>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<sbyte, sbyte, sbyte>> SignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<sbyte>();
        public Dictionary<ASC.AtomicOps, Func<sbyte, int, sbyte>> SignedBitwiseOperations { get; set; } = ASC.Signed8BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<sbyte, int, sbyte>> SignedShiftOperations { get; set; } = ASC.Signed8ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Add( sbyte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Subtract( sbyte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Multiply( sbyte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Divide( sbyte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Modulus( sbyte value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Max( sbyte value )
        {
            return sbyte.Max( ReadSbyte(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Min( sbyte value )
        {
            return sbyte.Min( ReadSbyte(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte And( sbyte value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Or( sbyte value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Xor( sbyte value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public sbyte RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private sbyte PerformArithmeticOperation( ASC.AtomicOps op, bool overload, sbyte value )
        {
            if ( !SignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            sbyte result = operation( ReadSbyte(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private sbyte PerformBitwiseOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            sbyte result = operation( ReadSbyte(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private sbyte PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            sbyte result = operation( ReadSbyte(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        ~AtomicSbyte()
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
            if ( obj is AtomicSbyte atomic )
            {
                return ReadSbyte() == atomic.ReadSbyte() &&
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
        public static implicit operator AtomicSbyte( sbyte value )
        {
            return new AtomicSbyte( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicSbyte atomic, sbyte value )
        {
            return atomic.ReadSbyte() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicSbyte atomic, sbyte value )
        {
            return atomic.ReadSbyte() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( sbyte value, AtomicSbyte atomic )
        {
            return value == atomic.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( sbyte value, AtomicSbyte atomic )
        {
            return value != atomic.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicSbyte atomic, sbyte value )
        {
            return atomic.ReadSbyte() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicSbyte atomic, sbyte value )
        {
            return atomic.ReadSbyte() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( sbyte value, AtomicSbyte atomic )
        {
            return value > atomic.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( sbyte value, AtomicSbyte atomic )
        {
            return value < atomic.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.ReadSbyte() > atomic2.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.ReadSbyte() < atomic2.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicSbyte atomic, sbyte value )
        {
            return atomic.ReadSbyte() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicSbyte atomic, sbyte value )
        {
            return atomic.ReadSbyte() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( sbyte value, AtomicSbyte atomic )
        {
            return value >= atomic.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( sbyte value, AtomicSbyte atomic )
        {
            return value <= atomic.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.ReadSbyte() >= atomic2.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.ReadSbyte() <= atomic2.ReadSbyte();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator +( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator +( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value + atomic.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator +( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Add, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator -( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator -( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value - atomic.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator -( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator *( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator *( sbyte value, AtomicSbyte atomic )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator *( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator /( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator /( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value / atomic.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator %( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator %( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value % atomic.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator %( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator &( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator &( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.And, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator &( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value & atomic.ReadSbyte() );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator |( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value | atomic.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator |( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator |( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Or, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator ^( AtomicSbyte atomic, sbyte value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator ^( sbyte value, AtomicSbyte atomic )
        {
            return ( sbyte ) ( value ^ atomic.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator ^( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator ~( AtomicSbyte atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator <<( AtomicSbyte atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator >>( AtomicSbyte atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator <<( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, atomic2.ReadSbyte() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static sbyte operator >>( AtomicSbyte atomic1, AtomicSbyte atomic2 )
        {
            return atomic1.PerformShiftOperation( ASC.AtomicOps.RightShift, true, atomic2.ReadSbyte() );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicSbyte operator ++( AtomicSbyte atomic )
        {
            return atomic.Increment();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicSbyte operator --( AtomicSbyte atomic )
        {
            return atomic.Decrement();
        }
    }
}
