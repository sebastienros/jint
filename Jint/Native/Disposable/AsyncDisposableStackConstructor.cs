using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Disposable;

internal sealed class AsyncDisposableStackConstructor : Constructor
{
    private static readonly JsString _name = new("AsyncDisposableStack");

    public AsyncDisposableStackConstructor(Engine engine, Realm realm) : base(engine, realm, _name)
    {
        PrototypeObject = new AsyncDisposableStackPrototype(engine, realm, this, engine.Intrinsics.Object.PrototypeObject);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal AsyncDisposableStackPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm);
        }

        var stack = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.AsyncDisposableStack.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new DisposableStack(engine, DisposeHint.Async));

        return stack;
    }
}
