using System.Numerics;
using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.BigInt;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-bigint-prototype-object
/// </summary>
public sealed class BigIntPrototype : ObjectInstance
{
    private readonly Realm _realm;
    private readonly BigIntConstructor _constructor;

    internal BigIntPrototype(
        Engine engine,
        Realm realm,
        BigIntConstructor constructor,
        ObjectPrototype objectPrototype)
        : base(engine)
    {
        _prototype = objectPrototype;
        _realm = realm;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["constructor"] = new(_constructor, true, false, true),
            ["toString"] = new(new ClrFunctionInstance(Engine, "toString", ToBigIntString, 0, PropertyFlag.Configurable), true, false, true),
            ["toLocaleString"] = new(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), true, false, true),
            ["valueOf"] = new(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("BigInt", false, false, true)
        };
        SetSymbols(symbols);
    }

    private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
    {
        if (!thisObject.IsNumber() && ReferenceEquals(thisObject.TryCast<BigIntInstance>(), null))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var m = TypeConverter.ToNumber(thisObject);

        if (double.IsNaN(m))
        {
            return "NaN";
        }

        if (m == 0)
        {
            return JsString.NumberZeroString;
        }

        if (m < 0)
        {
            return "-" + ToLocaleString(-m, arguments);
        }

        if (double.IsPositiveInfinity(m) || m >= double.MaxValue)
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(m) || m <= -double.MaxValue)
        {
            return "-Infinity";
        }

        return m.ToString("n", Engine.Options.Culture);
    }

    private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
    {
        if (thisObj is BigIntInstance ni)
        {
            return ni.BigIntData;
        }

        if (thisObj is JsBigInt)
        {
            return thisObj;
        }

        ExceptionHelper.ThrowTypeError(_realm);
        return null;
    }

    private JsValue ToBigIntString(JsValue thisObject, JsValue[] arguments)
    {
        var x = ThisBigIntValue(thisObject);

        var radix = arguments.At(0);

        var radixMV = radix.IsUndefined()
            ? 10
            : (int) TypeConverter.ToIntegerOrInfinity(radix);

        if (radixMV is < 2 or > 36)
        {
            ExceptionHelper.ThrowRangeError(_realm, "radix must be between 2 and 36");
        }

        if (radixMV == 10)
        {
            return BigIntToString(x._value);
        }

        ExceptionHelper.ThrowNotImplementedException();
        return "";
    }

    private JsBigInt ThisBigIntValue(JsValue value)
    {
        switch (value)
        {
            case JsBigInt bigInt:
                return bigInt;
            case BigIntInstance bigIntInstance:
                return bigIntInstance.BigIntData;
            default:
                ExceptionHelper.ThrowTypeError(_realm);
                return JsBigInt.One;
        }
    }

    public string ToBase(long n, int radix)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (n == 0)
        {
            return "0";
        }

        using (var result = StringBuilderPool.Rent())
        {
            while (n > 0)
            {
                var digit = (int) (n % radix);
                n = n / radix;
                result.Builder.Insert(0, digits[digit]);
            }

            return result.ToString();
        }
    }

    public string ToFractionBase(double n, int radix)
    {
        // based on the repeated multiplication method
        // http://www.mathpath.org/concepts/Num/frac.htm

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (n == 0)
        {
            return "0";
        }

        using (var result = StringBuilderPool.Rent())
        {
            while (n > 0 && result.Length < 50) // arbitrary limit
            {
                var c = n*radix;
                var d = (int) c;
                n = c - d;

                result.Builder.Append(digits[d]);
            }

            return result.ToString();
        }
    }

    private string ToBigIntString(BigInteger m)
    {
        return BigIntToString(m);
    }

    internal static string BigIntToString(BigInteger m)
    {
        return m.ToString("R");
    }
}