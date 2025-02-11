using System.Numerics;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.BigInt;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-bigint-constructor
/// </summary>
internal sealed class BigIntConstructor : Constructor
{
    private static readonly JsString _functionName = new("BigInt");

    public BigIntConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new BigIntPrototype(engine, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["asIntN"] = new(new ClrFunction(Engine, "asIntN", AsIntN, 2, PropertyFlag.Configurable), true, false, true),
            ["asUintN"] = new(new ClrFunction(Engine, "asUintN", AsUintN, 2, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.asintn
    /// </summary>
    private JsValue AsIntN(JsValue thisObject, JsCallArguments arguments)
    {
        var bits = (int) TypeConverter.ToIndex(_realm, arguments.At(0));
        var bigint = arguments.At(1).ToBigInteger(_engine);

        var mod = TypeConverter.BigIntegerModulo(bigint, BigInteger.Pow(2, bits));
        if (bits > 0 && mod >= BigInteger.Pow(2, bits - 1))
        {
            return (mod - BigInteger.Pow(2, bits));
        }

        return mod;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.asuintn
    /// </summary>
    private JsValue AsUintN(JsValue thisObject, JsCallArguments arguments)
    {
        var bits = (int) TypeConverter.ToIndex(_realm, arguments.At(0));
        var bigint = arguments.At(1).ToBigInteger(_engine);

        var result = TypeConverter.BigIntegerModulo(bigint, BigInteger.Pow(2, bits));

        return result;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            return JsBigInt.Zero;
        }

        var prim = TypeConverter.ToPrimitive(arguments.At(0), Types.Number);
        if (prim.IsNumber())
        {
            return NumberToBigInt((JsNumber) prim);
        }

        return prim.ToBigInteger(_engine);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-numbertobigint
    /// </summary>
    private JsBigInt NumberToBigInt(JsNumber value)
    {
        if (TypeConverter.IsIntegralNumber(value._value))
        {
            return JsBigInt.Create((long) value._value);
        }

        ExceptionHelper.ThrowRangeError(_realm, "The number " + value + " cannot be converted to a BigInt because it is not an integer");
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint-constructor-number-value
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var value = arguments.Length > 0
            ? JsBigInt.Create(arguments[0].ToBigInteger(_engine))
            : JsBigInt.Zero;

        if (newTarget.IsUndefined())
        {
            return Construct(value);
        }

        var o = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.BigInt.PrototypeObject,
            static (engine, realm, state) => new BigIntInstance(engine, state!),
            value);

        return o;
    }

    public BigIntPrototype PrototypeObject { get; }

    public BigIntInstance Construct(JsBigInt value)
    {
        var instance = new BigIntInstance(Engine, value)
        {
            _prototype = PrototypeObject
        };

        return instance;
    }
}
