using Jint.Native;

namespace Jint.Runtime.Debugger;

public sealed class DebugInformation : EventArgs
{
    private readonly Engine _engine;
    private readonly SourceLocation _currentLocation;
    private readonly JsValue? _returnValue;
    private readonly long _generation;

    private DebugCallStack? _callStack;

    internal DebugInformation(
        Engine engine,
        Node? currentNode,
        in SourceLocation currentLocation,
        JsValue? returnValue,
        long currentMemoryUsage,
        PauseType pauseType,
        BreakPoint? breakPoint,
        long generation,
        JsValue? thrownValue = null,
        bool isUncaught = false)
    {
        _engine = engine;
        _generation = generation;
        CurrentNode = currentNode;
        _currentLocation = currentLocation;
        _returnValue = returnValue;
        CurrentMemoryUsage = currentMemoryUsage;
        PauseType = pauseType;
        BreakPoint = breakPoint;
        ThrownValue = thrownValue;
        IsUncaught = isUncaught;
    }

    /// <summary>
    /// Gets the value that was thrown, or <see langword="null"/> unless this is a
    /// <see cref="PauseType.Exception"/> pause.
    /// </summary>
    /// <remarks>
    /// It is the thrown value itself, whatever its type — an <c>Error</c> object for a normal throw, but a
    /// number for <c>throw 42</c> — and never a wrapper around it.
    /// </remarks>
    public JsValue? ThrownValue { get; }

    /// <summary>
    /// Whether no <c>catch</c> clause on the stack will handle the thrown exception.
    /// </summary>
    /// <remarks>
    /// Meaningful only for a <see cref="PauseType.Exception"/> pause; it is
    /// <see langword="false"/> for every other pause. See
    /// <see cref="DebugHandler.PauseOnExceptions"/> for what counts as caught.
    /// </remarks>
    public bool IsUncaught { get; }

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
    public DebugCallStack CallStack
    {
        get
        {
            using var ownership = _engine.EnterHostCall();
            return _callStack ??= new DebugCallStack(_engine, _currentLocation, _engine.CallStack, _returnValue, _generation);
        }
    }

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
