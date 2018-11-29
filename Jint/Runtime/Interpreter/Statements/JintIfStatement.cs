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

        public override Completion Execute()
        {
            Completion result;
            if (TypeConverter.ToBoolean(_engine.GetValue(_test.Evaluate(), true)))
            {
                result = _statementConsequent.Execute();
            }
            else if (_alternate != null)
            {
                result = _alternate.Execute();
            }
            else
            {
                return new Completion(CompletionType.Normal, null, null);
            }

            return result;
        }
    }
}