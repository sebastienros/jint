using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Boolean;

internal sealed class BooleanConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Boolean");

    internal BooleanConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new BooleanPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public BooleanPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            return false;
        }

        return TypeConverter.ToBoolean(arguments[0]);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-boolean-constructor-boolean-value
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var b = TypeConverter.ToBoolean(arguments.At(0))
            ? JsBoolean.True
            : JsBoolean.False;

        if (newTarget.IsUndefined())
        {
            return Construct(b);
        }

        var o = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Boolean.PrototypeObject,
            static (engine, realm, state) => new BooleanInstance(engine, state!), b);
        return o;
    }

    public BooleanInstance Construct(JsBoolean value)
    {
        var instance = new BooleanInstance(Engine, value)
        {
            _prototype = PrototypeObject
        };

        return instance;
    }
}
