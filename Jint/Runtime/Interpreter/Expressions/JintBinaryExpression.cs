using System;
using System.Collections.Concurrent;
using System.Linq;
using Esprima.Ast;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintBinaryExpression : JintExpression
    {
#if NETSTANDARD
        private static readonly ConcurrentDictionary<(string OperatorName, System.Type Left, System.Type Right), MethodDescriptor> _knownOperators =
            new ConcurrentDictionary<(string OperatorName, System.Type Left, System.Type Right), MethodDescriptor>();
#else
        private static readonly ConcurrentDictionary<string, MethodDescriptor> _knownOperators = new ConcurrentDictionary<string, MethodDescriptor>();
#endif

        private readonly JintExpression _left;
        private readonly JintExpression _right;

        private JintBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
        {
            _left = Build(_engine, expression.Left);
            _right = Build(_engine, expression.Right);
        }

        protected bool TryOperatorOverloading(string clrName, out object result)
        {
            var leftValue = _left.GetValue();
            var rightValue = _right.GetValue();

            var left = leftValue.ToObject();
            var right = rightValue.ToObject();

            if (left != null && right != null)
            {
                var leftType = left.GetType();
                var rightType = right.GetType();
                var arguments = new[] { leftValue, rightValue };

#if NETSTANDARD
                var key = (clrName, leftType, rightType);
#else
                var key = $"{clrName}->{leftType}->{rightType}";
#endif
                var method = _knownOperators.GetOrAdd(key, _ =>
                {
                    var leftMethods = leftType.GetOperatorOverloadMethods();
                    var rightMethods = rightType.GetOperatorOverloadMethods();

                    var methods = leftMethods.Concat(rightMethods).Where(x => x.Name == clrName && x.GetParameters().Length == 2);
                    var _methods = MethodDescriptor.Build(methods.ToArray());

                    return TypeConverter.FindBestMatch(_engine, _methods, _ => arguments).FirstOrDefault()?.Item1;
                });

                if (method != null)
                {
                    result = method.Call(_engine, null, arguments);
                    return true;
                }
            }
            result = null;
            return false;
        }

        internal static JintExpression Build(Engine engine, BinaryExpression expression)
        {
            JintBinaryExpression result = null;
            switch (expression.Operator)
            {
                case BinaryOperator.StrictlyEqual:
                    result = new StrictlyEqualBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.StricltyNotEqual:
                    result = new StrictlyNotEqualBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Less:
                    result = new LessBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Greater:
                    result = new GreaterBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Plus:
                    result = new PlusBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Minus:
                    result = new MinusBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Times:
                    result = new TimesBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Divide:
                    result = new DivideBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Equal:
                    result = new EqualBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.NotEqual:
                    result = new EqualBinaryExpression(engine, expression, invert: true);
                    break;
                case BinaryOperator.GreaterOrEqual:
                    result = new CompareBinaryExpression(engine, expression, leftFirst: true);
                    break;
                case BinaryOperator.LessOrEqual:
                    result = new CompareBinaryExpression(engine, expression, leftFirst: false);
                    break;
                case BinaryOperator.BitwiseAnd:
                case BinaryOperator.BitwiseOr:
                case BinaryOperator.BitwiseXOr:
                case BinaryOperator.LeftShift:
                case BinaryOperator.RightShift:
                case BinaryOperator.UnsignedRightShift:
                    result = new BitwiseBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.InstanceOf:
                    result = new InstanceOfBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Exponentiation:
                    result = new ExponentiationBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.Modulo:
                    result = new ModuloBinaryExpression(engine, expression);
                    break;
                case BinaryOperator.In:
                    result = new InBinaryExpression(engine, expression);
                    break;
                default:
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(expression.Operator), "cannot handle operator");
                    break;
            }

            if (expression.Operator != BinaryOperator.InstanceOf
                && expression.Operator != BinaryOperator.In
                && expression.Left is Literal leftLiteral
                && expression.Right is Literal rightLiteral)
            {
                var lval = JintLiteralExpression.ConvertToJsValue(leftLiteral);
                var rval = JintLiteralExpression.ConvertToJsValue(rightLiteral);

                if (lval is not null && rval is not null)
                {
                    // we have fixed result
                    return new JintConstantExpression(engine, expression, result.GetValue());
                }
            }

            return result;
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            // we always create a JsValue
            return (JsValue) EvaluateInternal();
        }

        public static bool StrictlyEqual(JsValue x, JsValue y)
        {
            var typeX = x._type & ~InternalTypes.InternalFlags;
            var typeY = y._type & ~InternalTypes.InternalFlags;

            if (typeX != typeY)
            {
                if (typeX == InternalTypes.Integer)
                {
                    typeX = InternalTypes.Number;
                }

                if (typeY == InternalTypes.Integer)
                {
                    typeY = InternalTypes.Number;
                }

                if (typeX != typeY)
                {
                    return false;
                }
            }

            if (typeX == InternalTypes.Undefined || typeX == InternalTypes.Null)
            {
                return true;
            }

            if (typeX == InternalTypes.Integer)
            {
                return x.AsInteger() == y.AsInteger();
            }

            if (typeX == InternalTypes.Number)
            {
                var nx = ((JsNumber) x)._value;
                var ny = ((JsNumber) y)._value;
                return !double.IsNaN(nx) && !double.IsNaN(ny) && nx == ny;
            }

            if ((typeX & InternalTypes.String) != 0)
            {
                return x.ToString() == y.ToString();
            }

            if (typeX == InternalTypes.Boolean)
            {
                return ((JsBoolean) x)._value == ((JsBoolean) y)._value;
            }

            if ((typeX & InternalTypes.Object) != 0 && x.AsObject() is IObjectWrapper xw)
            {
                var yw = y.AsObject() as IObjectWrapper;
                if (yw == null)
                    return false;
                return Equals(xw.Target, yw.Target);
            }

            return x == y;
        }

        private sealed class StrictlyEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                var equal = StrictlyEqual(left, right);
                return equal ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class StrictlyNotEqualBinaryExpression : JintBinaryExpression
        {
            public StrictlyNotEqualBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();
                return StrictlyEqual(left, right)
                    ? JsBoolean.False
                    : JsBoolean.True;
            }
        }

        private sealed class LessBinaryExpression : JintBinaryExpression
        {
            public LessBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_LessThan", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();
                var value = Compare(left, right);

                return value._type == InternalTypes.Undefined
                    ? JsBoolean.False
                    : value;
            }
        }

        private sealed class GreaterBinaryExpression : JintBinaryExpression
        {
            public GreaterBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_GreaterThan", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();
                var value = Compare(right, left, false);

                return value._type == InternalTypes.Undefined
                    ? JsBoolean.False
                    : value;
            }
        }

        private sealed class PlusBinaryExpression : JintBinaryExpression
        {
            public PlusBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_Addition", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                if (AreIntegerOperands(left, right))
                {
                    return JsNumber.Create(left.AsInteger() + right.AsInteger());
                }

                var lprim = TypeConverter.ToPrimitive(left);
                var rprim = TypeConverter.ToPrimitive(right);
                return lprim.IsString() || rprim.IsString()
                    ? JsString.Create(TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim))
                    : JsNumber.Create(TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim));
            }
        }
        private sealed class MinusBinaryExpression : JintBinaryExpression
        {
            public MinusBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_Subtraction", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                return AreIntegerOperands(left, right)
                    ? JsNumber.Create(left.AsInteger() - right.AsInteger())
                    : JsNumber.Create(TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right));
            }
        }

        private sealed class TimesBinaryExpression : JintBinaryExpression
        {
            public TimesBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_Multiply", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                if (AreIntegerOperands(left, right))
                {
                    return JsNumber.Create((long) left.AsInteger() * right.AsInteger());
                }

                if (left.IsUndefined() || right.IsUndefined())
                {
                    return Undefined.Instance;
                }

                return JsNumber.Create(TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right));
            }
        }

        private sealed class DivideBinaryExpression : JintBinaryExpression
        {
            public DivideBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_Division", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                return Divide(left, right);
            }
        }

        private sealed class EqualBinaryExpression : JintBinaryExpression
        {
            private readonly bool _invert;

            public EqualBinaryExpression(Engine engine, BinaryExpression expression, bool invert = false) : base(engine, expression)
            {
                _invert = invert;
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading(_invert ? "op_Inequality" : "op_Equality", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                return Equal(left, right) == !_invert
                    ? JsBoolean.True
                    : JsBoolean.False;
            }
        }

        private sealed class CompareBinaryExpression : JintBinaryExpression
        {
            private readonly bool _leftFirst;

            public CompareBinaryExpression(Engine engine, BinaryExpression expression, bool leftFirst) : base(engine, expression)
            {
                _leftFirst = leftFirst;
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading(_leftFirst ? "op_GreaterThanOrEqual" : "op_LessThanOrEqual", out var opResult))
                {
                    return opResult;
                }

                var leftValue = _left.GetValue();
                var rightValue = _right.GetValue();

                var left = _leftFirst ? leftValue : rightValue;
                var right = _leftFirst ? rightValue : leftValue;

                var value = Compare(left, right, _leftFirst);
                return value.IsUndefined() || ((JsBoolean) value)._value
                    ? JsBoolean.False
                    : JsBoolean.True;
            }
        }

        private sealed class InstanceOfBinaryExpression : JintBinaryExpression
        {
            public InstanceOfBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var value = _left.GetValue();
                return value.InstanceofOperator(_right.GetValue())
                    ? JsBoolean.True
                    : JsBoolean.False;
            }
        }

        private sealed class ExponentiationBinaryExpression : JintBinaryExpression
        {
            public ExponentiationBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                return JsNumber.Create(Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right)));
            }
        }
        private sealed class InBinaryExpression : JintBinaryExpression
        {
            public InBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                var left = _left.GetValue();
                var right = _right.GetValue();

                var oi = right as ObjectInstance;
                if (oi is null)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, "in can only be used with an object");
                }

                return oi.HasProperty(left) ? JsBoolean.True : JsBoolean.False;
            }
        }

        private sealed class ModuloBinaryExpression : JintBinaryExpression
        {
            public ModuloBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading("op_Modulus", out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                if (AreIntegerOperands(left, right))
                {
                    var leftInteger = left.AsInteger();
                    var rightInteger = right.AsInteger();
                    if (leftInteger > 0 && rightInteger != 0)
                    {
                        return JsNumber.Create(leftInteger % rightInteger);
                    }
                }

                if (left.IsUndefined() || right.IsUndefined())
                {
                    return Undefined.Instance;
                }

                return JsNumber.Create(TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right));
            }
        }

        private sealed class BitwiseBinaryExpression : JintBinaryExpression
        {
            private string OperatorClrName
            {
                get
                {
                    switch (_operator)
                    {
                        case BinaryOperator.BitwiseAnd:
                            return "op_BitwiseAnd";
                        case BinaryOperator.BitwiseOr:
                            return "op_BitwiseOr";
                        case BinaryOperator.BitwiseXOr:
                            return "op_ExclusiveOr";
                        case BinaryOperator.LeftShift:
                            return "op_LeftShift";
                        case BinaryOperator.RightShift:
                            return "op_RightShift";
                        case BinaryOperator.UnsignedRightShift:
                            return "op_UnsignedRightShift";
                        default:
                            return null;
                    }
                }
            }

            private readonly BinaryOperator _operator;

            public BitwiseBinaryExpression(Engine engine, BinaryExpression expression) : base(engine, expression)
            {
                _operator = expression.Operator;
            }

            protected override object EvaluateInternal()
            {
                if (_engine.Options.Interop.OperatorOverloadingAllowed
                    && TryOperatorOverloading(OperatorClrName, out var opResult))
                {
                    return opResult;
                }

                var left = _left.GetValue();
                var right = _right.GetValue();

                if (AreIntegerOperands(left, right))
                {
                    int leftValue = left.AsInteger();
                    int rightValue = right.AsInteger();

                    switch (_operator)
                    {
                        case BinaryOperator.BitwiseAnd:
                            return JsNumber.Create(leftValue & rightValue);

                        case BinaryOperator.BitwiseOr:
                            return
                                JsNumber.Create(leftValue | rightValue);

                        case BinaryOperator.BitwiseXOr:
                            return
                                JsNumber.Create(leftValue ^ rightValue);

                        case BinaryOperator.LeftShift:
                            return JsNumber.Create(leftValue << (int) ((uint) rightValue & 0x1F));

                        case BinaryOperator.RightShift:
                            return JsNumber.Create(leftValue >> (int) ((uint) rightValue & 0x1F));

                        case BinaryOperator.UnsignedRightShift:
                            return JsNumber.Create((uint) leftValue >> (int) ((uint) rightValue & 0x1F));
                        default:
                            ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(_operator), "unknown shift operator");
                            return null;
                    }

                }

                return EvaluateNonInteger(left, right);
            }

            private object EvaluateNonInteger(JsValue left, JsValue right)
            {
                switch (_operator)
                {
                    case BinaryOperator.BitwiseAnd:
                        return JsNumber.Create(TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right));

                    case BinaryOperator.BitwiseOr:
                        return
                            JsNumber.Create(TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right));

                    case BinaryOperator.BitwiseXOr:
                        return
                            JsNumber.Create(TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right));

                    case BinaryOperator.LeftShift:
                        return JsNumber.Create(TypeConverter.ToInt32(left) <<
                                               (int) (TypeConverter.ToUint32(right) & 0x1F));

                    case BinaryOperator.RightShift:
                        return JsNumber.Create(TypeConverter.ToInt32(left) >>
                                               (int) (TypeConverter.ToUint32(right) & 0x1F));

                    case BinaryOperator.UnsignedRightShift:
                        return JsNumber.Create((uint) TypeConverter.ToInt32(left) >>
                                               (int) (TypeConverter.ToUint32(right) & 0x1F));
                    default:
                        ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(_operator), "unknown shift operator");
                        return null;
                }
            }
        }

    }
}