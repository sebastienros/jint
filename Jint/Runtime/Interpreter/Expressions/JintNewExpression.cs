using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintNewExpression : JintExpression
    {
        private JintExpression _calleeExpression;
        private CallArgumentsBuilder _argumentsBuilder;

        public JintNewExpression(NewExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;

            var expression = (NewExpression) _expression;
            _calleeExpression = Build(engine, expression.Callee);

            var arguments = new JintExpression[expression.Arguments.Count];
            for (var i = 0; i < arguments.Length; i++)
            {
                var argument = expression.Arguments[i];
                arguments[i] = Build(engine, argument);
            }

            _argumentsBuilder = CallArgumentsBuilder.GetArgumentsBuilder(arguments);
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;

            // todo: optimize by defining a common abstract class or interface
            var jsValue = _calleeExpression.GetValue(context).Value;
            var arguments = _argumentsBuilder.Build(context);

            if (!jsValue.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm,  _calleeExpression.SourceText + " is not a constructor");
            }

            // construct the new instance using the Function's constructor method
            var instance = engine.Construct(jsValue, arguments, jsValue, _calleeExpression);

            return NormalCompletion(instance);
        }
    }
}
