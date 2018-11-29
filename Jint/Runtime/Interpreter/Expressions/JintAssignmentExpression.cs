using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintAssignmentExpression : JintExpression<AssignmentExpression>
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;

        public JintAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
        {
            _left = Build(engine, (Expression) expression.Left);
            _right = Build(engine, expression.Right);
        }

        public override object Evaluate()
        {
            var lref = _left.Evaluate() as Reference;
            JsValue rval = _engine.GetValue(_right.Evaluate(), true);

            if (lref == null)
            {
                ExceptionHelper.ThrowReferenceError(_engine);
            }

            if (_expression.Operator == AssignmentOperator.Assign) // "="
            {
                lref.AssertValid(_engine);

                _engine.PutValue(lref, rval);
                _engine._referencePool.Return(lref);
                return rval;
            }

            JsValue lval = _engine.GetValue(lref, false);

            switch (_expression.Operator)
            {
                case AssignmentOperator.PlusAssign:
                    var lprim = TypeConverter.ToPrimitive(lval);
                    var rprim = TypeConverter.ToPrimitive(rval);
                    if (lprim.IsString() || rprim.IsString())
                    {
                        if (!(lprim is JsString jsString))
                        {
                            jsString = new JsString.ConcatenatedString(TypeConverter.ToString(lprim));
                        }

                        lval = jsString.Append(rprim);
                    }
                    else
                    {
                        lval = TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim);
                    }

                    break;

                case AssignmentOperator.MinusAssign:
                    lval = TypeConverter.ToNumber(lval) - TypeConverter.ToNumber(rval);
                    break;

                case AssignmentOperator.TimesAssign:
                    if (lval.IsUndefined() || rval.IsUndefined())
                    {
                        lval = Undefined.Instance;
                    }
                    else
                    {
                        lval = TypeConverter.ToNumber(lval) * TypeConverter.ToNumber(rval);
                    }

                    break;

                case AssignmentOperator.DivideAssign:
                    lval = Divide(lval, rval);
                    break;

                case AssignmentOperator.ModuloAssign:
                    if (lval.IsUndefined() || rval.IsUndefined())
                    {
                        lval = Undefined.Instance;
                    }
                    else
                    {
                        lval = TypeConverter.ToNumber(lval) % TypeConverter.ToNumber(rval);
                    }

                    break;

                case AssignmentOperator.BitwiseAndAssign:
                    lval = TypeConverter.ToInt32(lval) & TypeConverter.ToInt32(rval);
                    break;

                case AssignmentOperator.BitwiseOrAssign:
                    lval = TypeConverter.ToInt32(lval) | TypeConverter.ToInt32(rval);
                    break;

                case AssignmentOperator.BitwiseXOrAssign:
                    lval = TypeConverter.ToInt32(lval) ^ TypeConverter.ToInt32(rval);
                    break;

                case AssignmentOperator.LeftShiftAssign:
                    lval = TypeConverter.ToInt32(lval) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.RightShiftAssign:
                    lval = TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.UnsignedRightShiftAssign:
                    lval = (uint) TypeConverter.ToInt32(lval) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                default:
                    ExceptionHelper.ThrowNotImplementedException();
                    return null;
            }

            _engine.PutValue(lref, lval);

            _engine._referencePool.Return(lref);
            return lval;
        }
    }
}