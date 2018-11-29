using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
    {
        private readonly JintExpression _expression;

        public JintExpressionStatement(Engine engine, ExpressionStatement statement) : base(engine, statement)
        {
            _expression = JintExpression.Build(engine, statement.Expression);
        }

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Normal, _engine.GetValue(_expression.Evaluate(), true), null);
        }
    }
}