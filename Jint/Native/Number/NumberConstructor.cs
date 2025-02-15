using Jint.Collections;
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
internal sealed class NumberConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Number");

    private const long MinSafeInteger = -9007199254740991;
    internal const long MaxSafeInteger = 9007199254740991;

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
        var properties = new PropertyDictionary(15, checkExistingKeys: false)
        {
            ["MAX_VALUE"] = new PropertyDescriptor(new PropertyDescriptor(double.MaxValue, PropertyFlag.AllForbidden)),
            ["MIN_VALUE"] = new PropertyDescriptor(new PropertyDescriptor(double.Epsilon, PropertyFlag.AllForbidden)),
            ["NaN"] = new PropertyDescriptor(new PropertyDescriptor(double.NaN, PropertyFlag.AllForbidden)),
            ["NEGATIVE_INFINITY"] = new PropertyDescriptor(new PropertyDescriptor(double.NegativeInfinity, PropertyFlag.AllForbidden)),
            ["POSITIVE_INFINITY"] = new PropertyDescriptor(new PropertyDescriptor(double.PositiveInfinity, PropertyFlag.AllForbidden)),
            ["EPSILON"] = new PropertyDescriptor(new PropertyDescriptor(JsNumber.JavaScriptEpsilon, PropertyFlag.AllForbidden)),
            ["MIN_SAFE_INTEGER"] = new PropertyDescriptor(new PropertyDescriptor(MinSafeInteger, PropertyFlag.AllForbidden)),
            ["MAX_SAFE_INTEGER"] = new PropertyDescriptor(new PropertyDescriptor(MaxSafeInteger, PropertyFlag.AllForbidden)),
            ["isFinite"] = new PropertyDescriptor(new ClrFunction(Engine, "isFinite", IsFinite, 1, PropertyFlag.Configurable), true, false, true),
            ["isInteger"] = new PropertyDescriptor(new ClrFunction(Engine, "isInteger", IsInteger, 1, PropertyFlag.Configurable), true, false, true),
            ["isNaN"] = new PropertyDescriptor(new ClrFunction(Engine, "isNaN", IsNaN, 1, PropertyFlag.Configurable), true, false, true),
            ["isSafeInteger"] = new PropertyDescriptor(new ClrFunction(Engine, "isSafeInteger", IsSafeInteger, 1, PropertyFlag.Configurable), true, false, true),
            ["parseFloat"] = new PropertyDescriptor(new ClrFunction(Engine, "parseFloat", GlobalObject.ParseFloat, 0, PropertyFlag.Configurable), true, false, true),
            ["parseInt"] = new PropertyDescriptor(new ClrFunction(Engine, "parseInt", GlobalObject.ParseInt, 0, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);
    }

    private static JsValue IsFinite(JsValue thisObject, JsCallArguments arguments)
    {
        if (!(arguments.At(0) is JsNumber num))
        {
            return false;
        }

        return double.IsInfinity(num._value) || double.IsNaN(num._value) ? JsBoolean.False : JsBoolean.True;
    }

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

    private static JsValue IsNaN(JsValue thisObject, JsCallArguments arguments)
    {
        if (!(arguments.At(0) is JsNumber num))
        {
            return false;
        }

        return double.IsNaN(num._value);
    }

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
                n = JsNumber.Create((long) ((JsBigInt) prim)._value);
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
