using System.Numerics;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.BigInt;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-bigint-constructor
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class BigIntConstructor : Constructor
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

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.asintn
    /// </summary>
    [JsFunction]
    private JsValue AsIntN(JsValue thisObject, JsValue bitsValue, JsValue bigintValue)
    {
        var bits = TypeConverter.ToIndex(_realm, bitsValue);
        var bigint = bigintValue.ToBigInteger(_engine);

        // A value that already fits in that many signed bits is its own result, whatever the count. One that does
        // not is wider than bits, so bits fits in an int and 2^bits is never larger than the value itself.
        if ((ulong) bigint.GetBitLength() < bits)
        {
            return bigint;
        }

        var modulus = BigInteger.One << (int) bits;
        var mod = bigint & (modulus - BigInteger.One);
        if (bits > 0 && mod >= modulus >> 1)
        {
            return mod - modulus;
        }

        return mod;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bigint.asuintn
    /// </summary>
    [JsFunction]
    private JsValue AsUintN(JsValue thisObject, JsValue bitsValue, JsValue bigintValue)
    {
        var bits = TypeConverter.ToIndex(_realm, bitsValue);
        var bigint = bigintValue.ToBigInteger(_engine);

        // A non-negative value that already fits is its own result, and a wider one is cut down to its low bits.
        // Only a negative value grows: its result is bits wide whatever its own size.
        if (bigint.Sign >= 0)
        {
            if ((ulong) bigint.GetBitLength() <= bits)
            {
                return bigint;
            }
        }
        else if (bits > JsBigInt.MaxBitLength)
        {
            Throw.RangeError(_realm, "Maximum BigInt size exceeded");
        }

        return bigint & ((BigInteger.One << (int) bits) - BigInteger.One);
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
            return JsBigInt.Create(new BigInteger(value._value));
        }

        Throw.RangeError(_realm, "The number " + value + " cannot be converted to a BigInt because it is not an integer");
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
