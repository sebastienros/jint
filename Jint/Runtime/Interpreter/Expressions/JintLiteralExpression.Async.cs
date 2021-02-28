using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal partial class JintLiteralExpression : JintExpression
    {
        protected override Task<object> EvaluateInternalAsync() => Task.FromResult(EvaluateInternal());
    }
}