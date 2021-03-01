using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintClassExpression : JintExpression
    {
        public JintClassExpression(Engine engine, ClassExpression expression) : base(engine, expression)
        {
        }

        protected override object EvaluateInternal()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var expression = (ClassExpression) _expression;
            var classDefinition = new ClassDefinition(expression.Id, expression.SuperClass, expression.Body);
            var closure = classDefinition.BuildConstructor(_engine, env);
            return closure;
        }
    }
}