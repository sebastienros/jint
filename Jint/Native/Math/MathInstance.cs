using System;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Math
{
    public sealed class MathInstance : ObjectInstance
    {
        private static Random _random = new Random();
        
        private MathInstance(Engine engine):base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "Math";
            }
        }

        public static MathInstance CreateMathObject(Engine engine)
        {
            var math = new MathInstance(engine);
            math.Extensible = true;
            math.Prototype = engine.Object.PrototypeObject;

            
            return math;
        }

        public void Configure()
        {
            FastAddProperty("abs", new ClrFunctionInstance(Engine, Abs), true, false, true);
            FastAddProperty("acos", new ClrFunctionInstance(Engine, Acos), true, false, true);
            FastAddProperty("asin", new ClrFunctionInstance(Engine, Asin), true, false, true);
            FastAddProperty("atan", new ClrFunctionInstance(Engine, Atan), true, false, true);
            FastAddProperty("atan2", new ClrFunctionInstance(Engine, Atan2), true, false, true);
            FastAddProperty("ceil", new ClrFunctionInstance(Engine, Ceil), true, false, true);
            FastAddProperty("cos", new ClrFunctionInstance(Engine, Cos), true, false, true);
            FastAddProperty("exp", new ClrFunctionInstance(Engine, Exp), true, false, true);
            FastAddProperty("floor", new ClrFunctionInstance(Engine, Floor), true, false, true);
            FastAddProperty("log", new ClrFunctionInstance(Engine, Log), true, false, true);
            FastAddProperty("max", new ClrFunctionInstance(Engine, Max, 2), true, false, true);
            FastAddProperty("min", new ClrFunctionInstance(Engine, Min, 2), true, false, true);
            FastAddProperty("pow", new ClrFunctionInstance(Engine, Pow, 2), true, false, true);
            FastAddProperty("random", new ClrFunctionInstance(Engine, Random), true, false, true);
            FastAddProperty("round", new ClrFunctionInstance(Engine, Round), true, false, true);
            FastAddProperty("sin", new ClrFunctionInstance(Engine, Sin), true, false, true);
            FastAddProperty("sqrt", new ClrFunctionInstance(Engine, Sqrt), true, false, true);
            FastAddProperty("tan", new ClrFunctionInstance(Engine, Tan), true, false, true);

            FastAddProperty("E", System.Math.E, false, false, false);
            FastAddProperty("LN10", System.Math.Log(10), false, false, false);
            FastAddProperty("LN2", System.Math.Log(2), false, false, false);
            FastAddProperty("LOG2E", System.Math.Log(System.Math.E, 2), false, false, false);
            FastAddProperty("LOG10E", System.Math.Log(System.Math.E, 10), false, false, false);
            FastAddProperty("PI", System.Math.PI, false, false, false);
            FastAddProperty("SQRT1_2", System.Math.Sqrt(0.5), false, false, false);
            FastAddProperty("SQRT2", System.Math.Sqrt(2), false, false, false);

        }

        private static JsValue Abs(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Abs(x);
        }

        private static JsValue Acos(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Acos(x);
        }

        private static JsValue Asin(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Asin(x);
        }

        private static JsValue Atan(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Atan(x);
        }

        private static JsValue Atan2(JsValue thisObject, JsValue[] arguments)
        {
            var y = TypeConverter.ToNumber(arguments.At(0));
            var x = TypeConverter.ToNumber(arguments.At(1));

            // If either x or y is NaN, the result is NaN.
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                return double.NaN;
            }

            if (y > 0 && x.Equals(0))
            {
                return System.Math.PI/2;
            }
            
            if (NumberInstance.IsPositiveZero(y))
            {
                // If y is +0 and x>0, the result is +0.
                if (x > 0)
                {
                    return +0;
                }

                // If y is +0 and x is +0, the result is +0.
                if (NumberInstance.IsPositiveZero(x))
                {
                    return +0;
                }

                // If y is +0 and x is −0, the result is an implementation-dependent approximation to +π.
                if (NumberInstance.IsNegativeZero(x))
                {
                    return System.Math.PI;
                }
                
                // If y is +0 and x<0, the result is an implementation-dependent approximation to +π.
                if (x < 0)
                {
                    return System.Math.PI;
                }
            }

            if (NumberInstance.IsNegativeZero(y))
            {
                // If y is −0 and x>0, the result is −0.
                if (x > 0)
                {
                    return -0;
                }

                // If y is −0 and x is +0, the result is −0.
                if (NumberInstance.IsPositiveZero(x))
                {
                    return -0;
                }

                // If y is −0 and x is −0, the result is an implementation-dependent approximation to −π.
                if (NumberInstance.IsNegativeZero(x))
                {
                    return -System.Math.PI;
                }

                // If y is −0 and x<0, the result is an implementation-dependent approximation to −π.
                if (x < 0)
                {
                    return -System.Math.PI;
                }
            }

            // If y<0 and x is +0, the result is an implementation-dependent approximation to −π/2.
            // If y<0 and x is −0, the result is an implementation-dependent approximation to −π/2.
            if (y < 0 && x.Equals(0))
            {
                return -System.Math.PI/2;
            }

            // If y>0 and y is finite and x is +∞, the result is +0.
            if (y > 0 && !double.IsInfinity(y))
            {
                if (double.IsPositiveInfinity(x))
                {
                    return +0;
                }

                // If y>0 and y is finite and x is −∞, the result if an implementation-dependent approximation to +π.
                if (double.IsNegativeInfinity(x))
                {
                    return System.Math.PI;
                }
            }


            // If y<0 and y is finite and x is +∞, the result is −0.
            // If y<0 and y is finite and x is −∞, the result is an implementation-dependent approximation to −π.
            if (y < 0 && !double.IsInfinity(y))
            {
                if (double.IsPositiveInfinity(x))
                {
                    return -0;
                }

                // If y>0 and y is finite and x is −∞, the result if an implementation-dependent approximation to +π.
                if (double.IsNegativeInfinity(x))
                {
                    return -System.Math.PI;
                }
            }
            
            // If y is +∞ and x is finite, the result is an implementation-dependent approximation to +π/2.
            if (double.IsPositiveInfinity(y) && !double.IsInfinity(x))
            {
                return System.Math.PI/2;
            }

            // If y is −∞ and x is finite, the result is an implementation-dependent approximation to −π/2.
            if (double.IsNegativeInfinity(y) && !double.IsInfinity(x))
            {
                return -System.Math.PI / 2;
            }

            // If y is +∞ and x is +∞, the result is an implementation-dependent approximation to +π/4.
            if (double.IsPositiveInfinity(y) && double.IsPositiveInfinity(x))
            {
                return System.Math.PI/4;
            }
            
            // If y is +∞ and x is −∞, the result is an implementation-dependent approximation to +3π/4.
            if (double.IsPositiveInfinity(y) && double.IsNegativeInfinity(x))
            {
                return 3 * System.Math.PI / 4;
            }
            
            // If y is −∞ and x is +∞, the result is an implementation-dependent approximation to −π/4.
            if (double.IsNegativeInfinity(y) && double.IsPositiveInfinity(x))
            {
                return -System.Math.PI / 4;
            }
            
            // If y is −∞ and x is −∞, the result is an implementation-dependent approximation to −3π/4.
            if (double.IsNegativeInfinity(y) && double.IsNegativeInfinity(x))
            {
                return - 3 * System.Math.PI / 4;
            }
            
            return System.Math.Atan2(y, x);
        }

        private static JsValue Ceil(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Ceiling(x);
        }

        private static JsValue Cos(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Cos(x);
        }

        private static JsValue Exp(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Exp(x);
        }

        private static JsValue Floor(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Floor(x);
        }

        private static JsValue Log(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Log(x);
        }

        private static JsValue Max(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Double.NegativeInfinity;
            }

            double max = TypeConverter.ToNumber(arguments.At(0));
            for (int i = 0; i < arguments.Length; i++)
            {
                max = System.Math.Max(max, TypeConverter.ToNumber(arguments[i]));
            }
            return max;
        }

        private static JsValue Min(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Double.PositiveInfinity;
            }

            double min = TypeConverter.ToNumber(arguments.At(0));
            for (int i = 0; i < arguments.Length; i++)
            {
                min = System.Math.Min(min, TypeConverter.ToNumber(arguments[i]));
            }
            return min;
        }

        private static JsValue Pow(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            var y = TypeConverter.ToNumber(arguments.At(1));

            if (double.IsNaN(y))
            {
                return double.NaN;
            }

            if (y.Equals(0))
            {
                return 1;
            }

            if (double.IsNaN(x) && !y.Equals(0))
            {
                return double.NaN;
            }

            if (System.Math.Abs(x) > 1)
            {
                if (double.IsPositiveInfinity(y))
                {
                    return double.PositiveInfinity;
                }

                if (double.IsNegativeInfinity(y))
                {
                    return +0;
                }
            }

            if (System.Math.Abs(x).Equals(1))
            {
                if (double.IsInfinity(y))
                {
                    return double.NaN;
                }
            }

            if (System.Math.Abs(x) < 1)
            {
                if (double.IsPositiveInfinity(y))
                {
                    return 0;
                }

                if (double.IsNegativeInfinity(y))
                {
                    return double.PositiveInfinity;
                }
            }

            if (double.IsPositiveInfinity(x))
            {
                if (y > 0)
                {
                    return double.PositiveInfinity;
                }

                if (y < 0)
                {
                    return +0;
                }
            }

            if (double.IsNegativeInfinity(x))
            {
                if (y > 0)
                {
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return double.NegativeInfinity;
                    }

                    return double.PositiveInfinity;
                }

                if (y < 0)
                {
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return -0;
                    }

                    return +0;
                }
            }

            if (NumberInstance.IsPositiveZero(x))
            {
                // If x is +0 and y>0, the result is +0.
                if (y > 0)
                {
                    return 0;
                }

                // If x is +0 and y<0, the result is +∞.
                if (y < 0)
                {
                    return double.PositiveInfinity;
                }
            }


            if (NumberInstance.IsNegativeZero(x))
            {
                if (y > 0)
                {
                    // If x is −0 and y>0 and y is an odd integer, the result is −0.
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return -0;
                    }

                    // If x is −0 and y>0 and y is not an odd integer, the result is +0.
                    return +0;
                }

                if (y < 0)
                {
                    // If x is −0 and y<0 and y is an odd integer, the result is −∞.
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return double.NegativeInfinity;
                    }

                    // If x is −0 and y<0 and y is not an odd integer, the result is +∞.
                    return double.PositiveInfinity;
                }
            }

            // If x<0 and x is finite and y is finite and y is not an integer, the result is NaN.
            if (x < 0 && !double.IsInfinity(x) && !double.IsInfinity(y) && !y.Equals((int)y))
            {
                return double.NaN;
            }

            return System.Math.Pow(x, y);
        }

        private static JsValue Random(JsValue thisObject, JsValue[] arguments)
        {
            return _random.NextDouble();
        }

        private static JsValue Round(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            var round = System.Math.Round(x);
            if (round.Equals(x - 0.5))
            {
                return round + 1;
            }

            return round;
        }

        private static JsValue Sin(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Sin(x);
        }

        private static JsValue Sqrt(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Sqrt(x);
        }

        private static JsValue Tan(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Tan(x);
        }


    }
}
