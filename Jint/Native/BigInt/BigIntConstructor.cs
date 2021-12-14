using System;
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
public sealed class BigIntConstructor : FunctionInstance, IConstructor
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
        PrototypeObject = new BigIntPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["asIntN"] = new(new ClrFunctionInstance(Engine, "asIntN", AsIntN, 2, PropertyFlag.Configurable), true, false, true),
            ["asUintN"] = new(new ClrFunctionInstance(Engine, "asUintN", AsUintN, 2, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.asintn
    /// </summary>
    private JsValue AsIntN(JsValue thisObj, JsValue[] arguments)
    {
        var bits = TypeConverter.ToIndex(_realm, arguments.At(0));

        if (bits == 0)
        {
            return JsBigInt.Zero;
        }

        var bigint = TypeConverter.ToBigInt(arguments.At(1));
        var bitsPow = (ulong) System.Math.Pow(2, bits);
        var mod = bigint % bitsPow;
        if (mod >= bitsPow - 1)
        {
            return mod - bitsPow;
        }

        return mod;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.asuintn
    /// </summary>
    private JsValue AsUintN(JsValue thisObj, JsValue[] arguments)
    {
        var bits = TypeConverter.ToIndex(_realm, arguments.At(0));
        var bigint = TypeConverter.ToBigInt(arguments.At(1));

        return JsBigInt.Create(bigint % (BigInteger) System.Math.Pow(2, bits));
    }

    public override JsValue Call(JsValue thisObject, JsValue[] arguments)
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

        return JsBigInt.Create(prim);
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
    public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        var value = arguments.Length > 0
            ? JsBigInt.Create(arguments[0])
            : JsBigInt.Zero;

        if (newTarget.IsUndefined())
        {
            return Construct(value);
        }

        var o = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.BigInt.PrototypeObject,
            static (engine, realm, state) => new BigIntInstance(engine, (JsBigInt) state), value);
        return o;
    }

    public BigIntPrototype PrototypeObject { get; }

    public BigIntInstance Construct(JsBigInt value)
    {
        var instance = new BigIntInstance(Engine)
        {
            _prototype = PrototypeObject,
            BigIntData = value
        };

        return instance;
    }
}