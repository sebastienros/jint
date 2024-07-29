using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
/// </summary>
internal sealed class JintSwitchStatement : JintStatement<SwitchStatement>
{
    private JintSwitchBlock _switchBlock = null!;
    private JintExpression _discriminant = null!;

    public JintSwitchStatement(SwitchStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _switchBlock = new JintSwitchBlock(_statement.Cases);
        _discriminant = JintExpression.Build(_statement.Discriminant);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var value = _discriminant.GetValue(context);
        var r = _switchBlock.Execute(context, value);
        if (r.Type == CompletionType.Break && string.Equals(context.Target, _statement.LabelSet?.Name, StringComparison.Ordinal))
        {
            return new Completion(CompletionType.Normal, r.Value, ((JintStatement) this)._statement);
        }

        return r;
    }
}