using Jint.Native;

namespace Jint.Runtime.Debugger;

public sealed class DebugInformation : EventArgs
{
    private readonly Engine _engine;
    private readonly SourceLocation _currentLocation;
    private readonly JsValue? _returnValue;

    private DebugCallStack? _callStack;

    internal DebugInformation(
        Engine engine,
        Node? currentNode,
        in SourceLocation currentLocation,
        JsValue? returnValue,
        long currentMemoryUsage,
        PauseType pauseType,
        BreakPoint? breakPoint)
    {
        _engine = engine;
        CurrentNode = currentNode;
        _currentLocation = currentLocation;
        _returnValue = returnValue;
        CurrentMemoryUsage = currentMemoryUsage;
        PauseType = pauseType;
        BreakPoint = breakPoint;
    }

    /// <summary>
    /// Indicates the type of pause that resulted in this DebugInformation being generated.
    /// </summary>
    public PauseType PauseType { get; }

    /// <summary>
    /// Breakpoint at the current location. This will be set even if the pause wasn't caused by the breakpoint.
    /// </summary>
    public BreakPoint? BreakPoint { get; }

    /// <summary>
    /// The current call stack.
    /// </summary>
    /// <remarks>This will always include at least a call frame for the global environment.</remarks>
    public DebugCallStack CallStack =>
        _callStack ??= new DebugCallStack(_engine, _currentLocation, _engine.CallStack, _returnValue);

    /// <summary>
    /// The AST Node that will be executed on next step.
    /// Note that this will be null when execution is at a return point.
    /// </summary>
    public Node? CurrentNode { get; }

    /// <summary>
    /// The current source Location.
    /// For return points, this starts and ends at the end of the function body.
    /// </summary>
    public ref readonly SourceLocation Location => ref CurrentCallFrame.Location;

    /// <summary>
    /// Not implemented. Will always return 0.
    /// </summary>
    public long CurrentMemoryUsage { get; }

    /// <summary>
    /// The currently executing call frame.
    /// </summary>
    public CallFrame CurrentCallFrame => CallStack[0];

    /// <summary>
    /// The scope chain of the currently executing call frame.
    /// </summary>
    public DebugScopes CurrentScopeChain => CurrentCallFrame.ScopeChain;

    /// <summary>
    /// The return value of the currently executing call frame.
    /// This is null if execution is not at a return point.
    /// </summary>
    public JsValue? ReturnValue => CurrentCallFrame.ReturnValue;
}
