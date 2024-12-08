using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SCB.Atomics
{

    using ASC = AtomicSupportClass;

    public unsafe class UnsafeAtomicNumerics<T> : IDisposable
    {

        private bool disposed = false;

        private volatile uint refCount = 1;

        void* allocHeader = null;

        protected ASC.AtomicStorage atomicStorage;

        protected Type VarType { get; set; } = typeof( T );

        protected bool IsUnsigned { get; set; } = ASC.IsUnsigned<T>();

        protected bool IsFloatingPoint { get; set; } = ASC.IsFloatingPoint<T>();

        protected readonly object highContentionSyncLock = new();


        /// <summary>
        /// Constructor for the atomic operations.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public UnsafeAtomicNumerics( T inputValue )
        {
            // Check if the type is supported
            if ( !ASC.IsSupported<T>() )
            {
                throw new NotSupportedException( "Type not supported" );
            }

            // Setup the atomic storage
            AllocAlignedMemory();

            // Set atomic value to check if its properly working
            atomicStorage.lAtomic = -6969;

            // Check if the atomic storage is not null
            if ( atomicStorage.lAtomic != -6969 )
            {
                throw new InvalidOperationException( "Atomic storage is null" );
            }

            // Set atomic value to all zeros first, then set the input value
            atomicStorage.ulAtomic ^= atomicStorage.ulAtomic;

            atomicStorage.lAtomic = ASC.AtomiCast<long, T>( inputValue );
        }

        private void AllocAlignedMemory()
        {
            allocHeader = NativeMemory.AlignedAlloc( 16, 16 );

            if ( allocHeader == null )
            {
                throw new InvalidOperationException( "Memory allocation failed" );
            }

            atomicStorage = new ASC.AtomicStorage();

            // Pin the atomic storage in memory
            Unsafe.Copy( allocHeader, ref atomicStorage );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected T ReadGeneric() => InternalRead<T>( ASC.RetType.GenericRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected double ReadDouble() => InternalRead<double>( ASC.RetType.DoubleRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected long ReadLong() => InternalRead<long>( ASC.RetType.LongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected ulong ReadUlong() => InternalRead<ulong>( ASC.RetType.ULongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected int ReadInt32() => InternalRead<int>( ASC.RetType.LongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected uint ReadUint32() => InternalRead<uint>( ASC.RetType.ULongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected short ReadInt16() => InternalRead<short>( ASC.RetType.LongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected ushort ReadUint16() => InternalRead<ushort>( ASC.RetType.ULongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected byte ReadByte() => InternalRead<byte>( ASC.RetType.LongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected sbyte ReadSbyte() => InternalRead<sbyte>( ASC.RetType.LongRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected float ReadFloat() => InternalRead<float>( ASC.RetType.DoubleRead );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected bool ReadBool() => InternalRead<bool>( ASC.RetType.LongRead );


        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        private C InternalRead<C>( ASC.RetType returnType )
        {

            switch ( returnType )
            {

                case ASC.RetType.DoubleRead:
                case ASC.RetType.GenericRead:
                case ASC.RetType.LongRead:
                {
                    long readValue;
                    if ( refCount == 1 )
                    {
                        readValue = Volatile.Read( ref atomicStorage.lAtomic );
                    } else if ( refCount >= 5 )
                    {
                        int spinCount = 0;
                        while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                        {
                            spinCount++;
                            Thread.SpinWait( spinCount << 1 );
                        }

                        lock ( highContentionSyncLock! )
                        {
                            readValue = atomicStorage.lAtomic;
                        }
                    } else
                    {
                        readValue = Interlocked.Read( ref atomicStorage.lAtomic );
                    }

                    return ASC.AtomiCast<C, long>( readValue );
                }
                case ASC.RetType.ULongRead:
                {
                    ulong readValue;
                    if ( refCount == 1 )
                    {
                        readValue = Volatile.Read( ref atomicStorage.ulAtomic );
                    } else if ( refCount >= 5 )
                    {
                        int spinCount = 0;
                        while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                        {
                            spinCount++;
                            Thread.SpinWait( spinCount << 1 );
                        }

                        lock ( highContentionSyncLock! )
                        {
                            readValue = atomicStorage.ulAtomic;
                        }
                    } else
                    {
                        readValue = Interlocked.Read( ref atomicStorage.ulAtomic );
                    }

                    return ASC.AtomiCast<C, ulong>( readValue );
                }
                default:
                {
                    throw new InvalidOperationException( "Invalid return type" );
                }
            }
        }




        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        private void Write( T value )
        {
            long lCastValue = 0;
            ulong ulCastValue = 0;

            if ( IsUnsigned )
            {
                ulCastValue = ASC.AtomiCast<ulong, T>( value );
            } else if ( IsFloatingPoint )
            {
                lCastValue = ASC.AtomiCast<long, T>( value );
            } else
            {
                lCastValue = ASC.AtomiCast<long, T>( value );
            }

            if ( refCount == 1 )
            {
                if ( IsUnsigned )
                {
                    Volatile.Write( ref atomicStorage.ulAtomic, ulCastValue );
                } else
                {
                    Volatile.Write( ref atomicStorage.lAtomic, lCastValue );
                }
            } else if ( refCount >= 5 )
            {
                int spinCount = 0;
                while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                {
                    spinCount++;
                    Thread.SpinWait( spinCount << 1 );
                }

                lock ( highContentionSyncLock! )
                {
                    if ( IsUnsigned )
                    {
                        atomicStorage.ulAtomic = ulCastValue;
                    } else
                    {
                        atomicStorage.lAtomic = lCastValue;
                    }
                }
            } else
            {
                if ( IsUnsigned )
                {
                    Interlocked.Exchange( ref atomicStorage.ulAtomic, ulCastValue );
                } else
                {
                    Interlocked.Exchange( ref atomicStorage.lAtomic, lCastValue );
                }
            }
        }



        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        public T CompareExchange( T value, T comparand )
        {
            if ( IsUnsigned )
            {
                ulong ulValue = ASC.AtomiCast<ulong, T>( value );
                ulong ulComparand = ASC.AtomiCast<ulong, T>( comparand );

                return ASC.AtomiCast<T, ulong>( Interlocked.CompareExchange( ref atomicStorage.ulAtomic, ulValue, ulComparand ) );
            } else
            {
                long lValue = ASC.AtomiCast<long, T>( value );
                long lComparand = ASC.AtomiCast<long, T>( comparand );

                return ASC.AtomiCast<T, long>( Interlocked.CompareExchange( ref atomicStorage.lAtomic, lValue, lComparand ) );
            }
        }



        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        public T Increment()
        {
            if ( IsFloatingPoint )
            {
                double castDbl = ReadDouble();
                var newValue = ASC.AtomiCast<T, double>( castDbl + 1.0 );
                Write( newValue );
                return newValue;
            } else if ( IsUnsigned )
            {
                return ASC.AtomiCast<T, ulong>( Interlocked.Increment( ref atomicStorage.ulAtomic ) );
            } else
            {
                return ASC.AtomiCast<T, long>( Interlocked.Increment( ref atomicStorage.lAtomic ) );
            }
        }



        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        public T Decrement()
        {
            if ( IsFloatingPoint )
            {
                double castDbl = ReadDouble();
                var newValue = ASC.AtomiCast<T, double>( castDbl - 1.0 );
                Write( newValue );
                return newValue;
            } else if ( IsUnsigned )
            {
                return ASC.AtomiCast<T, ulong>( Interlocked.Decrement( ref atomicStorage.ulAtomic ) );
            } else
            {
                return ASC.AtomiCast<T, long>( Interlocked.Decrement( ref atomicStorage.lAtomic ) );
            }
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T VALUE() => ReadGeneric();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void VALUE( T value ) => Write( value );


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool TryLock()
        {
            return Monitor.TryEnter( highContentionSyncLock! );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Unlock()
        {
            Monitor.Exit( highContentionSyncLock! );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void AddReference()
        {
            if ( refCount == uint.MaxValue )
            {
                throw new InvalidOperationException( "Reference count overflow" );
            }

            Interlocked.Increment( ref refCount );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void RemoveReference()
        {
            if ( Interlocked.Decrement( ref refCount ) == 0 )
            {
                Dispose();
            }
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
                // Deallocate the memory
                NativeMemory.AlignedFree( allocHeader );
            }

            disposed = true;
        }

        // Object overrides
        public override string ToString()
        {
            return ReadGeneric()!.ToString()!;
        }

        public override bool Equals( object? obj )
        {
            if ( obj is UnsafeAtomicNumerics<T> other )
            {

                return refCount == other.refCount &&
                       ToString() == other.ToString() &&
                       VarType == other.VarType &&
                       IsUnsigned == other.IsUnsigned &&
                       IsFloatingPoint == other.IsFloatingPoint;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine( refCount, ToString(), VarType, IsUnsigned, IsFloatingPoint );
        }
    }


    /// <summary>
    /// This has a lot of left over stuff from me making a safe atomic class.
    /// i decided to remove it because i ended up making a base class and derived classes.
    /// Thats just to much work to set up for both safe and unsafe atomic classes.
    /// </summary>
    public static class AtomicSupportClass
    {
        private readonly struct FD
        {
            public static readonly int SIGN_MASK = 0x1;
            public static readonly int EXPONENT_MASK = 0xFF;
            public static readonly int MANTISSA_MASK = 0x7FFFFF;
            public static readonly int EXPONENT_BIAS = 127;
            public static readonly int SIGN_SHIFT = 31;
            public static readonly int EXPONENT_SHIFT = 23;
            public static readonly int MAX_INT32 = 0x7FFFFFFF;
            public static readonly uint MAX_UINT32 = 0xFFFFFFFF;
        }

        readonly struct DD
        {
            public static readonly long SIGN_MASK = 0x1;
            public static readonly long EXPONENT_MASK = 0x7FF;
            public static readonly long MANTISSA_MASK = 0x000FFFFFFFFFFFFF;
            public static readonly int EXPONENT_BIAS = 1023;
            public static readonly int SIGN_SHIFT = 63;
            public static readonly int EXPONENT_SHIFT = 52;
        }



        // C# Methods to convert using BitConverter
        public static float Int32ToFloat( ref int value ) => BitConverter.Int32BitsToSingle( value );
        public static float Uint32ToFloat( ref uint value ) => BitConverter.UInt32BitsToSingle( value );

        public static double Int64ToDouble( ref long value ) => BitConverter.Int64BitsToDouble( value );

        public static double Uint64ToDouble( ref ulong value ) => BitConverter.UInt64BitsToDouble( value );

        // Methods to Get the bits of a float or double

        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        public static int FloatToInt32Bits( ref float value )
        {
            return BitConverter.SingleToInt32Bits( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        public static long DoubleToInt64Bits( ref double value )
        {
            return BitConverter.DoubleToInt64Bits( value );
        }

        // Custom methods to convert 32 and 64 bit variables to float and double

        public static float Sixty4BitsToFloat( ref long value )
        {
            return Thirty2BitsToFloatInternal( ( int ) ( value & FD.MAX_INT32 ) );
        }

        public static float Sixty4BitsToFloat( ref ulong value )
        {
            return Thirty2BitsToFloatInternal( ( int ) ( value & FD.MAX_UINT32 ) );
        }

        public static double Sixty4BitsToDouble( ref long value )
        {
            return Sixty4BitsToDoubleInternal( value );
        }

        public static double Sixty4BitsToDouble( ref ulong value )
        {
            return Sixty4BitsToDoubleInternal( ( long ) value );
        }

        public static float Thirty2BitsToFloat( ref int value )
        {
            return Thirty2BitsToFloatInternal( value );
        }

        public static float Thirty2BitsToFloat( ref uint value )
        {
            return Thirty2BitsToFloatInternal( ( int ) value );
        }

        public static int FloatToInt32( ref float value )
        {
            return FloatToInt32Internal( value );
        }

        public static long DoubleToInt64( ref double value )
        {
            return DoubleToInt64Internal( value );
        }

        public static long FloatToInt64( ref float value )
        {
            return DoubleToInt64Internal( value );
        }

        public static int DoubleToInt32( ref double value )
        {
            return FloatToInt32Internal( ( float ) value );
        }

        public static ulong FloatToUint64( ref float value )
        {
            return ( ulong ) DoubleToInt64Internal( value );
        }

        public static uint FloatToUint32( ref float value )
        {
            return ( uint ) FloatToInt32Internal( value );
        }

        public static ulong DoubleToUint64( ref double value )
        {
            return ( ulong ) DoubleToInt64Internal( value );
        }






        /// <summary>
        ///  Thread safe method to convert 64 bit variable to double
        /// </summary>
        /// <param name="value"> long variable from reading memory address.</param>
        /// <returns>Original double value from memory address.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private static float Thirty2BitsToFloatInternal( int value )
        {
            int mantissa = value & FD.MANTISSA_MASK;
            int exponent = value >> FD.EXPONENT_SHIFT & FD.EXPONENT_MASK;
            int sign = ( value >> FD.SIGN_SHIFT & FD.SIGN_MASK ) == 1 ? -1 : 1;

            if ( mantissa < 0 || mantissa > FD.MANTISSA_MASK )
            {
                throw new InvalidOperationException( "FloatLayoutData: Invalid mantissa value" );
            }

            if ( exponent < 0 || exponent > FD.EXPONENT_MASK )
            {
                throw new InvalidOperationException( "FloatLayoutData: Invalid exponent value" );
            }

            if ( sign != 1 && sign != -1 )
            {
                throw new InvalidOperationException( "FloatLayoutData: Invalid sign value" );
            }

            exponent -= FD.EXPONENT_BIAS;

            if ( exponent == -FD.EXPONENT_BIAS )
            {
                if ( mantissa == 0 )
                {
                    return sign == 1 ? 0.0f : -0.0f;
                } else
                {
                    return sign * ( mantissa / ( float ) ( 1 << FD.EXPONENT_SHIFT ) ) * ( float ) Math.Pow( 2, -( FD.EXPONENT_BIAS - 1 ) );
                }
            } else
            {
                return sign * ( 1 + mantissa / ( float ) ( 1 << FD.EXPONENT_SHIFT ) ) * ( float ) Math.Pow( 2, exponent );
            }
        }


        /// <summary>
        ///  Thread safe method to convert 64 bit variable to double
        /// </summary>
        /// <param name="value"> long variable from reading memory address.</param>
        /// <returns>Original double value from memory address.</returns>
        /// <exception cref="InvalidOperationException"></exception>

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private static double Sixty4BitsToDoubleInternal( long value )
        {
            long mantissa = value & DD.MANTISSA_MASK;
            long exponent = value >> DD.EXPONENT_SHIFT & DD.EXPONENT_MASK;
            long sign = ( value >> DD.SIGN_SHIFT & DD.SIGN_MASK ) == 1L ? -1L : 1L;

            if ( mantissa < 0L || mantissa > DD.MANTISSA_MASK )
            {
                throw new InvalidOperationException( "DoubleLayoutData: Invalid mantissa value" );
            }

            if ( exponent < 0L || exponent > DD.EXPONENT_MASK )
            {
                throw new InvalidOperationException( "DoubleLayoutData: Invalid exponent value" );
            }

            if ( sign != 1L && sign != -1L )
            {
                throw new InvalidOperationException( "DoubleLayoutData: Invalid sign value" );
            }

            exponent -= DD.EXPONENT_BIAS;

            if ( exponent == -DD.EXPONENT_BIAS )
            {
                if ( mantissa == 0 )
                {
                    return sign == 1L ? 0.0 : -0.0;
                } else
                {
                    return sign * ( mantissa / ( double ) ( 1L << DD.EXPONENT_SHIFT ) ) * Math.Pow( 2L, -( DD.EXPONENT_BIAS - 1L ) );
                }
            } else
            {
                return sign * ( 1L + mantissa / ( double ) ( 1L << DD.EXPONENT_SHIFT ) ) * Math.Pow( 2L, exponent );
            }
        }

        /// <summary>
        /// Thread safe method to get the bits of a float variable
        /// </summary>
        /// <param name="value">Original float value.</param>
        /// <returns>Returns the bit structure of the float</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private static int FloatToInt32Internal( float value )
        {
            // Get the sign of the float
            int sign = value < 0 ? 1 : 0;
            value = Math.Abs( value );

            int exponent = 0;
            int mantissa = 0;

            // If the absolute value is 0, then the exponent and mantissa are 0
            if ( value == 0 )
            {
                exponent = mantissa = 0;
            } else
            {
                // Get the exponent and mantissa bits of the float
                exponent = ( int ) Math.Floor( Math.Log( value, 2 ) ) + FD.EXPONENT_BIAS;
                mantissa = ( int ) ( ( value / Math.Pow( 2, exponent - FD.EXPONENT_BIAS ) - 1 ) * ( 1 << FD.EXPONENT_SHIFT ) );
            }

            // Combine the sign, exponent and mantissa bits to get the original float value
            int result = sign << FD.SIGN_SHIFT | exponent << FD.EXPONENT_SHIFT | mantissa;
            return result;
        }



        /// <summary>
        /// Thread safe method to get the bits of a double variable
        /// </summary>
        /// <param name="value">Original double value</param>
        /// <returns>Returns the bit structure of the double</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private static long DoubleToInt64Internal( double value )
        {
            // Get the sign of the double
            long sign = value < 0L ? 1L : 0L;

            // Get the absolute value of the double
            value = Math.Abs( value );

            long exponent = 0;
            long mantissa = 0;


            // If the absolute value is 0, then the exponent and mantissa are 0
            if ( value == 0 )
            {
                exponent = mantissa = 0;
            } else
            {
                // Get the exponent and mantissa bits of the double
                exponent = ( long ) Math.Floor( Math.Log( value, 2L ) ) + DD.EXPONENT_BIAS;
                mantissa = ( long ) ( ( value / Math.Pow( 2L, exponent - DD.EXPONENT_BIAS ) - 1L ) * ( 1L << DD.EXPONENT_SHIFT ) );
            }

            // Combine the sign, exponent and mantissa bits to get the original double value
            long result = sign << DD.SIGN_SHIFT | exponent << DD.EXPONENT_SHIFT | mantissa;
            return result;
        }

        public static long BitCastUlongToLong( ulong value )
        {
            long result = 0;

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.For( 0, 64, parallelOptions, i =>
            {
                result |= ( ( long ) value >> i & 1 ) << i;
            } );

            return result;
        }

        public static ulong BitCastLongToUlong( long value )
        {
            ulong result = 0;

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.For( 0, 64, parallelOptions, i =>
            {
                result |= ( ( ulong ) value >> i & 1 ) << i;
            } );

            return result;
        }


        [StructLayout( LayoutKind.Explicit, Size = 16 )]
        public struct AtomicStorage
        {
            [FieldOffset( 0 )]
            public long lAtomic;

            [FieldOffset( 0 )]
            public ulong ulAtomic;
        }


        /// <summary>
        /// Check if the type is supported.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool IsSupported<G>()
        {
            return typeof( G ) == typeof( bool )
            || typeof( G ) == typeof( byte )
            || typeof( G ) == typeof( short )
            || typeof( G ) == typeof( int )
            || typeof( G ) == typeof( long )
            || typeof( G ) == typeof( sbyte )
            || typeof( G ) == typeof( ushort )
            || typeof( G ) == typeof( uint )
            || typeof( G ) == typeof( float )
            || typeof( G ) == typeof( double )
            || typeof( G ) == typeof( ulong );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool IsFloatingPoint<G>()
        {
            return typeof( G ) == typeof( float )
                || typeof( G ) == typeof( double );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool IsUnsigned<G>()
        {
            return typeof( G ) == typeof( byte )
                || typeof( G ) == typeof( ushort )
                || typeof( G ) == typeof( uint )
                || typeof( G ) == typeof( ulong );
        }



        /// <summary>
        /// Safe cast using boxing and unboxing.
        /// </summary>
        public static T AtomiCast<T>( object? value )
        {
            if ( value is T t )
            {
                return t;
            } else
            {
                return default!;
            }
        }

        public unsafe static T AtomiCast<T, R>( R value )
        {
            return Unsafe.As<R, T>( ref value );
        }

        public unsafe static ref T RefAtomiCast<T, R>( R value )
        {
            return ref Unsafe.AsRef<T>( &value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool ArithmeticOperation( AtomicOps operation )
        {
            return operation switch
            {
                AtomicOps.Add => true,
                AtomicOps.Subtract => true,
                AtomicOps.Multiply => true,
                AtomicOps.Divide => true,
                AtomicOps.Modulus => true,
                _ => false,
            };
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool BitwiseOperation( AtomicOps operation )
        {
            return operation switch
            {
                AtomicOps.And => true,
                AtomicOps.Or => true,
                AtomicOps.Xor => true,
                AtomicOps.Not => true,
                _ => false,
            };
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool ShiftOperation( AtomicOps operation )
        {
            return operation switch
            {
                AtomicOps.LeftShift => true,
                AtomicOps.RightShift => true,
                AtomicOps.RotateLeft => true,
                AtomicOps.RotateRight => true,
                _ => false,
            };
        }


        public enum AtomicOps
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            Modulus,
            And,
            Or,
            Xor,
            Not,
            LeftShift,
            RightShift,
            RotateLeft,
            RotateRight,
        }

        public enum RetType
        {
            LongRead,
            ULongRead,
            DoubleRead,
            GenericRead,

        }

        public static Dictionary<AtomicOps, Func<bool, bool, bool>> BoolOperations()
        {
            return new Dictionary<AtomicOps, Func<bool, bool, bool>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => atomicValue && inputValue },
                { AtomicOps.Or, (atomicValue, inputValue) => atomicValue || inputValue },
                { AtomicOps.Xor, (atomicValue, inputValue) => atomicValue ^ inputValue },
                { AtomicOps.Not, (atomicValue, _) => !atomicValue }
            };
        }

        public static Dictionary<AtomicOps, Func<G, G, G>> ArithmeticOperations<G>() where G : INumber<G>
        {
            return new Dictionary<AtomicOps, Func<G, G, G>>()
            {
                { AtomicOps.Add, (atomicValue, inputValue) => inputValue + atomicValue },
                { AtomicOps.Subtract, (atomicValue, inputValue) => inputValue - atomicValue },
                { AtomicOps.Multiply, (atomicValue, inputValue) => inputValue * atomicValue },
                { AtomicOps.Divide, (atomicValue, inputValue) => inputValue / atomicValue },
                { AtomicOps.Modulus, (atomicValue, inputValue) => inputValue % atomicValue }
            };
        }

        public static Dictionary<AtomicOps, Func<long, long, long>> Signed64BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<long, long, long>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => atomicValue & inputValue },
                { AtomicOps.Or, (atomicValue, inputValue) => atomicValue | inputValue },
                { AtomicOps.Xor, (atomicValue, inputValue) =>  atomicValue ^ inputValue },
                { AtomicOps.Not, (atomicValue, _) => ~atomicValue }
            };
        }

        public static Dictionary<AtomicOps, Func<ulong, ulong, ulong>> Unsigned64BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<ulong, ulong, ulong>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => atomicValue & inputValue },
                { AtomicOps.Or, (atomicValue, inputValue) => atomicValue | inputValue },
                { AtomicOps.Xor, (atomicValue, inputValue) =>  atomicValue ^ inputValue },
                { AtomicOps.Not, (atomicValue, _) => ~atomicValue }
            };
        }

        public static Dictionary<AtomicOps, Func<long, int, long>> Signed64ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<long, int, long>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => ( atomicValue & 0x7FFFFFFFFFFFFFFF ) << inputValue },
                { AtomicOps.RightShift, (atomicValue, inputValue) => ( atomicValue & 0x7FFFFFFFFFFFFFFF ) >> inputValue },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) =>  ( atomicValue & 0x7FFFFFFFFFFFFFFF  ) << inputValue  |  ( atomicValue & 0x7FFFFFFFFFFFFFFF  ) >>  64 - inputValue   },
                { AtomicOps.RotateRight, (atomicValue, inputValue) =>  ( atomicValue & 0x7FFFFFFFFFFFFFFF ) >> inputValue  |  ( atomicValue & 0x7FFFFFFFFFFFFFFF ) <<  64 - inputValue   }
            };
        }

        public static Dictionary<AtomicOps, Func<ulong, int, ulong>> Unsigned64ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<ulong, int, ulong>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => atomicValue << inputValue },
                { AtomicOps.RightShift, (atomicValue, inputValue) => atomicValue >> inputValue },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) =>  atomicValue << inputValue  |  atomicValue >>  64 - inputValue   },
                { AtomicOps.RotateRight, (atomicValue, inputValue) =>  atomicValue >> inputValue  |  atomicValue <<  64 - inputValue   }
            };
        }

        public static Dictionary<AtomicOps, Func<int, int, int>> Signed32BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<int, int, int>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => atomicValue & inputValue },
                { AtomicOps.Or, (atomicValue, inputValue) => atomicValue | inputValue },
                { AtomicOps.Xor, (atomicValue, inputValue) =>  atomicValue ^ inputValue },
                { AtomicOps.Not, (atomicValue, _) => ~atomicValue }
            };
        }

        public static Dictionary<AtomicOps, Func<uint, uint, uint>> Unsigned32BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<uint, uint, uint>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => atomicValue & inputValue },
                { AtomicOps.Or, (atomicValue, inputValue) => atomicValue | inputValue },
                { AtomicOps.Xor, (atomicValue, inputValue) =>  atomicValue ^ inputValue },
                { AtomicOps.Not, (atomicValue, _) => ~atomicValue }
            };
        }

        public static Dictionary<AtomicOps, Func<int, int, int>> Signed32ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<int, int, int>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => ( atomicValue & 0x7FFFFFFF ) << inputValue },
                { AtomicOps.RightShift, (atomicValue, inputValue) => ( atomicValue & 0x7FFFFFFF ) >> inputValue },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) =>  ( atomicValue & 0x7FFFFFFF ) << inputValue  |  ( atomicValue & 0x7FFFFFFF ) >>  32 - inputValue   },
                { AtomicOps.RotateRight, (atomicValue, inputValue) =>  ( atomicValue & 0x7FFFFFFF ) >> inputValue  |  ( atomicValue & 0x7FFFFFFF ) <<  32 - inputValue   }
            };
        }

        public static Dictionary<AtomicOps, Func<uint, int, uint>> Unsigned32ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<uint, int, uint>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => atomicValue << inputValue },
                { AtomicOps.RightShift, (atomicValue, inputValue) => atomicValue >> inputValue },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) =>  atomicValue << inputValue  |  atomicValue >>  32 - inputValue   },
                { AtomicOps.RotateRight, (atomicValue, inputValue) =>  atomicValue >> inputValue  |  atomicValue <<  32 - inputValue   }
            };
        }



        public static Dictionary<AtomicOps, Func<short, int, short>> Signed16BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<short, int, short>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => ( short ) (  atomicValue & 0xFFFF  & inputValue ) },
                { AtomicOps.Or, (atomicValue, inputValue) => ( short ) (  atomicValue & 0xFFFF  | inputValue ) },
                { AtomicOps.Xor, (atomicValue, inputValue) => ( short ) (  atomicValue & 0xFFFF  ^ inputValue ) },
                { AtomicOps.Not, (atomicValue, _) => ( short )  ~( atomicValue & 0xFFFF )  }
            };
        }

        public static Dictionary<AtomicOps, Func<ushort, int, ushort>> Unsigned16BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<ushort, int, ushort>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => ( ushort ) (  atomicValue & 0xFFFF  & inputValue ) },
                { AtomicOps.Or, (atomicValue, inputValue) => ( ushort ) (  atomicValue & 0xFFFF  | inputValue ) },
                { AtomicOps.Xor, (atomicValue, inputValue) => ( ushort ) (  atomicValue & 0xFFFF  ^ inputValue ) },
                { AtomicOps.Not, (atomicValue, _) => ( ushort )  ~( atomicValue & 0x7FFF )  }
            };
        }

        public static Dictionary<AtomicOps, Func<short, int, short>> Signed16ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<short, int, short>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => ( short ) ( ( atomicValue & 0x7FFF ) << inputValue ) },
                { AtomicOps.RightShift, (atomicValue, inputValue) => ( short ) ( ( atomicValue & 0x7FFF ) >> inputValue ) },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) => ( short ) (  ( atomicValue & 0x7FFF ) << inputValue  |  ( atomicValue & 0x7FFF ) >>  16 - inputValue   ) },
                { AtomicOps.RotateRight, (atomicValue, inputValue) => ( short ) (  ( atomicValue & 0x7FFF ) >> inputValue  |  ( atomicValue & 0x7FFF ) <<  16 - inputValue   ) }
            };
        }

        public static Dictionary<AtomicOps, Func<ushort, int, ushort>> Unsigned16ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<ushort, int, ushort>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => ( ushort ) ( ( atomicValue & 0xFFFF ) << inputValue ) },
                { AtomicOps.RightShift, (atomicValue, inputValue) => ( ushort ) ( ( atomicValue & 0xFFFF ) >> inputValue ) },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) => ( ushort ) (  ( atomicValue & 0xFFFF ) << inputValue  |  ( atomicValue & 0xFFFF ) >>  16 - inputValue   ) },
                { AtomicOps.RotateRight, (atomicValue, inputValue) => ( ushort ) (  ( atomicValue & 0xFFFF ) >> inputValue  |  ( atomicValue & 0xFFFF ) <<  16 - inputValue   ) }
            };
        }

        public static Dictionary<AtomicOps, Func<sbyte, int, sbyte>> Signed8BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<sbyte, int, sbyte>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => ( sbyte ) (  atomicValue & 0x7F  & inputValue ) },
                { AtomicOps.Or, (atomicValue, inputValue) => ( sbyte ) (  atomicValue & 0x7F  | inputValue ) },
                { AtomicOps.Xor, (atomicValue, inputValue) => ( sbyte ) (  atomicValue & 0x7F  ^ inputValue ) },
                { AtomicOps.Not, (atomicValue, _) => ( sbyte )  ~( atomicValue & 0xFF )  }
            };
        }

        public static Dictionary<AtomicOps, Func<byte, int, byte>> Unsigned8BitwiseOperations()
        {
            return new Dictionary<AtomicOps, Func<byte, int, byte>>()
            {
                { AtomicOps.And, (atomicValue, inputValue) => ( byte ) (  atomicValue & 0xFF  & inputValue ) },
                { AtomicOps.Or, (atomicValue, inputValue) => ( byte ) (  atomicValue & 0xFF  | inputValue ) },
                { AtomicOps.Xor, (atomicValue, inputValue) => ( byte ) (  atomicValue & 0xFF  ^ inputValue ) },
                { AtomicOps.Not, (atomicValue, _) => ( byte )  ~( atomicValue & 0x7F )  }
            };
        }

        public static Dictionary<AtomicOps, Func<sbyte, int, sbyte>> Signed8ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<sbyte, int, sbyte>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => ( sbyte ) ( ( atomicValue & 0x7F ) << inputValue ) },
                { AtomicOps.RightShift, (atomicValue, inputValue) => ( sbyte ) ( ( atomicValue & 0x7F ) >> inputValue ) },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) => ( sbyte ) (  ( atomicValue & 0x7F ) << inputValue  |  ( atomicValue & 0x7F ) >>  8 - inputValue   ) },
                { AtomicOps.RotateRight, (atomicValue, inputValue) => ( sbyte ) (  ( atomicValue & 0x7F ) >> inputValue  |  ( atomicValue & 0x7F ) <<  8 - inputValue   ) }
            };
        }

        public static Dictionary<AtomicOps, Func<byte, int, byte>> Unsigned8ShiftOperations()
        {
            return new Dictionary<AtomicOps, Func<byte, int, byte>>()
            {
                { AtomicOps.LeftShift, (atomicValue, inputValue) => ( byte ) ( ( atomicValue & 0xFF ) << inputValue ) },
                { AtomicOps.RightShift, (atomicValue, inputValue) => ( byte ) ( ( atomicValue & 0xFF ) >> inputValue ) },
                { AtomicOps.RotateLeft, (atomicValue, inputValue) => ( byte ) (  ( atomicValue & 0xFF ) << inputValue  |  ( atomicValue & 0xFF ) >>  8 - inputValue   ) },
                { AtomicOps.RotateRight, (atomicValue, inputValue) => ( byte ) (  ( atomicValue & 0xFF ) >> inputValue  |  ( atomicValue & 0xFF ) <<  8 - inputValue   ) }
            };
        }
    }
}
