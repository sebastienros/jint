using System.Collections;
using Jint.Native;
using Jint.Runtime.CallStack;

namespace Jint.Runtime.Debugger;

public sealed class DebugCallStack : IReadOnlyList<CallFrame>
{
    private readonly List<CallFrame> _stack;

    internal DebugCallStack(Engine engine, SourceLocation location, JintCallStack callStack, JsValue? returnValue)
    {
        _stack = new List<CallFrame>(callStack.Count + 1);
        var executionContext = new CallStackExecutionContext(engine.ExecutionContext);
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

    public CallFrame this[int index] => _stack[index];

    public int Count => _stack.Count;

    public IEnumerator<CallFrame> GetEnumerator()
    {
        return _stack.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
