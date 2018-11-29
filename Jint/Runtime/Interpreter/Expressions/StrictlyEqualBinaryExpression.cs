using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class StrictlyEqualBinaryExpression : JintBinaryExpression
    {
        public StrictlyEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
        }

        public override object Evaluate()
        {
            var left = _leftLiteral ?? _engine.GetValue(_left.Evaluate(), true);
            var right = _rightLiteral ?? _engine.GetValue(_right.Evaluate(), true);
            return StrictlyEqual(left, right) ? JsBoolean.True : JsBoolean.False;
        }
    }
}