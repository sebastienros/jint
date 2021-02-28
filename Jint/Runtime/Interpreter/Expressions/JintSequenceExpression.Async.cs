using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintSequenceExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            var result = Undefined.Instance;
            var expressions = _expressions;
            for (var i = 0; i < (uint)expressions.Length; i++)
            {
                var expression = expressions[i];
                result = await expression.GetValueAsync();
            }

            return result;
        }
    }
}