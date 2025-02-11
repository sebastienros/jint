using System.Globalization;
using System.Numerics;
using System.Text;
using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.BigInt;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-bigint-prototype-object
/// </summary>
internal sealed class BigIntPrototype : Prototype
{
    private readonly BigIntConstructor _constructor;

    internal BigIntPrototype(
        Engine engine,
        BigIntConstructor constructor,
        ObjectPrototype objectPrototype)
        : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["constructor"] = new(_constructor, true, false, true),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToBigIntString, 0, PropertyFlag.Configurable), true, false, true),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), true, false, true),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("BigInt", false, false, true)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sup-bigint.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        var x = ThisBigIntValue(thisObject);
        //var numberFormat = (NumberFormat) Construct(_realm.Intrinsics.NumberFormat, new[] {  locales, options });
        // numberFormat.FormatNumeric(x);
        return x._value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is BigIntInstance ni)
        {
            return ni.BigIntData;
        }

        if (thisObject is JsBigInt)
        {
            return thisObject;
        }

        ExceptionHelper.ThrowTypeError(_realm);
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.tostring
    /// </summary>
    private JsValue ToBigIntString(JsValue thisObject, JsCallArguments arguments)
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

        var value = x._value;
        if (value == BigInteger.Zero)
        {
            return JsString.NumberZeroString;
        }

        if (radixMV == 10)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        var negative = value < 0;

        if (negative)
        {
            value = -value;
        }

        const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        var sb = new ValueStringBuilder(stackalloc char[64]);

        for (; value > 0; value /= radixMV)
        {
            var d = (int) (value % radixMV);
            sb.Append(Digits[d]);
        }

        if (negative)
        {
            sb.Append('-');
        }

        sb.Reverse();

        return sb.ToString();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#thisbigintvalue
    /// </summary>
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
                return default;
        }
    }
}
