using System.Globalization;
using System.Numerics;
using System.Text;
using Jint.Native.Intl;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.BigInt;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-bigint-prototype-object
/// </summary>
[JsObject]
internal sealed partial class BigIntPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly BigIntConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString BigIntToStringTag = new("BigInt");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sup-bigint.prototype.tolocalestring
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        var x = ThisBigIntValue(thisObject);

        // Use Intl.NumberFormat for locale-aware formatting
        var numberFormat = (JsNumberFormat) Engine.Realm.Intrinsics.NumberFormat.Construct([locales, options], Engine.Realm.Intrinsics.NumberFormat);

        // Use BigInteger overload to avoid precision loss
        return numberFormat.Format(x._value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.valueof
    /// </summary>
    [JsFunction(Length = 0)]
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

        Throw.TypeError(_realm, "BigInt.prototype.valueOf requires that 'this' be a BigInt");
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.prototype.tostring
    /// </summary>
    [JsFunction(Length = 0, Name = "toString")]
    private JsValue ToBigIntString(JsValue thisObject, JsCallArguments arguments)
    {
        var x = ThisBigIntValue(thisObject);

        var radix = arguments.At(0);

        var radixMV = radix.IsUndefined()
            ? 10
            : (int) TypeConverter.ToIntegerOrInfinity(radix);

        if (radixMV is < 2 or > 36)
        {
            Throw.RangeError(_realm, "radix must be between 2 and 36");
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
                Throw.TypeError(_realm, "BigInt.prototype.valueOf requires that 'this' be a BigInt");
                return default;
        }
    }
}
