using Jint.Collections;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Math;

internal sealed class MathInstance : ObjectInstance
{
    private Random? _random;

    internal MathInstance(Engine engine, ObjectPrototype objectPrototype) : base(engine)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(45, checkExistingKeys: false)
        {
            ["E"] = new PropertyDescriptor(System.Math.E, PropertyFlag.AllForbidden),
            ["LN10"] = new PropertyDescriptor(System.Math.Log(10), PropertyFlag.AllForbidden),
            ["LN2"] = new PropertyDescriptor(System.Math.Log(2), PropertyFlag.AllForbidden),
            ["LOG10E"] = new PropertyDescriptor(System.Math.Log(System.Math.E, 10), PropertyFlag.AllForbidden),
            ["LOG2E"] = new PropertyDescriptor(System.Math.Log(System.Math.E, 2), PropertyFlag.AllForbidden),
            ["PI"] = new PropertyDescriptor(System.Math.PI, PropertyFlag.AllForbidden),
            ["SQRT1_2"] = new PropertyDescriptor(System.Math.Sqrt(0.5), PropertyFlag.AllForbidden),
            ["SQRT2"] = new PropertyDescriptor(System.Math.Sqrt(2), PropertyFlag.AllForbidden),
            ["abs"] = new PropertyDescriptor(new ClrFunction(Engine, "abs", Abs, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["acos"] = new PropertyDescriptor(new ClrFunction(Engine, "acos", Acos, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["acosh"] = new PropertyDescriptor(new ClrFunction(Engine, "acosh", Acosh, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["asin"] = new PropertyDescriptor(new ClrFunction(Engine, "asin", Asin, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["asinh"] = new PropertyDescriptor(new ClrFunction(Engine, "asinh", Asinh, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["atan"] = new PropertyDescriptor(new ClrFunction(Engine, "atan", Atan, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["atan2"] = new PropertyDescriptor(new ClrFunction(Engine, "atan2", Atan2, 2, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["atanh"] = new PropertyDescriptor(new ClrFunction(Engine, "atanh", Atanh, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["cbrt"] = new PropertyDescriptor(new ClrFunction(Engine, "cbrt", Cbrt, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["ceil"] = new PropertyDescriptor(new ClrFunction(Engine, "ceil", Ceil, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["clz32"] = new PropertyDescriptor(new ClrFunction(Engine, "clz32", Clz32, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["cos"] = new PropertyDescriptor(new ClrFunction(Engine, "cos", Cos, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["cosh"] = new PropertyDescriptor(new ClrFunction(Engine, "cosh", Cosh, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["exp"] = new PropertyDescriptor(new ClrFunction(Engine, "exp", Exp, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["expm1"] = new PropertyDescriptor(new ClrFunction(Engine, "expm1", Expm1, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["f16round"] = new PropertyDescriptor(new ClrFunction(Engine, "f16round", F16Round, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["floor"] = new PropertyDescriptor(new ClrFunction(Engine, "floor", Floor, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["fround"] = new PropertyDescriptor(new ClrFunction(Engine, "fround", Fround, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["hypot"] = new PropertyDescriptor(new ClrFunction(Engine, "hypot", Hypot, 2, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["imul"] = new PropertyDescriptor(new ClrFunction(Engine, "imul", Imul, 2, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["log"] = new PropertyDescriptor(new ClrFunction(Engine, "log", Log, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["log10"] = new PropertyDescriptor(new ClrFunction(Engine, "log10", Log10, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["log1p"] = new PropertyDescriptor(new ClrFunction(Engine, "log1p", Log1p, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["log2"] = new PropertyDescriptor(new ClrFunction(Engine, "log2", Log2, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["max"] = new PropertyDescriptor(new ClrFunction(Engine, "max", Max, 2, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["min"] = new PropertyDescriptor(new ClrFunction(Engine, "min", Min, 2, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["pow"] = new PropertyDescriptor(new ClrFunction(Engine, "pow", Pow, 2, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["random"] = new PropertyDescriptor(new ClrFunction(Engine, "random", Random, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["round"] = new PropertyDescriptor(new ClrFunction(Engine, "round", Round, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["sign"] = new PropertyDescriptor(new ClrFunction(Engine, "sign", Sign, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["sin"] = new PropertyDescriptor(new ClrFunction(Engine, "sin", Sin, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["sinh"] = new PropertyDescriptor(new ClrFunction(Engine, "sinh", Sinh, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["sumPrecise"] = new PropertyDescriptor(new ClrFunction(Engine, "sumPrecise", SumPrecise, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["sqrt"] = new PropertyDescriptor(new ClrFunction(Engine, "sqrt", Sqrt, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["tan"] = new PropertyDescriptor(new ClrFunction(Engine, "tan", Tan, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["tanh"] = new PropertyDescriptor(new ClrFunction(Engine, "tanh", Tanh, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            ["trunc"] = new PropertyDescriptor(new ClrFunction(Engine, "trunc", Truncate, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor(new JsString("Math"), PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private static JsValue Abs(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Acos(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Acosh(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToNumber(arguments.At(0));

        if (double.IsNaN(x) || x < 1)
        {
            return JsNumber.DoubleNaN;
        }

        return System.Math.Log(x + System.Math.Sqrt(x * x - 1.0));
    }

    private static JsValue Asin(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Asinh(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToNumber(arguments.At(0));
        if (double.IsInfinity(x) || NumberInstance.IsPositiveZero(x) || NumberInstance.IsNegativeZero(x))
        {
            return x;
        }

        return System.Math.Log(x + System.Math.Sqrt(x * x + 1.0));
    }

    private static JsValue Atan(JsValue thisObject, JsCallArguments arguments)
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
    private static JsValue Atanh(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Atan2(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Ceil(JsValue thisObject, JsCallArguments arguments)
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

#if NETFRAMEWORK
        if (x < 0 && x > -1)
        {
            return JsNumber.NegativeZero;
        }
#endif

        return System.Math.Ceiling(x);
    }

    private static JsValue Cos(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Cosh(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Exp(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Expm1(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Floor(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Log(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Log1p(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Log2(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Log10(JsValue thisObject, JsCallArguments arguments)
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.max
    /// </summary>
    private static JsValue Max(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        var highest = double.NegativeInfinity;
        foreach (var number in Coerced(arguments))
        {
            if (double.IsNaN(number))
            {
                return JsNumber.DoubleNaN;
            }

            if (NumberInstance.IsPositiveZero(number) && NumberInstance.IsNegativeZero(highest))
            {
                highest = 0;
            }

            if (number > highest)
            {
                highest = number;
            }
        }

        return highest;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.min
    /// </summary>
    private static JsValue Min(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            return JsNumber.DoublePositiveInfinity;
        }

        var lowest = double.PositiveInfinity;
        foreach (var number in Coerced(arguments))
        {
            if (double.IsNaN(number))
            {
                return JsNumber.DoubleNaN;
            }

            if (NumberInstance.IsNegativeZero(number) && NumberInstance.IsPositiveZero(lowest))
            {
                lowest = JsNumber.NegativeZero._value;
            }

            if (number < lowest)
            {
                lowest = number;
            }
        }

        return lowest;
    }

    private static JsValue Pow(JsValue thisObject, JsCallArguments arguments)
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

    private JsValue Random(JsValue thisObject, JsCallArguments arguments)
    {
        if(_random == null)
        {
            _random = new Random();
        }

        return _random.NextDouble();
    }

    private static JsValue Round(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToNumber(arguments.At(0));
        var round = System.Math.Round(x);
        if (round.Equals(x - 0.5))
        {
            return round + 1;
        }

        return round;
    }

    private static JsValue Fround(JsValue thisObject, JsCallArguments arguments)
    {
        var x = arguments.At(0);
        var n = TypeConverter.ToNumber(x);
        return (double) (float) n;
    }

    /// <summary>
    /// https://tc39.es/proposal-float16array/#sec-math.f16round
    /// </summary>
    private static JsValue F16Round(JsValue thisObject, JsCallArguments arguments)
    {
#if SUPPORTS_HALF
            var x = arguments.At(0);
            var n = TypeConverter.ToNumber(x);

            if (double.IsNaN(n))
            {
                return JsNumber.DoubleNaN;
            }

            if (double.IsInfinity(n) || NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
            {
                return x;
            }

            return (double) (Half) n;
#else
        ExceptionHelper.ThrowNotImplementedException("Float16/Half type is not supported in this build");
        return default;
#endif
    }

    private static JsValue Sin(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Sinh(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Sqrt(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToNumber(arguments.At(0));
        return System.Math.Sqrt(x);
    }

    private static JsValue Tan(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToNumber(arguments.At(0));
        return System.Math.Tan(x);
    }

    private static JsValue Tanh(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToNumber(arguments.At(0));
        return System.Math.Tanh(x);
    }

    private static JsValue Truncate(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Sign(JsValue thisObject, JsCallArguments arguments)
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

    private static JsValue Cbrt(JsValue thisObject, JsCallArguments arguments)
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.hypot
    /// </summary>
    private static JsValue Hypot(JsValue thisObject, JsCallArguments arguments)
    {
        var coerced = Coerced(arguments);

        foreach (var number in coerced)
        {
            if (double.IsInfinity(number))
            {
                return JsNumber.DoublePositiveInfinity;
            }
        }

        var onlyZero = true;
        double y = 0;
        foreach (var number in coerced)
        {
            if (double.IsNaN(number))
            {
                return JsNumber.DoubleNaN;
            }

            if (onlyZero && number != 0)
            {
                onlyZero = false;
            }

            y += number * number;
        }

        if (onlyZero)
        {
            return JsNumber.PositiveZero;
        }

        return System.Math.Sqrt(y);
    }

    /// <summary>
    /// https://github.com/tc39/proposal-math-sum
    /// </summary>
    private JsValue SumPrecise(JsValue thisObject, JsCallArguments arguments)
    {
        var items = arguments.At(0);
        if (items.IsNullOrUndefined())
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        var iteratorRecord = items.GetIterator(_engine.Realm);
        var state = JsNumber.NegativeZero._value;
        List<double> sum = [];
        long count = 0;
        const double Finite = 1;
        try
        {
            while (iteratorRecord.TryIteratorStep(out var next))
            {
                next.TryGetValue(CommonProperties.Value, out var value);
                count++;
                if (count > 9007199254740992)
                {
                    ExceptionHelper.ThrowRangeError(_engine.Realm);
                }

                if (value is not JsNumber jsNumber)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, "Input is not a number: " + next);
                    return default;
                }

                if (!double.IsNaN(state))
                {
                    var n = jsNumber._value;
                    if (double.IsNaN(n))
                    {
                        state = double.NaN;
                    }
                    else if (double.IsPositiveInfinity(n))
                    {
                        if (double.IsNegativeInfinity(state))
                        {
                            state = double.NaN;
                        }
                        else
                        {
                            state = double.PositiveInfinity;
                        }
                    }
                    else if (double.IsNegativeInfinity(n))
                    {
                        if (double.IsPositiveInfinity(state))
                        {
                            state = double.NaN;
                        }
                        else
                        {
                            state = double.NegativeInfinity;
                        }
                    }
                    else if (!NumberInstance.IsNegativeZero(n) && (NumberInstance.IsNegativeZero(state)  || state == Finite))
                    {
                        state = Finite;
                        sum.Add(n);
                    }
                }
            }

        }
        catch
        {
            iteratorRecord.Close(CompletionType.Throw);
            iteratorRecord = null;
            throw;
        }
        finally
        {
            iteratorRecord?.Close(CompletionType.Normal);
        }

        if (state != Finite)
        {
            return state;
        }

        return Math.SumPrecise.Sum(sum);
    }

    private static double[] Coerced(JsCallArguments arguments)
    {
        // TODO stackalloc
        var coerced = new double[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            coerced[i] = TypeConverter.ToNumber(argument);
        }

        return coerced;
    }

    private static JsValue Imul(JsValue thisObject, JsCallArguments arguments)
    {
        var x = TypeConverter.ToInt32(arguments.At(0));
        var y = TypeConverter.ToInt32(arguments.At(1));

        return x * y;
    }

    private static JsValue Clz32(JsValue thisObject, JsCallArguments arguments)
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
