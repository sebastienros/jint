using Jint.Native.Object;

namespace Jint.Native.Disposable;

internal sealed class AsyncDisposableStackInstance : ObjectInstance
{
    public AsyncDisposableStackInstance(Engine engine) : base(engine)
    {
    }

    public DisposableState State { get; internal set; }
}
