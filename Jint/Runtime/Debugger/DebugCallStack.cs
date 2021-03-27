using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.CallStack;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public class DebugCallStack : IReadOnlyList<CallFrame>
    {
        private List<CallFrame> _stack;
        public CallFrame this[int index] => _stack[index];

        public int Count => _stack.Count;

        internal DebugCallStack(Engine engine, Location location, JintCallStack callStack, JsValue returnValue)
        {
            _stack = new List<CallFrame>();
            var executionContext = engine.ExecutionContext;
            foreach (var element in callStack.Stack)
            {
                _stack.Add(new CallFrame(element, executionContext, location, returnValue));
                location = element.Location;
                returnValue = null;
                executionContext = element.CallingExecutionContext;
            }
            // Add root location
            _stack.Add(new CallFrame(null, executionContext, location, returnValue: null));
        }

        public IEnumerator<CallFrame> GetEnumerator()
        {
            return _stack.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
