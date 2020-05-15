using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;
using System.Threading.Tasks;

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
            var value = _expression.GetValue();
            return new Completion(CompletionType.Normal, value, null, Location);
        }

        protected async override Task<Completion> ExecuteInternalAsync() {
            var value = await _expression.GetValueAsync();
            return new Completion(CompletionType.Normal, value, null, Location);
        }
    }
}