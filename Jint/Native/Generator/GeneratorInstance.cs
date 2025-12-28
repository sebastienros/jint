using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-generator-instances
/// </summary>
internal sealed class GeneratorInstance : ObjectInstance
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
    /// When set, indicates that the generator should complete immediately with this value.
    /// Used by yield* when the inner iterator's return method is null/undefined.
    /// </summary>
    internal JsValue? _earlyReturnValue;

    /// <summary>
    /// Whether an early return was triggered (e.g., from yield* with null return method).
    /// </summary>
    internal bool _shouldEarlyReturn;

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

        // If we're in a delegation, set the resume type so the delegation loop handles it
        if (_delegatingIterator is not null)
        {
            _delegationResumeType = abruptCompletion.Type;
        }
        else if (abruptCompletion.Type == CompletionType.Throw)
        {
            // Not delegating - throw immediately goes to exception handling
            _error = abruptCompletion.Value;
            Throw.JavaScriptException(_engine, _error, AstExtensions.DefaultLocation);
        }

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

        // Check for early return (e.g., from yield* with null return method)
        if (_shouldEarlyReturn)
        {
            var earlyValue = _earlyReturnValue ?? JsValue.Undefined;
            _shouldEarlyReturn = false;
            _earlyReturnValue = null;
            _generatorState = GeneratorState.Completed;
            return IteratorResult.CreateValueIteratorPosition(_engine, earlyValue, done: JsBoolean.True);
        }

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
                resultValue = IteratorResult.CreateValueIteratorPosition(_engine, result.Value, done: JsBoolean.False);
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
