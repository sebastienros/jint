using Jint.Collections;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Math;

[JsObject]
internal sealed partial class MathInstance : ObjectInstance
{
#if !NET6_0_OR_GREATER
    private Random? _random;
#endif

    internal MathInstance(Engine engine, ObjectPrototype objectPrototype) : base(engine)
    {
        _prototype = objectPrototype;
    }

    private void CreateProperties()
    {
        var properties = new PropertyDictionary(45, checkExistingKeys: false)
        {
            /*
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
            */
        };
        SetProperties(properties);
    }

    private void CreateSymbols()
    {
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new(new JsString("Math"), PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    [JsProperty(Name = "E")] private static JsNumber E { get; } = new(System.Math.E);
    [JsProperty(Name = "LN10")] private static JsNumber LN10 { get; } = new(System.Math.Log(10));
    [JsProperty(Name = "LN2")] private static JsNumber LN2 { get; } = new(System.Math.Log(2));
    [JsProperty(Name = "LOG2E")] private static JsNumber LOG2E { get; } = new(System.Math.Log(System.Math.E, 2));
    [JsProperty(Name = "LOG10E")] private static JsNumber LOG10E { get; } = new(System.Math.Log(System.Math.E, 10));
    [JsProperty(Name = "PI")] private static JsNumber PI { get; } = new(System.Math.PI);
    [JsProperty(Name = "SQRT1_2")] private static JsNumber SQRT1_2 { get; } = new(System.Math.Sqrt(0.5));
    [JsProperty(Name = "SQRT2")] private static JsNumber SQRT2 { get; } = new(System.Math.Sqrt(2));

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.abs
    /// </summary>
    [JsFunction]
    private static JsValue Abs(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.PositiveZero;
        }
        else if (double.IsInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }

        return System.Math.Abs(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.acos
    /// </summary>
    [JsFunction]
    private static JsValue Acos(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n) || (n > 1) || (n < -1))
        {
            return JsNumber.DoubleNaN;
        }

        if (n == 1)
        {
            return JsNumber.PositiveZero;
        }

        return System.Math.Acos(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.acosh
    /// </summary>
    [JsFunction]
    private static JsValue Acosh(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n) || n < 1)
        {
            return JsNumber.DoubleNaN;
        }

        return System.Math.Log(n + System.Math.Sqrt(n * n - 1.0));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.asin
    /// </summary>
    [JsFunction]
    private static JsValue Asin(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n) || (n > 1) || (n < -1))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }

