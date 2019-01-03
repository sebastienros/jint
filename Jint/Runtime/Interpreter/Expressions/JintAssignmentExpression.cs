using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintAssignmentExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;

        private JintAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
        {
            _left = Build(engine, (Expression) expression.Left);
            _right = Build(engine, expression.Right);
        }

        internal static JintExpression Build(Engine engine, AssignmentExpression expression)
        {
            if (expression.Operator == AssignmentOperator.Assign)
            {
                if (expression.Left is Expression)
                {
                    return new SimpleAssignmentExpression(engine, expression);
                }

                if (expression.Left is ObjectPattern)
                {
                    return new ObjectPatternAssignmentExpression(engine, expression);
                }

                if (expression.Left is ArrayPattern)
                {
                    return new ArrayPatternAssignmentExpression(engine, expression);
                }
            }

            return new JintAssignmentExpression(engine, expression);
        }

        protected override object EvaluateInternal()
        {
            var lref = _left.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);
            JsValue rval = _right.GetValue();

            JsValue lval = _engine.GetValue(lref, false);

            var expression = (AssignmentExpression) _expression;
            switch (expression.Operator)
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

        internal sealed class SimpleAssignmentExpression : JintExpression
        {
            private readonly JintExpression _left;
            private readonly JintExpression _right;

            private readonly JintIdentifierExpression _leftIdentifier;
            private readonly bool _evalOrArguments;
            private readonly ArrayPattern _arrayPattern;

            public SimpleAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
            {
                if (expression.Left is ArrayPattern arrayPattern)
                {
                    _arrayPattern = arrayPattern;
                }
                else
                {
                    _left = Build(engine, (Expression) expression.Left);
                    _leftIdentifier = _left as JintIdentifierExpression;
                    _evalOrArguments = _leftIdentifier?._expressionName == "eval" || _leftIdentifier?._expressionName == "arguments";
                }

                _right = Build(engine, expression.Right);
            }

            protected override object EvaluateInternal()
            {
                JsValue rval = null;
                if (_leftIdentifier != null)
                {
                    rval = AssignToIdentifier(_engine, _leftIdentifier, _right, _evalOrArguments);
                }
                else if (_arrayPattern != null)
                {
                    foreach (var element in _arrayPattern.Elements)
                    {
                        AssignToIdentifier(_engine, _leftIdentifier, _right, false);
                    }
                }

                return rval ?? SetValue();
            }

            private JsValue SetValue()
            {
                // slower version
                var lref = _left.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(_engine);
                lref.AssertValid(_engine);

                var rval = _right.GetValue();
                _engine.PutValue(lref, rval);
                _engine._referencePool.Return(lref);
                return rval;
            }

            internal static JsValue AssignToIdentifier(
                Engine engine,
                JintIdentifierExpression left,
                JintExpression right,
                bool hasEvalOrArguments)
            {
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    left._expressionName,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    if (strict && hasEvalOrArguments)
                    {
                        ExceptionHelper.ThrowSyntaxError(engine);
                    }

                    var rval = right.GetValue();
                    environmentRecord.SetMutableBinding(left._expressionName, rval, strict);
                    return rval;
                }

                return null;
            }
        }

        internal sealed class ObjectPatternAssignmentExpression : JintExpression
        {
            private readonly ObjectPattern _objectPattern;
            private readonly JintExpression _right;

            public ObjectPatternAssignmentExpression(Engine engine, AssignmentExpression expression) : base(engine, expression)
            {
                _objectPattern = (ObjectPattern) expression.Left;
                _right = Build(engine, expression.Right);
            }

            protected override object EvaluateInternal()
            {
                AssignToPattern(_engine, _objectPattern, _right.GetValue());
                return JsValue.Undefined;
            }
            
            internal static void AssignToPattern(Engine engine, ObjectPattern objectPattern, JsValue argument)
            {
                var source = (ObjectInstance) argument;
                for (uint i = 0; i < objectPattern.Properties.Count; i++)
                {
                    var left = objectPattern.Properties[(int) i];
                    if (left.Key is Identifier identifier)
                    {
                        if (!source.TryGetValue(identifier.Name, out var value)
                            && left.Value is AssignmentPattern assignmentPattern
                            && assignmentPattern.Right is Literal l)
                        {
                            value = JintLiteralExpression.ConvertToJsValue(l);
                        }
                        var target = left.Value as Identifier ?? identifier;
                        AssignToIdentifier(engine, target.Name, value);
                    }
                    else
                    {
                        ExceptionHelper.ThrowArgumentOutOfRangeException("pattern", "Unable to determine how to handle object pattern element");
                        break;
                    }
                }
            }
            
            private static JsValue AssignToIdentifier(
                Engine engine,
                string name,
                JsValue rval)
            {
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    name,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    environmentRecord.SetMutableBinding(name, rval, strict);
                    return rval;
                }

                return null;
            }

        }

        internal sealed class ArrayPatternAssignmentExpression : JintExpression
        {
            private readonly AssignmentExpression _assignmentExpression;
            private ArrayPattern _arrayPattern;
            private JintExpression _right;

            public ArrayPatternAssignmentExpression(
                Engine engine,
                AssignmentExpression expression) : base(engine, expression)
            {
                _assignmentExpression = expression;
                _initialized = false;
            }

            protected override void Initialize()
            {
                _arrayPattern = (ArrayPattern) _assignmentExpression.Left;
                _right = Build(_engine, _assignmentExpression.Right);
            }

            protected override object EvaluateInternal()
            {
                AssignToPattern(_engine, _arrayPattern, _right.GetValue());
                return JsValue.Undefined;
            }

            internal static void AssignToPattern(Engine engine, ArrayPattern arrayPattern, JsValue argument)
            {
                var source = ArrayPrototype.ArrayOperations.For(engine, argument);
                for (uint i = 0; i < arrayPattern.Elements.Count; i++)
                {
                    var left = arrayPattern.Elements[(int) i];
                    if (left is Identifier identifier)
                    {
                        source.TryGetValue(i, out var value);
                        AssignToIdentifier(engine, identifier.Name, value);
                    }
                    else if (left is ArrayPatternElement arrayPatternElement)
                    {
                        if (arrayPatternElement is RestElement restElement)
                        {
                            var length = source.GetLength();
                            var array = engine.Array.ConstructFast(length - i);
                            for (uint j = i; j < length; ++j)
                            {
                                source.TryGetValue(j, out var indexValue);
                                array.SetIndexValue(j - i, indexValue, updateLength: false);
                            }

                            AssignToIdentifier(engine, ((Identifier) restElement.Argument).Name, array);
                        }
                        else if (arrayPatternElement is AssignmentPattern assignmentPattern)
                        {
                            if (!source.TryGetValue(i, out var value)
                                && assignmentPattern.Right is Literal l)
                            {
                                value = JintLiteralExpression.ConvertToJsValue(l);
                            } 
                            AssignToIdentifier(engine, ((Identifier) assignmentPattern.Left).Name, value);
                        }
                        else
                        {
                            ExceptionHelper.ThrowArgumentOutOfRangeException("pattern", "Unable to determine how to handle array pattern element");
                            break;
                        }
                    }
                }
            }

            private static JsValue AssignToIdentifier(
                Engine engine,
                string name,
                JsValue rval)
            {
                var env = engine.ExecutionContext.LexicalEnvironment;
                var strict = StrictModeScope.IsStrictModeCode;
                if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    name,
                    strict,
                    out var environmentRecord,
                    out _))
                {
                    environmentRecord.SetMutableBinding(name, rval, strict);
                    return rval;
                }

                return null;
            }
        }
    }
}