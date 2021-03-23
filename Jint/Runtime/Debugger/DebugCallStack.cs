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

        internal DebugCallStackElement(Location location, CallStackElement? element)
        {
            // e.g. Chromium uses "(anonymous)" for call from global scope - we do the same:
            ShortDescription = element?.ToString() ?? "(anonymous)";
            Arguments = element?.Arguments;
            Location = location;
        }
    }

    public class DebugCallStack : IReadOnlyList<DebugCallStackElement>
    {
        private List<DebugCallStackElement> _stack;

        public DebugCallStackElement this[int index] => _stack[index];

        public int Count => _stack.Count;

        internal DebugCallStack(Location location, JintCallStack callStack)
        {
            _stack = new List<DebugCallStackElement>();
            foreach (var element in callStack.Stack)
            {
                _stack.Add(new DebugCallStackElement(location, element));
                location = element.Location;
            }
            // Add root location
            _stack.Add(new DebugCallStackElement(location, null));
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
