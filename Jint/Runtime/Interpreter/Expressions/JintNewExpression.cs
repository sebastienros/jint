using System;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintNewExpression : JintExpression
    {
        private JintExpression _calleeExpression;
        private JintExpression[] _jintArguments = Array.Empty<JintExpression>();
        private bool _hasSpreads;

        public JintNewExpression(NewExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;

            var expression = (NewExpression) _expression;
            _calleeExpression = Build(engine, expression.Callee);

            if (expression.Arguments.Count <= 0)
            {
                return;
            }

            _jintArguments = new JintExpression[expression.Arguments.Count];
            for (var i = 0; i < _jintArguments.Length; i++)
            {
                var argument = expression.Arguments[i];
                _jintArguments[i] = Build(engine, argument);
                _hasSpreads |= argument.Type == Nodes.SpreadElement;
            }
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;

            // todo: optimize by defining a common abstract class or interface
            var jsValue = _calleeExpression.GetValue(context).Value;

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

            if (!jsValue.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm,  _calleeExpression.SourceText + " is not a constructor");
            }

            // construct the new instance using the Function's constructor method
            var instance = engine.Construct((IConstructor) jsValue, arguments, jsValue, _calleeExpression);

            engine._jsValueArrayPool.ReturnArray(arguments);

            return NormalCompletion(instance);
        }
    }
}