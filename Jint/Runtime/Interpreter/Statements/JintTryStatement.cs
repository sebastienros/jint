using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-try-statement
/// </summary>
internal sealed class JintTryStatement : JintStatement<TryStatement>
{
    private readonly JintBlockStatement _block;
    private JintBlockStatement? _catch;
    private readonly JintBlockStatement? _finalizer;

    public JintTryStatement(TryStatement statement) : base(statement)
    {
        _block = new JintBlockStatement(statement.Block);
        if (statement.Finalizer != null)
        {
            _finalizer = new JintBlockStatement(statement.Finalizer);
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        // Check if we're resuming from inside the catch or finally block
        // If so, skip the try block and go directly to the appropriate block
        var suspensionNode = GetSuspensionNode(suspendable);
        if (suspensionNode is not null)
        {
            if (_statement.Handler is not null && IsNodeInsideRange(suspensionNode, _statement.Handler.Range))
            {
                // Resuming from inside catch block - execute catch directly
                return ExecuteCatchResume(context);
            }
            if (_statement.Finalizer is not null && IsNodeInsideRange(suspensionNode, _statement.Finalizer.Range))
            {
                // Resuming from inside finally block - execute finally directly
                return ExecuteFinallyResume(context, suspendable!);
            }
        }

        // Check if we're resuming from inside the finally block (async functions use CurrentFinallyStatement)
        if (suspendable is { IsResuming: true } && ReferenceEquals(suspendable.CurrentFinallyStatement, this))
        {
            // Resuming from inside finally block - execute finally directly
            return ExecuteFinallyResume(context, suspendable!);
        }

        var b = _block.Execute(context);

        if (b.Type == CompletionType.Throw)
        {
            b = ExecuteCatch(context, b, engine);
        }

        // If a generator/async is suspended, don't run the finally yet.
        // The finally will run when we resume and exit the try block.
        if (context.IsSuspended())
        {
            return b;
        }

        return ExecuteFinalizer(context, b, engine, suspendable);
    }

    private Completion ExecuteCatchResume(EvaluationContext context)
    {
        // Initialize catch block if needed
        if (_catch is null && _statement.Handler is not null)
        {
            _catch = new JintBlockStatement(_statement.Handler.Body);
        }

        if (_catch is null)
        {
            return Completion.Empty();
        }

        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        var suspendData = GetCatchSuspendData(suspendable);
        if (suspendData?.CatchEnvironment is not null)
        {
            engine.UpdateLexicalEnvironment(suspendData.CatchEnvironment);
        }

        // Execute catch block (it will resume from the saved position)
        var b = _catch.Execute(context);

        // If suspended (yield/await), don't run the finally yet
        if (context.IsSuspended())
        {
            RestoreOuterEnvironmentAfterCatchResume(engine, suspendData);
            return b;
        }

        RestoreOuterEnvironmentAfterCatchResume(engine, suspendData);
        suspendable?.Data.Clear(this);

        return ExecuteFinalizer(context, b, engine, suspendable);
    }

    private Completion ExecuteFinallyResume(EvaluationContext context, ISuspendable suspendable)
    {
        // Execute finally block (it will resume from the saved position)
        var f = _finalizer!.Execute(context);

        // If still suspended, don't process the pending completion yet
        if (context.IsSuspended())
        {
            return f;
        }

        // After finally completes normally, restore the pending completion
        if (f.Type == CompletionType.Normal)
        {
            var pendingType = suspendable.PendingCompletionType;
            var pendingValue = suspendable.PendingCompletionValue;

            // Clear the pending completion
            suspendable.PendingCompletionType = CompletionType.Normal;
            suspendable.PendingCompletionValue = null;
            suspendable.CurrentFinallyStatement = null;

            if (pendingType == CompletionType.Throw)
            {
                return new Completion(CompletionType.Throw, pendingValue ?? JsValue.Undefined, _statement);
            }

            if (pendingType == CompletionType.Return)
            {
                return new Completion(CompletionType.Return, pendingValue ?? JsValue.Undefined, _statement);
            }
        }

        return f.UpdateEmpty(JsValue.Undefined);
    }

    private Completion ExecuteCatch(EvaluationContext context, Completion b, Engine engine)
    {
        // execute catch
        if (_statement.Handler is not null)
        {
            // initialize lazily
            if (_catch is null)
            {
                _catch = new JintBlockStatement(_statement.Handler.Body);
            }

            // https://tc39.es/ecma262/#sec-runtime-semantics-catchclauseevaluation

            var thrownValue = b.Value;
            var oldEnv = engine.ExecutionContext.LexicalEnvironment;
            var catchEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv, catchEnvironment: true);

            var boundNames = new List<Key>();
            _statement.Handler.Param.GetBoundNames(boundNames);

            for (var i = 0; i < boundNames.Count; i++)
            {
                catchEnv.CreateMutableBinding(boundNames[i]);
            }

            engine.UpdateLexicalEnvironment(catchEnv);

            var catchParam = _statement.Handler?.Param;
            catchParam.BindingInitialization(context, thrownValue, catchEnv);

            b = _catch.Execute(context);

            var suspendable = engine.ExecutionContext.Suspendable;
            if (context.IsSuspended() && suspendable is not null)
            {
                var suspendData = suspendable.Data.GetOrCreate<CatchSuspendData>(this);
                suspendData.CatchEnvironment = catchEnv;
                suspendData.OuterEnvironment = oldEnv;
            }
            else
            {
                suspendable?.Data.Clear(this);
            }

            engine.UpdateLexicalEnvironment(oldEnv);
        }

        return b;
    }

