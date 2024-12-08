using System.Runtime.CompilerServices;


namespace SCB.Atomics
{
    using ASC = AtomicSupportClass;


    sealed unsafe partial class AtomicBool( bool value ) : UnsafeAtomicNumerics<bool>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<bool, bool, bool>> BoolOperations { get; set; } = ASC.BoolOperations();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool And( bool value )
        {
            return PerformOperation( ASC.AtomicOps.And, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool Or( bool value )
        {
            return PerformOperation( ASC.AtomicOps.Or, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool Xor( bool value )
        {
            return PerformOperation( ASC.AtomicOps.Xor, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool Not()
        {
            return PerformOperation( ASC.AtomicOps.Not, false, false );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private bool PerformOperation( ASC.AtomicOps op, bool overload, bool value )
        {
            if ( !BoolOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            bool result = operation( ReadBool(), value );

            if ( !overload )
            {
                VALUE( result );
            }
            return result;
        }

        ~AtomicBool()
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
            if ( obj is AtomicBool atomic )
            {
                return ReadBool() == atomic.ReadBool() &&
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
        public static implicit operator AtomicBool( bool value )
        {
            return new AtomicBool( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static implicit operator bool( AtomicBool atomic )
        {
            return atomic.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicBool atomic1, AtomicBool atomic2 )
        {
            return atomic1.ReadBool() == atomic2.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicBool atomic1, AtomicBool atomic2 )
        {
            return atomic1.ReadBool() != atomic2.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicBool atomic, bool value )
        {
            return atomic.ReadBool() == value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicBool atomic, bool value )
        {
            return atomic.ReadBool() != value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( bool value, AtomicBool atomic )
        {
            return value == atomic.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( bool value, AtomicBool atomic )
        {
            return value != atomic.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !( AtomicBool atomic )
        {
            return atomic.PerformOperation( ASC.AtomicOps.Not, true, false );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator &( AtomicBool atomic, bool value )
        {
            return atomic.PerformOperation( ASC.AtomicOps.And, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator &( bool value, AtomicBool atomic )
        {
            return value & atomic.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator &( AtomicBool atomic1, AtomicBool atomic2 )
        {
            return atomic1.ReadBool() & atomic2.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator |( AtomicBool atomic, bool value )
        {
            return atomic.PerformOperation( ASC.AtomicOps.Or, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator |( bool value, AtomicBool atomic )
        {
            return value | atomic.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator |( AtomicBool atomic1, AtomicBool atomic2 )
        {
            return atomic1.ReadBool() | atomic2.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator true( AtomicBool atomic )
        {
            return atomic.ReadBool();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator false( AtomicBool atomic )
        {
            return !atomic.ReadBool();
        }
    }
}
