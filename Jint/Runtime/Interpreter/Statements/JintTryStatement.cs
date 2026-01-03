using Jint.Native;
using Jint.Runtime.Environments;
using Range = Acornima.Range;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-try-statement
/// </summary>
internal sealed class JintTryStatement : JintStatement<TryStatement>
{
    private JintBlockStatement _block = null!;
    private JintBlockStatement? _catch;
    private JintBlockStatement? _finalizer;

    public JintTryStatement(TryStatement statement) : base(statement)
    {

    }

    protected override void Initialize(EvaluationContext context)
    {
        _block = new JintBlockStatement(_statement.Block);
        if (_statement.Finalizer != null)
        {
            _finalizer = new JintBlockStatement(_statement.Finalizer);
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var generator = engine.ExecutionContext.Generator;

        // Check if we're resuming from inside the catch or finally block
        // If so, skip the try block and go directly to the appropriate block
        if (generator is not null && generator._isResuming && generator._lastYieldNode is Node yieldNode)
        {
            if (_statement.Handler is not null && IsNodeInsideRange(yieldNode, _statement.Handler.Range))
            {
                // Resuming from inside catch block - execute catch directly
                return ExecuteCatchResume(context, engine);
            }
            if (_statement.Finalizer is not null && IsNodeInsideRange(yieldNode, _statement.Finalizer.Range))
            {
                // Resuming from inside finally block - execute finally directly
                return ExecuteFinallyResume(context, engine);
            }
        }

        var b = _block.Execute(context);

        if (b.Type == CompletionType.Throw)
        {
            b = ExecuteCatch(context, b, engine);
        }

        // If a generator is suspended (yield), don't run the finally yet.
        // The finally will run when the generator resumes and exits the try block.
        if (context.IsSuspended())
        {
            return b;
        }

        if (_finalizer != null)
        {
            // Save the pending completion before running finally
            // This is needed because if finally yields, we need to remember the
            // original completion (throw/return) to restore after finally completes
            if (b.Type == CompletionType.Throw || b.Type == CompletionType.Return)
            {
                if (generator is not null)
                {
                    generator._pendingCompletionType = b.Type;
                    generator._pendingCompletionValue = b.Value;
                    generator._currentFinallyStatement = _statement;
                }
            }

            // Clear _returnRequested before running finally block.
            // Per ECMAScript spec, a return in the finally block supersedes any pending return.
            // If we don't clear this, the finally block's statements will incorrectly use _suspendedValue.
            if (generator is not null)
            {
                generator._returnRequested = false;
            }

            var f = _finalizer.Execute(context);

            // Clear the pending completion tracking if we completed normally
            if (generator is not null && ReferenceEquals(generator._currentFinallyStatement, _statement))
            {
                generator._currentFinallyStatement = null;
            }

            if (f.Type == CompletionType.Normal)
            {
                return b;
            }

            return f.UpdateEmpty(JsValue.Undefined);
        }

        return b.UpdateEmpty(JsValue.Undefined);
    }

    private static bool IsNodeInsideRange(Node node, in Range range)
    {
        return range.Start <= node.Range.Start && node.Range.End <= range.End;
    }

    private Completion ExecuteCatchResume(EvaluationContext context, Engine engine)
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

        // Execute catch block (it will resume from the saved position)
        var b = _catch.Execute(context);

        // If a generator is suspended (yield), don't run the finally yet
        if (context.IsSuspended())
        {
            return b;
        }

        // Run finally if present
        if (_finalizer != null)
        {
            var f = _finalizer.Execute(context);
            if (f.Type != CompletionType.Normal)
            {
                return f.UpdateEmpty(JsValue.Undefined);
            }
        }

        return b.UpdateEmpty(JsValue.Undefined);
    }

    private Completion ExecuteFinallyResume(EvaluationContext context, Engine engine)
    {
        var generator = engine.ExecutionContext.Generator;

        // Execute finally block (it will resume from the saved position)
        var f = _finalizer!.Execute(context);

        // If a generator is suspended (yield), don't process the pending completion yet
        if (context.IsSuspended())
        {
            return f;
        }

        // After finally completes normally, restore the pending completion
        if (f.Type == CompletionType.Normal && generator is not null)
        {
            var pendingType = generator._pendingCompletionType;
            var pendingValue = generator._pendingCompletionValue;

            // Clear the pending completion
            generator._pendingCompletionType = CompletionType.Normal;
            generator._pendingCompletionValue = null;
            generator._currentFinallyStatement = null;

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

            engine.UpdateLexicalEnvironment(oldEnv);
        }

        return b;
    }
}
