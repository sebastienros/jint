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
        : base(engine, ObjectClass.Object)
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
    {
        if (!thisObject.IsBigInt() && thisObject is not BigIntInstance)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var m = TypeConverter.ToBigInt(thisObject);
        return m.ToString("R");
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.valueof
    /// </summary>
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
                return JsBigInt.One;
        }
    }
}
