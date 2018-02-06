using System;
using Esprima;
using Jint.Native;
using Jint.Runtime;

namespace Jint
{
    /// <summary>
    /// Cache reusable <see cref="Completion" /> instances as we allocate them a lot.
    /// </summary>
    internal sealed class CompletionPool
    {
        private const int PoolSize = 15;
        private readonly Completion[] _pool;
        private int _currentSize;

        public CompletionPool()
        {
            // pre-allocate so we don't show up in benchmarks
            _pool = new Completion[PoolSize];
            for (var i = 0; i < PoolSize; ++i)
            {
                _pool[i] = new Completion(string.Empty, JsValue.Undefined, string.Empty);
            }

            _currentSize = PoolSize;
        }

        public Completion Rent(string type, JsValue value, string identifier, Location location = null)
        {
            if (type == Completion.Normal)
            {
                if (identifier == null)
                {
                    if (value == null)
                    {
                        return Completion.Empty;
                    }

                    if (ReferenceEquals(value, Undefined.Instance))
                    {
                        return Completion.EmptyUndefined;
                    }
                }
            }

            if (_currentSize > 0)
            {
                _currentSize--;
                var completion = _pool[_currentSize];
                return completion.Reassign(type, value, identifier, location);
            }

            return new Completion(type, value, identifier, location);
        }

        public void Return(Completion completion)
        {
            if (completion == null
                || _currentSize >= PoolSize
                || ReferenceEquals(completion, Completion.Empty)
                || ReferenceEquals(completion, Completion.EmptyUndefined))
            {
                return;
            }
            _pool[_currentSize] = completion;
            _currentSize++;
        }
    }
}