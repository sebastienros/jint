using System;
using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    public sealed class ExpressionInterpreter
    {
        private readonly Engine _engine;
        private readonly bool _isDebugMode;
        private readonly int _maxRecursionDepth;
        private readonly IReferenceResolver _referenceResolver;

        public ExpressionInterpreter(Engine engine)
        {
            _engine = engine;
            
            // gather some options as fields for faster checks
            _isDebugMode = engine.Options.IsDebugMode;
            _maxRecursionDepth = engine.Options.MaxRecursionDepth;
            _referenceResolver = engine.Options.ReferenceResolver;
        }

        private object EvaluateExpression(Expression expression)
        {
            return _engine.EvaluateExpression(expression);
        }

        public JsValue EvaluateConditionalExpression(ConditionalExpression conditionalExpression)
        {
            var lref = _engine.EvaluateExpression(conditionalExpression.Test);
            if (TypeConverter.ToBoolean(_engine.GetValue(lref, true)))
            {
                var trueRef = _engine.EvaluateExpression(conditionalExpression.Consequent);
                return _engine.GetValue(trueRef, true);
            }
            else
            {
                var falseRef = _engine.EvaluateExpression(conditionalExpression.Alternate);
                return _engine.GetValue(falseRef, true);
            }
        }

        public JsValue EvaluateAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            var lref = _engine.EvaluateExpression((Expression) assignmentExpression.Left) as Reference;
            JsValue rval = _engine.GetValue(_engine.EvaluateExpression(assignmentExpression.Right), true);

            if (lref == null)
            {
                ExceptionHelper.ThrowReferenceError(_engine);
            }

            if (assignmentExpression.Operator == AssignmentOperator.Assign) // "="
            {
                lref.AssertValid(_engine);

                _engine.PutValue(lref, rval);
                _engine._referencePool.Return(lref);
                return rval;
            }

            JsValue lval = _engine.GetValue(lref, false);

            switch (assignmentExpression.Operator)
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
                    lval = TypeConverter.ToInt32(lval) << (int)(TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.RightShiftAssign:
                    lval = TypeConverter.ToInt32(lval) >> (int)(TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                case AssignmentOperator.UnsignedRightShiftAssign:
                    lval = (uint)TypeConverter.ToInt32(lval) >> (int)(TypeConverter.ToUint32(rval) & 0x1F);
                    break;

                default:
                    ExceptionHelper.ThrowNotImplementedException();
                    return null;
            }

            _engine.PutValue(lref, lval);

            _engine._referencePool.Return(lref);
            return lval;
        }

        private JsValue Divide(JsValue lval, JsValue rval)
        {
            if (lval.IsUndefined() || rval.IsUndefined())
            {
                return Undefined.Instance;
            }
            else
            {
                var lN = TypeConverter.ToNumber(lval);
                var rN = TypeConverter.ToNumber(rval);

                if (double.IsNaN(rN) || double.IsNaN(lN))
                {
                    return double.NaN;
                }

                if (double.IsInfinity(lN) && double.IsInfinity(rN))
                {
                    return double.NaN;
                }

                if (double.IsInfinity(lN) && rN == 0)
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return -lN;
                    }

                    return lN;
                }

                if (lN == 0 && rN == 0)
                {
                    return double.NaN;
                }

                if (rN == 0)
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return lN > 0 ? -double.PositiveInfinity : -double.NegativeInfinity;
                    }

                    return lN > 0 ? double.PositiveInfinity : double.NegativeInfinity;
                }

                return lN/rN;
            }
        }

        public JsValue EvaluateBinaryExpression(BinaryExpression expression)
        {
            JsValue left;
            if (expression.Left.Type == Nodes.Literal)
            {
                left = EvaluateLiteral((Literal) expression.Left);
            }
            else
            {
                left = _engine.GetValue(_engine.EvaluateExpression(expression.Left), true);
            }

            JsValue right;
            if (expression.Right.Type == Nodes.Literal)
            {
                right = EvaluateLiteral((Literal) expression.Right);
            }
            else
            {
                right = _engine.GetValue(_engine.EvaluateExpression(expression.Right), true);
            }

            JsValue value;

            switch (expression.Operator)
            {
                case BinaryOperator.Plus:
                    var lprim = TypeConverter.ToPrimitive(left);
                    var rprim = TypeConverter.ToPrimitive(right);
                    if (lprim.IsString() || rprim.IsString())
                    {
                        value = TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim);
                    }
                    else
                    {
                        value = TypeConverter.ToNumber(lprim) + TypeConverter.ToNumber(rprim);
                    }
                    break;

                case BinaryOperator.Minus:
                    value = TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right);
                    break;

                case BinaryOperator.Times:
                    if (left.IsUndefined() || right.IsUndefined())
                    {
                        value = Undefined.Instance;
                    }
                    else
                    {
                        value = TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right);
                    }
                    break;

                case BinaryOperator.Divide:
                    value = Divide(left, right);
                    break;

                case BinaryOperator.Modulo:
                    if (left.IsUndefined() || right.IsUndefined())
                    {
                        value = Undefined.Instance;
                    }
                    else
                    {
                        value = TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right);
                    }
                    break;

                case BinaryOperator.Equal:
                    value = Equal(left, right) ? JsBoolean.True : JsBoolean.False;
                    break;

                case BinaryOperator.NotEqual:
                    value = Equal(left, right) ? JsBoolean.False : JsBoolean.True;
                    break;

                case BinaryOperator.Greater:
                    value = Compare(right, left, false);
                    if (value.IsUndefined())
                    {
                        value = false;
                    }
                    break;

                case BinaryOperator.GreaterOrEqual:
                    value = Compare(left, right);
                    if (value.IsUndefined() || ((JsBoolean) value)._value)
                    {
                        value = false;
                    }
                    else
                    {
                        value = true;
                    }
                    break;

                case BinaryOperator.Less:
                    value = Compare(left, right);
                    if (value.IsUndefined())
                    {
                        value = false;
                    }
                    break;

                case BinaryOperator.LessOrEqual:
                    value = Compare(right, left, false);
                    if (value.IsUndefined() || ((JsBoolean) value)._value)
                    {
                        value = false;
                    }
                    else
                    {
                        value = true;
                    }
                    break;

                case BinaryOperator.StrictlyEqual:
                    return StrictlyEqual(left, right) ? JsBoolean.True : JsBoolean.False;

                case BinaryOperator.StricltyNotEqual:
                    return StrictlyEqual(left, right)? JsBoolean.False : JsBoolean.True;

                case BinaryOperator.BitwiseAnd:
                    return TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right);

                case BinaryOperator.BitwiseOr:
                    return TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right);

                case BinaryOperator.BitwiseXOr:
                    return TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right);

                case BinaryOperator.LeftShift:
                    return TypeConverter.ToInt32(left) << (int)(TypeConverter.ToUint32(right) & 0x1F);

                case BinaryOperator.RightShift:
                    return TypeConverter.ToInt32(left) >> (int)(TypeConverter.ToUint32(right) & 0x1F);

                case BinaryOperator.UnsignedRightShift:
                    return (uint)TypeConverter.ToInt32(left) >> (int)(TypeConverter.ToUint32(right) & 0x1F);

                case BinaryOperator.InstanceOf:
                    var f = right.TryCast<FunctionInstance>();
                    if (ReferenceEquals(f, null))
                    {
                        ExceptionHelper.ThrowTypeError(_engine, "instanceof can only be used with a function object");
                    }
                    value = f.HasInstance(left);
                    break;

                case BinaryOperator.In:
                    if (!right.IsObject())
                    {
                        ExceptionHelper.ThrowTypeError(_engine, "in can only be used with an object");
                    }

                    value = right.AsObject().HasProperty(TypeConverter.ToString(left));
                    break;

                default:
                    ExceptionHelper.ThrowNotImplementedException();
                    return null;
            }

            return value;
        }

        public JsValue EvaluateLogicalExpression(BinaryExpression binaryExpression)
        {
            var left = _engine.GetValue(_engine.EvaluateExpression(binaryExpression.Left), true);

            switch (binaryExpression.Operator)
            {
                case BinaryOperator.LogicalAnd:
                    if (!TypeConverter.ToBoolean(left))
                    {
                        return left;
                    }

                    return _engine.GetValue(_engine.EvaluateExpression(binaryExpression.Right), true);

                case BinaryOperator.LogicalOr:
                    if (TypeConverter.ToBoolean(left))
                    {
                        return left;
                    }

                    return _engine.GetValue(_engine.EvaluateExpression(binaryExpression.Right), true);

                default:
                    ExceptionHelper.ThrowNotImplementedException();
                    return null;
            }
        }

        private static bool Equal(JsValue x, JsValue y)
        {
            if (x._type == y._type)
            {
				return StrictlyEqual(x, y);
            }

            if (x._type == Types.Null && y._type == Types.Undefined)
            {
                return true;
            }

            if (x._type == Types.Undefined && y._type == Types.Null)
            {
                return true;
            }

            if (x._type == Types.Number && y._type == Types.String)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (x._type == Types.String && y._type == Types.Number)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (x._type == Types.Boolean)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (y._type == Types.Boolean)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (y._type == Types.Object && (x._type == Types.String || x._type == Types.Number))
            {
                return Equal(x, TypeConverter.ToPrimitive(y));
            }

            if (x._type == Types.Object && (y._type == Types.String || y._type == Types.Number))
            {
                return Equal(TypeConverter.ToPrimitive(x), y);
            }

            return false;
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

        public static bool SameValue(JsValue x, JsValue y)
        {
            var typea = TypeConverter.GetPrimitiveType(x);
            var typeb = TypeConverter.GetPrimitiveType(y);

            if (typea != typeb)
            {
                return false;
            }

            switch (typea)
            {
                case Types.None:
                    return true;
                case Types.Number:
                var nx = TypeConverter.ToNumber(x);
                var ny = TypeConverter.ToNumber(y);

                if (double.IsNaN(nx) && double.IsNaN(ny))
                {
                    return true;
                }

                if (nx == ny)
                {
                    if (nx == 0)
                    {
                        // +0 !== -0
                        return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                    }

                    return true;
                }

                return false;
                case Types.String:
                    return TypeConverter.ToString(x) == TypeConverter.ToString(y);
                case Types.Boolean:
                    return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
                default:
                    return x == y;
            }

        }

        public static JsValue Compare(JsValue x, JsValue y, bool leftFirst = true)
        {
            JsValue px, py;
            if (leftFirst)
            {
                px = TypeConverter.ToPrimitive(x, Types.Number);
                py = TypeConverter.ToPrimitive(y, Types.Number);
            }
            else
            {
                py = TypeConverter.ToPrimitive(y, Types.Number);
                px = TypeConverter.ToPrimitive(x, Types.Number);
            }

            var typea = px.Type;
            var typeb = py.Type;

            if (typea != Types.String || typeb != Types.String)
            {
                var nx = TypeConverter.ToNumber(px);
                var ny = TypeConverter.ToNumber(py);

                if (double.IsNaN(nx) || double.IsNaN(ny))
                {
                    return Undefined.Instance;
                }

                if (nx == ny)
                {
                    return false;
                }

                if (double.IsPositiveInfinity(nx))
                {
                    return false;
                }

                if (double.IsPositiveInfinity(ny))
                {
                    return true;
                }

                if (double.IsNegativeInfinity(ny))
                {
                    return false;
                }

                if (double.IsNegativeInfinity(nx))
                {
                    return true;
                }

                return nx < ny;
            }
            else
            {
                return String.CompareOrdinal(TypeConverter.ToString(x), TypeConverter.ToString(y)) < 0;
            }
        }

        public Reference EvaluateIdentifier(Identifier identifier)
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;

            return LexicalEnvironment.GetIdentifierReference(env, identifier.Name, strict);
        }

        public JsValue EvaluateLiteral(Literal literal)
        {
            switch (literal.TokenType)
            {
                case TokenType.BooleanLiteral:
                    // bool is fast enough
                    return literal.NumericValue > 0.0 ? JsBoolean.True : JsBoolean.False;
                
                case TokenType.NullLiteral:
                    // and so is null
                    return JsValue.Null;

                case TokenType.NumericLiteral:
                    return (JsValue) (literal.CachedValue = literal.CachedValue ?? JsNumber.Create(literal.NumericValue));
                
                case TokenType.StringLiteral:
                    return (JsValue) (literal.CachedValue = literal.CachedValue ?? JsString.Create((string) literal.Value));
                
                case TokenType.RegularExpression:
                    // should not cache
                    return _engine.RegExp.Construct((System.Text.RegularExpressions.Regex) literal.Value, literal.Regex.Flags);

                default:
                    // a rare case, above types should cover all
                    return JsValue.FromObject(_engine, literal.Value);
            }
        }

        public JsValue EvaluateObjectExpression(ObjectExpression objectExpression)
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-11.1.5
            var propertiesCount = objectExpression.Properties.Count;
            var obj = _engine.Object.Construct(propertiesCount);
            for (var i = 0; i < propertiesCount; i++)
            {
                var property = objectExpression.Properties[i];
                var propName = property.Key.GetKey();
                if (!obj._properties.TryGetValue(propName, out var previous))
                {
                    previous = PropertyDescriptor.Undefined;
                }
                
                PropertyDescriptor propDesc;

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var exprValue = _engine.EvaluateExpression(property.Value);
                    var propValue = _engine.GetValue(exprValue, true);
                    propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                }
                else if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
                {
                    var function = property.Value as IFunction;

                    if (function == null)
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    ScriptFunctionInstance functionInstance;
                    using (new StrictModeScope(function.Strict))
                    {
                        functionInstance = new ScriptFunctionInstance(
                            _engine,
                            function,
                            _engine.ExecutionContext.LexicalEnvironment,
                            StrictModeScope.IsStrictModeCode
                        );
                    }

                    propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? functionInstance : null,
                        set: property.Kind == PropertyKind.Set ? functionInstance : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);
                }
                else
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException();
                    return null;
                }

                if (previous != PropertyDescriptor.Undefined)
                {
                    if (StrictModeScope.IsStrictModeCode && previous.IsDataDescriptor() && propDesc.IsDataDescriptor())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    if (previous.IsDataDescriptor() && propDesc.IsAccessorDescriptor())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    if (previous.IsAccessorDescriptor() && propDesc.IsDataDescriptor())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    if (previous.IsAccessorDescriptor() && propDesc.IsAccessorDescriptor())
                    {
                        if (!ReferenceEquals(propDesc.Set, null) && !ReferenceEquals(previous.Set, null))
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }

                        if (!ReferenceEquals(propDesc.Get, null) && !ReferenceEquals(previous.Get, null))
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }
                    }

                    obj.DefineOwnProperty(propName, propDesc, false);
                }
                else
                {
                    // do faster direct set
                    obj._properties[propName] = propDesc;
                }
            }

            return obj;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
        /// </summary>
        /// <param name="memberExpression"></param>
        /// <returns></returns>
        public Reference EvaluateMemberExpression(MemberExpression memberExpression)
        {
            var baseReference = _engine.EvaluateExpression(memberExpression.Object);
            var baseValue = _engine.GetValue(baseReference, false);

            string propertyNameString;
            if (!memberExpression.Computed) // index accessor ?
            {
                // we can take fast path without querying the engine again
                propertyNameString = ((Identifier) memberExpression.Property).Name;
            }
            else
            {
                var propertyNameReference = _engine.EvaluateExpression(memberExpression.Property);
                var propertyNameValue = _engine.GetValue(propertyNameReference, true);
                propertyNameString = TypeConverter.ToString(propertyNameValue);
            }

            TypeConverter.CheckObjectCoercible(_engine, baseValue, memberExpression, baseReference);

            if (baseReference is Reference r)
            {
                _engine._referencePool.Return(r);
            }
            return _engine._referencePool.Rent(baseValue, propertyNameString, StrictModeScope.IsStrictModeCode);
        }

        public JsValue EvaluateFunctionExpression(IFunction functionExpression)
        {
            var funcEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var envRec = (DeclarativeEnvironmentRecord) funcEnv._record;

            var closure = new ScriptFunctionInstance(
                _engine,
                functionExpression,
                funcEnv,
                functionExpression.Strict
                );

            if (!string.IsNullOrEmpty(functionExpression.Id?.Name))
            {
                envRec.CreateMutableBinding(functionExpression.Id.Name, closure);
            }

            return closure;
        }

        public JsValue EvaluateCallExpression(CallExpression callExpression)
        {
            var callee = _engine.EvaluateExpression(callExpression.Callee);
            
            if (_isDebugMode)
            {
                _engine.DebugHandler.AddToDebugCallStack(callExpression);
            }

            // todo: implement as in http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.4

            var arguments = ArrayExt.Empty<JsValue>();
            if (callExpression.Cached)
            {
                arguments = (JsValue[]) callExpression.CachedArguments;
            }
            else
            {
                var allLiteral = true;
                if (callExpression.Arguments.Count > 0)
                {
                    arguments = _engine._jsValueArrayPool.RentArray(callExpression.Arguments.Count);
                    BuildArguments(callExpression.Arguments, arguments, out allLiteral);
                }

                if (callExpression.CanBeCached)
                {
                    // The arguments array can be cached if they are all literals
                    if (allLiteral)
                    {
                        callExpression.CachedArguments = arguments;
                        callExpression.Cached = true;
                    }
                    else
                    {
                        callExpression.CanBeCached = false;
                    }
                }
            }

            var func = _engine.GetValue(callee, false);

            var r = callee as Reference;
            if (_maxRecursionDepth >= 0)
            {
                var stackItem = new CallStackElement(callExpression, func, r?._name ?? "anonymous function");

                var recursionDepth = _engine.CallStack.Push(stackItem);

                if (recursionDepth > _maxRecursionDepth)
                {
                    _engine.CallStack.Pop();
                    ExceptionHelper.ThrowRecursionDepthOverflowException(_engine.CallStack, stackItem.ToString());
                }
            }

            if (func._type == Types.Undefined)
            {
                ExceptionHelper.ThrowTypeError(_engine, r == null ? "" : $"Object has no method '{r.GetReferencedName()}'");
            }

            if (func._type != Types.Object)
            {
                if (_referenceResolver == null || !_referenceResolver.TryGetCallable(_engine, callee, out func))
                {
                    ExceptionHelper.ThrowTypeError(_engine,
                        r == null ? "" : $"Property '{r.GetReferencedName()}' of object is not a function");
                }
            }

            var callable = func as ICallable;
            if (callable == null)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var thisObject = Undefined.Instance;
            if (r != null)
            {
                if (r.IsPropertyReference())
                {
                    thisObject = r._baseValue;
                }
                else
                {
                    var env = (EnvironmentRecord) r._baseValue;
                    thisObject = env.ImplicitThisValue();
                }
                
                // is it a direct call to eval ? http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.1.1
                if (r._name == "eval" && callable is EvalFunctionInstance instance)
                {
                    var value = instance.Call(thisObject, arguments, true);
                    _engine._referencePool.Return(r);
                    return value;
                }
            }

            var result = callable.Call(thisObject, arguments);

            if (_isDebugMode)
            {
                _engine.DebugHandler.PopDebugCallStack();
            }

            if (_maxRecursionDepth >= 0)
            {
                _engine.CallStack.Pop();
            }

            if (!callExpression.Cached && arguments.Length > 0)
            {
                _engine._jsValueArrayPool.ReturnArray(arguments);
            }

            _engine._referencePool.Return(r);
            return result;
        }

        public JsValue EvaluateSequenceExpression(SequenceExpression sequenceExpression)
        {
            var result = Undefined.Instance;
            var expressionsCount = sequenceExpression.Expressions.Count;
            for (var i = 0; i < expressionsCount; i++)
            {
                var expression = sequenceExpression.Expressions[i];
                result = _engine.GetValue(_engine.EvaluateExpression(expression), true);
            }

            return result;
        }

        public JsValue EvaluateUpdateExpression(UpdateExpression updateExpression)
        {
            var value = _engine.EvaluateExpression(updateExpression.Argument);

            var r = (Reference) value;
            r.AssertValid(_engine);

            var oldValue = TypeConverter.ToNumber(_engine.GetValue(value, false));
            double newValue = 0;
            if (updateExpression.Operator == UnaryOperator.Increment)
            {
                newValue = oldValue + 1;
            }
            else if (updateExpression.Operator == UnaryOperator.Decrement)
            {
                newValue = oldValue - 1;
            }
            else
            {
                ExceptionHelper.ThrowArgumentException();
            }

            _engine.PutValue(r, newValue);
            _engine._referencePool.Return(r);
            return updateExpression.Prefix ? newValue : oldValue;
        }

        public JsValue EvaluateThisExpression(ThisExpression thisExpression)
        {
            return _engine.ExecutionContext.ThisBinding;
        }

        public JsValue EvaluateNewExpression(NewExpression newExpression)
        {
            var arguments = _engine._jsValueArrayPool.RentArray(newExpression.Arguments.Count);
            BuildArguments(newExpression.Arguments, arguments, out _);

            // todo: optimize by defining a common abstract class or interface
            var callee = _engine.GetValue(_engine.EvaluateExpression(newExpression.Callee), true).TryCast<IConstructor>();

            if (callee == null)
            {
                ExceptionHelper.ThrowTypeError(_engine, "The object can't be used as constructor.");
            }

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments);

            _engine._jsValueArrayPool.ReturnArray(arguments);

            return instance;
        }

        public JsValue EvaluateArrayExpression(ArrayExpression arrayExpression)
        {
            var elements = arrayExpression.Elements;
            var count = elements.Count;
            
            var a = _engine.Array.ConstructFast((uint) count);
            for (var n = 0; n < count; n++)
            {
                var expr = elements[n];
                if (expr != null)
                {
                    var value = _engine.GetValue(_engine.EvaluateExpression((Expression) expr), true);
                    a.SetIndexValue((uint) n, value, updateLength: false);
                }
            }
            return a;
        }

        public JsValue EvaluateUnaryExpression(UnaryExpression unaryExpression)
        {
            var value = _engine.EvaluateExpression(unaryExpression.Argument);

            switch (unaryExpression.Operator)
            {
                case UnaryOperator.Plus:
                    return TypeConverter.ToNumber(_engine.GetValue(value, true));

                case UnaryOperator.Minus:
                    var n = TypeConverter.ToNumber(_engine.GetValue(value, true));
                    return double.IsNaN(n) ? double.NaN : n*-1;

                case UnaryOperator.BitwiseNot:
                    return ~TypeConverter.ToInt32(_engine.GetValue(value, true));

                case UnaryOperator.LogicalNot:
                    return !TypeConverter.ToBoolean(_engine.GetValue(value, true));

                case UnaryOperator.Delete:
                    var r = value as Reference;
                    if (r == null)
                    {
                        return true;
                    }
                    if (r.IsUnresolvableReference())
                    {
                        if (r._strict)
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }

                        _engine._referencePool.Return(r);
                        return true;
                    }
                    if (r.IsPropertyReference())
                    {
                        var o = TypeConverter.ToObject(_engine, r.GetBase());
                        var jsValue = o.Delete(r._name, r._strict);
                        _engine._referencePool.Return(r);
                        return jsValue;
                    }
                    if (r._strict)
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    var referencedName = r.GetReferencedName();
                    _engine._referencePool.Return(r);

                    return bindings.DeleteBinding(referencedName);

                case UnaryOperator.Void:
                    _engine.GetValue(value, true);
                    return Undefined.Instance;

                case UnaryOperator.TypeOf:
                    r = value as Reference;
                    if (r != null)
                    {
                        if (r.IsUnresolvableReference())
                        {
                            _engine._referencePool.Return(r);
                            return "undefined";
                        }
                    }

                    var v = _engine.GetValue(value, true);

                    if (v.IsUndefined())
                    {
                        return "undefined";
                    }
                    if (v.IsNull())
                    {
                        return "object";
                    }
                    switch (v.Type)
                    {
                        case Types.Boolean: return "boolean";
                        case Types.Number: return "number";
                        case Types.String: return "string";
                    }
                    if (v.TryCast<ICallable>() != null)
                    {
                        return "function";
                    }
                    return "object";

                default:
                    ExceptionHelper.ThrowArgumentException();
                    return null;
            }
        }

        private void BuildArguments(
            List<ArgumentListElement> expressionArguments, 
            JsValue[] targetArray,
            out bool cacheable)
        {
            cacheable = true;
            for (var i = 0; i < (uint) targetArray.Length; i++)
            {
                var argument = (Expression) expressionArguments[i];
                targetArray[i] = _engine.GetValue(_engine.EvaluateExpression(argument), true);
                cacheable &= argument is Literal;
            }
        }
    }
}
