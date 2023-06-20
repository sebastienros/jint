using System.Numerics;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintAssignmentExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;
        private readonly AssignmentOperator _operator;

        private JintAssignmentExpression(AssignmentExpression expression) : base(expression)
        {
            _left = Build((Expression) expression.Left);
            _right = Build(expression.Right);
            _operator = expression.Operator;
        }

        internal static JintExpression Build(AssignmentExpression expression)
        {
            if (expression.Operator == AssignmentOperator.Assign)
            {
                if (expression.Left is BindingPattern)
                {
                    return new BindingPatternAssignmentExpression(expression);
                }

                return new SimpleAssignmentExpression(expression);
            }

            return new JintAssignmentExpression(expression);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var lref = _left.Evaluate(context) as Reference;
            if (lref is null)
            {
                ExceptionHelper.ThrowReferenceError(context.Engine.Realm, "not a valid reference");
            }

            var engine = context.Engine;
            var originalLeftValue = context.Engine.GetValue(lref, false);
            var handledByOverload = false;
            JsValue? newLeftValue = null;

            if (context.OperatorOverloadingAllowed)
            {
                string? operatorClrName = null;
                switch (_operator)
                {
                    case AssignmentOperator.PlusAssign:
                        operatorClrName = "op_Addition";
                        break;
                    case AssignmentOperator.MinusAssign:
                        operatorClrName = "op_Subtraction";
                        break;
                    case AssignmentOperator.TimesAssign:
                        operatorClrName = "op_Multiply";
                        break;
                    case AssignmentOperator.DivideAssign:
                        operatorClrName = "op_Division";
                        break;
                    case AssignmentOperator.ModuloAssign:
                        operatorClrName = "op_Modulus";
                        break;
                    case AssignmentOperator.BitwiseAndAssign:
                        operatorClrName = "op_BitwiseAnd";
                        break;
                    case AssignmentOperator.BitwiseOrAssign:
                        operatorClrName = "op_BitwiseOr";
                        break;
                    case AssignmentOperator.BitwiseXorAssign:
                        operatorClrName = "op_ExclusiveOr";
                        break;
                    case AssignmentOperator.LeftShiftAssign:
                        operatorClrName = "op_LeftShift";
                        break;
                    case AssignmentOperator.RightShiftAssign:
                        operatorClrName = "op_RightShift";
                        break;
                    case AssignmentOperator.UnsignedRightShiftAssign:
                        operatorClrName = "op_UnsignedRightShift";
                        break;
                    case AssignmentOperator.ExponentiationAssign:
                    case AssignmentOperator.Assign:
                    default:
                        break;
                }

                if (operatorClrName != null)
                {
                    var rval = _right.GetValue(context);
                    if (JintBinaryExpression.TryOperatorOverloading(context, originalLeftValue, rval, operatorClrName, out var result))
                    {
                        newLeftValue = JsValue.FromObject(context.Engine, result);
                        handledByOverload = true;
                    }
                }
            }

            var wasMutatedInPlace = false;
            if (!handledByOverload)
            {
                switch (_operator)
                {
                    case AssignmentOperator.PlusAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = (long) originalLeftValue.AsInteger() + rval.AsInteger();
                        }
                        else
                        {
                            var lprim = TypeConverter.ToPrimitive(originalLeftValue);
                            var rprim = TypeConverter.ToPrimitive(rval);

                            if (lprim.IsString() || rprim.IsString())
                            {
                                wasMutatedInPlace = lprim is JsString.ConcatenatedString;
                                if (lprim is not JsString jsString)
                                {
                                    jsString = new JsString.ConcatenatedString(TypeConverter.ToString(lprim));
                                }

                                newLeftValue = jsString.Append(rprim);
                            }
                            else if (!AreIntegerOperands(originalLeftValue, rval))
                            {
                                newLeftValue = TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim);
                            }
                            else
                            {
                                newLeftValue = TypeConverter.ToBigInt(lprim) + TypeConverter.ToBigInt(rprim);
                            }
                        }

                        break;
                    }

                    case AssignmentOperator.MinusAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = JsNumber.Create(originalLeftValue.AsInteger() - rval.AsInteger());
                        }
                        else if (!AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = JsNumber.Create(TypeConverter.ToNumber(originalLeftValue) - TypeConverter.ToNumber(rval));
                        }
                        else
                        {
                            newLeftValue = JsNumber.Create(TypeConverter.ToBigInt(originalLeftValue) - TypeConverter.ToBigInt(rval));
                        }

                        break;
                    }

                    case AssignmentOperator.TimesAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = (long) originalLeftValue.AsInteger() * rval.AsInteger();
                        }
                        else if (originalLeftValue.IsUndefined() || rval.IsUndefined())
                        {
                            newLeftValue = JsValue.Undefined;
                        }
                        else if (!AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = TypeConverter.ToNumber(originalLeftValue) * TypeConverter.ToNumber(rval);
                        }
                        else
                        {
                            newLeftValue = TypeConverter.ToBigInt(originalLeftValue) * TypeConverter.ToBigInt(rval);
                        }

                        break;
                    }

                    case AssignmentOperator.DivideAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = Divide(context, originalLeftValue, rval);
                        break;
                    }

                    case AssignmentOperator.ModuloAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (originalLeftValue.IsUndefined() || rval.IsUndefined())
                        {
                            newLeftValue = JsValue.Undefined;
                        }
                        else if (!AreIntegerOperands(originalLeftValue, rval))
                        {
                            newLeftValue = TypeConverter.ToNumber(originalLeftValue) % TypeConverter.ToNumber(rval);
                        }
                        else
                        {
                            newLeftValue = TypeConverter.ToNumber(originalLeftValue) % TypeConverter.ToNumber(rval);
                        }

                        break;
                    }

                    case AssignmentOperator.BitwiseAndAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) & TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.BitwiseOrAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) | TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.BitwiseXorAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) ^ TypeConverter.ToInt32(rval);
                        break;
                    }

                    case AssignmentOperator.LeftShiftAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) << (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.RightShiftAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = TypeConverter.ToInt32(originalLeftValue) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.UnsignedRightShiftAssign:
                    {
                        var rval = _right.GetValue(context);
                        newLeftValue = (uint) TypeConverter.ToInt32(originalLeftValue) >> (int) (TypeConverter.ToUint32(rval) & 0x1F);
                        break;
                    }

                    case AssignmentOperator.NullishAssign:
                    {
                        if (!originalLeftValue.IsNullOrUndefined())
                        {
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        newLeftValue = rval;
                        break;
                    }

                    case AssignmentOperator.AndAssign:
                    {
                        if (!TypeConverter.ToBoolean(originalLeftValue))
                        {
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        newLeftValue = rval;
                        break;
                    }

                    case AssignmentOperator.OrAssign:
                    {
                        if (TypeConverter.ToBoolean(originalLeftValue))
                        {
                            return originalLeftValue;
                        }

                        var rval = NamedEvaluation(context, _right);
                        newLeftValue = rval;
                        break;
                    }

                    case AssignmentOperator.ExponentiationAssign:
                    {
                        var rval = _right.GetValue(context);
                        if (!originalLeftValue.IsBigInt() && !rval.IsBigInt())
                        {
                            newLeftValue = JsNumber.Create(Math.Pow(TypeConverter.ToNumber(originalLeftValue), TypeConverter.ToNumber(rval)));
                        }
                        else
                        {
                            var exponent = TypeConverter.ToBigInt(rval);
                            if (exponent > int.MaxValue || exponent < int.MinValue)
                            {
                                ExceptionHelper.ThrowTypeError(context.Engine.Realm, "Cannot do exponentation with exponent not fitting int32");
                            }
                            newLeftValue = JsBigInt.Create(BigInteger.Pow(TypeConverter.ToBigInt(originalLeftValue), (int) exponent));
                        }

                        break;
                    }

                    default:
                        ExceptionHelper.ThrowNotImplementedException();
                        return default;
                }
            }

            // if we did string concatenation in-place, we don't need to update records, objects might have evil setters
            if (!wasMutatedInPlace || lref.Base is not EnvironmentRecord)
            {
                engine.PutValue(lref, newLeftValue!);
            }

            engine._referencePool.Return(lref);
            return newLeftValue!;
        }

        private JsValue NamedEvaluation(EvaluationContext context, JintExpression expression)
        {
            var rval = expression.GetValue(context);
            if (expression._expression.IsAnonymousFunctionDefinition() && _left._expression.Type == Nodes.Identifier)
            {
                ((FunctionInstance) rval).SetFunctionName(((Identifier) _left._expression).Name);
            }

            return rval;
        }

        internal sealed class SimpleAssignmentExpression : JintExpression
        {
            private JintExpression _left = null!;
            private JintExpression _right = null!;

            private JintIdentifierExpression? _leftIdentifier;
            private bool _evalOrArguments;

            public SimpleAssignmentExpression(AssignmentExpression expression) : base(expression)
            {
                _initialized = false;
            }

            protected override void Initialize(EvaluationContext context)
            {
                var assignmentExpression = (AssignmentExpression) _expression;
                _left = Build((Expression) assignmentExpression.Left);
                _leftIdentifier = _left as JintIdentifierExpression;
                _evalOrArguments = _leftIdentifier?.HasEvalOrArguments == true;

                _right = Build(assignmentExpression.Right);
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                object? completion = null;
                if (_leftIdentifier != null)
                {
                    completion = AssignToIdentifier(context, _leftIdentifier, _right, _evalOrArguments);
                }
                return completion ?? SetValue(context);
            }

            // https://262.ecma-international.org/5.1/#sec-11.13.1
            private JsValue SetValue(EvaluationContext context)
            {
                // slower version
                var engine = context.Engine;
                var lref = _left.Evaluate(context) as Reference;
                if (lref is null)
                {
                    ExceptionHelper.ThrowReferenceError(engine.Realm, "not a valid reference");
                }

                lref.AssertValid(engine.Realm);

                var rval = _right.GetValue(context);

                engine.PutValue(lref, rval);
                engine._referencePool.Return(lref);
                return rval;
            }

            internal static object? AssignToIdentifier(
                EvaluationContext context,
                JintIdentifierExpression left,
                JintExpression right,
                bool hasEvalOrArguments)
            {
                var engine = context.Engine;
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                var identifier = left.Identifier;
                if (JintEnvironment.TryGetIdentifierEnvironmentWithBinding(
                    env,
                    identifier,
                    out var environmentRecord))
                {
                    if (strict && hasEvalOrArguments && identifier.Key != KnownKeys.Eval)
                    {
                        ExceptionHelper.ThrowSyntaxError(engine.Realm, "Invalid assignment target");
                    }

                    var completion = right.GetValue(context);
                    if (context.IsAbrupt())
                    {
                        return completion;
                    }

                    var rval = completion.Clone();

                    if (right._expression.IsFunctionDefinition())
                    {
                        ((FunctionInstance) rval).SetFunctionName(identifier.Value);
                    }

                    environmentRecord.SetMutableBinding(identifier, rval, strict);
                    return rval;
                }

                return null;
            }
        }
    }
}
