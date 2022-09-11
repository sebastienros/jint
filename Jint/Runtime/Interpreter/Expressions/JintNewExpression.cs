using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintNewExpression : JintExpression
    {
        private JintExpression _calleeExpression = null!;
        private JintExpression[] _jintArguments = Array.Empty<JintExpression>();
        private bool _hasSpreads;

        public JintNewExpression(NewExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var expression = (NewExpression) _expression;
            _calleeExpression = Build(expression.Callee);

            if (expression.Arguments.Count <= 0)
            {
                return;
            }

            _jintArguments = new JintExpression[expression.Arguments.Count];
            for (var i = 0; i < _jintArguments.Length; i++)
            {
                var argument = expression.Arguments[i];
                _jintArguments[i] = Build(argument);
                _hasSpreads |= argument.Type == Nodes.SpreadElement;
            }
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;

            // todo: optimize by defining a common abstract class or interface
            var jsValue = _calleeExpression.GetValue(context);

            JsValue[] arguments;
            if (_jintArguments.Length == 0)
            {
                arguments = Array.Empty<JsValue>();
            }
            else if (_hasSpreads)
            {
                arguments = BuildArgumentsWithSpreads(context, _jintArguments);
            }
            else
            {
                arguments = engine._jsValueArrayPool.RentArray(_jintArguments.Length);
                BuildArguments(context, _jintArguments, arguments);
            }

            // Reset the location to the "new" keyword so that if an Error object is
            // constructed below, the stack trace will capture the correct location.
            context.LastSyntaxElement = _expression;

            if (!jsValue.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, _calleeExpression.SourceText + " is not a constructor");
            }

            // construct the new instance using the Function's constructor method
            var instance = engine.Construct(jsValue, arguments, jsValue, _calleeExpression);

            engine._jsValueArrayPool.ReturnArray(arguments);

            return instance;
        }
    }
}
