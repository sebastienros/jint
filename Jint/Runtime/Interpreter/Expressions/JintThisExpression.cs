using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintThisExpression : JintExpression<ThisExpression>
    {
        public JintThisExpression(Engine engine, ThisExpression expression) : base(engine, expression)
        {
        }

        public override object Evaluate()
        {
            return _engine.ExecutionContext.ThisBinding;
        }
    }
}