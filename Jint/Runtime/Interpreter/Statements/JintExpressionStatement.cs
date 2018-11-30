using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintExpressionStatement : JintStatement<ExpressionStatement>
    {
        private readonly JintExpression _expression;
        private readonly JsValue _value;

        public JintExpressionStatement(Engine engine, ExpressionStatement statement) : base(engine, statement)
        {
            _expression = JintExpression.Build(engine, statement.Expression);
            _value = JintExpression.FastResolve(_expression);
        }

        protected override Completion ExecuteInternal()
        {
            var value = _value ?? _engine.GetValue(_expression.Evaluate(), true);
            return new Completion(CompletionType.Normal, value, null);
        }
    }
}