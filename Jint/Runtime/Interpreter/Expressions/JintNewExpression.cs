using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintNewExpression : JintExpression<NewExpression>
    {
        private readonly JintExpression _calleeExpression;
        private JintExpression[] _jintArguments;

        public JintNewExpression(Engine engine, NewExpression expression) : base(engine, expression)
        {
            _calleeExpression = Build(engine, expression.Callee);
        }

        protected override void Initialize()
        {
            _jintArguments = new JintExpression[_expression.Arguments.Count];
            for (var i = 0; i < _jintArguments.Length; i++)
            {
                _jintArguments[i] = Build(_engine, (Expression) _expression.Arguments[i]);
            }
        }

        protected override object EvaluateInternal()
        {
            var arguments = _engine._jsValueArrayPool.RentArray(_expression.Arguments.Count);

            BuildArguments(_jintArguments, arguments);

            // todo: optimize by defining a common abstract class or interface
            var callee = _engine.GetValue(_calleeExpression.Evaluate(), true).TryCast<IConstructor>();

            if (callee == null)
            {
                return ExceptionHelper.ThrowTypeError<object>(_engine, "The object can't be used as constructor.");
            }

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments);

            _engine._jsValueArrayPool.ReturnArray(arguments);

            return instance;
        }
    }
}