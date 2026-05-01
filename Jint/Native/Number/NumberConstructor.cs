using Jint.Native.Function;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Number;

/// <summary>
/// https://tc39.es/ecma262/#sec-number-constructor
/// </summary>
[JsObject]
internal sealed partial class NumberConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Number");

    private const long MinSafeInteger = -9007199254740991;
    internal const long MaxSafeInteger = 9007199254740991;

    [JsProperty(Name = "MAX_VALUE", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber MaxValue = new(double.MaxValue);
    [JsProperty(Name = "MIN_VALUE", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber MinValueField = new(double.Epsilon);
    [JsProperty(Name = "NaN", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber NaNField = JsNumber.DoubleNaN;
    [JsProperty(Name = "NEGATIVE_INFINITY", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber NegativeInfinityField = JsNumber.DoubleNegativeInfinity;
    [JsProperty(Name = "POSITIVE_INFINITY", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber PositiveInfinityField = JsNumber.DoublePositiveInfinity;
    [JsProperty(Name = "EPSILON", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber Epsilon = new(JsNumber.JavaScriptEpsilon);
    [JsProperty(Name = "MIN_SAFE_INTEGER", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber MinSafeIntegerField = new(MinSafeInteger);
    [JsProperty(Name = "MAX_SAFE_INTEGER", Flags = PropertyFlag.AllForbidden)] private static readonly JsNumber MaxSafeIntegerField = new(MaxSafeInteger);

    public NumberConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new NumberPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();

        // Per spec, Number.parseInt and Number.parseFloat are the same function objects as the global
        // parseInt / parseFloat. Pre-source-gen Jint's behaviour relied on ClrFunction.Equals comparing
        // the underlying delegate; the source generator's per-method dispatcher would break that
        // identity, so register parseInt/parseFloat hand-rolled and let them keep the same delegate.
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        SetProperty("parseInt", new PropertyDescriptor(new ClrFunction(Engine, "parseInt", GlobalObject.ParseInt, 0, PropertyFlag.Configurable), PropertyFlags));
        SetProperty("parseFloat", new PropertyDescriptor(new ClrFunction(Engine, "parseFloat", GlobalObject.ParseFloat, 0, PropertyFlag.Configurable), PropertyFlags));
    }

    [JsFunction(Length = 1)]
    private static JsValue IsFinite(JsValue thisObject, JsCallArguments arguments)
    {
        if (!(arguments.At(0) is JsNumber num))
        {
            return false;
        }

        return double.IsInfinity(num._value) || double.IsNaN(num._value) ? JsBoolean.False : JsBoolean.True;
    }

    [JsFunction(Length = 1)]
    private static JsValue IsInteger(JsValue thisObject, JsCallArguments arguments)
    {
        if (!(arguments.At(0) is JsNumber num))
        {
            return false;
        }

        if (double.IsInfinity(num._value) || double.IsNaN(num._value))
        {
            return JsBoolean.False;
        }

        var integer = TypeConverter.ToInteger(num);

        return integer == num._value;
    }

    [JsFunction(Length = 1)]
    private static JsValue IsNaN(JsValue thisObject, JsCallArguments arguments)
    {
        if (!(arguments.At(0) is JsNumber num))
        {
            return false;
        }

        return double.IsNaN(num._value);
    }

    [JsFunction(Length = 1)]
    private static JsValue IsSafeInteger(JsValue thisObject, JsCallArguments arguments)
    {
        if (!(arguments.At(0) is JsNumber num))
        {
            return false;
        }

        if (double.IsInfinity(num._value) || double.IsNaN(num._value))
        {
            return JsBoolean.False;
        }

        var integer = TypeConverter.ToInteger(num);

        if (integer != num._value)
        {
            return false;
        }

        return System.Math.Abs(integer) <= MaxSafeInteger;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var n = ProcessFirstParameter(arguments);
        return n;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-number-constructor-number-value
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var n = ProcessFirstParameter(arguments);

        if (newTarget.IsUndefined())
        {
            return Construct(n);
        }

        var o = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Number.PrototypeObject,
            static (engine, realm, state) => new NumberInstance(engine, state!), n);
        return o;
    }

    private static JsNumber ProcessFirstParameter(JsCallArguments arguments)
    {
        var n = JsNumber.PositiveZero;
        if (arguments.Length > 0)
        {
            var prim = TypeConverter.ToNumeric(arguments[0]);
            if (prim.IsBigInt())
            {
                n = JsNumber.Create(Jint.Native.Temporal.TemporalHelpers.BigIntToF64(((JsBigInt) prim)._value));
            }
            else
            {
                n = (JsNumber) prim;
            }
        }

        return n;
    }

    public NumberPrototype PrototypeObject { get; }

    public NumberInstance Construct(JsNumber value)
    {
        var instance = new NumberInstance(Engine, value)
        {
            _prototype = PrototypeObject
        };

        return instance;
    }
}
