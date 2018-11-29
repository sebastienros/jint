using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
    {
        public StrictlyNotEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
        }

        public override object Evaluate()
        {
            var left = _leftLiteral ?? _engine.GetValue(_left.Evaluate(), true);
            var right = _rightLiteral ?? _engine.GetValue(_right.Evaluate(), true);
            return StrictlyEqual(left, right) ? JsBoolean.False : JsBoolean.True;
        }
    }
}