        return System.Math.Asin(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.asinh
    /// </summary>
    [JsFunction]
    private static JsValue Asinh(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);
        if (double.IsInfinity(n) || NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }

        return System.Math.Log(n + System.Math.Sqrt(n * n + 1.0));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.atan
    /// </summary>
    [JsFunction]
    private static JsValue Atan(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return System.Math.PI / 2;
        }
        else if (double.IsNegativeInfinity(n))
        {
            return -System.Math.PI / 2;
        }

        return System.Math.Atan(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.atanh
    /// </summary>
    [JsFunction]
    private static JsValue Atanh(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }

        if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }

        return 0.5 * System.Math.Log((1.0 + n) / (1.0 - n));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.atan2
    /// </summary>
    [JsFunction]
    private static JsValue Atan2(JsValue y, JsValue x)
    {
        var ny = TypeConverter.ToNumber(y);
        var nx = TypeConverter.ToNumber(x);

        // If either x or y is NaN, the result is NaN.
        if (double.IsNaN(nx) || double.IsNaN(ny))
        {
            return JsNumber.DoubleNaN;
        }

        if (ny > 0 && nx.Equals(0))
        {
            return System.Math.PI/2;
        }

        if (NumberInstance.IsPositiveZero(ny))
        {
            // If y is +0 and x>0, the result is +0.
            if (nx > 0)
            {
                return JsNumber.PositiveZero;
            }

            // If y is +0 and x is +0, the result is +0.
            if (NumberInstance.IsPositiveZero(nx))
            {
                return JsNumber.PositiveZero;
            }

            // If y is +0 and x is −0, the result is an implementation-dependent approximation to +π.
            if (NumberInstance.IsNegativeZero(nx))
            {
                return JsNumber.PI;
            }

            // If y is +0 and x<0, the result is an implementation-dependent approximation to +π.
            if (nx < 0)
            {
                return JsNumber.PI;
            }
        }

        if (NumberInstance.IsNegativeZero(ny))
        {
            // If y is −0 and x>0, the result is −0.
            if (nx > 0)
            {
                return JsNumber.NegativeZero;
            }

            // If y is −0 and x is +0, the result is −0.
            if (NumberInstance.IsPositiveZero(nx))
            {
                return JsNumber.NegativeZero;
            }

            // If y is −0 and x is −0, the result is an implementation-dependent approximation to −π.
            if (NumberInstance.IsNegativeZero(nx))
            {
                return -System.Math.PI;
            }

            // If y is −0 and x<0, the result is an implementation-dependent approximation to −π.
            if (nx < 0)
            {
                return -System.Math.PI;
            }
        }

        // If y<0 and x is +0, the result is an implementation-dependent approximation to −π/2.
        // If y<0 and x is −0, the result is an implementation-dependent approximation to −π/2.
        if (ny < 0 && nx.Equals(0))
        {
            return -System.Math.PI/2;
        }

        // If y>0 and y is finite and x is +∞, the result is +0.
        if (ny > 0 && !double.IsInfinity(ny))
        {
            if (double.IsPositiveInfinity(nx))
            {
                return JsNumber.PositiveZero;
            }

            // If y>0 and y is finite and x is −∞, the result if an implementation-dependent approximation to +π.
            if (double.IsNegativeInfinity(nx))
            {
                return JsNumber.PI;
            }
        }


        // If y<0 and y is finite and x is +∞, the result is −0.
        // If y<0 and y is finite and x is −∞, the result is an implementation-dependent approximation to −π.
        if (ny < 0 && !double.IsInfinity(ny))
        {
            if (double.IsPositiveInfinity(nx))
            {
                return JsNumber.NegativeZero;
            }

            // If y>0 and y is finite and x is −∞, the result if an implementation-dependent approximation to +π.
            if (double.IsNegativeInfinity(nx))
            {
                return -System.Math.PI;
            }
        }

        // If y is +∞ and x is finite, the result is an implementation-dependent approximation to +π/2.
        if (double.IsPositiveInfinity(ny) && !double.IsInfinity(nx))
        {
            return System.Math.PI/2;
        }

        // If y is −∞ and x is finite, the result is an implementation-dependent approximation to −π/2.
        if (double.IsNegativeInfinity(ny) && !double.IsInfinity(nx))
        {
            return -System.Math.PI / 2;
        }

        // If y is +∞ and x is +∞, the result is an implementation-dependent approximation to +π/4.
        if (double.IsPositiveInfinity(ny) && double.IsPositiveInfinity(nx))
        {
            return System.Math.PI/4;
        }

        // If y is +∞ and x is −∞, the result is an implementation-dependent approximation to +3π/4.
        if (double.IsPositiveInfinity(ny) && double.IsNegativeInfinity(nx))
        {
            return 3 * System.Math.PI / 4;
        }

        // If y is −∞ and x is +∞, the result is an implementation-dependent approximation to −π/4.
        if (double.IsNegativeInfinity(ny) && double.IsPositiveInfinity(nx))
        {
            return -System.Math.PI / 4;
        }

        // If y is −∞ and x is −∞, the result is an implementation-dependent approximation to −3π/4.
        if (double.IsNegativeInfinity(ny) && double.IsNegativeInfinity(nx))
        {
            return - 3 * System.Math.PI / 4;
        }

        return System.Math.Atan2(ny, nx);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.ceil
    /// </summary>
    [JsFunction]
    private static JsValue Ceil(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n))
        {
            return JsNumber.PositiveZero;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.NegativeZero;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (double.IsNegativeInfinity(n))
        {
            return JsNumber.DoubleNegativeInfinity;
        }

#if NETFRAMEWORK
            if (n < 0 && n > -1)
            {
                return JsNumber.NegativeZero;
            }
#endif

        return System.Math.Ceiling(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.cos
    /// </summary>
    [JsFunction]
    private static JsValue Cos(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n))
        {
            return JsNumber.PositiveOne;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.PositiveOne;
        }
        else if (double.IsInfinity(n))
        {
            return JsNumber.DoubleNaN;
        }

        return System.Math.Cos(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.cosh
    /// </summary>
    [JsFunction]
    private static JsValue Cosh(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n))
        {
            return JsNumber.PositiveOne;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.PositiveOne;
        }
        else if (double.IsInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }

        return System.Math.Cosh(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.exp
    /// </summary>
    [JsFunction]
    private static JsValue Exp(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.PositiveOne;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (double.IsNegativeInfinity(n))
        {
            return JsNumber.PositiveZero;
        }

        return System.Math.Exp(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.expm1
    /// </summary>
    [JsFunction]
    private static JsValue Expm1(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n) || NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n) || double.IsPositiveInfinity(n))
        {
            return x;
        }
        if (double.IsNegativeInfinity(n))
        {
            return JsNumber.DoubleNegativeOne;
        }

        return System.Math.Exp(n) - 1.0;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.floor
    /// </summary>
    [JsFunction]
    private static JsValue Floor(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n))
        {
            return JsNumber.PositiveZero;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.NegativeZero;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (double.IsNegativeInfinity(n))
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        return System.Math.Floor(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.log
    /// </summary>
    [JsFunction]
    private static JsValue Log(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        if (n < 0)
        {
            return JsNumber.DoubleNaN;
        }
        else if (n == 0)
        {
            return JsNumber.DoubleNegativeInfinity;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (n == 1)
        {
            return JsNumber.PositiveZero;
        }

        return System.Math.Log(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.log1p
    /// </summary>
    [JsFunction]
    private static JsValue Log1p(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }

        if (n < -1)
        {
            return JsNumber.DoubleNaN;
        }

        if (n == -1)
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        if (n == 0 || double.IsPositiveInfinity(n))
        {
            return x;
        }

        return System.Math.Log(1 + n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.log2
    /// </summary>
    [JsFunction]
    private static JsValue Log2(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        if (n < 0)
        {
            return JsNumber.DoubleNaN;
        }
        else if (n == 0)
        {
            return JsNumber.DoubleNegativeInfinity;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (n == 1)
        {
            return JsNumber.PositiveZero;
        }

        return System.Math.Log(n, 2);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.log10
    /// </summary>
    [JsFunction]
    private static JsValue Log10(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        if (n < 0)
        {
            return JsNumber.DoubleNaN;
        }
        else if (n == 0)
        {
            return JsNumber.DoubleNegativeInfinity;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (n == 1)
        {
            return JsNumber.PositiveZero;
        }

        return System.Math.Log10(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.max
    /// </summary>
    [JsFunction]
    private static JsValue Max(JsValue[] args)
    {
        if (args.Length == 0)
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        var highest = double.NegativeInfinity;

        var coerced = args.Length < 128
            ? stackalloc double[args.Length]
            : new double[args.Length];

        Coerced(args, coerced);

        foreach (var number in coerced)
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
    [JsFunction]
    private static JsValue Min(JsValue[] args)
    {
        if (args.Length == 0)
        {
            return JsNumber.DoublePositiveInfinity;
        }

        var coerced = args.Length < 128
            ? stackalloc double[args.Length]
            : new double[args.Length];

        Coerced(args, coerced);

        var lowest = double.PositiveInfinity;
        foreach (var number in coerced)
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.pow
    /// </summary>
    [JsFunction]
    private static JsValue Pow(JsValue @base, JsValue exponent)
    {
        var x = TypeConverter.ToNumber(@base);
        var y = TypeConverter.ToNumber(exponent);

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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.random
    /// </summary>
    [JsFunction]
#pragma warning disable CA1822 // Mark members as static
    private JsValue Random()
    {
#if NET6_0_OR_GREATER
        return System.Random.Shared.NextDouble();
#else
        if(_random == null)
        {
            _random = new Random();
        }

        return _random.NextDouble();
#endif
    }
#pragma warning restore CA1822

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.round
    /// </summary>
    [JsFunction]
    private static JsValue Round(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);
        var round = System.Math.Round(n);
        if (round.Equals(n - 0.5))
        {
            return round + 1;
        }

        return round;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.fround
    /// </summary>
    [JsFunction]
    private static JsValue Fround(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);
        return (double) (float) n;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.sin
    /// </summary>
    [JsFunction]
    private static JsValue Sin(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n))
        {
            return JsNumber.PositiveZero;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.NegativeZero;
        }
        else if (double.IsInfinity(n))
        {
            return JsNumber.DoubleNaN;
        }

        return System.Math.Sin(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.sinh
    /// </summary>
    [JsFunction]
    private static JsValue Sinh(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n))
        {
            return JsNumber.PositiveZero;
        }
        else if (NumberInstance.IsNegativeZero(n))
        {
            return JsNumber.NegativeZero;
        }
        else if (double.IsNegativeInfinity(n))
        {
            return JsNumber.DoubleNegativeInfinity;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }

        return System.Math.Sinh(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.sqrt
    /// </summary>
    [JsFunction]
    private static JsValue Sqrt(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);
        return System.Math.Sqrt(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.tan
    /// </summary>
    [JsFunction]
    private static JsValue Tan(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);
        return System.Math.Tan(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.tanh
    /// </summary>
    [JsFunction]
    private static JsValue Tanh(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);
        return System.Math.Tanh(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.trunc
    /// </summary>
    [JsFunction]
    private static JsValue Trunc(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }

        if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }

        if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }

        if (double.IsNegativeInfinity(n))
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        return System.Math.Truncate(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.sign
    /// </summary>
    [JsFunction]
    private static JsValue Sign(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }

        if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }

        if (double.IsPositiveInfinity(n))
        {
            return 1;
        }

        if (double.IsNegativeInfinity(n))
        {
            return -1;
        }

        return System.Math.Sign(n);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.cbrt
    /// </summary>
    [JsFunction]
    private static JsValue Cbrt(JsValue x)
    {
        var n = TypeConverter.ToNumber(x);

        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }
        else if (NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }
        else if (double.IsPositiveInfinity(n))
        {
            return JsNumber.DoublePositiveInfinity;
        }
        else if (double.IsNegativeInfinity(n))
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        if (System.Math.Sign(n) >= 0)
        {
            return System.Math.Pow(n, 1.0/3.0);
        }

        return -1 * System.Math.Pow(System.Math.Abs(n), 1.0 / 3.0);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.hypot
    /// </summary>
    [JsFunction]
    private static JsValue Hypot(JsValue[] args)
    {
        var coerced = args.Length < 128
            ? stackalloc double[args.Length]
            : new double[args.Length];

        Coerced(args, coerced);

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
    /// https://tc39.es/ecma262/#sec-math.imul
    /// </summary>
    [JsFunction]
    private static JsValue Imul(JsValue x, JsValue y)
    {
        var a = TypeConverter.ToInt32(x);
        var b = TypeConverter.ToInt32(y);

        return a * b;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.clz32
    /// </summary>
    [JsFunction]
    private static JsValue Clz32(JsValue x)
    {
        var n = TypeConverter.ToInt32(x);
        if (n < 0)
        {
            return 0;
        }

        if (n == 0)
        {
            return 32;
        }

        var res = 0;
        var shift = 16;
        while (n > 1)
        {
            var temp = n >> shift;
            if (temp != 0)
            {
                n = temp;
                res += shift;
            }

            shift >>= 1;
        }

        return 31 - res;
    }

    private static void Coerced(JsValue[] arguments, Span<double> coerced)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            coerced[i] = TypeConverter.ToNumber(argument);
        }
    }
}
