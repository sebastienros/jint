using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintThisExpression : JintExpression
    {
        public JintThisExpression(Engine engine, ThisExpression expression) : base(engine, expression)
        {
        }

        protected override object EvaluateInternal()
        {
            return _engine.ExecutionContext.ThisBinding;
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            return _engine.ExecutionContext.ThisBinding;
        }
    }
}