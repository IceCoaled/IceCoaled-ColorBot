using System.Runtime.CompilerServices;



namespace SCB.Atomics
{
    using ASC = AtomicSupportClass;

    public unsafe class AtomicInt64( long value ) : UnsafeAtomicNumerics<long>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<long, long, long>> SignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<long>();
        public Dictionary<ASC.AtomicOps, Func<long, long, long>> SignedBitwiseOperations { get; set; } = ASC.Signed64BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<long, int, long>> SignedShiftOperations { get; set; } = ASC.Signed64ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Add( long value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Subtract( long value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Multiply( long value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Divide( long value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Modulus( long value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long And( long value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Or( long value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Xor( long value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public long RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private long PerformArithmeticOperation( ASC.AtomicOps op, bool overload, long value )
        {
            if ( !SignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            long result = operation( ReadLong(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private long PerformBitwiseOperation( ASC.AtomicOps op, bool overload, long value )
        {
            if ( !SignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            long result = operation( ReadLong(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private long PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !SignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            long result = operation( ReadLong(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        ~AtomicInt64()
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
            if ( obj is AtomicInt64 atomic )
            {
                return ReadLong() == atomic.ReadLong() &&
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
        public static implicit operator AtomicInt64( long value )
        {
            return new AtomicInt64( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicInt64 atomic, long value )
        {
            return atomic.ReadLong() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicInt64 atomic, long value )
        {
            return atomic.ReadLong() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( long value, AtomicInt64 atomic )
        {
            return value == atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( long value, AtomicInt64 atomic )
        {
            return value != atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicInt64 atomic, long value )
        {
            return atomic.ReadLong() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicInt64 atomic, long value )
        {
            return atomic.ReadLong() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( long value, AtomicInt64 atomic )
        {
            return value > atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( long value, AtomicInt64 atomic )
        {
            return value < atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() > atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() < atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicInt64 atomic, long value )
        {
            return atomic.ReadLong() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicInt64 atomic, long value )
        {
            return atomic.ReadLong() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( long value, AtomicInt64 atomic )
        {
            return value >= atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( long value, AtomicInt64 atomic )
        {
            return value <= atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() >= atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() <= atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator +( AtomicInt64 atomic, long value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator +( long value, AtomicInt64 atomic )
        {
            return value + atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator +( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() + atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator -( AtomicInt64 atomic, long value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator -( long value, AtomicInt64 atomic )
        {
            return value - atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator -( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() - atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator *( AtomicInt64 atomic, long value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator *( long value, AtomicInt64 atomic )
        {
            return value * atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator *( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() * atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator /( AtomicInt64 atomic, long value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator /( long value, AtomicInt64 atomic )
        {
            return value / atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator /( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() / atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator %( AtomicInt64 atomic, long value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator %( long value, AtomicInt64 atomic )
        {
            return value % atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator %( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() % atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator &( AtomicInt64 atomic, long value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator &( long value, AtomicInt64 atomic )
        {
            return value & atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator &( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() & atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator |( AtomicInt64 atomic, long value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator |( long value, AtomicInt64 atomic )
        {
            return value | atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator |( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() | atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator ^( AtomicInt64 atomic, long value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator ^( long value, AtomicInt64 atomic )
        {
            return value ^ atomic.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator ^( AtomicInt64 atomic1, AtomicInt64 atomic2 )
        {
            return atomic1.ReadLong() ^ atomic2.ReadLong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator ~( AtomicInt64 atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator <<( AtomicInt64 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static long operator >>( AtomicInt64 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicInt64 operator ++( AtomicInt64 atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicInt64 operator --( AtomicInt64 atomic )
        {
            atomic.Decrement();
            return atomic;
        }
    }



    public unsafe class AtomicUint64( ulong value ) : UnsafeAtomicNumerics<ulong>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<ulong, ulong, ulong>> UnsignedArithmeticOperations { get; set; } = ASC.ArithmeticOperations<ulong>();
        public Dictionary<ASC.AtomicOps, Func<ulong, ulong, ulong>> UnsignedBitwiseOperations { get; set; } = ASC.Unsigned64BitwiseOperations();
        public Dictionary<ASC.AtomicOps, Func<ulong, int, ulong>> UnsignedShiftOperations { get; set; } = ASC.Unsigned64ShiftOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Add( ulong value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Subtract( ulong value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Multiply( ulong value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Divide( ulong value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Modulus( ulong value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Max( ulong value )
        {
            return ulong.Max( ReadUlong(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Min( ulong value )
        {
            return ulong.Min( ReadUlong(), value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong And( ulong value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Or( ulong value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Xor( ulong value )
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong Not()
        {
            return PerformBitwiseOperation( ASC.AtomicOps.Not, false, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong LeftShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong RightShift( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong RotateLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateLeft, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong RotateRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RotateRight, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong ShiftLeft( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.LeftShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public ulong ShiftRight( int value )
        {
            return PerformShiftOperation( ASC.AtomicOps.RightShift, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private ulong PerformArithmeticOperation( ASC.AtomicOps op, bool overload, ulong value )
        {
            if ( !UnsignedArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            ulong result = operation( ReadUlong(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private ulong PerformBitwiseOperation( ASC.AtomicOps op, bool overload, ulong value )
        {
            if ( !UnsignedBitwiseOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            ulong result = operation( ReadUlong(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private ulong PerformShiftOperation( ASC.AtomicOps op, bool overload, int value )
        {
            if ( !UnsignedShiftOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            ulong result = operation( ReadUlong(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        ~AtomicUint64()
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
            if ( obj is AtomicUint64 atomic )
            {
                return ReadUlong() == atomic.ReadUlong() &&
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
        public static implicit operator AtomicUint64( ulong value )
        {
            return new AtomicUint64( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicUint64 atomic, ulong value )
        {
            return atomic.ReadUlong() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicUint64 atomic, ulong value )
        {
            return atomic.ReadUlong() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( ulong value, AtomicUint64 atomic )
        {
            return value == atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( ulong value, AtomicUint64 atomic )
        {
            return value != atomic.ReadUlong();
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicUint64 atomic, ulong value )
        {
            return atomic.ReadUlong() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicUint64 atomic, ulong value )
        {
            return atomic.ReadUlong() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( ulong value, AtomicUint64 atomic )
        {
            return value > atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( ulong value, AtomicUint64 atomic )
        {
            return value < atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() > atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() < atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicUint64 atomic, ulong value )
        {
            return atomic.ReadUlong() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicUint64 atomic, ulong value )
        {
            return atomic.ReadUlong() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( ulong value, AtomicUint64 atomic )
        {
            return value >= atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( ulong value, AtomicUint64 atomic )
        {
            return value <= atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() >= atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() <= atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator +( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator +( ulong value, AtomicUint64 atomic )
        {
            return value + atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator +( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() + atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator -( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator -( ulong value, AtomicUint64 atomic )
        {
            return value - atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator -( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() - atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator *( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator *( ulong value, AtomicUint64 atomic )
        {
            return value * atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator *( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() * atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator /( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator /( ulong value, AtomicUint64 atomic )
        {
            return value / atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator /( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() / atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator %( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator %( ulong value, AtomicUint64 atomic )
        {
            return value % atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator %( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() % atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator &( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator &( ulong value, AtomicUint64 atomic )
        {
            return value & atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator &( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() & atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator |( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator |( ulong value, AtomicUint64 atomic )
        {
            return value | atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator |( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() | atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator ^( AtomicUint64 atomic, ulong value )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Xor, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator ^( ulong value, AtomicUint64 atomic )
        {
            return value ^ atomic.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator ^( AtomicUint64 atomic1, AtomicUint64 atomic2 )
        {
            return atomic1.ReadUlong() ^ atomic2.ReadUlong();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator ~( AtomicUint64 atomic )
        {
            return atomic.PerformBitwiseOperation( ASC.AtomicOps.Not, true, 0 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator <<( AtomicUint64 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.LeftShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static ulong operator >>( AtomicUint64 atomic, int value )
        {
            return atomic.PerformShiftOperation( ASC.AtomicOps.RightShift, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicUint64 operator ++( AtomicUint64 atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicUint64 operator --( AtomicUint64 atomic )
        {
            atomic.Decrement();
            return atomic;
        }
    }
}
