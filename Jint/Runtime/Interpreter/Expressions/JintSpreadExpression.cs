using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSpreadExpression : JintExpression
    {
        private readonly JintExpression _argument;
        private readonly string _argumentName;

        public JintSpreadExpression(Engine engine, SpreadElement expression) : base(engine, expression)
        {
            _argument = Build(engine, expression.Argument);
            _argumentName = (expression.Argument as Esprima.Ast.Identifier)?.Name;
        }

        protected override object EvaluateInternal()
        {
            GetValueAndCheckIterator(out var objectInstance, out var iterator);
            return objectInstance;
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            GetValueAndCheckIterator(out var objectInstance, out var iterator);
            return objectInstance;
        }

        internal void GetValueAndCheckIterator(out JsValue instance, out IIterator iterator)
        {
            instance = _argument.GetValue();
            if (instance is null || !instance.TryGetIterator(_engine, out iterator))
            {
                iterator = null;
                ExceptionHelper.ThrowTypeError(_engine, _argumentName + " is not iterable");
            }
        }
    }
}