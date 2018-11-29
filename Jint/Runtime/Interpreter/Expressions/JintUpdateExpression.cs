using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintUpdateExpression : JintExpression<UpdateExpression>
    {
        private readonly JintExpression _argument;

        public JintUpdateExpression(Engine engine, UpdateExpression expression) : base(engine, expression)
        {
            _argument = Build(engine, expression.Argument);
        }

        public override object Evaluate()
        {
            var value = _argument.Evaluate();

            var r = (Reference) value;
            r.AssertValid(_engine);

            var oldValue = TypeConverter.ToNumber(_engine.GetValue(value, false));
            double newValue = 0;
            if (_expression.Operator == UnaryOperator.Increment)
            {
                newValue = oldValue + 1;
            }
            else if (_expression.Operator == UnaryOperator.Decrement)
            {
                newValue = oldValue - 1;
            }
            else
            {
                ExceptionHelper.ThrowArgumentException();
            }

            _engine.PutValue(r, newValue);
            _engine._referencePool.Return(r);
            return JsNumber.Create(_expression.Prefix ? newValue : oldValue);
        }
    }
}