    private Completion ExecuteFinalizer(EvaluationContext context, Completion b, Engine engine, ISuspendable? suspendable)
    {
        if (_finalizer is null)
        {
            return b.UpdateEmpty(JsValue.Undefined);
        }

        // Save the pending completion before running finally. If finally awaits,
        // ExecuteFinallyResume restores this completion after the await resumes.
        if (suspendable is not null && (b.Type == CompletionType.Throw || b.Type == CompletionType.Return))
        {
            suspendable.PendingCompletionType = b.Type;
            suspendable.PendingCompletionValue = b.Value;
            suspendable.CurrentFinallyStatement = this;
        }

        // Clear _returnRequested before running finally block.
        // Per ECMAScript spec, a return in the finally block supersedes any pending return.
        // If we don't clear this, the finally block's statements will incorrectly use _suspendedValue.
        var generator = engine.ExecutionContext.Generator;
        if (generator is not null)
        {
            generator._returnRequested = false;
        }

        var asyncGenerator = engine.ExecutionContext.AsyncGenerator;
        if (asyncGenerator is not null)
        {
            asyncGenerator._returnRequested = false;
        }

        var f = _finalizer.Execute(context);

        // Check for suspension in finally
        if (context.IsSuspended())
        {
            // Suspended in finally - the pending completion is preserved
            return f;
        }

        // Clear the pending completion tracking if we completed normally
        if (suspendable is not null && ReferenceEquals(suspendable.CurrentFinallyStatement, this))
        {
            suspendable.CurrentFinallyStatement = null;
            suspendable.PendingCompletionType = CompletionType.Normal;
            suspendable.PendingCompletionValue = null;
        }

        if (f.Type == CompletionType.Normal)
        {
            // Per spec: If F.[[type]] is normal, let F be B.
            // And step 6: If F.[[value]] is empty, return undefined
            return b.UpdateEmpty(JsValue.Undefined);
        }

        return f.UpdateEmpty(JsValue.Undefined);
    }

    private CatchSuspendData? GetCatchSuspendData(ISuspendable? suspendable)
    {
        return suspendable?.Data.TryGet(this, out CatchSuspendData? suspendData) == true
            ? suspendData
            : null;
    }

    private static void RestoreOuterEnvironmentAfterCatchResume(Engine engine, CatchSuspendData? suspendData)
    {
        if (suspendData?.OuterEnvironment is not null)
        {
            engine.UpdateLexicalEnvironment(suspendData.OuterEnvironment);
            return;
        }

        if (engine.ExecutionContext.LexicalEnvironment is DeclarativeEnvironment { _catchEnvironment: true, _outerEnv: { } outerEnv })
        {
            engine.UpdateLexicalEnvironment(outerEnv);
        }
    }
}
