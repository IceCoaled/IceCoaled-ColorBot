using System.Runtime.CompilerServices;


namespace SCB.Atomics
{
    using ASC = AtomicSupportClass;

    public unsafe class AtomicFloat( float value ) : UnsafeAtomicNumerics<float>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<float, float, float>> FloatArithmeticOperations { get; set; } = ASC.ArithmeticOperations<float>();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Add( float value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Subtract( float value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Multiply( float value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Divide( float value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Modulus( float value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Min( float value )
        {
            float readValue = ReadFloat();
            float result = float.Min( readValue, value );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Max( float value )
        {
            float readValue = ReadFloat();
            float result = float.Max( readValue, value );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Abs()
        {
            return float.Abs( ReadFloat() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Negate()
        {
            float readValue = ReadFloat();
            Write( -readValue );
            return -readValue;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Sqrt()
        {
            float readValue = ReadFloat();
            float result = float.Sqrt( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Pow( float value )
        {
            float readValue = ReadFloat();
            float result = float.Pow( readValue, value );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Log( float value )
        {
            float readValue = ReadFloat();
            float result = float.Log( readValue, value );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Log10()
        {
            float readValue = ReadFloat();
            float result = float.Log10( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Exp()
        {
            float readValue = ReadFloat();
            float result = float.Exp( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Floor()
        {
            float readValue = ReadFloat();
            float result = float.Floor( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Ceiling()
        {
            float readValue = ReadFloat();
            float result = float.Ceiling( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public float Round()
        {
            float readValue = ReadFloat();
            float result = float.Round( readValue );
            Write( result );
            return result;
        }



        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private float PerformArithmeticOperation( ASC.AtomicOps op, bool overload, float value )
        {
            if ( !FloatArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            float result = operation( ReadFloat(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        ~AtomicFloat()
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
            if ( obj is AtomicFloat atomic )
            {
                return ReadFloat() == atomic.ReadFloat() &&
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
        public static implicit operator AtomicFloat( float value )
        {
            return new AtomicFloat( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicFloat atomic, float value )
        {
            return atomic.Abs() == float.Abs( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicFloat atomic, float value )
        {
            return atomic.Abs() != float.Abs( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( float value, AtomicFloat atomic )
        {
            return float.Abs( value ) == atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( float value, AtomicFloat atomic )
        {
            return float.Abs( value ) != atomic.Abs();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicFloat atomic, float value )
        {
            return atomic.ReadFloat() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicFloat atomic, float value )
        {
            return atomic.ReadFloat() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( float value, AtomicFloat atomic )
        {
            return value > atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( float value, AtomicFloat atomic )
        {
            return value < atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() < atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() > atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() >= atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() <= atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicFloat atomic, float value )
        {
            return atomic.ReadFloat() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicFloat atomic, float value )
        {
            return atomic.ReadFloat() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( float value, AtomicFloat atomic )
        {
            return value >= atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( float value, AtomicFloat atomic )
        {
            return value <= atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator +( AtomicFloat atomic, float value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator +( float value, AtomicFloat atomic )
        {
            return value + atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator +( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() + atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator -( AtomicFloat atomic, float value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator -( float value, AtomicFloat atomic )
        {
            return value - atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator -( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() - atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator *( AtomicFloat atomic, float value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicFloat operator *( float value, AtomicFloat atomic )
        {
            return new AtomicFloat( atomic.ReadFloat() * value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator *( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() * atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator /( AtomicFloat atomic, float value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator /( float value, AtomicFloat atomic )
        {
            return value / atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator /( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() / atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator %( AtomicFloat atomic, float value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator %( float value, AtomicFloat atomic )
        {
            return value % atomic.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static float operator %( AtomicFloat atomic1, AtomicFloat atomic2 )
        {
            return atomic1.ReadFloat() % atomic2.ReadFloat();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicFloat operator ++( AtomicFloat atomic )
        {
            return atomic.Increment();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicFloat operator --( AtomicFloat atomic )
        {
            return atomic.Decrement();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicFloat operator +( AtomicFloat atomic )
        {
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicFloat operator -( AtomicFloat atomic )
        {
            return atomic.Negate();
        }
    }





    public unsafe class AtomicDouble( double value ) : UnsafeAtomicNumerics<double>( value )
    {
        private bool disposed = false;
        public Dictionary<ASC.AtomicOps, Func<double, double, double>> DoubleArithmeticOperations { get; set; } = ASC.ArithmeticOperations<double>();

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Add( double value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Add, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Subtract( double value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Subtract, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Multiply( double value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Multiply, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Divide( double value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Divide, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Modulus( double value )
        {
            return PerformArithmeticOperation( ASC.AtomicOps.Modulus, false, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Min( double value )
        {
            double readValue = ReadDouble();
            double result = double.Min( readValue, value );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Max( double value )
        {
            double readValue = ReadDouble();
            double result = double.Max( readValue, value );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Abs()
        {
            return double.Abs( ReadDouble() );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Negate()
        {
            double readValue = ReadDouble();
            Write( -readValue );
            return -readValue;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Sqrt()
        {
            double readValue = ReadDouble();
            double result = double.Sqrt( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Pow( double value )
        {
            double readValue = ReadDouble();
            double result = double.Pow( readValue, value );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Log( double value )
        {
            double readValue = ReadDouble();
            double result = double.Log( readValue, value );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Log10()
        {
            double readValue = ReadDouble();
            double result = double.Log10( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Exp()
        {
            double readValue = ReadDouble();
            double result = double.Exp( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Floor()
        {
            double readValue = ReadDouble();
            double result = double.Floor( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Ceiling()
        {
            double readValue = ReadDouble();
            double result = double.Ceiling( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public double Round()
        {
            double readValue = ReadDouble();
            double result = double.Round( readValue );
            Write( result );
            return result;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        private double PerformArithmeticOperation( ASC.AtomicOps op, bool overload, double value )
        {
            if ( !DoubleArithmeticOperations.TryGetValue( op, out var operation ) )
            {
                throw new InvalidOperationException( "Invalid operation" );
            }

            double result = operation( ReadDouble(), value );

            if ( !overload )
            {
                Write( result );
            }
            return result;
        }

        ~AtomicDouble()
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
            if ( obj is AtomicDouble atomic )
            {
                return ReadDouble() == atomic.ReadDouble() &&
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
        public static implicit operator AtomicDouble( double value )
        {
            return new AtomicDouble( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicDouble atomic, double value )
        {
            return atomic.Abs() == double.Abs( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicDouble atomic, double value )
        {
            return atomic.Abs() != double.Abs( value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( double value, AtomicDouble atomic )
        {
            return double.Abs( value ) == atomic.Abs();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( double value, AtomicDouble atomic )
        {
            return value != atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator ==( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator !=( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return !atomic1.Equals( atomic2 );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicDouble atomic, double value )
        {
            return atomic.ReadDouble() > value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicDouble atomic, double value )
        {
            return atomic.ReadDouble() < value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( double value, AtomicDouble atomic )
        {
            return value > atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( double value, AtomicDouble atomic )
        {
            return value < atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() < atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() > atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() >= atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() <= atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( AtomicDouble atomic, double value )
        {
            return atomic.ReadDouble() >= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( AtomicDouble atomic, double value )
        {
            return atomic.ReadDouble() <= value;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator >=( double value, AtomicDouble atomic )
        {
            return value >= atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool operator <=( double value, AtomicDouble atomic )
        {
            return value <= atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator +( double value, AtomicDouble atomic )
        {
            return value + atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator +( AtomicDouble atomic, double value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Add, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator +( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() + atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator -( AtomicDouble atomic, double value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Subtract, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator -( double value, AtomicDouble atomic )
        {
            return value - atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator -( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() - atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator *( AtomicDouble atomic, double value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Multiply, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator *( double value, AtomicDouble atomic )
        {
            return value * atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator *( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() * atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator /( AtomicDouble atomic, double value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Divide, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator /( double value, AtomicDouble atomic )
        {
            return value / atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator /( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() / atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator %( AtomicDouble atomic, double value )
        {
            return atomic.PerformArithmeticOperation( ASC.AtomicOps.Modulus, true, value );
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator %( double value, AtomicDouble atomic )
        {
            return value % atomic.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static double operator %( AtomicDouble atomic1, AtomicDouble atomic2 )
        {
            return atomic1.ReadDouble() % atomic2.ReadDouble();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicDouble operator ++( AtomicDouble atomic )
        {
            return atomic.Increment();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicDouble operator --( AtomicDouble atomic )
        {
            return atomic.Decrement();
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicDouble operator +( AtomicDouble atomic )
        {
            return atomic;
        }

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static AtomicDouble operator -( AtomicDouble atomic )
        {
            return atomic.Negate();
        }
    }
}
