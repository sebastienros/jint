using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-generator-instances
/// </summary>
internal sealed class GeneratorInstance : ObjectInstance, ISuspendable
{
    internal GeneratorState _generatorState;
    private ExecutionContext _generatorContext;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private readonly JsValue? _generatorBrand;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    private JintStatementList _generatorBody = null!;

    public JsValue? _nextValue;
    public JsValue? _error;

    /// <summary>
    /// Tracks whether we are resuming from a suspended yield (vs first call from SuspendedStart).
    /// When true, the first yield expression encountered should return _nextValue instead of yielding.
    /// </summary>
    internal bool _isResuming;

    /// <summary>
    /// Stores the value that was yielded when the generator suspended.
    /// This is needed because the statement containing the yield might have a different completion value
    /// (e.g., variable declarations return Empty, not the yielded value).
    /// </summary>
    internal JsValue? _suspendedValue;

    /// <summary>
    /// The yield expression node we suspended at. Used for node-based yield tracking
    /// which works correctly for both loops (same node) and multi-yield expressions (different nodes).
    /// </summary>
    internal object? _lastYieldNode;

    /// <summary>
    /// Maps yield expression nodes to their return values from previous resumes.
    /// When re-executing code with yields (e.g., in loops), yields that have already been
    /// resumed return their stored value instead of yielding again.
    /// </summary>
    internal Dictionary<object, JsValue>? _yieldNodeValues;

    /// <summary>
    /// The iterator we're delegating to during yield* evaluation.
    /// When this is non-null, we're in the middle of yield* delegation.
    /// </summary>
    internal IteratorInstance? _delegatingIterator;

    /// <summary>
    /// The yield* expression we're delegating from.
    /// </summary>
    internal object? _delegatingYieldNode;

    /// <summary>
    /// The type of completion we received when resuming during yield* delegation.
    /// </summary>
    internal CompletionType _delegationResumeType;

    /// <summary>
    /// When yield* suspends, stores the inner iterator's result object directly.
    /// This allows us to pass through the exact result (including its 'done' property state)
    /// instead of creating a new result with done: false.
    /// </summary>
    internal ObjectInstance? _delegationInnerResult;

    /// <summary>
    /// Signals that generator.return() was called and execution should complete
    /// after running finally blocks. Aligns with spec's Return completion handling
    /// in GeneratorResumeAbrupt.
    /// </summary>
    internal bool _returnRequested;

    /// <summary>
    /// The completion type used when resuming via GeneratorResumeAbrupt.
    /// Used by yield expressions to know if they should trigger an early return.
    /// </summary>
    internal CompletionType _resumeCompletionType;

    /// <summary>
    /// Tracks a pending completion (throw/return) that needs to be processed after a finally block
    /// that yielded. When a finally block yields, we need to remember any pending completion
    /// so we can restore it after the finally block completes.
    /// </summary>
    internal CompletionType _pendingCompletionType;

    /// <summary>
    /// The value associated with the pending completion (the thrown error or return value).
    /// </summary>
    internal JsValue? _pendingCompletionValue;

    /// <summary>
    /// Tracks which try statement's finally block we're currently inside.
    /// Used to properly restore pending completion after finally completes.
    /// </summary>
    internal object? _currentFinallyStatement;

    public SuspendDataDictionary SuspendData { get; } = new();

    // ISuspendable implementation
    bool ISuspendable.IsSuspended => _generatorState == GeneratorState.SuspendedYield;

    bool ISuspendable.IsResuming
    {
        get => _isResuming;
        set => _isResuming = value;
    }

    JsValue? ISuspendable.SuspendedValue => _suspendedValue;

    object? ISuspendable.LastSuspensionNode => _lastYieldNode;

    bool ISuspendable.ReturnRequested => _returnRequested;

    CompletionType ISuspendable.PendingCompletionType
    {
        get => _pendingCompletionType;
        set => _pendingCompletionType = value;
    }

    JsValue? ISuspendable.PendingCompletionValue
    {
        get => _pendingCompletionValue;
        set => _pendingCompletionValue = value;
    }

    object? ISuspendable.CurrentFinallyStatement
    {
        get => _currentFinallyStatement;
        set => _currentFinallyStatement = value;
    }

