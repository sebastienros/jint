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
internal sealed class InstantConstructor : Constructor
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

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["from"] = new(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags),
            ["fromEpochMilliseconds"] = new(new ClrFunction(Engine, "fromEpochMilliseconds", FromEpochMilliseconds, 1, LengthFlags), PropertyFlags),
            ["fromEpochNanoseconds"] = new(new ClrFunction(Engine, "fromEpochNanoseconds", FromEpochNanoseconds, 1, LengthFlags), PropertyFlags),
            ["compare"] = new(new ClrFunction(Engine, "compare", Compare, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.from
    /// </summary>
    private JsInstant From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        return ToTemporalInstant(item);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.fromepochmilliseconds
    /// </summary>
    private JsInstant FromEpochMilliseconds(JsValue thisObject, JsCallArguments arguments)
    {
        var epochMilliseconds = arguments.At(0);
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
    private JsInstant FromEpochNanoseconds(JsValue thisObject, JsCallArguments arguments)
    {
        var epochNanoseconds = arguments.At(0);

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
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalInstant(arguments.At(0));
        var two = ToTemporalInstant(arguments.At(1));

        return JsNumber.Create(one.EpochNanoseconds.CompareTo(two.EpochNanoseconds));
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
