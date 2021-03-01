using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintMetaPropertyExpression : JintExpression
    {
        protected override Task<object> EvaluateInternalAsync() => Task.FromResult(EvaluateInternal());
    }
}