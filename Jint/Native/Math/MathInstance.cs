using System.Runtime.CompilerServices;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Math;

[JsObject]
internal sealed partial class MathInstance : BuiltinShapeObject
{
    private readonly Realm _realm;
    private Random? _random;

    [JsProperty(Name = "E", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber EValue = new(System.Math.E);
    [JsProperty(Name = "LN10", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber LN10Value = new(System.Math.Log(10));
    [JsProperty(Name = "LN2", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber LN2Value = new(System.Math.Log(2));
    [JsProperty(Name = "LOG10E", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber LOG10EValue = new(System.Math.Log(System.Math.E, 10));
    [JsProperty(Name = "LOG2E", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber LOG2EValue = new(System.Math.Log(System.Math.E, 2));
    [JsProperty(Name = "PI", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber PIValue = JsNumber.PI;
    [JsProperty(Name = "SQRT1_2", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber SQRT1_2Value = new(System.Math.Sqrt(0.5));
    [JsProperty(Name = "SQRT2", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber SQRT2Value = new(System.Math.Sqrt(2));

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString MathToStringTag = new("Math");

    internal MathInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        // CreateProperties_Generated installs the built-in shape (this derives from BuiltinShapeObject).
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Abs(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Acos(JsValue thisObject, [ToNumber] double x)
    {
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.acosh
    /// </summary>
    // The whole special-case table (NaN and x < 1 give NaN, 1 gives +0, +inf gives +inf) is
    // decided inside the fdlibm port, so guarding for it again here would only be a second copy of
    // the same branches. See Fdlibm.Acosh.
    [JsFunction]
    private static JsValue Acosh(JsValue thisObject, [ToNumber] double x) => Fdlibm.Acosh(x);

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Asin(JsValue thisObject, [ToNumber] double x)
    {
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.asinh
    /// </summary>
    // NaN, +-inf and +-0 all return the argument unchanged, which the fdlibm port already does.
    [JsFunction]
    private static JsValue Asinh(JsValue thisObject, [ToNumber] double x) => Fdlibm.Asinh(x);

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Atan(JsValue thisObject, [ToNumber] double x)
    {
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.atanh
    /// </summary>
    // NaN and +-0 return the argument, +-1 return +-inf and |x| > 1 returns NaN; the fdlibm port
    // decides all four up front rather than leaving them to fall out of the arithmetic. The
    // accuracy is what changed: 0.5*Log((1+x)/(1-x)) cancels for small x, so every argument below
    // 2**-53 came back as zero, and even Math.atanh(0.5) was a ULP out.
    [JsFunction]
    private static JsValue Atanh(JsValue thisObject, [ToNumber] double x) => Fdlibm.Atanh(x);

    [JsFunction]
    private static JsValue Atan2(JsValue thisObject, [ToNumber] double y, [ToNumber] double x)
    {
        // If either x or y is NaN, the result is NaN.
        if (double.IsNaN(x) || double.IsNaN(y))
        {
            return JsNumber.DoubleNaN;
        }

        if (y > 0 && x.Equals(0))
        {
            return System.Math.PI / 2;
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
            return -System.Math.PI / 2;
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
            return System.Math.PI / 2;
        }

        // If y is −∞ and x is finite, the result is an implementation-dependent approximation to −π/2.
        if (double.IsNegativeInfinity(y) && !double.IsInfinity(x))
        {
            return -System.Math.PI / 2;
        }

        // If y is +∞ and x is +∞, the result is an implementation-dependent approximation to +π/4.
        if (double.IsPositiveInfinity(y) && double.IsPositiveInfinity(x))
        {
            return System.Math.PI / 4;
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
            return -3 * System.Math.PI / 4;
        }

        return System.Math.Atan2(y, x);
    }

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Ceil(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Cos(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Cosh(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Exp(JsValue thisObject, [ToNumber] double x)
    {
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.expm1
    /// </summary>
    // "The result is computed in a way that is accurate even when the value of x is close to 0",
    // which Exp(x) - 1.0 is not: everything below 2**-53 cancelled away to zero. The fdlibm port
    // also produces the special cases (NaN, +-0 and +inf return x; -inf returns -1).
    [JsFunction]
    private static JsNumber Expm1(JsValue thisObject, [ToNumber] double x) => JsNumber.Create(Fdlibm.Expm1(x));

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Floor(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Log(JsValue thisObject, [ToNumber] double x)
    {
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.log1p
    /// </summary>
    // Same normative "accurate even when the value of x is close to 0" as expm1, and Log(1 + x) was
    // just as far from honouring it: 1 + 1e-300 is 1, and 1 + 1e-15 loses a tenth of its digits.
    // NaN, +-0 and +inf return x, -1 returns -inf, x < -1 returns NaN, all inside the port.
    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Log1p(JsValue thisObject, [ToNumber] double x) => Fdlibm.Log1p(x);

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Log2(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Log10(JsValue thisObject, [ToNumber] double x)
    {
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
    // Leaf through the variadic lane: the tail's declared ToNumber gives every register a Number
    // guard, and with all-numeric arguments the body is arithmetic that neither throws nor calls out.
    // Call sites passing more arguments than the lane's registers simply keep the framed path.
    [JsFunction(Length = 2, Leaf = true)]
    private static JsValue Max(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<double> values)
    {
        // [Rest, ToNumber] makes the dispatcher coerce every element via TypeConverter.ToNumber
        // BEFORE this body runs (spec requirement — observable via valueOf side effects). The
        // span is stack-allocated for ≤16 elements, heap for larger.
        if (values.Length == 0)
        {
            return JsNumber.DoubleNegativeInfinity;
        }

        var highest = double.NegativeInfinity;
        for (var i = 0; i < values.Length; i++)
        {
            var number = values[i];
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
    // Leaf through the variadic lane — see Max.
    [JsFunction(Length = 2, Leaf = true)]
    private static JsValue Min(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<double> values)
    {
        // [Rest, ToNumber] preamble coerces every element first (see Max for spec-side rationale).
        if (values.Length == 0)
        {
            return JsNumber.DoublePositiveInfinity;
        }

        var lowest = double.PositiveInfinity;
        for (var i = 0; i < values.Length; i++)
        {
            var number = values[i];
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

    [JsFunction]
    private static JsValue Pow(JsValue thisObject, [ToNumber] double x, [ToNumber] double y)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private JsValue Random(JsValue thisObject)
    {
        if (_random == null)
        {
            _random = new Random();
        }

        return _random.NextDouble();
    }

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Round(JsValue thisObject, [ToNumber] double x)
    {
        var round = System.Math.Round(x);
        if (round.Equals(x - 0.5))
        {
            return round + 1;
        }

        return round;
    }

    [JsFunction(Length = 1, FastCall = true)]
    private static JsValue Fround(JsValue thisObject, JsValue arg0)
    {
        var x = arg0;
        var n = TypeConverter.ToNumber(x);
        return (double) (float) n;
    }

    /// <summary>
    /// https://tc39.es/proposal-float16array/#sec-math.f16round
    /// </summary>
    // FastCall only, like Fround: on the TFMs without Half this throws, and the frame belongs on a
    // throwing built-in. The declared [ToNumber] is what keeps the coercion out of the body — where
    // it used to make the infinity / signed-zero branches hand back the *argument*, so an object
    // coercing to Infinity was returned as itself instead of as a Number.
    [JsFunction(Length = 1, Name = "f16round", FastCall = true)]
    private static JsValue F16Round(JsValue thisObject, [ToNumber] double n)
    {
#if SUPPORTS_HALF
        if (double.IsNaN(n))
        {
            return JsNumber.DoubleNaN;
        }

        if (double.IsInfinity(n) || NumberInstance.IsPositiveZero(n) || NumberInstance.IsNegativeZero(n))
        {
            return n;
        }

        return (double) (Half) n;
#else
        Throw.NotImplementedException("Float16/Half type is not supported in this build");
        return default;
#endif
    }

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Sin(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Sinh(JsValue thisObject, [ToNumber] double x)
    {
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

    // The 3 wrappers below are pure forwards to System.Math — single-line bodies that the JIT can
    // inline into the dispatcher's switch case so the call site collapses to the underlying intrinsic.
    // Without the hint, the generator's `Call(...)` dispatcher (one switch with ~30 cases) is large
    // enough that JIT may decline to inline these tiny callees by default. Spec edge cases (NaN,
    // ±0, ±Infinity) are handled by the System.Math implementations themselves for these three.

    [JsFunction]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue Sqrt(JsValue thisObject, [ToNumber] double x)
    {
        return System.Math.Sqrt(x);
    }

    [JsFunction]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue Tan(JsValue thisObject, [ToNumber] double x)
    {
        return System.Math.Tan(x);
    }

    [JsFunction]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue Tanh(JsValue thisObject, [ToNumber] double x)
    {
        return System.Math.Tanh(x);
    }

    [JsFunction(Name = "trunc", FastCall = true, Leaf = true)]
    private static JsValue Truncate(JsValue thisObject, [ToNumber] double x)
    {
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

    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Sign(JsValue thisObject, [ToNumber] double x)
    {
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.cbrt
    /// </summary>
    // Pow(|x|, 1.0/3.0) is not a cube root: 1/3 is not representable, so the exponent is already
    // wrong before Pow starts, and Math.cbrt(1e-300) came out ~58 ULP high. NaN, +-inf and +-0
    // return the argument, which the fdlibm port does itself.
    [JsFunction(FastCall = true, Leaf = true)]
    private static JsValue Cbrt(JsValue thisObject, [ToNumber] double x) => Fdlibm.Cbrt(x);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-math.hypot
    /// </summary>
    // Leaf through the variadic lane — see Max.
    [JsFunction(Length = 2, Leaf = true)]
    private static JsValue Hypot(JsValue thisObject, [Rest, ToNumber] ReadOnlySpan<double> values)
    {
        // [Rest, ToNumber] preamble coerces every element first. Any Infinity returns +Infinity
        // (even if a later value is NaN); otherwise NaN; otherwise sum-of-squares root.
        for (var i = 0; i < values.Length; i++)
        {
            if (double.IsInfinity(values[i]))
            {
                return JsNumber.DoublePositiveInfinity;
            }
        }

        var onlyZero = true;
        double y = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var number = values[i];
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
    [JsFunction(Length = 1, Name = "sumPrecise")]
    private JsValue SumPrecise(JsValue thisObject, JsCallArguments arguments)
    {
        var items = arguments.At(0);
        if (items.IsNullOrUndefined())
        {
            Throw.TypeError(_realm);
        }

        var iteratorRecord = items.GetIterator(_realm);
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
                    Throw.RangeError(_realm);
                }

                if (value is not JsNumber jsNumber)
                {
                    Throw.TypeError(_realm, "Input is not a number: " + next);
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
                    else if (!NumberInstance.IsNegativeZero(n) && (NumberInstance.IsNegativeZero(state) || state == Finite))
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

    [JsFunction]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue Imul(JsValue thisObject, [ToInt32] int x, [ToInt32] int y)
    {
        return x * y;
    }

    [JsFunction]
    private static JsValue Clz32(JsValue thisObject, [ToInt32] int x)
    {
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
