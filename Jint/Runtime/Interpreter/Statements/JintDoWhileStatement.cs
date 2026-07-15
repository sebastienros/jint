using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.1
/// </summary>
internal sealed class JintDoWhileStatement : JintStatement<DoWhileStatement>
{
    private readonly ProbablyBlockStatement _body;
    private readonly string? _labelSetName;
    private readonly JintExpression _test;

    // Tight-lane state, mirroring JintWhileStatement (see there for the eligibility rationale).
    private readonly bool _tightBodyEligible;
    private readonly JintStatement? _tightSingleStatement;
    private readonly JintStatementList? _tightBodyList;

    public JintDoWhileStatement(DoWhileStatement statement) : base(statement)
    {
        _body = new ProbablyBlockStatement(statement.Body);
        _test = JintExpression.Build(statement.Test);
        _labelSetName = statement.LabelSet?.Name;

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
        // Same gate as the while/for tight lanes; see JintWhileStatement.ExecuteInternal.
        if (_tightBodyEligible
            && !context.CompletionValuesObservable
            && !context.ShouldRunPerStatementChecks
            && !context.DebugMode
            && context.Engine.ExecutionContext.Suspendable is null)
        {
            return TightDoWhileBody(context);
        }

        JsValue v = JsValue.Undefined;
        bool iterating;
        var suspensionNode = GetSuspensionNode(context.Engine.ExecutionContext.Suspendable);
        var skipBodyOnce = suspensionNode is not null && IsNodeInsideRange(suspensionNode, _statement.Test.Range);

        do
        {
            context.Engine.ExecutionContext.ClearCompletedAwaitsIfNotResuming();

            if (!skipBodyOnce)
            {
                var completion = _body.Execute(context);
                if (!completion.Value.IsEmpty)
                {
                    v = completion.Value;
                }

                // Check for generator suspension - if the generator is suspended, we need to exit the loop
                if (context.IsSuspended())
                {
                    var generator = context.Engine.ExecutionContext.Generator;
                    var suspendedValue = generator?._suspendedValue ?? completion.Value;
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
            else
            {
                skipBodyOnce = false;
            }

            if (context.DebugMode)
            {
                context.Engine.Debugger.OnStep(_test._expression);
            }

            var testValue = _test.GetValue(context);

            // Check for async/generator suspension after evaluating the test expression
            if (context.IsSuspended())
            {
                var generator = context.Engine.ExecutionContext.Generator;
                var asyncFn = context.Engine.ExecutionContext.AsyncFunction;
                var suspendedValue = generator?._suspendedValue ?? asyncFn?._resumeValue ?? JsValue.Undefined;
                return new Completion(CompletionType.Return, suspendedValue, _statement);
            }

            iterating = TypeConverter.ToBoolean(testValue);
        } while (iterating);

        return new Completion(CompletionType.Normal, v, ((JintStatement) this)._statement);
    }

    /// <summary>
    /// The bare per-iteration loop for structurally-Normal bodies — the do-while twin of
    /// <see cref="JintWhileStatement"/>'s tight lane (body first, then test); see there for the
    /// deferred-error and amortized-constraint reasoning.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private Completion TightDoWhileBody(EvaluationContext context)
    {
        var test = _test;
        var single = _tightSingleStatement;
        var list = _tightBodyList;
        var engine = context.Engine;

        do
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
        } while (test.GetBooleanValue(context));

        return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
    }
}
