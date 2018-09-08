using System;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Math
{
    public sealed class MathInstance : ObjectInstance
    {
        private static readonly Random _random = new Random();

        private MathInstance(Engine engine) : base(engine, "Math")
        {
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
            FastAddProperty("abs", new ClrFunctionInstance(Engine, "abs", Abs), true, false, true);
            FastAddProperty("acos", new ClrFunctionInstance(Engine, "acos", Acos), true, false, true);
            FastAddProperty("asin", new ClrFunctionInstance(Engine, "asin", Asin), true, false, true);
            FastAddProperty("atan", new ClrFunctionInstance(Engine, "atan", Atan), true, false, true);
            FastAddProperty("atan2", new ClrFunctionInstance(Engine, "atan2", Atan2), true, false, true);
            FastAddProperty("ceil", new ClrFunctionInstance(Engine, "ceil", Ceil), true, false, true);
            FastAddProperty("cos", new ClrFunctionInstance(Engine, "cos", Cos), true, false, true);
            FastAddProperty("exp", new ClrFunctionInstance(Engine, "exp", Exp), true, false, true);
            FastAddProperty("floor", new ClrFunctionInstance(Engine, "floor", Floor), true, false, true);
            FastAddProperty("log", new ClrFunctionInstance(Engine, "log", Log), true, false, true);
            FastAddProperty("max", new ClrFunctionInstance(Engine, "max", Max, 2), true, false, true);
            FastAddProperty("min", new ClrFunctionInstance(Engine, "min", Min, 2), true, false, true);
            FastAddProperty("pow", new ClrFunctionInstance(Engine, "pow", Pow, 2), true, false, true);
            FastAddProperty("random", new ClrFunctionInstance(Engine, "random", Random), true, false, true);
            FastAddProperty("round", new ClrFunctionInstance(Engine, "round", Round), true, false, true);
            FastAddProperty("sin", new ClrFunctionInstance(Engine, "sin", Sin), true, false, true);
            FastAddProperty("sqrt", new ClrFunctionInstance(Engine, "sqrt", Sqrt), true, false, true);
            FastAddProperty("tan", new ClrFunctionInstance(Engine, "tan", Tan), true, false, true);

            FastAddProperty("trunc", new ClrFunctionInstance(Engine, "trunc", Truncate, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("sign", new ClrFunctionInstance(Engine, "sign", Sign, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("cbrt", new ClrFunctionInstance(Engine, "cbrt", Cbrt, 1, PropertyFlag.Configurable), true, false, true);

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

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return +0;
            }
            else if (double.IsInfinity(x))
            {
                return double.PositiveInfinity;
            }

            return System.Math.Abs(x);
        }

        private static JsValue Acos(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x) || (x > 1) || (x < -1))
            {
                return double.NaN;
            }
            else if (x == 1)
            {
                return 0;
            }

            return System.Math.Acos(x);
        }

        private static JsValue Asin(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x) || (x > 1) || (x < -1))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            return System.Math.Asin(x);
        }

        private static JsValue Atan(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return System.Math.PI / 2;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return -System.Math.PI / 2;
            }

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

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return +0;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return -0;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return double.PositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return double.NegativeInfinity;
            }

            return System.Math.Ceiling(x);
        }

        private static JsValue Cos(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return 1;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return 1;
            }
            else if (double.IsInfinity(x))
            {
                return double.NaN;
            }

            return System.Math.Cos(x);
        }

        private static JsValue Exp(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return 1;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return double.PositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return +0;
            }

            return System.Math.Exp(x);
        }

        private static JsValue Floor(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return +0;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return -0;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return double.PositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return double.NegativeInfinity;
            }

            return System.Math.Floor(x);
        }

        private static JsValue Log(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            if (x < 0)
            {
                return double.NaN;
            }
            else if (x == 0)
            {
                return double.NegativeInfinity;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return double.PositiveInfinity;
            }
            else if (x == 1)
            {
                return +0;
            }

            return System.Math.Log(x);
        }

        private static JsValue Max(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Double.NegativeInfinity;
            }

            double max = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(max))
            {
                return double.NaN;
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                var value = TypeConverter.ToNumber(arguments[i]);

                if (double.IsNaN(value))
                {
                    return double.NaN;
                }

                max = System.Math.Max(max, value);
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

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return +0;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return -0;
            }
            else if (double.IsInfinity(x))
            {
                return double.NaN;
            }

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

        private static JsValue Truncate(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }

            if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            if (double.IsPositiveInfinity(x))
            {
                return double.PositiveInfinity;
            }

            if (double.IsNegativeInfinity(x))
            {
                return double.NegativeInfinity;
            }

            return System.Math.Truncate(x);
        }

        private static JsValue Sign(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }

            if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            if (double.IsPositiveInfinity(x))
            {
                return 1;
            }

            if (double.IsNegativeInfinity(x))
            {
                return -1;
            }

            return System.Math.Sign(x);
        }

        private static JsValue Cbrt(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return double.NaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return double.PositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return double.NegativeInfinity;
            }

            if (System.Math.Sign(x) >= 0)
            {
                return System.Math.Pow(x, 1.0/3.0);
            }

            return -1 * System.Math.Pow(System.Math.Abs(x), 1.0 / 3.0);
        }
    }
}
