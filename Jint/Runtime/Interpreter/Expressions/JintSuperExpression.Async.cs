using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintSuperExpression : JintExpression
    {
        protected override Task<object> EvaluateInternalAsync() => throw new System.NotImplementedException();
    }
}