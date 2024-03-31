using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintIfStatement : JintStatement<IfStatement>
{
    private ProbablyBlockStatement _statementConsequent;
    private JintExpression _test = null!;
    private ProbablyBlockStatement? _alternate;

    public JintIfStatement(IfStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _statementConsequent = new ProbablyBlockStatement(_statement.Consequent);
        _test = JintExpression.Build(_statement.Test);
        _alternate = _statement.Alternate != null ? new ProbablyBlockStatement(_statement.Alternate) : null;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var result = Completion.Empty();
        if (TypeConverter.ToBoolean(_test.GetValue(context)))
        {
            result = _statementConsequent.Execute(context);
        }
        else if (_alternate != null)
        {
            result = _alternate.Value.Execute(context);
        }

        return result.UpdateEmpty(JsValue.Undefined);
    }
}
