using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Runtime.CallStack;

namespace Jint.Runtime.Debugger
{
    public class DebugCallStackElement
    {
        public string ShortDescription { get; }
        public NodeList<Expression>? Arguments { get; }
        public Location Location { get; }

        internal DebugCallStackElement(CallStackElement element)
        {
            ShortDescription = element.ToString();
            Arguments = element.Arguments;
            Location = element.Location;
        }
    }

    public class DebugCallStack : IReadOnlyList<DebugCallStackElement>
    {
        private List<DebugCallStackElement> _stack;

        public DebugCallStackElement this[int index] => _stack[index];

        public int Count => _stack.Count;

        internal DebugCallStack(JintCallStack callStack)
        {
            _stack = callStack.Stack.Select(e => new DebugCallStackElement(e)).ToList();
        }

        public IEnumerator<DebugCallStackElement> GetEnumerator()
        {
            return _stack.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
