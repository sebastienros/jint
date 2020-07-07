using Esprima.Ast;
using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintThisExpression : JintExpression
    {
        public JintThisExpression(Engine engine, ThisExpression expression) : base(engine, expression)
        {
        }

        protected override object EvaluateInternal()
        {
            return _engine.ResolveThisBinding();
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            return _engine.ResolveThisBinding();
        }

        protected override Task<object> EvaluateInternalAsync() => Task.FromResult(EvaluateInternal());
    }
}