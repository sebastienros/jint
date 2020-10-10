using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSuperExpression : JintExpression
    {
        public JintSuperExpression(Super expression) : base(expression)
        {
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var envRec = (FunctionEnvironmentRecord) context.Engine.ExecutionContext.GetThisEnvironment();
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return NormalCompletion(superConstructor);
        }
    }
}