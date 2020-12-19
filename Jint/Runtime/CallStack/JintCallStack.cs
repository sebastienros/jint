#nullable  enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jint.Runtime.CallStack
{
    internal class JintCallStack : IEnumerable<CallStackElement>
    {
        private readonly Stack<CallStackElement> _stack = new();
        private readonly Dictionary<CallStackElement, int>? _statistics;

        public JintCallStack(bool trackRecursionDepth)
        {
            if (trackRecursionDepth)
            {
                _statistics = new Dictionary<CallStackElement, int>(CallStackElementComparer.Instance);
            }
        }

        public int Push(in CallStackElement item)
        {
            _stack.Push(item);
            if (_statistics is not null)
            {
                if (_statistics.ContainsKey(item))
                {
                    return ++_statistics[item];
                }
                else
                {
                    _statistics.Add(item, 0);
                    return 0;
                }
            }
            return -1;
        }

        public CallStackElement Pop()
        {
            var item = _stack.Pop();
            if (_statistics is not null)
            {
                if (_statistics[item] == 0)
                {
                    _statistics.Remove(item);
                }
                else
                {
                    _statistics[item]--;
                }
            }

            return item;
        }

        public void Clear()
        {
            _stack.Clear();
            _statistics?.Clear();
        }

        public IEnumerator<CallStackElement> GetEnumerator()
        {
            return _stack.GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join("->", _stack.Select(cse => cse.ToString()).Reverse());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