    public GeneratorInstance(Engine engine) : base(engine)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorstart
    /// </summary>
    public JsValue GeneratorStart(JintStatementList generatorBody)
    {
        var genContext = _engine.UpdateGenerator(this);
        _generatorBody = generatorBody;

        _generatorContext = genContext;
        _generatorState = GeneratorState.SuspendedStart;

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorresume
    /// </summary>
    public ObjectInstance GeneratorResume(JsValue? value, JsValue? generatorBrand)
    {
        var state = GeneratorValidate(generatorBrand);
        if (state == GeneratorState.Completed)
        {
            return new IteratorResult(_engine, Undefined, JsBoolean.True);
        }

        var genContext = _generatorContext;
        var methodContext = _engine.ExecutionContext;

        // 6. Suspend methodContext.

        _nextValue = value;

        // Track if we're resuming from a yield (vs first call from SuspendedStart)
        // When resuming from SuspendedYield, the first yield encountered should return _nextValue
        _isResuming = (state == GeneratorState.SuspendedYield);

        // Clear the suspended value from previous suspension
        _suspendedValue = null;

        var context = _engine._activeEvaluationContext;
        return ResumeExecution(genContext, context!);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorresumeabrupt
    /// </summary>
    public JsValue GeneratorResumeAbrupt(in Completion abruptCompletion, JsValue? generatorBrand)
    {
        var state = GeneratorValidate(generatorBrand);
        if (state == GeneratorState.SuspendedStart)
        {
            _generatorState = GeneratorState.Completed;
            state = GeneratorState.Completed;
        }

        if (state == GeneratorState.Completed)
        {
            if (abruptCompletion.Type == CompletionType.Return)
            {
                return new IteratorResult(_engine, abruptCompletion.Value, JsBoolean.True);
            }

            Throw.JavaScriptException(_engine, abruptCompletion.Value, AstExtensions.DefaultLocation);
        }

        var genContext = _generatorContext;

        // Store the value for resumption
        _nextValue = abruptCompletion.Value;
        _isResuming = true;

        // Track the completion type for the yield expression to handle
        _resumeCompletionType = abruptCompletion.Type;

        // If we're in a delegation, set the resume type so the delegation loop handles it
        if (_delegatingIterator is not null)
        {
            _delegationResumeType = abruptCompletion.Type;
        }
        // For Throw/Return completion: resume execution so try-catch/try-finally can handle properly
        // The exception will be thrown at the yield point by JintYieldExpression

        // Clear the suspended value from previous suspension
        _suspendedValue = null;

        var context = _engine._activeEvaluationContext;
        return ResumeExecution(genContext, context ?? new EvaluationContext(_engine));
    }

    private ObjectInstance ResumeExecution(in ExecutionContext genContext, EvaluationContext context)
    {
        _generatorState = GeneratorState.Executing;
        _engine.EnterExecutionContext(genContext);

        var result = _generatorBody.Execute(context);
        _engine.LeaveExecutionContext();

        ObjectInstance? resultValue = null;
        if (result.Type == CompletionType.Normal)
        {
            _generatorState = GeneratorState.Completed;
            // Per spec 25.4.3.4 step 13.a: normal completion becomes return undefined
            resultValue = IteratorResult.CreateValueIteratorPosition(_engine, JsValue.Undefined, done: JsBoolean.True);
        }
        else if (result.Type == CompletionType.Return)
        {
            if (_generatorState == GeneratorState.SuspendedYield)
            {
                // For yield* delegation, return the inner iterator's result directly
                // to preserve its exact 'done' property state (including undefined)
                if (_delegationInnerResult is not null)
                {
                    resultValue = _delegationInnerResult;
                    _delegationInnerResult = null;
                }
                else
                {
                    resultValue = IteratorResult.CreateValueIteratorPosition(_engine, result.Value, done: JsBoolean.False);
                }
            }
            else
            {
                _generatorState = GeneratorState.Completed;
                resultValue = IteratorResult.CreateValueIteratorPosition(_engine, result.Value, done: JsBoolean.True);
            }
        }

        if (result.Type == CompletionType.Throw)
        {
            _generatorState = GeneratorState.Completed;
            Throw.JavaScriptException(_engine, result.Value, result);
        }

        return resultValue!;
    }

    private GeneratorState GeneratorValidate(JsValue? generatorBrand)
    {
        if (!ReferenceEquals(generatorBrand, _generatorBrand))
        {
            Throw.TypeError(_engine.Realm, "Generator brand differs from attached brand");
        }

        if (_generatorState == GeneratorState.Executing)
        {
            Throw.TypeError(_engine.Realm, "Generator state was unexpectedly executing");
        }

        return _generatorState;
    }
}
