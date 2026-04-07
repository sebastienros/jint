using Jint.Native;
using Jint.Pooling;
using Jint.Runtime;

namespace Jint.Collections;


/// <summary>
/// Cache reusable <see cref="Reference" /> instances as we allocate them a lot.
/// </summary>
internal sealed class ObjectTraverseStackPool
{
    private const int PoolSize = 10;
    private readonly ObjectPool<ObjectTraverseStack> _pool;

    public ObjectTraverseStackPool()
    {
        _pool = new ObjectPool<ObjectTraverseStack>(Factory, PoolSize);
    }

    private static ObjectTraverseStack Factory()
    {
        return new ObjectTraverseStack(null!);
    }

    public ObjectTraverseStack Rent(Engine engine)
    {
        var stack = _pool.Allocate();
        stack.Reset(engine);
        return stack;
    }

    public void Return(ObjectTraverseStack? reference)
    {
        if (reference == null)
        {
            return;
        }
        _pool.Free(reference);
    }
}

/// <summary>
/// Helps traversing objects and checks for cyclic references.
/// </summary>
internal sealed class ObjectTraverseStack
{
    private Engine _engine;
    private readonly Stack<object> _stack = new();

    public ObjectTraverseStack(Engine engine)
    {
        _engine = engine;
    }

    public bool TryEnter(JsValue value)
    {
        if (value is null)
        {
            Throw.ArgumentNullException(nameof(value));
        }

        if (_stack.Contains(value))
        {
            return false;
        }

        _stack.Push(value);

        return true;
    }

    public void Enter(JsValue value)
    {
        if (!TryEnter(value))
        {
            Throw.TypeError(_engine.Realm, "Cyclic reference detected.");
        }
    }

    public void Exit()
    {
        _stack.Pop();
    }

    public void Reset(Engine engine)
    {
        _stack.Clear();
        _engine = engine;
    }
}
