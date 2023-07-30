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

    /// <summary>
    /// https://tc39.es/ecma402/#sup-bigint.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        var x = ThisBigIntValue(thisObject);
        //var numberFormat = (NumberFormat) Construct(_realm.Intrinsics.NumberFormat, new[] {  locales, options });
        // numberFormat.FormatNumeric(x);
        return x._value.ToString("R");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
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

        var value = x._value;
        if (value == BigInteger.Zero)
        {
            return JsString.NumberZeroString;
        }

        if (radixMV == 10)
        {
            return value.ToString("R");
        }

        var negative = value < 0;

        if (negative)
        {
            value = -value;
        }

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        using var builder = StringBuilderPool.Rent();
        var sb = builder.Builder;

        for (; value > 0; value /= radixMV)
        {
            var d = (int) (value % radixMV);
            sb.Append(digits[d]);
        }

        var charArray = new char[sb.Length];
        sb.CopyTo(0, charArray, 0, charArray.Length);
        System.Array.Reverse(charArray);

        return (negative ? "-" : "") + new string(charArray);
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
