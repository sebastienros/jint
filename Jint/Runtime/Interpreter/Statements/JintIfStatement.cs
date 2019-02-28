using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintIfStatement : JintStatement<IfStatement>
    {
        private readonly JintStatement _statementConsequent;
        private readonly JintExpression _test;
        private readonly JintStatement _alternate;

        public JintIfStatement(Engine engine, IfStatement statement) : base(engine, statement)
        {
            _statementConsequent = Build(engine, _statement.Consequent);
            _test = JintExpression.Build(engine, _statement.Test);
            _alternate = _statement.Alternate != null ? Build(engine, _statement.Alternate) : null;
        }

        protected override Completion ExecuteInternal()
        {
            Completion result;
            if (TypeConverter.ToBoolean(_test.GetValue()))
            {
                result = _statementConsequent.Execute();
            }
            else if (_alternate != null)
            {
                result = _alternate.Execute();
            }
            else
            {
                return new Completion(CompletionType.Normal, null, null, Location);
            }

            return result;
        }
    }
}