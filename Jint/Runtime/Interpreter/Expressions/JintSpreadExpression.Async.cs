using Jint.Native;
using Jint.Native.Iterator;
using System;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintSpreadExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            var iterationState = await GetValueAndCheckIteratorAsync();
            return iterationState.instance;
        }

        public async override Task<JsValue> GetValueAsync()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            var iterationState = await GetValueAndCheckIteratorAsync();
            return iterationState.instance;
        }

        internal async Task<(JsValue instance, IIterator iterator)> GetValueAndCheckIteratorAsync()
        {
            var instance = await _argument.GetValueAsync();
            var iterator = await instance.TryGetIteratorAsync(_engine);

            if (instance is null || iterator is null)
            {
                iterator = null;
                ExceptionHelper.ThrowTypeError(_engine, _argumentName + " is not iterable");
            }

            return (instance: instance, iterator: iterator);
        }
    }
}