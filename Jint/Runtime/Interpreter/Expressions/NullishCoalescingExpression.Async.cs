using Jint.Native;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class NullishCoalescingExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            return await EvaluateConstantOrExpressionAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<JsValue> EvaluateConstantOrExpressionAsync()
        {
            var left = await _left.GetValueAsync();

            return !left.IsNullOrUndefined()
                ? left
                : _constant ?? await _right.GetValueAsync();
        }


    }
}