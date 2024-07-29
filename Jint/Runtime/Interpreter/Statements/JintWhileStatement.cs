using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
/// </summary>
internal sealed class JintWhileStatement : JintStatement<WhileStatement>
{
    private string? _labelSetName;
    private ProbablyBlockStatement _body;
    private JintExpression _test = null!;

    public JintWhileStatement(WhileStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _labelSetName = _statement.LabelSet?.Name;
        _body = new ProbablyBlockStatement(_statement.Body);
        _test = JintExpression.Build(_statement.Test);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var v = JsValue.Undefined;
        while (true)
        {
            if (context.DebugMode)
            {
                context.Engine.Debugger.OnStep(_test._expression);
            }

            var jsValue = _test.GetValue(context);
            if (!TypeConverter.ToBoolean(jsValue))
            {
                return new Completion(CompletionType.Normal, v, _statement);
            }

            var completion = _body.Execute(context);

            if (!completion.Value.IsEmpty)
            {
                v = completion.Value;
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