using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintClassExpression : JintExpression
    {
        public JintClassExpression(ClassExpression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var env = context.Engine.ExecutionContext.LexicalEnvironment;
            var expression = (ClassExpression) _expression;
            var classDefinition = new ClassDefinition(expression.Id, expression.SuperClass, expression.Body);
            var closure = classDefinition.BuildConstructor(context, env);
            return closure;
        }
    }
}