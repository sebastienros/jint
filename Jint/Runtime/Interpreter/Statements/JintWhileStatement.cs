using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
/// </summary>
internal sealed class JintWhileStatement : JintStatement<WhileStatement>
{
    private readonly string? _labelSetName;
    private readonly ProbablyBlockStatement _body;
    private readonly JintExpression _test;

    // Tight-lane state, mirroring JintForStatement: a structurally-Normal body executes via
    // ExecuteDiscarded with no per-statement ceremony. While has no loop-env flattening
    // machinery, so bodies with lexical declarations (which need a fresh block environment
    // per iteration) stay on the generic path.
    private readonly bool _tightBodyEligible;
    private readonly JintStatement? _tightSingleStatement;
    private readonly JintStatementList? _tightBodyList;

    public JintWhileStatement(WhileStatement statement) : base(statement)
    {
        _labelSetName = statement.LabelSet?.Name;
        _body = new ProbablyBlockStatement(statement.Body);
        _test = JintExpression.Build(statement.Test);

        if (JintForStatement.IsTightBodyShape(statement.Body))
        {
            if (_body.BlockStatement is { } bodyBlock)
            {
                if (bodyBlock.State.Declarations.Count == 0)
                {
                    _tightBodyEligible = true;
                    // single-statement blocks live in SingleStatement, larger (and empty) ones in the list
                    _tightSingleStatement = bodyBlock.SingleStatement;
                    _tightBodyList = _tightSingleStatement is null ? bodyBlock.StatementList : null;
                }
            }
            else
            {
                _tightBodyEligible = true;
                if (_body.Statement is not JintEmptyStatement)
                {
                    // an EmptyStatement body leaves both references null (nothing to run per iteration)
                    _tightSingleStatement = _body.Statement;
                }
            }
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        // Same gate as JintForStatement.ForBodyEvaluation: with a structurally-Normal body,
        // a non-suspendable frame, no per-statement (exact) constraint/debug checks and dead
        // completion values, nothing per iteration remains observable but the test and the
        // body expressions. Amortized constraints stay live through the shared countdown.
        if (_tightBodyEligible
            && !context.CompletionValuesObservable
            && !context.ShouldRunPerStatementChecks
            && !context.DebugMode
            && context.Engine.ExecutionContext.Suspendable is null)
        {
            return TightWhileBody(context);
        }

        var v = JsValue.Undefined;
        var suspensionNode = GetSuspensionNode(context.Engine.ExecutionContext.Suspendable);
        var skipTestOnce = suspensionNode is not null && IsNodeInsideRange(suspensionNode, _statement.Body.Range);

        while (true)
        {
            context.Engine.ExecutionContext.ClearCompletedAwaitsIfNotResuming();

            if (!skipTestOnce)
            {
                if (context.DebugMode)
                {
                    context.Engine.Debugger.OnStep(_test._expression);
                }

                if (!_test.GetBooleanValue(context))
                {
                    // GetBooleanValue returns false for both actual false condition
                    // and suspended evaluation (async/generator); check which case
                    if (context.IsSuspended())
                    {
                        var suspendable = context.Engine.ExecutionContext.Suspendable;
                        var suspendedValue = suspendable?.SuspendedValue ?? JsValue.Undefined;
                        return new Completion(CompletionType.Return, suspendedValue, _statement);
                    }

                    return new Completion(CompletionType.Normal, v, _statement);
                }
            }

            skipTestOnce = false;

            var completion = _body.Execute(context);

            if (!completion.Value.IsEmpty)
            {
                v = completion.Value;
            }

            // Check for suspension - if suspended, we need to exit the loop
            if (context.IsSuspended())
            {
                var bodySuspendable = context.Engine.ExecutionContext.Suspendable;
                var suspendedValue = bodySuspendable?.SuspendedValue ?? completion.Value;
                return new Completion(CompletionType.Return, suspendedValue, _statement);
            }

            if (completion.Type != CompletionType.Continue || !string.Equals(context.Target, _labelSetName, StringComparison.Ordinal))
            {
                if (completion.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _labelSetName, StringComparison.Ordinal)))
                {
                    return new Completion(CompletionType.Normal, v, _statement);
                }

                if (completion.Type != CompletionType.Normal)
                {
                    return completion;
                }
            }
        }
    }

    /// <summary>
    /// The bare per-iteration loop for structurally-Normal bodies: test, discarded body
    /// statements, deferred-error polls — nothing else. The loop's Normal completion value is
    /// dead by the caller's gate, so Undefined stands in. Deferred errors surface as
    /// Engine._error and convert per statement, exactly as JintStatementList would. Amortized
    /// constraints are driven once per iteration through the context's shared countdown; exact
    /// constraints and debug mode never reach this lane by the caller's gate.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private Completion TightWhileBody(EvaluationContext context)
    {
        var test = _test;
        var single = _tightSingleStatement;
        var list = _tightBodyList;
        var engine = context.Engine;

        while (test.GetBooleanValue(context))
        {
            context.RunAmortizedConstraintChecks();

            if (single is not null)
            {
                single.ExecuteDiscarded(context);
                if (engine._error is not null)
                {
                    return JintStatementList.HandleError(engine, single);
                }
            }
            else if (list is not null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var statement = list.GetStatement(i);
                    statement.ExecuteDiscarded(context);
                    if (engine._error is not null)
                    {
                        return JintStatementList.HandleError(engine, statement);
                    }
                }
            }
        }

        return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
    }
}
