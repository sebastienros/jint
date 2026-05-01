using System.Globalization;
using System.Numerics;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.instant
/// </summary>
[JsObject]
internal sealed partial class InstantConstructor : Constructor
{
    private static readonly JsString _functionName = new("Instant");

    // Valid range for Instant: -100_000_000 days to +100_000_000 days from Unix epoch
    private static readonly BigInteger MinEpochNs = BigInteger.Parse("-8640000000000000000000", CultureInfo.InvariantCulture);
    private static readonly BigInteger MaxEpochNs = BigInteger.Parse("8640000000000000000000", CultureInfo.InvariantCulture);

    internal InstantConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new InstantPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public InstantPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();


    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.from
    /// </summary>
    [JsFunction(Length = 1)]
    private JsInstant From(JsValue thisObject, JsValue item)
    {
        return ToTemporalInstant(item);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.fromepochmilliseconds
    /// </summary>
    [JsFunction(Length = 1)]
    private JsInstant FromEpochMilliseconds(JsValue thisObject, JsValue epochMilliseconds)
    {
        var ms = TypeConverter.ToNumber(epochMilliseconds);

        // NumberToBigInt: must be an integral number
        if (double.IsNaN(ms) || double.IsInfinity(ms))
        {
            Throw.RangeError(_realm, "Invalid epoch milliseconds");
        }

        if (ms != System.Math.Truncate(ms))
        {
            Throw.RangeError(_realm, "epochMilliseconds must be an integer");
        }

        var ns = (BigInteger) ms * 1_000_000;

        if (!IsValidEpochNanoseconds(ns))
        {
            Throw.RangeError(_realm, "Instant outside of valid range");
        }

        return Construct(ns);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.fromepochnanoseconds
    /// </summary>
    [JsFunction(Length = 1)]
    private JsInstant FromEpochNanoseconds(JsValue thisObject, JsValue epochNanoseconds)
    {
        if (epochNanoseconds is not JsBigInt bigInt)
        {
            Throw.TypeError(_realm, "epochNanoseconds must be a BigInt");
            return null!;
        }

        var ns = bigInt._value;

        if (!IsValidEpochNanoseconds(ns))
        {
            Throw.RangeError(_realm, "Instant outside of valid range");
        }

        return Construct(ns);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.compare
    /// </summary>
    [JsFunction(Length = 2)]
    private JsNumber Compare(JsValue thisObject, JsValue one, JsValue two)
    {
        return JsNumber.Create(
            ToTemporalInstant(one).EpochNanoseconds.CompareTo(ToTemporalInstant(two).EpochNanoseconds));
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Instant cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var epochNanoseconds = arguments.At(0);

        // Convert to BigInt using ToBigInt (spec step 2)
        var ns = TypeConverter.ToBigInt(epochNanoseconds);

        if (!IsValidEpochNanoseconds(ns))
        {
            Throw.RangeError(_realm, "Instant outside of valid range");
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TemporalInstant.PrototypeObject,
            static (engine, _, state) => new JsInstant(engine, null!, state),
            ns);
    }

    internal JsInstant Construct(BigInteger epochNanoseconds)
    {
        return new JsInstant(_engine, PrototypeObject, epochNanoseconds);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalinstant
    /// </summary>
    internal JsInstant ToTemporalInstant(JsValue item)
    {
        // Step 1: If item is a Temporal.Instant, create a new copy
        if (item is JsInstant instant)
        {
            return Construct(instant.EpochNanoseconds);
        }

        // Step 2: If item is a Temporal.ZonedDateTime, return its instant
        if (item is JsZonedDateTime zonedDateTime)
        {
            return Construct(zonedDateTime.EpochNanoseconds);
        }

        // Step 3: If item is an object, convert to string
        string str;
        if (item.IsObject() && item is not JsString)
        {
            str = TypeConverter.ToString(item);
        }
        else if (item.IsString())
        {
            str = item.ToString();
        }
        else
        {
            Throw.TypeError(_realm, "Invalid instant");
            return null!;
        }

        // Step 4: Parse the string
        var parsed = TemporalHelpers.ParseInstantString(str);
        if (parsed is null)
        {
            Throw.RangeError(_realm, "Invalid instant string");
        }
        return Construct(parsed.Value);
    }

    /// <summary>
    /// Validates that epoch nanoseconds are within the valid range.
    /// </summary>
    internal static bool IsValidEpochNanoseconds(BigInteger ns)
    {
        return ns >= MinEpochNs && ns <= MaxEpochNs;
    }
}
