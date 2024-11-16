using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Atomics
{

    using ASC = AtomicSupportClass;

    /// <summary>
    /// Base class for atomic operations.
    /// Holds the basic functionality to kickstart the atomic operations for different types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AtomicNumerics<T> : IDisposable
    {
        [StructLayout( LayoutKind.Explicit, Size = 16 )]
        private struct AtomicStorage
        {
            [FieldOffset( 0 )]
            public long lAtomic;

            [FieldOffset( 0 )]
            public ulong ulAtomic;
        }


        private bool disposed = false;

        private volatile uint refCount = 1;

        private System.Buffers.MemoryHandle atomicStorageHandle;

        private GCHandle objHandle;

        private AtomicStorage atomicStorage;

        protected Type VarType { get; set; } = typeof( T );

        protected object highContentionSyncLock = new();

        public Overloads OperatorOverloads;



        /// <summary>
        /// Constructor for the atomic operations.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public AtomicNumerics( T inputValue )
        {
            // Check if the type is supported
            if ( !IsSupported() )
            {
                throw new NotSupportedException( "Type not supported" );
            }

            // Pin our object in memory
            objHandle = GCHandle.Alloc( this, GCHandleType.Pinned );

            // Setup the atomic storage
            AllocAlignedMemory();

            // Set atomic value
            if ( IsFloatingPoint() )
            {
                double dblValue = ASC.AtomiCast<double>( inputValue );
                long lValue = ASC.DoubleToInt64Bits( ref dblValue );
                atomicStorage.lAtomic = lValue;
            } else if ( IsUnsigned() )
            {
                atomicStorage.ulAtomic = ASC.AtomiCast<ulong>( inputValue );
            } else
            {
                atomicStorage.lAtomic = ASC.AtomiCast<long>( inputValue );
            }

            // Setup operator overload class
            OperatorOverloads = new( AtomicOperationBase );
        }


        ~AtomicNumerics()
        {
            Dispose( false );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        private T Read()
        {
            T readValue;

            if ( refCount == 1 )
            {
                if ( IsFloatingPoint() )
                {
                    long lRead = Volatile.Read( ref atomicStorage.lAtomic );
                    double castDbl = ASC.Int64ToDouble( ref lRead );
                    readValue = ASC.AtomiCast<T>( castDbl );
                } else if ( IsUnsigned() )
                {
                    readValue = ASC.AtomiCast<T>( Volatile.Read( ref atomicStorage.ulAtomic ) );
                } else
                {
                    readValue = ASC.AtomiCast<T>( atomicStorage.lAtomic );
                }
            } else if ( refCount >= 3 )
            {
                int spinCount = 0;
                while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                {
                    spinCount++;
                    Thread.SpinWait( spinCount << 1 );
                }

                lock ( highContentionSyncLock! )
                {
                    if ( IsFloatingPoint() )
                    {
                        long lRead = atomicStorage.lAtomic;
                        double castDbl = ASC.Int64ToDouble( ref lRead );
                        readValue = ASC.AtomiCast<T>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        readValue = ASC.AtomiCast<T>( atomicStorage.ulAtomic );
                    } else
                    {
                        readValue = ASC.AtomiCast<T>( atomicStorage.lAtomic );
                    }
                }
            } else
            {
                if ( IsFloatingPoint() )
                {
                    long lRead = Interlocked.Read( ref atomicStorage.lAtomic );
                    double castDbl = ASC.Int64ToDouble( ref lRead );
                    readValue = ASC.AtomiCast<T>( castDbl );
                } else if ( IsUnsigned() )
                {
                    readValue = ASC.AtomiCast<T>( Interlocked.Read( ref atomicStorage.ulAtomic ) );
                } else
                {
                    readValue = ASC.AtomiCast<T>( Interlocked.Read( ref atomicStorage.lAtomic ) );
                }
            }

            return readValue;
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.Synchronized )]
        private void Write( T value )
        {

            if ( refCount == 1 )
            {
                if ( IsFloatingPoint() )
                {
                    double dblValue = ASC.AtomiCast<double>( value );
                    long lValue = ASC.DoubleToInt64Bits( ref dblValue );
                    Volatile.Write( ref atomicStorage.lAtomic, lValue );
                } else if ( IsUnsigned() )
                {
                    Volatile.Write( ref atomicStorage.ulAtomic, ASC.AtomiCast<ulong>( value ) );
                } else
                {
                    Volatile.Write( ref atomicStorage.lAtomic, ASC.AtomiCast<long>( value ) );
                }
            } else if ( refCount >= 3 )
            {
                int spinCount = 0;
                while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                {
                    spinCount++;
                    Thread.SpinWait( spinCount << 1 );
                }

                lock ( highContentionSyncLock! )
                {
                    if ( IsFloatingPoint() )
                    {
                        double dblValue = ASC.AtomiCast<double>( value );
                        long lValue = ASC.DoubleToInt64Bits( ref dblValue );
                        atomicStorage.lAtomic = lValue;
                    } else if ( IsUnsigned() )
                    {
                        atomicStorage.ulAtomic = ASC.AtomiCast<ulong>( value );
                    } else
                    {
                        atomicStorage.lAtomic = ASC.AtomiCast<long>( value );
                    }
                }
            } else
            {
                if ( IsFloatingPoint() )
                {
                    double dblValue = ASC.AtomiCast<double>( value );
                    long lValue = ASC.DoubleToInt64Bits( ref dblValue );
                    Interlocked.Exchange( ref atomicStorage.lAtomic, lValue );
                } else if ( IsUnsigned() )
                {
                    Interlocked.Exchange( ref atomicStorage.ulAtomic, ASC.AtomiCast<ulong>( value ) );
                } else
                {
                    Interlocked.Exchange( ref atomicStorage.lAtomic, ASC.AtomiCast<long>( value ) );
                }
            }
        }


        private void AllocAlignedMemory()
        {
            Memory<byte> atomicStorageBuffer = new( new byte[ 31 ] );

            // copy atomic storage to the buffer
            atomicStorage = new AtomicStorage(); // size is 16 just in case we want to use SSE instructions
            atomicStorageBuffer.Span[ ..16 ].CopyTo( MemoryMarshal.AsBytes( MemoryMarshal.CreateSpan( ref atomicStorage, 1 ) ) );

            // Pin the atomic storage in memory
            atomicStorageHandle = atomicStorageBuffer.Pin();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T CompareExchange( T value, T comparand )
        {
            if ( VarType == typeof( double ) ||
                VarType == typeof( float ) )
            {
                double dblValue = ASC.AtomiCast<double>( value );
                double dblComparand = ASC.AtomiCast<double>( comparand );
                long lValue = ASC.DoubleToInt64Bits( ref dblValue );
                long lComparand = ASC.DoubleToInt64Bits( ref dblComparand );
                long result = Interlocked.CompareExchange( ref atomicStorage.lAtomic, lValue, lComparand );
                return ASC.AtomiCast<T>( ASC.Int64ToDouble( ref result ) );
            } else if ( VarType == typeof( ulong ) )
            {
                return ASC.AtomiCast<T>( Interlocked.CompareExchange( ref atomicStorage.ulAtomic, ASC.AtomiCast<ulong>( value ), ASC.AtomiCast<ulong>( comparand ) ) );
            } else
            {
                return ASC.AtomiCast<T>( Interlocked.CompareExchange( ref atomicStorage.lAtomic, ASC.AtomiCast<long>( value ), ASC.AtomiCast<long>( comparand ) ) );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Increment()
        {
            if ( VarType == typeof( double ) ||
                VarType == typeof( float ) )
            {
                long readValue = ASC.AtomiCast<long>( Read() );
                double castDbl = ASC.Int64ToDouble( ref readValue );
                castDbl++;
                Write( ASC.AtomiCast<T>( castDbl ) );
            } else if ( VarType == typeof( ulong ) )
            {
                Interlocked.Increment( ref atomicStorage.ulAtomic );
            } else
            {
                Interlocked.Increment( ref atomicStorage.lAtomic );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Decrement()
        {
            if ( VarType == typeof( double ) ||
                VarType == typeof( float ) )
            {
                long readValue = ASC.AtomiCast<long>( Read() );
                double castDbl = ASC.Int64ToDouble( ref readValue );
                castDbl--;
                Write( ASC.AtomiCast<T>( castDbl ) );
            } else if ( VarType == typeof( ulong ) )
            {
                Interlocked.Decrement( ref atomicStorage.ulAtomic );
            } else
            {
                Interlocked.Decrement( ref atomicStorage.lAtomic );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private T AtomicOperationBase( T value, ASC.AtomicOps operation, bool operatorOverload )
        {
            T readValue = Read();

            switch ( operation )
            {
                case ASC.AtomicOps.Add:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double>( readValue );
                        double valueDbl = ASC.AtomiCast<double>( value );
                        castDbl += valueDbl;
                        readValue = ASC.AtomiCast<T>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU += inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );

                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL += inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Subtract:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double>( readValue );
                        double valueDbl = ASC.AtomiCast<double>( value );
                        castDbl -= valueDbl;
                        readValue = ASC.AtomiCast<T>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU -= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL -= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Multiply:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double>( readValue );
                        double valueDbl = ASC.AtomiCast<double>( value );
                        castDbl *= valueDbl;
                        readValue = ASC.AtomiCast<T>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU *= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL *= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Divide:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double>( readValue );
                        double valueDbl = ASC.AtomiCast<double>( value );
                        castDbl /= valueDbl;
                        readValue = ASC.AtomiCast<T>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU /= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL /= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Modulus:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double>( readValue );
                        double valueDbl = ASC.AtomiCast<double>( value );
                        castDbl %= valueDbl;
                        readValue = ASC.AtomiCast<T>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU %= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL %= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.And:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU &= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL &= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Or:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU |= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL |= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Xor:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong>( value );
                        readValueU ^= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        long inputValue = ASC.AtomiCast<long>( value );
                        readValueL ^= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Not:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        readValueU = ~readValueU;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        readValueL = ~readValueL;
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.LeftShift:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );
                        readValueU <<= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );
                        readValueL = ( readValueL << inputValue ) & ~( 1L << 63 );
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.RightShift:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );
                        readValueU >>= inputValue;
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );
                        readValueL = ( readValueL >> inputValue ) & ~( 1L << 63 );
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.RotateLeft:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );
                        readValueU = ( readValueU << inputValue ) | ( readValueU >> ( 64 - inputValue ) );
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );

                        if ( readValueL < 0 )
                        {
                            readValueL = ( readValueL << inputValue ) | ( ( readValueL & 0xFFFFFFFF ) >> ( 64 - inputValue ) );
                        } else
                        {
                            readValueL = ( readValueL << inputValue ) | ( readValueL >> ( 64 - inputValue ) );
                        }
                        readValue = ASC.AtomiCast<T>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.RotateRight:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong>( readValue );
                        int inputValue = ASC.AtomiCast<int>( value );
                        readValueU = ( readValueU >> inputValue ) | ( readValueU << ( 64 - inputValue ) );
                        readValue = ASC.AtomiCast<T>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        int inputValue = ASC.AtomiCast<int, T>( value );

                        if ( readValueL < 0 )
                        {
                            readValueL = ( readValueL >> inputValue ) | ( ( readValueL & 0xFFFFFFFF ) << ( 64 - inputValue ) );
                        } else
                        {
                            readValueL = ( readValueL >> inputValue ) | ( readValueL << ( 64 - inputValue ) );
                        }
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                default:
                {
                    throw new InvalidOperationException( "Invalid atomic operation" );
                }
            }

            if ( operatorOverload )
            {
                return readValue;
            } else
            {
                Write( readValue );
                return readValue;
            }
        }

        // Non-operator atomic operations

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Add( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Add, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Subtract( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Subtract, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Multiply( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Multiply, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Divide( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Divide, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Modulus( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Modulus, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T And( T value ) => AtomicOperationBase( value, ASC.AtomicOps.And, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Or( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Or, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Xor( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Xor, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Not() => AtomicOperationBase( default!, ASC.AtomicOps.Not, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T LeftShift( T value ) => AtomicOperationBase( value, ASC.AtomicOps.LeftShift, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T RightShift( T value ) => AtomicOperationBase( value, ASC.AtomicOps.RightShift, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T RotateLeft( T value ) => AtomicOperationBase( value, ASC.AtomicOps.RotateLeft, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T RotateRight( T value ) => AtomicOperationBase( value, ASC.AtomicOps.RotateRight, false );


        /// <summary>
        /// This class holds the overloads for the atomic operations.
        /// Its purpose is to segregate the operator overloads from the non-operator overloads.
        /// </summary>
        /// <param name="BaseAtomicOperation"></param>
        public class Overloads( Func<T, ASC.AtomicOps, bool, T> BaseAtomicOperation )
        {
            private readonly Func<T, ASC.AtomicOps, bool, T> atomicOperation = BaseAtomicOperation;

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Add( T value ) => atomicOperation( value, ASC.AtomicOps.Add, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Subtract( T value ) => atomicOperation( value, ASC.AtomicOps.Subtract, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Multiply( T value ) => atomicOperation( value, ASC.AtomicOps.Multiply, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Divide( T value ) => atomicOperation( value, ASC.AtomicOps.Divide, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Modulus( T value ) => atomicOperation( value, ASC.AtomicOps.Modulus, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T And( T value ) => atomicOperation( value, ASC.AtomicOps.And, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Or( T value ) => atomicOperation( value, ASC.AtomicOps.Or, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Xor( T value ) => atomicOperation( value, ASC.AtomicOps.Xor, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Not() => atomicOperation( default!, ASC.AtomicOps.Not, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T LeftShift( T value ) => atomicOperation( value, ASC.AtomicOps.LeftShift, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T RightShift( T value ) => atomicOperation( value, ASC.AtomicOps.RightShift, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T RotateLeft( T value ) => atomicOperation( value, ASC.AtomicOps.RotateLeft, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T RotateRight( T value ) => atomicOperation( value, ASC.AtomicOps.RotateRight, true );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T VALUE() => Read();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void VALUE( T value ) => Write( value );


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool TryLock()
        {
            return Monitor.TryEnter( highContentionSyncLock! );
        }

        /// <summary>
        /// Add a reference to the object.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void AddReference()
        {
            if ( refCount > uint.MaxValue )
            {
                throw new InvalidOperationException( "Reference count overflow" );
            }

            if ( refCount == 0 )
            {
                throw new InvalidOperationException( "Reference count underflow" );
            }

            Interlocked.Increment( ref refCount );
        }

        /// <summary>
        /// Release a reference to the object.
        /// Potentially dispose the object if the reference count is 0.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Release()
        {
            if ( refCount == 0 )
            {
                throw new InvalidOperationException( "Reference count underflow" );
            }

            if ( Interlocked.Decrement( ref refCount ) == 0 )
            {
                Dispose();
            }
        }


        /// <summary>
        /// Type check if the type is supported.
        /// </summary>
        /// <returns></returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool IsSupported()
        {
            return ( typeof( T ) == typeof( byte ) )
            || ( typeof( T ) == typeof( short ) )
            || ( typeof( T ) == typeof( int ) )
            || ( typeof( T ) == typeof( long ) )
            || ( typeof( T ) == typeof( sbyte ) )
            || ( typeof( T ) == typeof( ushort ) )
            || ( typeof( T ) == typeof( uint ) )
            || ( typeof( T ) == typeof( float ) )
            || ( typeof( T ) == typeof( double ) )
            || ( typeof( T ) == typeof( ulong ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool IsFloatingPoint()
        {
            return ( typeof( T ) == typeof( float ) )
                || ( typeof( T ) == typeof( double ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool IsUnsigned()
        {
            return ( typeof( T ) == typeof( byte ) )
                || ( typeof( T ) == typeof( ushort ) )
                || ( typeof( T ) == typeof( uint ) )
                || ( typeof( T ) == typeof( ulong ) );
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
                // Unpin the objects from memory
                objHandle.Free();
                atomicStorageHandle.Dispose();
            }

            disposed = true;
        }

        // Object overrides
        public override string ToString()
        {
            return Read()!.ToString()!;
        }

        public override bool Equals( object? obj )
        {
            if ( obj is AtomicNumerics<T> other )
            {
                return this.refCount == other.refCount &&
                       this.atomicStorage.Equals( other.atomicStorage ) &&
                       this.VarType == other.VarType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine( disposed, refCount, atomicStorage, objHandle, atomicStorageHandle, VarType );
        }


        // Operator overloads
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator AtomicNumerics<T>( T value ) => new( value );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator T( AtomicNumerics<T> atomic ) => atomic.Read();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicNumerics<T> atomic, T value )
        {
            return atomic.Read()!.Equals( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicNumerics<T> atomic, T value )
        {
            return !atomic.Read()!.Equals( value );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicNumerics<T> operator ++( AtomicNumerics<T> atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicNumerics<T> operator --( AtomicNumerics<T> atomic )
        {
            atomic.Decrement();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator +( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Add( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator -( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Subtract( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator *( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Multiply( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator /( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Divide( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator %( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Modulus( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator &( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.And( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator |( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Or( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator ^( AtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Xor( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator <<( AtomicNumerics<T> atomic, int value )
        {
            return atomic.OperatorOverloads.LeftShift( ASC.AtomiCast<T>( value ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator >>( AtomicNumerics<T> atomic, int value )
        {
            return atomic.OperatorOverloads.RightShift( ASC.AtomiCast<T>( value ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static T operator ~( AtomicNumerics<T> atomic )
        {
            return atomic.OperatorOverloads.Not();
        }
    }


    public unsafe class UnsafeAtomicNumerics<T> : IDisposable
    {

        private bool disposed = false;

        private volatile uint refCount = 1;

        void* allocHeader = null;

        private GCHandle objHandle;

        private long atomicStorage;

        protected Type VarType { get; set; } = typeof( T );

        protected object highContentionSyncLock = new();

        public Overloads OperatorOverloads;

        /// <summary>
        /// Constructor for the atomic operations.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public UnsafeAtomicNumerics( T inputValue )
        {
            // Check if the type is supported
            if ( !IsSupported() )
            {
                throw new NotSupportedException( "Type not supported" );
            }

            // Pin our object in memory
            objHandle = GCHandle.Alloc( this, GCHandleType.Pinned );

            // Setup the atomic storage
            AllocAlignedMemory();

            // Set atomic value
            atomicStorage = ASC.AtomiCast<long, T>( inputValue );

            // Check if the atomic storage is not null
            if ( atomicStorage == 0 )
            {
                throw new InvalidOperationException( "Atomic storage is null" );
            }

            // Setup operator overload class
            OperatorOverloads = new( AtomicOperationBase );
        }

        private void AllocAlignedMemory()
        {
            allocHeader = NativeMemory.AlignedAlloc( 16, 16 );

            if ( allocHeader == null )
            {
                throw new InvalidOperationException( "Memory allocation failed" );
            }

            atomicStorage = default!;

            // Pin the atomic storage in memory
            Unsafe.Copy( allocHeader, ref atomicStorage );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private T Read()
        {
            T result;

            if ( refCount == 1 )
            {
                long readValue = Volatile.Read( ref atomicStorage );
                result = ASC.AtomiCast<T, long>( readValue );
            } else if ( refCount >= 3 )
            {
                int spinCount = 0;
                while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                {
                    spinCount++;
                    Thread.SpinWait( spinCount << 1 );
                }

                lock ( highContentionSyncLock! )
                {
                    long readValue = atomicStorage;
                    result = ASC.AtomiCast<T, long>( readValue );
                }
            } else
            {
                long readValue = Interlocked.Read( ref atomicStorage );
                result = ASC.AtomiCast<T, long>( readValue );
            }

            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private void Write( T value )
        {
            if ( refCount == 1 )
            {
                Volatile.Write( ref atomicStorage, ASC.AtomiCast<long, T>( value ) );
            } else if ( refCount >= 3 )
            {
                int spinCount = 0;
                while ( !Monitor.TryEnter( highContentionSyncLock! ) )
                {
                    spinCount++;
                    Thread.SpinWait( spinCount << 1 );
                }

                lock ( highContentionSyncLock! )
                {
                    atomicStorage = ASC.AtomiCast<long, T>( value );
                }
            } else
            {
                Interlocked.Exchange( ref atomicStorage, ASC.AtomiCast<long, T>( value ) );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T CompareExchange( T value, T comparand )
        {
            return ASC.AtomiCast<T, long>( Interlocked.CompareExchange( ref atomicStorage, ASC.AtomiCast<long, T>( value ), ASC.AtomiCast<long, T>( comparand ) ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Increment()
        {
            if ( VarType == typeof( double ) ||
                VarType == typeof( float ) )
            {
                double castDbl = ASC.AtomiCast<double, T>( Read() );
                castDbl++;
                Write( ASC.AtomiCast<T, double>( castDbl ) );
            } else
            {
                Interlocked.Increment( ref atomicStorage );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Decrement()
        {
            if ( VarType == typeof( double ) ||
                VarType == typeof( float ) )
            {

                double castDbl = ASC.AtomiCast<double, T>( Read() );
                castDbl--;
                Write( ASC.AtomiCast<T, double>( castDbl ) );
            } else
            {
                Interlocked.Decrement( ref atomicStorage );
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        protected T AtomicOperationBase( T value, ASC.AtomicOps operation, bool operatorOverload )
        {
            T readValue = Read();

            switch ( operation )
            {
                case ASC.AtomicOps.Add:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double, T>( readValue );
                        double valueDbl = ASC.AtomiCast<double, T>( value );
                        castDbl += valueDbl;
                        readValue = ASC.AtomiCast<T, double>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU += inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );

                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL += inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Subtract:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double, T>( readValue );
                        double valueDbl = ASC.AtomiCast<double, T>( value );
                        castDbl -= valueDbl;
                        readValue = ASC.AtomiCast<T, double>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU -= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL -= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Multiply:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double, T>( readValue );
                        double valueDbl = ASC.AtomiCast<double, T>( value );
                        castDbl *= valueDbl;
                        readValue = ASC.AtomiCast<T, double>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU *= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL *= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Divide:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double, T>( readValue );
                        double valueDbl = ASC.AtomiCast<double, T>( value );
                        castDbl /= valueDbl;
                        readValue = ASC.AtomiCast<T, double>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU /= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL /= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Modulus:
                {
                    if ( IsFloatingPoint() )
                    {
                        double castDbl = ASC.AtomiCast<double, T>( readValue );
                        double valueDbl = ASC.AtomiCast<double, T>( value );
                        castDbl %= valueDbl;
                        readValue = ASC.AtomiCast<T, double>( castDbl );
                    } else if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU %= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL %= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.And:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU &= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL &= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Or:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU |= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL |= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Xor:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        ulong inputValue = ASC.AtomiCast<ulong, T>( value );
                        readValueU ^= inputValue;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        long inputValue = ASC.AtomiCast<long, T>( value );
                        readValueL ^= inputValue;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.Not:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        readValueU = ~readValueU;
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        readValueL = ~readValueL;
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.LeftShift:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        readValueU <<= ASC.AtomiCast<int, T>( value );
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        readValueL = ( readValueL << ASC.AtomiCast<int, T>( value ) ) & ~( 1L << 63 );
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.RightShift:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        readValueU >>= ASC.AtomiCast<int, T>( value );
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );
                        readValueL = ( readValueL >> ASC.AtomiCast<int, T>( value ) ) & ~( 1L << 63 );
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.RotateLeft:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        readValueU = ( readValueU << ASC.AtomiCast<int, T>( value ) ) | ( readValueU >> ( 64 - ASC.AtomiCast<int, T>( value ) ) );
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );

                        if ( readValueL < 0 )
                        {
                            readValueL = ( readValueL << ASC.AtomiCast<int, T>( value ) ) | ( ( readValueL & 0xFFFFFFFF ) >> ( 64 - ASC.AtomiCast<int, T>( value ) ) );
                        } else
                        {
                            readValueL = ( readValueL << ASC.AtomiCast<int, T>( value ) ) | ( readValueL >> ( 64 - ASC.AtomiCast<int, T>( value ) ) );
                        }
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                case ASC.AtomicOps.RotateRight:
                {
                    if ( IsFloatingPoint() )
                    {
                        throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
                    }

                    if ( IsUnsigned() )
                    {
                        ulong readValueU = ASC.AtomiCast<ulong, T>( readValue );
                        readValueU = ( readValueU >> ASC.AtomiCast<int, T>( value ) ) | ( readValueU << ( 64 - ASC.AtomiCast<int, T>( value ) ) );
                        readValue = ASC.AtomiCast<T, ulong>( readValueU );
                    } else
                    {
                        long readValueL = ASC.AtomiCast<long, T>( readValue );

                        if ( readValueL < 0 )
                        {
                            readValueL = ( readValueL >> ASC.AtomiCast<int, T>( value ) ) | ( ( readValueL & 0xFFFFFFFF ) << ( 64 - ASC.AtomiCast<int, T>( value ) ) );
                        } else
                        {
                            readValueL = ( readValueL >> ASC.AtomiCast<int, T>( value ) ) | ( readValueL << ( 64 - ASC.AtomiCast<int, T>( value ) ) );
                        }
                        readValue = ASC.AtomiCast<T, long>( readValueL );
                    }
                }
                break;
                default:
                {
                    throw new InvalidOperationException( "Invalid atomic operation" );
                }
            }

            if ( operatorOverload )
            {
                return readValue;
            } else
            {
                Write( readValue );
                return readValue;
            }
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Add( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Add, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Subtract( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Subtract, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Multiply( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Multiply, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Divide( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Divide, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Modulus( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Modulus, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T And( T value ) => AtomicOperationBase( value, ASC.AtomicOps.And, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Or( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Or, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Xor( T value ) => AtomicOperationBase( value, ASC.AtomicOps.Xor, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T Not() => AtomicOperationBase( default!, ASC.AtomicOps.Not, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T LeftShift( T value ) => AtomicOperationBase( value, ASC.AtomicOps.LeftShift, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T RightShift( T value ) => AtomicOperationBase( value, ASC.AtomicOps.RightShift, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T RotateLeft( T value ) => AtomicOperationBase( value, ASC.AtomicOps.RotateLeft, false );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T RotateRight( T value ) => AtomicOperationBase( value, ASC.AtomicOps.RotateRight, false );



        /// <summary>
        /// This class is simply a wrapper for the atomic operations to be used with operator overloads.
        /// Its purpose is to segregate the atomic operations from the operator overloads.
        /// </summary>
        /// <param name="BaseAtomicOperation"></param>
        public class Overloads( Func<T, ASC.AtomicOps, bool, T> BaseAtomicOperation )
        {
            private readonly Func<T, ASC.AtomicOps, bool, T> AtomicOperation = BaseAtomicOperation;

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Add( T value ) => AtomicOperation( value, ASC.AtomicOps.Add, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Subtract( T value ) => AtomicOperation( value, ASC.AtomicOps.Subtract, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Multiply( T value ) => AtomicOperation( value, ASC.AtomicOps.Multiply, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Divide( T value ) => AtomicOperation( value, ASC.AtomicOps.Divide, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Modulus( T value ) => AtomicOperation( value, ASC.AtomicOps.Modulus, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T And( T value ) => AtomicOperation( value, ASC.AtomicOps.And, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Or( T value ) => AtomicOperation( value, ASC.AtomicOps.Or, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Xor( T value ) => AtomicOperation( value, ASC.AtomicOps.Xor, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T Not() => AtomicOperation( default!, ASC.AtomicOps.Not, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T LeftShift( T value ) => AtomicOperation( value, ASC.AtomicOps.LeftShift, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T RightShift( T value ) => AtomicOperation( value, ASC.AtomicOps.RightShift, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T RotateLeft( T value ) => AtomicOperation( value, ASC.AtomicOps.RotateLeft, true );

            [MethodImpl( MethodImplOptions.AggressiveInlining )]
            public T RotateRight( T value ) => AtomicOperation( value, ASC.AtomicOps.RotateRight, true );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public T VALUE() => Read();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void VALUE( T value ) => Write( value );


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool TryLock()
        {
            return Monitor.TryEnter( highContentionSyncLock! );
        }

        /// <summary>
        /// Check if the type is supported.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool IsSupported()
        {
            return ( typeof( T ) == typeof( byte ) )
            || ( typeof( T ) == typeof( short ) )
            || ( typeof( T ) == typeof( int ) )
            || ( typeof( T ) == typeof( long ) )
            || ( typeof( T ) == typeof( sbyte ) )
            || ( typeof( T ) == typeof( ushort ) )
            || ( typeof( T ) == typeof( uint ) )
            || ( typeof( T ) == typeof( float ) )
            || ( typeof( T ) == typeof( double ) )
            || ( typeof( T ) == typeof( ulong ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool IsFloatingPoint()
        {
            return ( typeof( T ) == typeof( float ) )
                || ( typeof( T ) == typeof( double ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool IsUnsigned()
        {
            return ( typeof( T ) == typeof( byte ) )
                || ( typeof( T ) == typeof( ushort ) )
                || ( typeof( T ) == typeof( uint ) )
                || ( typeof( T ) == typeof( ulong ) );
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
                // Unpin the objects from memory
                objHandle.Free();
                NativeMemory.AlignedFree( allocHeader );
            }

            disposed = true;
        }

        // Object overrides
        public override string ToString()
        {
            return Read()!.ToString()!;
        }

        public override bool Equals( object? obj )
        {
            if ( obj is UnsafeAtomicNumerics<T> other )
            {
                return this.refCount == other.refCount &&
                       this.atomicStorage == other.atomicStorage &&
                       this.VarType == other.VarType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine( disposed, refCount, ( ulong ) allocHeader, objHandle, atomicStorage, highContentionSyncLock, VarType );
        }

        // Operator overloads
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator UnsafeAtomicNumerics<T>( T value ) => new( value );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator T( UnsafeAtomicNumerics<T> atomic ) => atomic.Read();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe bool operator ==( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return atomic.Read()!.Equals( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe bool operator !=( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return !atomic.Read()!.Equals( value );
        }


        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe UnsafeAtomicNumerics<T> operator ++( UnsafeAtomicNumerics<T> atomic )
        {
            atomic.Increment();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe UnsafeAtomicNumerics<T> operator --( UnsafeAtomicNumerics<T> atomic )
        {
            atomic.Decrement();
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator +( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Add( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator -( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Subtract( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator *( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Multiply( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator /( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Divide( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator %( UnsafeAtomicNumerics<T> atomic, T value )
        {
            return atomic.OperatorOverloads.Modulus( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator &( UnsafeAtomicNumerics<T> atomic, T value )
        {
            if ( typeof( T ) == typeof( double ) ||
                typeof( T ) == typeof( float ) )
            {
                throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
            }
            return atomic.OperatorOverloads.And( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator |( UnsafeAtomicNumerics<T> atomic, T value )
        {
            if ( typeof( T ) == typeof( double ) ||
                typeof( T ) == typeof( float ) )
            {
                throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
            }
            return atomic.OperatorOverloads.Or( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator ^( UnsafeAtomicNumerics<T> atomic, T value )
        {
            if ( typeof( T ) == typeof( double ) ||
                typeof( T ) == typeof( float ) )
            {
                throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
            }
            return atomic.OperatorOverloads.Xor( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator <<( UnsafeAtomicNumerics<T> atomic, int value )
        {
            if ( typeof( T ) == typeof( double ) ||
                typeof( T ) == typeof( float ) )
            {
                throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
            }
            return atomic.OperatorOverloads.LeftShift( ASC.AtomiCast<T>( value ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator >>( UnsafeAtomicNumerics<T> atomic, int value )
        {
            if ( typeof( T ) == typeof( double ) ||
                typeof( T ) == typeof( float ) )
            {
                throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
            }
            return atomic.OperatorOverloads.RightShift( ASC.AtomiCast<T>( value ) );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static unsafe T operator ~( UnsafeAtomicNumerics<T> atomic )
        {
            if ( typeof( T ) == typeof( double ) ||
                typeof( T ) == typeof( float ) )
            {
                throw new InvalidOperationException( "Bitwise operations are not supported for floating point types" );
            }
            return atomic.OperatorOverloads.Not();
        }
    }



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
            int exponent = ( value >> FD.EXPONENT_SHIFT ) & FD.EXPONENT_MASK;
            int sign = ( ( value >> FD.SIGN_SHIFT ) & FD.SIGN_MASK ) == 1 ? -1 : 1;

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
                    return ( sign * ( mantissa / ( float ) ( 1 << FD.EXPONENT_SHIFT ) ) * ( float ) Math.Pow( 2, -( FD.EXPONENT_BIAS - 1 ) ) );
                }
            } else
            {
                return ( sign * ( 1 + ( mantissa / ( float ) ( 1 << FD.EXPONENT_SHIFT ) ) ) * ( float ) Math.Pow( 2, exponent ) );
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
            long exponent = ( value >> DD.EXPONENT_SHIFT ) & DD.EXPONENT_MASK;
            long sign = ( ( value >> DD.SIGN_SHIFT ) & DD.SIGN_MASK ) == 1L ? -1L : 1L;

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
                    return ( sign * ( mantissa / ( double ) ( 1L << DD.EXPONENT_SHIFT ) ) * Math.Pow( 2L, -( DD.EXPONENT_BIAS - 1L ) ) );
                }
            } else
            {
                return ( sign * ( 1L + ( mantissa / ( double ) ( 1L << DD.EXPONENT_SHIFT ) ) ) * Math.Pow( 2L, exponent ) );
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
            int result = ( ( sign << FD.SIGN_SHIFT ) | ( exponent << FD.EXPONENT_SHIFT ) | mantissa );
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
            long result = ( ( sign << DD.SIGN_SHIFT ) | ( exponent << DD.EXPONENT_SHIFT ) | mantissa );
            return result;
        }

        public static long BitCastUlongToLong( ulong value )
        {
            Int64 result = 0;

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.For( 0, 64, parallelOptions, i =>
            {
                result |= ( ( ( long ) value >> i ) & 1 ) << i;
            } );

            return result;
        }

        public static ulong BitCastLongToUlong( long value )
        {
            UInt64 result = 0;

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.For( 0, 64, parallelOptions, i =>
            {
                result |= ( ( ( ulong ) value >> i ) & 1 ) << i;
            } );

            return result;
        }


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
    }
}
