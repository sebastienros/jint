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

    public JintWhileStatement(WhileStatement statement) : base(statement)
    {
        _labelSetName = statement.LabelSet?.Name;
        _body = new ProbablyBlockStatement(statement.Body);
        _test = JintExpression.Build(statement.Test);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
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
}
