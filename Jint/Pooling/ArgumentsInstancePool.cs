using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Pooling;

/// <summary>
/// Cache reusable <see cref="JsArguments" /> instances as we allocate them a lot.
/// </summary>
internal sealed class ArgumentsInstancePool
{
    private const int PoolSize = 10;
    private readonly Engine _engine;
    private readonly ObjectPool<JsArguments> _pool;

    public ArgumentsInstancePool(Engine engine)
    {
        _engine = engine;
        _pool = new ObjectPool<JsArguments>(Factory, PoolSize);
    }

    private JsArguments Factory()
    {
        return new JsArguments(_engine)
        {
            _prototype = _engine.Realm.Intrinsics.Object.PrototypeObject
        };
    }

    public JsArguments Rent(JsValue[] argumentsList) => Rent(null, null, argumentsList, null, false);

    public JsArguments Rent(
        Function? func,
        Key[]? formals,
        JsValue[] argumentsList,
        DeclarativeEnvironment? env,
        bool hasRestParameter)
    {
        var obj = _pool.Allocate();
        obj.Prepare(func!, formals!, argumentsList, env!, hasRestParameter);
        return obj;
    }

    public void Return(JsArguments instance)
    {
        if (instance is null)
        {
            return;
        }
        _pool.Free(instance);
    }
}
