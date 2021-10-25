using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintClassExpression : JintExpression
    {
        private readonly ClassDefinition _classDefinition;

        public JintClassExpression(ClassExpression expression) : base(expression)
        {
            _classDefinition = new ClassDefinition(expression.Id, expression.SuperClass, expression.Body);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var env = context.Engine.ExecutionContext.LexicalEnvironment;
            return NormalCompletion(_classDefinition.BuildConstructor(context, env));
        }
    }
}