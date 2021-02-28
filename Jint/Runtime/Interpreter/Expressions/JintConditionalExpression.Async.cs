using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintConditionalExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            return TypeConverter.ToBoolean(await _test.GetValueAsync())
                ? await _consequent.GetValueAsync()
                : await _alternate.GetValueAsync();
        }
    }
}