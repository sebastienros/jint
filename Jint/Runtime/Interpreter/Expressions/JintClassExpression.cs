using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintClassExpression : JintExpression
    {
        private readonly ClassDefinition _classDefinition;

        public JintClassExpression(Engine engine, ClassExpression expression) : base(engine, expression)
        {
            var name = expression.Id?.Name;
            _classDefinition = new ClassDefinition(name, expression.SuperClass, expression.Body);
        }

        protected override object EvaluateInternal()
        {
            var env = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var closure = _classDefinition.BuildConstructor(_engine, env);
            return closure;
        }
    }
}