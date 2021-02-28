using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract partial class JintBinaryExpression : JintExpression
    {
        public async override Task<JsValue> GetValueAsync()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            // we always create a JsValue
            return (JsValue)await EvaluateInternalAsync();
        }

        protected async override Task<object> EvaluateInternalAsync()
        {
            var left = await _left.GetValueAsync();
            var right = await _right.GetValueAsync();
            return EvaluateBinaryExpression(left, right);
        }
    }
}