using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.AsyncFunction;

/// <summary>
/// Tracks the execution state of an async function, enabling suspension at await points
/// and resumption when the awaited promise settles.
/// </summary>
internal sealed class AsyncFunctionInstance : ISuspendable
{
    internal AsyncFunctionState _state;

    /// <summary>
    /// The execution context captured when the async function started or last suspended.
    /// Used to restore the environment when resuming execution.
    /// </summary>
    internal ExecutionContext _savedContext;

    /// <summary>
    /// The promise capability representing this async function's return value.
    /// Resolved when the function completes normally, rejected on exception.
    /// </summary>
    internal PromiseCapability _capability = null!;

    /// <summary>
    /// The statement list being executed by this async function.
    /// Needed to resume execution from the saved position.
    /// </summary>
    internal JintStatementList? _body;

    /// <summary>
    /// The body function to execute (for expression bodies or statement list execution).
    /// </summary>
    internal Func<EvaluationContext, Completion>? _bodyFunction;

    /// <summary>
    /// The AST node (AwaitExpression) where execution last suspended.
    /// Used for node-based tracking similar to generator yield tracking.
    /// </summary>
    internal object? _lastAwaitNode;

    /// <summary>
    /// The value from the settled promise, to be returned when resuming at the await point.
    /// </summary>
    internal JsValue? _resumeValue;

    /// <summary>
    /// If true, the resumed await should throw the _resumeValue instead of returning it.
    /// Set when the awaited promise was rejected.
    /// </summary>
    internal bool _resumeWithThrow;

    /// <summary>
    /// Signals that we are resuming from a suspended await point.
    /// When true, the await expression at _lastAwaitNode should return _resumeValue.
    /// </summary>
    internal bool _isResuming;

    public SuspendDataDictionary Data { get; } = new();

    /// <summary>
    /// Stores the resolved values of completed await expressions.
    /// For expression bodies that re-evaluate on resume, already-completed awaits
    /// return their cached values instead of re-suspending.
    /// </summary>
    internal Dictionary<object, JsValue>? _completedAwaits;

    // ISuspendable implementation
    bool ISuspendable.IsSuspended => _state == AsyncFunctionState.SuspendedAwait;

    bool ISuspendable.IsResuming
    {
        get => _isResuming;
        set => _isResuming = value;
    }

    JsValue? ISuspendable.SuspendedValue => _resumeValue;

    object? ISuspendable.LastSuspensionNode => _lastAwaitNode;

    bool ISuspendable.ReturnRequested => false; // Async functions don't have return() like generators

    /// <summary>
    /// Tracks the pending completion type when suspended in a finally block.
    /// </summary>
    CompletionType ISuspendable.PendingCompletionType { get; set; }

    /// <summary>
    /// Tracks the pending completion value when suspended in a finally block.
    /// </summary>
    JsValue? ISuspendable.PendingCompletionValue { get; set; }

    /// <summary>
    /// The try statement whose finally block we're currently executing.
    /// Used to properly resume execution in finally blocks.
    /// </summary>
    object? ISuspendable.CurrentFinallyStatement { get; set; }
}

/// <summary>
/// The execution state of an async function.
/// </summary>
internal enum AsyncFunctionState
{
    /// <summary>
    /// The async function is currently executing statements.
    /// </summary>
    Executing,

    /// <summary>
    /// The async function is suspended at an await expression, waiting for a promise to settle.
    /// </summary>
    SuspendedAwait,

    /// <summary>
    /// The async function has completed (either resolved or rejected its return promise).
    /// </summary>
    Completed
}
