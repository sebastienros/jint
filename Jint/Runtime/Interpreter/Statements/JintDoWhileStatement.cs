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

    public JintDoWhileStatement(DoWhileStatement statement) : base(statement)
    {
        _body = new ProbablyBlockStatement(statement.Body);
        _test = JintExpression.Build(statement.Test);
        _labelSetName = statement.LabelSet?.Name;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        JsValue v = JsValue.Undefined;
        bool iterating;

        do
        {
            var asyncFn = context.Engine.ExecutionContext.AsyncFunction;

            // Only clear completed awaits cache when starting a NEW iteration, not when resuming.
            // When resuming from a nested await (e.g., "do {} while (await await await x)"),
            // we need the cached values of already-completed awaits to continue evaluation.
            if (asyncFn is null || !asyncFn._isResuming)
            {
                asyncFn?._completedAwaits?.Clear();
            }
            var asyncGen = context.Engine.ExecutionContext.AsyncGenerator;
            if (asyncGen is not null && !asyncGen._isResuming)
            {
                asyncGen._completedAwaits?.Clear();
            }

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

            if (context.DebugMode)
            {
                context.Engine.Debugger.OnStep(_test._expression);
            }

            var testValue = _test.GetValue(context);

            // Check for async/generator suspension after evaluating the test expression
            if (context.IsSuspended())
            {
                var generator = context.Engine.ExecutionContext.Generator;
                asyncFn = context.Engine.ExecutionContext.AsyncFunction;
                var suspendedValue = generator?._suspendedValue ?? asyncFn?._resumeValue ?? JsValue.Undefined;
                return new Completion(CompletionType.Return, suspendedValue, _statement);
            }

            iterating = TypeConverter.ToBoolean(testValue);
        } while (iterating);

        return new Completion(CompletionType.Normal, v, ((JintStatement) this)._statement);
    }
}
