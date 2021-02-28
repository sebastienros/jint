using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintLogicalOrExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            var left = await _left.GetValueAsync();

            if (left is JsBoolean b && b._value)
            {
                return b;
            }

            if (TypeConverter.ToBoolean(left))
            {
                return left;
            }

            return await _right.GetValueAsync();
        }
    }
}