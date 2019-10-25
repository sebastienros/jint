using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.CallStack
{

    public class JintCallStack : IEnumerable<CallStackElement>
    {
        internal JintCallStack Parent;
        private CallStackElement _element;

        private readonly IDictionary<CallStackElement, int> _statistics;


        internal JintCallStack(JintCallStack parent, CallStackElement element)
        {
            Parent = parent;
            _element = element;
            _statistics = parent == null
                ? new ConcurrentDictionary<CallStackElement, int>(new CallStackElementComparer())
                : new ConcurrentDictionary<CallStackElement, int>(parent._statistics, new CallStackElementComparer());

            if (element != null)
            {
                if (!_statistics.TryGetValue(element, out int i))
                {
                    i = -1;
                }

                _statistics[element] = i + 1;
            }
        }

        public int Depth => _statistics[_element];

        public IEnumerator<CallStackElement> GetEnumerator()
        {
            var current = this;
            var stack = new List<CallStackElement>();
            while (current?._element != null)
            {
                stack.Add(current._element);
                current = current.Parent;
            }

            return stack.GetEnumerator();
        }


        public override string ToString()
        {
            return string.Join("->", this.Select(cse => cse.ToString()).Reverse());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
