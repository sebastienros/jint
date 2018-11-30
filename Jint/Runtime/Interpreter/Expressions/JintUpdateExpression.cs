using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintUpdateExpression : JintExpression<UpdateExpression>
    {
        private readonly JintExpression _argument;
        private readonly int _change;

        public JintUpdateExpression(Engine engine, UpdateExpression expression) : base(engine, expression)
        {
            _argument = Build(engine, expression.Argument);
            if (_expression.Operator == UnaryOperator.Increment)
            {
                _change = 1;
            }
            else if (_expression.Operator == UnaryOperator.Decrement)
            {
                _change = - 1;
            }
            else
            {
                ExceptionHelper.ThrowArgumentException();
            }
        }

        protected override object EvaluateInternal()
        {
            var value = (Reference) _argument.Evaluate();
            value.AssertValid(_engine);

            var oldValue = TypeConverter.ToNumber(_engine.GetValue(value, false));
            var newValue = oldValue + _change;

            _engine.PutValue(value, newValue);
            _engine._referencePool.Return(value);

            return JsNumber.Create(_expression.Prefix ? newValue : oldValue);
        }
    }
}