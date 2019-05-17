using System;
using Jint.Collections;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Math
{
    public sealed class MathInstance : ObjectInstance
    {
        private Random _random;

        private MathInstance(Engine engine) : base(engine, "Math")
        {
        }

        public static MathInstance CreateMathObject(Engine engine)
        {
            var math = new MathInstance(engine)
            {
                Extensible = true,
                Prototype = engine.Object.PrototypeObject
            };

            return math;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(45)
            {
                ["abs"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "abs", Abs, 1, PropertyFlag.Configurable), true, false, true),
                ["acos"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "acos", Acos, 1, PropertyFlag.Configurable), true, false, true),
                ["acosh"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "acosh", Acosh, 1, PropertyFlag.Configurable), true, false, true),
                ["asin"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "asin", Asin, 1, PropertyFlag.Configurable), true, false, true),
                ["asinh"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "asinh", Asinh, 1, PropertyFlag.Configurable), true, false, true),
                ["atan"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "atan", Atan, 1, PropertyFlag.Configurable), true, false, true),
                ["atanh"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "atanh", Atanh, 1, PropertyFlag.Configurable), true, false, true),
                ["atan2"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "atan2", Atan2, 2, PropertyFlag.Configurable), true, false, true),
                ["ceil"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "ceil", Ceil, 1, PropertyFlag.Configurable), true, false, true),
                ["cos"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "cos", Cos, 1, PropertyFlag.Configurable), true, false, true),
                ["cosh"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "cosh", Cosh, 1, PropertyFlag.Configurable), true, false, true),
                ["exp"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "exp", Exp, 1, PropertyFlag.Configurable), true, false, true),
                ["expm1"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "expm1", Expm1, 1, PropertyFlag.Configurable), true, false, true),
                ["floor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "floor", Floor, 1, PropertyFlag.Configurable), true, false, true),
                ["log"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "log", Log, 1, PropertyFlag.Configurable), true, false, true),
                ["log1p"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "log1p", Log1p, 1, PropertyFlag.Configurable), true, false, true),
                ["log2"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "log2", Log2, 1, PropertyFlag.Configurable), true, false, true),
                ["log10"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "log10", Log10, 1, PropertyFlag.Configurable), true, false, true),
                ["max"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "max", Max, 2, PropertyFlag.Configurable), true, false, true),
                ["min"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "min", Min, 2, PropertyFlag.Configurable), true, false, true),
                ["pow"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "pow", Pow, 2, PropertyFlag.Configurable), true, false, true),
                ["random"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "random", Random, 0, PropertyFlag.Configurable), true, false, true),
                ["round"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "round", Round, 1, PropertyFlag.Configurable), true, false, true),
                ["fround"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "fround", Fround, 1, PropertyFlag.Configurable), true, false, true),
                ["sin"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sin", Sin, 1, PropertyFlag.Configurable), true, false, true),
                ["sinh"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sinh", Sinh, 1, PropertyFlag.Configurable), true, false, true),
                ["sqrt"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sqrt", Sqrt, 1, PropertyFlag.Configurable), true, false, true),
                ["tan"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "tan", Tan, 1, PropertyFlag.Configurable), true, false, true),
                ["tanh"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "tanh", Tanh, 1, PropertyFlag.Configurable), true, false, true),
                ["trunc"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "trunc", Truncate, 1, PropertyFlag.Configurable), true, false, true),
                ["sign"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sign", Sign, 1, PropertyFlag.Configurable), true, false, true),
                ["cbrt"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "cbrt", Cbrt, 1, PropertyFlag.Configurable), true, false, true),
                ["hypot"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "hypot", Hypot, 2, PropertyFlag.Configurable), true, false, true),
                ["imul"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "imul", Imul, 2, PropertyFlag.Configurable), true, false, true),
                ["clz32"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "clz32", Clz32, 1, PropertyFlag.Configurable), true, false, true),
                ["E"] = new PropertyDescriptor(System.Math.E, false, false, false),
                ["LN10"] = new PropertyDescriptor(System.Math.Log(10), false, false, false),
                ["LN2"] = new PropertyDescriptor(System.Math.Log(2), false, false, false),
                ["LOG2E"] = new PropertyDescriptor(System.Math.Log(System.Math.E, 2), false, false, false),
                ["LOG10E"] = new PropertyDescriptor(System.Math.Log(System.Math.E, 10), false, false, false),
                ["PI"] = new PropertyDescriptor(System.Math.PI, false, false, false),
                ["SQRT1_2"] = new PropertyDescriptor(System.Math.Sqrt(0.5), false, false, false),
                ["SQRT2"] = new PropertyDescriptor(System.Math.Sqrt(2), false, false, false)
            };
        }

        private static JsValue Abs(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return JsNumber.PositiveZero;
            }
            else if (double.IsInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }

            return System.Math.Abs(x);
        }

        private static JsValue Acos(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x) || (x > 1) || (x < -1))
            {
                return JsNumber.DoubleNaN;
            }
            else if (x == 1)
            {
                return 0;
            }

            return System.Math.Acos(x);
        }

        private static JsValue Acosh(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x) || x < 1)
            {
                return JsNumber.DoubleNaN;
            }

            return System.Math.Log(x + System.Math.Sqrt(x * x - 1.0));
        }

        private static JsValue Asin(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x) || (x > 1) || (x < -1))
            {
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            return System.Math.Asin(x);
        }

        private static JsValue Asinh(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            if (double.IsInfinity(x) || NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            return System.Math.Log(x + System.Math.Sqrt(x * x + 1.0));
        }

        private static JsValue Atan(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
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
        private static JsValue Atanh(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }

            if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            return 0.5 * System.Math.Log((1.0 + x) / (1.0 - x));
        }

        private static JsValue Atan2(JsValue thisObject, JsValue[] arguments)
        {
            var y = TypeConverter.ToNumber(arguments.At(0));
            var x = TypeConverter.ToNumber(arguments.At(1));

            // If either x or y is NaN, the result is NaN.
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                return JsNumber.DoubleNaN;
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
                    return JsNumber.PositiveZero;
                }

                // If y is +0 and x is +0, the result is +0.
                if (NumberInstance.IsPositiveZero(x))
                {
                    return JsNumber.PositiveZero;
                }

                // If y is +0 and x is −0, the result is an implementation-dependent approximation to +π.
                if (NumberInstance.IsNegativeZero(x))
                {
                    return JsNumber.PI;
                }

                // If y is +0 and x<0, the result is an implementation-dependent approximation to +π.
                if (x < 0)
                {
                    return JsNumber.PI;
                }
            }

            if (NumberInstance.IsNegativeZero(y))
            {
                // If y is −0 and x>0, the result is −0.
                if (x > 0)
                {
                    return JsNumber.NegativeZero;
                }

                // If y is −0 and x is +0, the result is −0.
                if (NumberInstance.IsPositiveZero(x))
                {
                    return JsNumber.NegativeZero;
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
                    return JsNumber.PositiveZero;
                }

                // If y>0 and y is finite and x is −∞, the result if an implementation-dependent approximation to +π.
                if (double.IsNegativeInfinity(x))
                {
                    return JsNumber.PI;
                }
            }


            // If y<0 and y is finite and x is +∞, the result is −0.
            // If y<0 and y is finite and x is −∞, the result is an implementation-dependent approximation to −π.
            if (y < 0 && !double.IsInfinity(y))
            {
                if (double.IsPositiveInfinity(x))
                {
                    return JsNumber.NegativeZero;
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
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return JsNumber.PositiveZero;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return JsNumber.NegativeZero;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return JsNumber.DoubleNegativeInfinity;
            }

            return System.Math.Ceiling(x);
        }

        private static JsValue Cos(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
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
                return JsNumber.DoubleNaN;
            }

            return System.Math.Cos(x);
        }

        private static JsValue Cosh(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
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
                return JsNumber.DoublePositiveInfinity;
            }

            return System.Math.Cosh(x);
        }

        private static JsValue Exp(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return 1;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return JsNumber.PositiveZero;
            }

            return System.Math.Exp(x);
        }

        private static JsValue Expm1(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x) || NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x) || double.IsPositiveInfinity(x))
            {
                return arguments.At(0);
            }
            if (double.IsNegativeInfinity(x))
            {
                return JsNumber.DoubleNegativeOne;
            }

            return System.Math.Exp(x) - 1.0;
        }

        private static JsValue Floor(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return JsNumber.PositiveZero;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return JsNumber.NegativeZero;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return JsNumber.DoubleNegativeInfinity;
            }

            return System.Math.Floor(x);
        }

        private static JsValue Log(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            if (x < 0)
            {
                return JsNumber.DoubleNaN;
            }
            else if (x == 0)
            {
                return JsNumber.DoubleNegativeInfinity;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (x == 1)
            {
                return JsNumber.PositiveZero;
            }

            return System.Math.Log(x);
        }

        private static JsValue Log1p(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }

            if (x < -1)
            {
                return JsNumber.DoubleNaN;
            }

            if (x == -1)
            {
                return JsNumber.DoubleNegativeInfinity;
            }

            if (x == 0 || double.IsPositiveInfinity(x))
            {
                return arguments.At(0);
            }

            return System.Math.Log(1 + x);
        }

        private static JsValue Log2(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            if (x < 0)
            {
                return JsNumber.DoubleNaN;
            }
            else if (x == 0)
            {
                return JsNumber.DoubleNegativeInfinity;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (x == 1)
            {
                return JsNumber.PositiveZero;
            }

            return System.Math.Log(x, 2);
        }

        private static JsValue Log10(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            if (x < 0)
            {
                return JsNumber.DoubleNaN;
            }
            else if (x == 0)
            {
                return JsNumber.DoubleNegativeInfinity;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (x == 1)
            {
                return JsNumber.PositiveZero;
            }

            return System.Math.Log10(x);
        }

        private static JsValue Max(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return JsNumber.DoubleNegativeInfinity;
            }

            double max = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(max))
            {
                return JsNumber.DoubleNaN;
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                var value = TypeConverter.ToNumber(arguments[i]);

                if (double.IsNaN(value))
                {
                    return JsNumber.DoubleNaN;
                }

                if (max == 0 && value == 0)
                {
                    max = NumberInstance.IsNegativeZero(value)
                        ? max
                        : value;
                }
                else
                {
                    max = System.Math.Max(max, value);
                }
            }
            return max;
        }

        private static JsValue Min(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return JsNumber.DoublePositiveInfinity;
            }

            double min = TypeConverter.ToNumber(arguments.At(0));
            for (int i = 0; i < arguments.Length; i++)
            {
                var value = TypeConverter.ToNumber(arguments[i]);
                if (min == 0 && value == 0)
                {
                    min = NumberInstance.IsNegativeZero(min)
                        ? min
                        : value;
                }
                else
                {
                    min = System.Math.Min(min, value);
                }
            }
            return min;
        }

        private static JsValue Pow(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            var y = TypeConverter.ToNumber(arguments.At(1));

            // check easy case where values are valid
            if (x > 1 && y > 1 && x < int.MaxValue && y < int.MaxValue)
            {
                return System.Math.Pow(x, y);
            }

            if (y == 0)
            {
                return 1;
            }
            
            return HandlePowUnlikely(y, x);
        }

        private static JsValue HandlePowUnlikely(double y, double x)
        {
            if (double.IsNaN(y))
            {
                return JsNumber.DoubleNaN;
            }

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }

            var absX = System.Math.Abs(x);
            if (absX > 1)
            {
                if (double.IsPositiveInfinity(y))
                {
                    return JsNumber.DoublePositiveInfinity;
                }

                if (double.IsNegativeInfinity(y))
                {
                    return JsNumber.PositiveZero;
                }
            }

            if (absX == 1)
            {
                if (double.IsInfinity(y))
                {
                    return JsNumber.DoubleNaN;
                }
            }

            if (absX < 1)
            {
                if (double.IsPositiveInfinity(y))
                {
                    return 0;
                }

                if (double.IsNegativeInfinity(y))
                {
                    return JsNumber.DoublePositiveInfinity;
                }
            }

            if (double.IsPositiveInfinity(x))
            {
                if (y > 0)
                {
                    return JsNumber.DoublePositiveInfinity;
                }

                if (y < 0)
                {
                    return JsNumber.PositiveZero;
                }
            }

            if (double.IsNegativeInfinity(x))
            {
                if (y > 0)
                {
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return JsNumber.DoubleNegativeInfinity;
                    }

                    return JsNumber.DoublePositiveInfinity;
                }

                if (y < 0)
                {
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return JsNumber.NegativeZero;
                    }

                    return JsNumber.PositiveZero;
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
                    return JsNumber.DoublePositiveInfinity;
                }
            }


            if (NumberInstance.IsNegativeZero(x))
            {
                if (y > 0)
                {
                    // If x is −0 and y>0 and y is an odd integer, the result is −0.
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return JsNumber.NegativeZero;
                    }

                    // If x is −0 and y>0 and y is not an odd integer, the result is +0.
                    return JsNumber.PositiveZero;
                }

                if (y < 0)
                {
                    // If x is −0 and y<0 and y is an odd integer, the result is −∞.
                    if (System.Math.Abs(y % 2).Equals(1))
                    {
                        return JsNumber.DoubleNegativeInfinity;
                    }

                    // If x is −0 and y<0 and y is not an odd integer, the result is +∞.
                    return JsNumber.DoublePositiveInfinity;
                }
            }

            // If x<0 and x is finite and y is finite and y is not an integer, the result is NaN.
            if (x < 0 && !double.IsInfinity(x) && !double.IsInfinity(y) && !y.Equals((int) y))
            {
                return JsNumber.DoubleNaN;
            }

            return System.Math.Pow(x, y);
        }

        private JsValue Random(JsValue thisObject, JsValue[] arguments)
        {
            if(_random == null)
            {
                _random = new Random();
            }
            
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

        private static JsValue Fround(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return (double) (float) x;
        }

        private static JsValue Sin(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return JsNumber.PositiveZero;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return JsNumber.NegativeZero;
            }
            else if (double.IsInfinity(x))
            {
                return JsNumber.DoubleNaN;
            }

            return System.Math.Sin(x);
        }

        private static JsValue Sinh(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x))
            {
                return JsNumber.PositiveZero;
            }
            else if (NumberInstance.IsNegativeZero(x))
            {
                return JsNumber.NegativeZero;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return JsNumber.DoubleNegativeInfinity;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }

            return System.Math.Sinh(x);
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

        private static JsValue Tanh(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return System.Math.Tanh(x);
        }

        private static JsValue Truncate(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
            }

            if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }

            if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }

            if (double.IsNegativeInfinity(x))
            {
                return JsNumber.DoubleNegativeInfinity;
            }

            return System.Math.Truncate(x);
        }

        private static JsValue Sign(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));

            if (double.IsNaN(x))
            {
                return JsNumber.DoubleNaN;
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
                return JsNumber.DoubleNaN;
            }
            else if (NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
            {
                return x;
            }
            else if (double.IsPositiveInfinity(x))
            {
                return JsNumber.DoublePositiveInfinity;
            }
            else if (double.IsNegativeInfinity(x))
            {
                return JsNumber.DoubleNegativeInfinity;
            }

            if (System.Math.Sign(x) >= 0)
            {
                return System.Math.Pow(x, 1.0/3.0);
            }

            return -1 * System.Math.Pow(System.Math.Abs(x), 1.0 / 3.0);
        }

        private static JsValue Hypot(JsValue thisObject, JsValue[] arguments)
        {
            double y = 0;
            for (int i = 0; i < arguments.Length; ++i)
            {
                var number = TypeConverter.ToNumber(arguments[i]);
                if (double.IsInfinity(number))
                {
                    return JsNumber.DoublePositiveInfinity;
                }
                y += number * number;
            }
            return System.Math.Sqrt(y);
        }

        private static JsValue Imul(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToInt32(arguments.At(0));
            var y = TypeConverter.ToInt32(arguments.At(1));

            return x * y;
        }

        private static JsValue Clz32(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToInt32(arguments.At(0));
            if (x < 0)
            {
                return 0;
            }

            if (x == 0)
            {
                return 32;
            }

            var res = 0;
            var shift = 16;
            while (x > 1)
            {
                var temp = x >> shift;
                if (temp != 0)
                {
                    x = temp;
                    res += shift;
                }

                shift >>= 1;
            }

            return 31 - res;
        }
    }
}
