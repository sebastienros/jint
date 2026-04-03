using Jint.Native;

namespace Jint.Runtime.Debugger;

/// <summary>
/// Provides data for the <see cref="DebugHandler.ExceptionThrown"/> event.
/// </summary>
public sealed class ExceptionThrownEventArgs : EventArgs
{
    private readonly Engine _engine;
    private readonly SourceLocation _location;
    private DebugCallStack? _callStack;

    internal ExceptionThrownEventArgs(Engine engine, JsValue thrownValue, in SourceLocation location)
    {
        _engine = engine;
        _location = location;
        ThrownValue = thrownValue;
    }

    /// <summary>
    /// The JavaScript value that was thrown.
    /// </summary>
    public JsValue ThrownValue { get; }

    /// <summary>
    /// The source location where the exception was thrown.
    /// </summary>
    public ref readonly SourceLocation Location => ref _location;

    /// <summary>
    /// The call stack at the point the exception was thrown.
    /// </summary>
    public DebugCallStack CallStack =>
        _callStack ??= new DebugCallStack(_engine, _location, _engine.CallStack, returnValue: null);
}
