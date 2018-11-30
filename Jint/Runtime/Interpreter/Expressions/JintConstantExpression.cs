using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintConstantExpression : JintLiteralExpression
    {
        public JintConstantExpression(Engine engine, JsValue value) : base(engine, value)
        {
        }
    }
}