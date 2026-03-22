using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
/// </summary>
internal sealed class JintSwitchStatement : JintStatement<SwitchStatement>
{
    private readonly JintSwitchBlock _switchBlock;
    private readonly JintExpression _discriminant;

    public JintSwitchStatement(SwitchStatement statement) : base(statement)
    {
        _switchBlock = new JintSwitchBlock(statement.Cases);
        _discriminant = JintExpression.Build(statement.Discriminant);
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