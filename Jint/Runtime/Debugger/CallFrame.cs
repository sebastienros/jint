using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.CallStack;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Debugger;

public sealed class CallFrame
{
    private readonly CallStackExecutionContext _context;
    private SourceLocation _location;
    private readonly CallStackElement? _element;
    private readonly Lazy<DebugScopes> _scopeChain;
    private readonly Engine _engine;
    private readonly long _generation;

    internal CallFrame(
        CallStackElement? element,
        in CallStackExecutionContext context,
        in SourceLocation location,
        JsValue? returnValue,
        int index,
        long generation)
    {
        _element = element;
        _context = context;
        _engine = context.LexicalEnvironment._engine;
        _location = location;
        ReturnValue = returnValue;
        Index = index;
        _generation = generation;

        _scopeChain = new Lazy<DebugScopes>(() => new DebugScopes(Environment));
    }

    private Environment Environment => _context.LexicalEnvironment;

    /// <summary>
    /// The engine this frame was taken from.
    /// </summary>
    internal Engine Engine => _engine;

    /// <summary>
    /// The lexical environment that was active in this frame when the debugger stopped.
    /// </summary>
    internal Environment LexicalEnvironment => _context.LexicalEnvironment;

    /// <summary>
    /// The function whose activation this frame is, or <see langword="null"/> for the global frame.
    /// </summary>
    internal Function? Function => _element?.Function;

    /// <summary>
    /// The value <see cref="DebugHandler"/> stamped on the notification this frame was taken from, so
    /// that a frame outliving its pause can be told apart from a live one.
    /// </summary>
    internal long Generation => _generation;

    /// <summary>
    /// Position of this call frame in the call stack, counting from zero at the currently executing frame.
    /// </summary>
    /// <remarks>
    /// The last frame is the global (or module) frame, so a frame's index also says how many calls deep
    /// it is. Indices belong to the pause they were taken in: the same function occupies a different
    /// index the next time the debugger stops.
    /// </remarks>
    public int Index { get; }

    /// <summary>
    /// Name of the function of this call frame. For global scope, this will be "(anonymous)".
    /// </summary>
    public string FunctionName => _element?.ToString() ?? "(anonymous)";

    /// <summary>
    /// Source location of function of this call frame.
    /// </summary>
    /// <remarks>For top level (global) call frames, as well as functions not defined in script, this will be null.</remarks>
    public SourceLocation? FunctionLocation => (_element?.Function._functionDefinition?.Function as Node)?.Location;

    /// <summary>
    /// Currently executing source location in this call frame.
    /// </summary>
    public ref SourceLocation Location => ref _location;

    /// <summary>
    /// The scope chain of this call frame.
    /// </summary>
    public DebugScopes ScopeChain
    {
        get
        {
            using var ownership = _engine.EnterHostCall();
            return _scopeChain.Value;
        }
    }

    /// <summary>
    /// The value of <c>this</c> in this call frame.
    /// </summary>
    public JsValue This
    {
        get
        {
            using var ownership = _engine.EnterHostCall();
            var environment = _context.GetThisEnvironment();
            return environment.GetThisBinding();
        }
    }

    /// <summary>
    /// The return value of this call frame. Will be null for call frames that aren't at the top of the stack,
    /// as well as if execution is not at a return point.
    /// </summary>
    public JsValue? ReturnValue { get; }
}
