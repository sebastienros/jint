using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime.Domain;

internal sealed class UuidConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Uuid");

    private UuidConstructor(Engine engine) : base(engine, engine.Realm, _functionName)
    {
    }

    private JsValue Parse(JsValue @this, JsValue[] arguments)
    {
        switch (arguments.At(0))
        {
            case JsUuid uid:
                return Construct(uid);

            case JsValue js when Guid.TryParse(js.AsString(), out var res):
                return Construct(res);
        }

        return Undefined;
    }

    protected internal override ObjectInstance GetPrototypeOf() => _prototype;

    internal new ObjectInstance _prototype;

    public UuidPrototype PrototypeObject { get; private set; }

    public static UuidConstructor CreateUuidConstructor(Engine engine)
    {
        var obj = new UuidConstructor(engine)
        {
            // The value of the [[Prototype]] internal property of the Uuid constructor is the Function prototype object
            _prototype = engine.Realm.Intrinsics.Function.PrototypeObject
        };
        obj.PrototypeObject = UuidPrototype.CreatePrototypeObject(engine, obj);

        // The initial value of Uuid.prototype is the Date prototype object
        obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, false, false, false));

        engine.SetValue("Uuid", obj);
        obj.Configure();
        obj.PrototypeObject.Configure();

        return obj;
    }

    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments) => Construct(arguments, null);

    public void Configure()
    {
        FastSetProperty("parse", new PropertyDescriptor(new ClrFunction(Engine, "parse", Parse), true, false, true));
        FastSetProperty("Empty", new PropertyDescriptor(JsUuid.Empty, true, false, true));
    }

    public UuidInstance Construct(JsUuid uuid) => new UuidInstance(Engine) { PrimitiveValue = uuid, _prototype = PrototypeObject };

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget) => Construct(Guid.NewGuid());
}