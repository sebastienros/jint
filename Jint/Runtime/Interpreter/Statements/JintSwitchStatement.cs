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
        var suspensionNode = GetSuspensionNode(context.Engine.ExecutionContext.Suspendable);
        if (suspensionNode is not null && IsNodeInsideRange(suspensionNode, _statement.Range))
        {
            var resume = _switchBlock.ExecuteResume(context, suspensionNode);
            if (resume is not null)
            {
                return HandleCompletion(context, resume.Value);
            }
        }

        var value = _discriminant.GetValue(context);
        var r = _switchBlock.Execute(context, value);
        return HandleCompletion(context, r);
    }

    private Completion HandleCompletion(EvaluationContext context, Completion r)
    {
        if (r.Type == CompletionType.Break && string.Equals(context.Target, _statement.LabelSet?.Name, StringComparison.Ordinal))
        {
            return new Completion(CompletionType.Normal, r.Value, ((JintStatement) this)._statement);
        }

        return r;
    }
}
