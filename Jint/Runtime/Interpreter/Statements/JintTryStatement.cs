using System.Threading;
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

    // A plain `catch (e) { ... }` allocates a fresh DeclarativeEnvironment + dictionary node per entry just
    // to hold the single caught binding. When the catch parameter is a lone identifier and its binding cannot
    // escape (no closures/eval/with in the handler body), that env can use fixed-slot storage and be pooled
    // across catch entries — the same reuse precedent as JintForStatement's pooled loop environment.
    private readonly bool _canPoolCatchEnv;
    private readonly Key[]? _catchSlotNames;
    private DeclarativeEnvironment? _cachedCatchEnv;

    public JintTryStatement(TryStatement statement) : base(statement)
    {
        _block = new JintBlockStatement(statement.Block);
        if (statement.Finalizer != null)
        {
            _finalizer = new JintBlockStatement(statement.Finalizer);
        }

        if (statement.Handler is { Param: Identifier catchIdentifier } handler
            && !JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(handler.Body))
        {
            _catchSlotNames = new Key[] { catchIdentifier.Name };
            _canPoolCatchEnv = true;
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
        var suspendData = GetTrySuspendData(suspendable);
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

        // "If finallyResult is a normal completion, set finallyResult to blockResult" — the parked
        // record goes back whatever its type, so a Break or Continue resumes its jump here exactly
        // as ExecuteFinalizer performs it when nothing suspended. Returning the record itself, and
        // not a fresh one built from a type and a value, is what carries the jump's [[Target]]:
        // Completion.Target reads the label out of the jump statement the record carries.
        var suspendData = GetTrySuspendData(suspendable);
        var pending = suspendData?.PendingCompletion;
        if (suspendData is not null)
        {
            suspendData.PendingCompletion = null;
        }

        ReleaseFinallyDispatchHint(suspendable);

        if (f.Type == CompletionType.Normal && pending is { } b)
        {
            return b.UpdateEmpty(JsValue.Undefined);
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
            var suspendable = engine.ExecutionContext.Suspendable;

            DeclarativeEnvironment catchEnv;
            var pooled = _canPoolCatchEnv && suspendable is null;
            if (pooled)
            {
                // Pooled fixed-slot catch environment, reset and reused across entries. Gated on a
                // non-suspendable context so it never has to round-trip through the async/generator
                // suspend/resume save-and-restore machinery (which would keep the env live).
                var cachedEnv = Interlocked.Exchange(ref _cachedCatchEnv, null);
                if (cachedEnv is not null && ReferenceEquals(cachedEnv._engine, engine))
                {
                    // Reattach the outer reference (detached at park). Slots are overwritten below.
                    cachedEnv._outerEnv = oldEnv;
                    catchEnv = cachedEnv;
                }
                else
                {
                    catchEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv, catchEnvironment: true);
                    catchEnv._slotNames = _catchSlotNames;
                    catchEnv._slots = new Binding[1];
                }

                // Single-identifier catch binding: an initialized, mutable, non-deletable binding — the
                // exact shape CreateMutableBinding + BindingInitialization would produce for the identifier.
                catchEnv._slots![0] = new Binding(thrownValue, canBeDeleted: false, mutable: true, strict: false);
                engine.UpdateLexicalEnvironment(catchEnv);
            }
            else
            {
                catchEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv, catchEnvironment: true);

                var boundNames = new List<Key>();
                _statement.Handler.Param.GetBoundNames(boundNames);

                for (var i = 0; i < boundNames.Count; i++)
                {
                    catchEnv.CreateMutableBinding(boundNames[i]);
                }

                engine.UpdateLexicalEnvironment(catchEnv);

                var catchParam = _statement.Handler?.Param;
                catchParam.BindingInitialization(context, thrownValue, catchEnv);
            }

            b = _catch.Execute(context);

            if (context.IsSuspended() && suspendable is not null)
            {
                var suspendData = suspendable.Data.GetOrCreate<TrySuspendData>(this);
                suspendData.CatchEnvironment = catchEnv;
                suspendData.OuterEnvironment = oldEnv;
            }
            else
            {
                suspendable?.Data.Clear(this);
            }

            engine.UpdateLexicalEnvironment(oldEnv);

            // Park the pooled env for the next entry (clean, non-suspended completion only). Reset the slot
            // at park so the cached env doesn't root the caught value or the completed scope chain.
            if (pooled && !context.IsSuspended())
            {
                catchEnv._outerEnv = null;
                catchEnv._slots![0] = default;
                Interlocked.Exchange(ref _cachedCatchEnv, catchEnv);
            }
        }

        return b;
    }

    private Completion ExecuteFinalizer(EvaluationContext context, Completion b, Engine engine, ISuspendable? suspendable)
    {
        if (_finalizer is null)
        {
            return b.UpdateEmpty(JsValue.Undefined);
        }

        // Save the pending completion before running finally. If finally suspends,
        // ExecuteFinallyResume reinstates this completion after the yield/await resumes.
        // Every abrupt type is parked, Break and Continue included: step 3 of
        // https://tc39.es/ecma262/#sec-try-statement-runtime-semantics-evaluation restores
        // blockResult whatever its type, and a jump dropped here silently lets the enclosing
        // loops run the iterations it was meant to skip.
        var parkedOn = suspendable is not null && b.Type != CompletionType.Normal ? suspendable : null;
        if (parkedOn is not null)
        {
            parkedOn.Data.GetOrCreate<TrySuspendData>(this).PendingCompletion = b;
            parkedOn.CurrentFinallyStatement = this;
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

        // Nothing suspended after all, so drop the park. The entry itself stays: it holds nothing
        // live once the completion is out of it, and the next abrupt completion through this
        // statement reuses it.
        if (parkedOn is not null)
        {
            var parkedData = GetTrySuspendData(parkedOn);
            if (parkedData is not null)
            {
                parkedData.PendingCompletion = null;
            }

            ReleaseFinallyDispatchHint(parkedOn);
        }

        if (f.Type == CompletionType.Normal)
        {
            // Per spec: If F.[[type]] is normal, let F be B.
            // And step 6: If F.[[value]] is empty, return undefined
            return b.UpdateEmpty(JsValue.Undefined);
        }

        return f.UpdateEmpty(JsValue.Undefined);
    }

    private TrySuspendData? GetTrySuspendData(ISuspendable? suspendable)
    {
        return suspendable?.Data.TryGet(this, out TrySuspendData? suspendData) == true
            ? suspendData
            : null;
    }

    /// <summary>
    /// Drops the resume hint if it still names this statement. A finalizer nested inside this one
    /// overwrites the single slot with its own, and has already cleared it by the time control gets
    /// back here — clearing unconditionally would then discard a hint belonging to a third statement.
    /// </summary>
    private void ReleaseFinallyDispatchHint(ISuspendable suspendable)
    {
        if (ReferenceEquals(suspendable.CurrentFinallyStatement, this))
        {
            suspendable.CurrentFinallyStatement = null;
        }
    }

    private static void RestoreOuterEnvironmentAfterCatchResume(Engine engine, TrySuspendData? suspendData)
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
