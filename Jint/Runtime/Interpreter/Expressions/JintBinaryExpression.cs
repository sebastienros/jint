using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions
{
        internal abstract class JintBinaryExpression : JintExpression<BinaryExpression>
        {
            protected readonly JintExpression _left;
            protected readonly JintExpression _right;

            protected readonly JsValue _leftLiteral;
            protected readonly JsValue _rightLiteral;

            protected JintBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                _left = Build(engine, _expression.Left);
                _right = Build(engine, _expression.Right);

                if (_expression.Left.Type == Nodes.Literal)
                {
                    _leftLiteral = (JsValue) _left.Evaluate();
                }

                if (_expression.Right.Type == Nodes.Literal)
                {
                    _rightLiteral = (JsValue) _right.Evaluate();
                }
            }

            internal static JintExpression Build(Engine engine, BinaryExpression expression)
            {
                switch (expression.Operator)
                {
                    case BinaryOperator.StrictlyEqual:
                        return new StrictlyEqualBinaryExpression(engine, expression);
                    case BinaryOperator.StricltyNotEqual:
                        return new StrictlyNotEqualBinaryExpression(engine, expression);
                    default:
                        return new JintGenericBinaryExpression(engine, expression);
                }
            }

            public static bool StrictlyEqual(JsValue x, JsValue y)
            {
                if (x._type != y._type)
                {
                    return false;
                }

                if (x._type == Types.Boolean || x._type == Types.String)
                {
                    return x.Equals(y);
                }


                if (x._type >= Types.None && x._type <= Types.Null)
                {
                    return true;
                }

                if (x is JsNumber jsNumber)
                {
                    var nx = jsNumber._value;
                    var ny = ((JsNumber) y)._value;
                    return !double.IsNaN(nx) && !double.IsNaN(ny) && nx == ny;
                }

                if (x is IObjectWrapper xw)
                {
                    if (!(y is IObjectWrapper yw))
                    {
                        return false;
                    }

                    return Equals(xw.Target, yw.Target);
                }

                return x == y;
            }

        }
}