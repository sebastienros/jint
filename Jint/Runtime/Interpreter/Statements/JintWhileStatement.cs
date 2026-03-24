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
        while (true)
        {
            // Only clear completed awaits cache when starting a NEW iteration, not when resuming.
            // When resuming from a nested await (e.g., "while (await await await x)"),
            // we need the cached values of already-completed awaits to continue evaluation.
            // When starting fresh (not resuming), clear the cache to ensure expressions like
            // "while (await p)" evaluate p fresh each time even if p changes.
            var asyncFn = context.Engine.ExecutionContext.AsyncFunction;
            if (asyncFn is null || !asyncFn._isResuming)
            {
                asyncFn?._completedAwaits?.Clear();
            }
            var asyncGen = context.Engine.ExecutionContext.AsyncGenerator;
            if (asyncGen is not null && !asyncGen._isResuming)
            {
                asyncGen._completedAwaits?.Clear();
            }

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