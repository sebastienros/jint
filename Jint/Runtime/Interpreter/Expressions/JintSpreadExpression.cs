using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSpreadExpression : JintExpression
    {
        private readonly JintExpression _argument;
        private readonly string _argumentname;

        public JintSpreadExpression(Engine engine, SpreadElement expression) : base(engine, expression)
        {
            _argument = Build(engine, expression.Argument);
            _argumentname = (expression.Argument as Identifier)?.Name;
        }

        protected override object EvaluateInternal()
        {
            GetValueAndCheckIterator(out var objectInstance, out var iterator);
            return objectInstance;
        }

        public override JsValue GetValue()
        {
            GetValueAndCheckIterator(out var objectInstance, out var iterator);
            return objectInstance;
        }

        internal void GetValueAndCheckIterator(out JsValue instance, out IIterator iterator)
        {
            _engine._lastSyntaxNode = _expression;
            instance = _argument.GetValue();
            if (instance is null || !instance.TryGetIterator(_engine, out iterator))
            {
                iterator = null;
                ExceptionHelper.ThrowTypeError(_engine, _argumentname + " is not iterable");
            }
        }
    }
}