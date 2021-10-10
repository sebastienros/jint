using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintMetaPropertyExpression : JintExpression
    {
        private readonly bool _newTarget;

        public JintMetaPropertyExpression(MetaProperty expression) : base(expression)
        {
            _newTarget = expression.Meta.Name == "new" && expression.Property.Name == "target";
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (_newTarget)
            {
                return context.Engine.GetNewTarget();
            }

            ExceptionHelper.ThrowNotImplementedException();
            return null;
        }
    }
}