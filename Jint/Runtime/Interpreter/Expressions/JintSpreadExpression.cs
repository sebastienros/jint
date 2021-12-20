using Esprima.Ast;
using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintSpreadExpression : JintExpression
    {
        private readonly JintExpression _argument;
        private readonly string _argumentName;

        public JintSpreadExpression(Engine engine, SpreadElement expression) : base(expression)
        {
            _argument = Build(engine, expression.Argument);
            _argumentName = (expression.Argument as Identifier)?.Name;
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
            return NormalCompletion(objectInstance);
        }

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
            return Completion.Normal(objectInstance, _expression.Location);
        }

        internal void GetValueAndCheckIterator(EvaluationContext context, out JsValue instance, out IteratorInstance iterator)
        {
            instance = _argument.GetValue(context).Value;
            if (instance is null || !instance.TryGetIterator(context.Engine.Realm, out iterator))
            {
                iterator = null;
                ExceptionHelper.ThrowTypeError(context.Engine.Realm, _argumentName + " is not iterable");
            }
        }
    }
}