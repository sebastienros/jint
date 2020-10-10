using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintIfStatement : JintStatement<IfStatement>
    {
        private JintStatement _statementConsequent;
        private JintExpression _test;
        private JintStatement _alternate;

        public JintIfStatement(IfStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _statementConsequent = Build(_statement.Consequent);
            _test = JintExpression.Build(context.Engine, _statement.Test);
            _alternate = _statement.Alternate != null ? Build(_statement.Alternate) : null;
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            Completion result;
            if (TypeConverter.ToBoolean(_test.GetValue(context).Value))
            {
                result = _statementConsequent.Execute(context);
            }
            else if (_alternate != null)
            {
                result = _alternate.Execute(context);
            }
            else
            {
                return new Completion(CompletionType.Normal, null, Location);
            }

            return result;
        }
    }
}