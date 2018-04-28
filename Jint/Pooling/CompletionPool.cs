using Esprima;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Pooling
{
    /// <summary>
    /// Cache reusable <see cref="Completion" /> instances as we allocate them a lot.
    /// </summary>
    internal sealed class CompletionPool
    {
        private const int PoolSize = 15;
        private readonly ObjectPool<Completion> _pool;

        public CompletionPool()
        {
            _pool = new ObjectPool<Completion>(Factory, PoolSize);
        }

        private static Completion Factory()
        {
            return new Completion(CompletionType.Normal, JsValue.Undefined, string.Empty);
        }
        
        public Completion Rent(CompletionType type, JsValue value, string identifier, Location location = null)
        {
            return _pool.Allocate().Reassign(type, value, identifier, location);
        }

        public void Return(Completion completion)
        {
            if (completion == null
                || completion == Completion.Empty
                || completion == Completion.EmptyUndefined)
            {
                return;
            }
            _pool.Free(completion);;
        }
    }
}