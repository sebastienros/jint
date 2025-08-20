using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Disposable;

internal sealed class DisposableStackConstructor : Constructor
{
    private static readonly JsString _name = new("DisposableStack");

    public DisposableStackConstructor(Engine engine, Realm realm) : base(engine, realm, _name)
    {
        PrototypeObject = new DisposableStackPrototype(engine, realm, this, engine.Intrinsics.Object.PrototypeObject);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal DisposableStackPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm);
        }

        var stack = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.DisposableStack.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new DisposableStack(engine, DisposeHint.Sync));

        return stack;
    }
}
