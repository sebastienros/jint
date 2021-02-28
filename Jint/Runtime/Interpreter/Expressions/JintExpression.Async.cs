using Jint.Native;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract partial class JintExpression
    {
        public async virtual Task<JsValue> GetValueAsync()
        {
            return await _engine.GetValueAsync(await EvaluateAsync(), true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<object> EvaluateAsync()
        {
            _engine._lastSyntaxNode = _expression;
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }
            return EvaluateInternalAsync();
        }

        protected abstract Task<object> EvaluateInternalAsync();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected async static Task BuildArgumentsAsync(JintExpression[] jintExpressions, JsValue[] targetArray)
        {
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                targetArray[i] = (await jintExpressions[i].GetValueAsync()).Clone();
            }
        }
    }
}