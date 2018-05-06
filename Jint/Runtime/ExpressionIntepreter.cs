using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    public class ExpressionInterpreter
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
            var lref = EvaluateExpression((Expression) assignmentExpression.Left) as Reference;
            JsValue rval = _engine.GetValue(EvaluateExpression(assignmentExpression.Right), true);

            if (lref == null)
            {
                throw new JavaScriptException(_engine.ReferenceError);
            }

            if (assignmentExpression.Operator == AssignmentOperator.Assign) // "="
            {

                if(lref.IsStrict()
                   && lref.GetBase() is EnvironmentRecord
                   && (lref.GetReferencedName() == "eval" || lref.GetReferencedName() == "arguments"))
                {
                    throw new JavaScriptException(_engine.SyntaxError);
                }

                _engine.PutValue(lref, rval);
                _engine.ReferencePool.Return(lref);
                return rval;
            }

            JsValue lval = _engine.GetValue(lref);

            switch (assignmentExpression.Operator)
            {
                case AssignmentOperator.PlusAssign:
                    var lprim = TypeConverter.ToPrimitive(lval);
                    var rprim = TypeConverter.ToPrimitive(rval);
                    if (lprim.IsString() || rprim.IsString())
                    {
                        var jsString = lprim as JsString;
                        if (ReferenceEquals(jsString, null))
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
                    if (ReferenceEquals(lval, Undefined.Instance) || ReferenceEquals(rval, Undefined.Instance))
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
                    if (ReferenceEquals(lval, Undefined.Instance) || ReferenceEquals(rval, Undefined.Instance))
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
                    throw new NotImplementedException();

            }

            _engine.PutValue(lref, lval);

            _engine.ReferencePool.Return(lref);
            return lval;
        }

        private JsValue Divide(JsValue lval, JsValue rval)
        {
            if (ReferenceEquals(lval, Undefined.Instance) || ReferenceEquals(rval, Undefined.Instance))
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

                if (double.IsInfinity(lN) && rN.Equals(0))
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return -lN;
                    }

                    return lN;
                }

                if (lN.Equals(0) && rN.Equals(0))
                {
                    return double.NaN;
                }

                if (rN.Equals(0))
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
                left = _engine.GetValue(EvaluateExpression(expression.Left), true);
            }

            JsValue right;
            if (expression.Right.Type == Nodes.Literal)
            {
                right = EvaluateLiteral((Literal) expression.Right);
            }
            else
            {
                right = _engine.GetValue(EvaluateExpression(expression.Right), true);
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
                    if (ReferenceEquals(left, Undefined.Instance) || ReferenceEquals(right, Undefined.Instance))
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
                    if (ReferenceEquals(left, Undefined.Instance) || ReferenceEquals(right, Undefined.Instance))
                    {
                        value = Undefined.Instance;
                    }
                    else
                    {
                        value = TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right);
                    }
                    break;

                case BinaryOperator.Equal:
                    value = Equal(left, right);
                    break;

                case BinaryOperator.NotEqual:
                    value = !Equal(left, right);
                    break;

                case BinaryOperator.Greater:
                    value = Compare(right, left, false);
                    if (ReferenceEquals(value, Undefined.Instance))
                    {
                        value = false;
                    }
                    break;

                case BinaryOperator.GreaterOrEqual:
                    value = Compare(left, right);
                    if (ReferenceEquals(value, Undefined.Instance) || ((JsBoolean) value)._value)
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
                    if (ReferenceEquals(value, Undefined.Instance))
                    {
                        value = false;
                    }
                    break;

                case BinaryOperator.LessOrEqual:
                    value = Compare(right, left, false);
                    if (ReferenceEquals(value, Undefined.Instance) || ((JsBoolean) value)._value)
                    {
                        value = false;
                    }
                    else
                    {
                        value = true;
                    }
                    break;

                case BinaryOperator.StrictlyEqual:
                    return StrictlyEqual(left, right);

                case BinaryOperator.StricltyNotEqual:
                    return !StrictlyEqual(left, right);

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
                        throw new JavaScriptException(_engine.TypeError, "instanceof can only be used with a function object");
                    }
                    value = f.HasInstance(left);
                    break;

                case BinaryOperator.In:
                    if (!right.IsObject())
                    {
                        throw new JavaScriptException(_engine.TypeError, "in can only be used with an object");
                    }

                    value = right.AsObject().HasProperty(TypeConverter.ToString(left));
                    break;

                default:
                    throw new NotImplementedException();
            }

            return value;
        }

        public JsValue EvaluateLogicalExpression(BinaryExpression binaryExpression)
        {
            var left = _engine.GetValue(EvaluateExpression(binaryExpression.Left), true);

            switch (binaryExpression.Operator)
            {

                case BinaryOperator.LogicalAnd:
                    if (!TypeConverter.ToBoolean(left))
                    {
                        return left;
                    }

                    return _engine.GetValue(EvaluateExpression(binaryExpression.Right), true);

                case BinaryOperator.LogicalOr:
                    if (TypeConverter.ToBoolean(left))
                    {
                        return left;
                    }

                    return _engine.GetValue(EvaluateExpression(binaryExpression.Right), true);

                default:
                    throw new NotImplementedException();
            }
        }

        private static bool Equal(JsValue x, JsValue y)
        {
            var typex = x.Type;
            var typey = y.Type;

            if (typex == typey)
            {
				return StrictlyEqual(x, y);
            }

            if (ReferenceEquals(x, Null.Instance) && ReferenceEquals(y, Undefined.Instance))
            {
                return true;
            }

            if (ReferenceEquals(x, Undefined.Instance) && ReferenceEquals(y, Null.Instance))
            {
                return true;
            }

            if (typex == Types.Number && typey == Types.String)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (typex == Types.String && typey == Types.Number)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (typex == Types.Boolean)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (typey == Types.Boolean)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (typey == Types.Object && (typex == Types.String || typex == Types.Number))
            {
                return Equal(x, TypeConverter.ToPrimitive(y));
            }

            if (typex == Types.Object && (typey == Types.String || typey == Types.Number))
            {
                return Equal(TypeConverter.ToPrimitive(x), y);
            }

            return false;
        }

        public static bool StrictlyEqual(JsValue x, JsValue y)
        {
            var typea = x.Type;
            var typeb = y.Type;

            if (typea != typeb)
            {
                return false;
            }

            if (typea == Types.Undefined || typea == Types.Null)
            {
                return true;
            }

            if (typea == Types.Number)
            {
                var nx = ((JsNumber) x)._value;
                var ny = ((JsNumber) y)._value;

                if (double.IsNaN(nx) || double.IsNaN(ny))
                {
                    return false;
                }

                if (nx.Equals(ny))
                {
                    return true;
                }

                return false;
            }

            if (typea == Types.String)
            {
                return x.AsStringWithoutTypeCheck() == y.AsStringWithoutTypeCheck();
            }

            if (typea == Types.Boolean)
            {
                return ((JsBoolean) x)._value == ((JsBoolean) y)._value;
            }

			if (typea == Types.Object)
			{
			    if (x.AsObject() is IObjectWrapper xw)
				{
					var yw = y.AsObject() as IObjectWrapper;
					return Equals(xw.Target, yw.Target);
				}
			}

            if (typea == Types.None)
            {
                return true;
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

            if (typea == Types.None)
            {
                return true;
            }
            if (typea == Types.Number)
            {
                var nx = TypeConverter.ToNumber(x);
                var ny = TypeConverter.ToNumber(y);

                if (double.IsNaN(nx) && double.IsNaN(ny))
                {
                    return true;
                }

                if (nx.Equals(ny))
                {
                    if (nx.Equals(0))
                    {
                        // +0 !== -0
                        return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                    }

                    return true;
                }

                return false;
            }
            if (typea == Types.String)
            {
                return TypeConverter.ToString(x) == TypeConverter.ToString(y);
            }
            if (typea == Types.Boolean)
            {
                return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
            }
            return x == y;
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

                if (nx.Equals(ny))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue EvaluateLiteral(Literal literal)
        {
            switch (literal.TokenType)
            {
                case TokenType.BooleanLiteral:
                    return literal.BooleanValue ? JsBoolean.True : JsBoolean.False;
                case TokenType.NullLiteral:
                    return JsValue.Null;
                case TokenType.NumericLiteral:
                    // implicit conversion operator goes through caching
                    return literal.NumericValue;
                case TokenType.StringLiteral:
                    // implicit conversion operator goes through caching
                    return literal.StringValue;
            }

            if (literal.RegexValue != null) //(literal.Type == Nodes.RegularExpressionLiteral)
            {
                return _engine.RegExp.Construct(literal.RegexValue, literal.Regex.Flags);
            }

            return JsValue.FromObject(_engine, literal.Value);
        }

        public JsValue EvaluateObjectExpression(ObjectExpression objectExpression)
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-11.1.5

            var obj = _engine.Object.Construct(Arguments.Empty);
            var propertiesCount = objectExpression.Properties.Count;
            for (var i = 0; i < propertiesCount; i++)
            {
                var property = objectExpression.Properties[i];
                var propName = property.Key.GetKey();
                var previous = obj.GetOwnProperty(propName);
                PropertyDescriptor propDesc;

                const PropertyFlag enumerableConfigurable = PropertyFlag.Enumerable | PropertyFlag.Configurable;
                
                switch (property.Kind)
                {
                    case PropertyKind.Init:
                    case PropertyKind.Data:
                        var exprValue = _engine.EvaluateExpression(property.Value);
                        var propValue = _engine.GetValue(exprValue, true);
                        propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                        break;

                    case PropertyKind.Get:
                        var getter = property.Value as IFunction;

                        if (getter == null)
                        {
                            throw new JavaScriptException(_engine.SyntaxError);
                        }

                        ScriptFunctionInstance get;
                        using (new StrictModeScope(getter.Strict))
                        {
                            get = new ScriptFunctionInstance(
                                _engine,
                                getter,
                                _engine.ExecutionContext.LexicalEnvironment,
                                StrictModeScope.IsStrictModeCode
                            );
                        }

                        propDesc = new GetSetPropertyDescriptor(get: get, set: null, enumerableConfigurable);
                        break;

                    case PropertyKind.Set:
                        var setter = property.Value as IFunction;

                        if (setter == null)
                        {
                            throw new JavaScriptException(_engine.SyntaxError);
                        }

                        ScriptFunctionInstance set;
                        using (new StrictModeScope(setter.Strict))
                        {
                            set = new ScriptFunctionInstance(
                                _engine,
                                setter,
                                _engine.ExecutionContext.LexicalEnvironment,
                                StrictModeScope.IsStrictModeCode
                            );
                        }

                        propDesc = new GetSetPropertyDescriptor(get: null, set: set, enumerableConfigurable);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (previous != PropertyDescriptor.Undefined)
                {
                    if (StrictModeScope.IsStrictModeCode && previous.IsDataDescriptor() && propDesc.IsDataDescriptor())
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    if (previous.IsDataDescriptor() && propDesc.IsAccessorDescriptor())
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    if (previous.IsAccessorDescriptor() && propDesc.IsDataDescriptor())
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    if (previous.IsAccessorDescriptor() && propDesc.IsAccessorDescriptor())
                    {
                        if (!ReferenceEquals(propDesc.Set, null) && !ReferenceEquals(previous.Set, null))
                        {
                            throw new JavaScriptException(_engine.SyntaxError);
                        }

                        if (!ReferenceEquals(propDesc.Get, null) && !ReferenceEquals(previous.Get, null))
                        {
                            throw new JavaScriptException(_engine.SyntaxError);
                        }
                    }
                }

                obj.DefineOwnProperty(propName, propDesc, false);
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
            var baseReference = EvaluateExpression(memberExpression.Object);
            var baseValue = _engine.GetValue(baseReference);

            string propertyNameString;
            if (!memberExpression.Computed) // index accessor ?
            {
                // we can take fast path without querying the engine again
                propertyNameString = ((Identifier) memberExpression.Property).Name;
            }
            else
            {
                var propertyNameReference = EvaluateExpression(memberExpression.Property);
                var propertyNameValue = _engine.GetValue(propertyNameReference, true);
                propertyNameString = TypeConverter.ToString(propertyNameValue);
            }

            TypeConverter.CheckObjectCoercible(_engine, baseValue, memberExpression, baseReference);

            if (baseReference is Reference r)
            {
                _engine.ReferencePool.Return(r);
            }
            return _engine.ReferencePool.Rent(baseValue, propertyNameString, StrictModeScope.IsStrictModeCode);
        }

        public JsValue EvaluateFunctionExpression(IFunction functionExpression)
        {
            var funcEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var envRec = (DeclarativeEnvironmentRecord)funcEnv.Record;

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
            var callee = EvaluateExpression(callExpression.Callee);
            
            if (_isDebugMode)
            {
                _engine.DebugHandler.AddToDebugCallStack(callExpression);
            }

            JsValue thisObject;
            // todo: implement as in http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.4

            var arguments = Array.Empty<JsValue>();
            if (callExpression.Cached)
            {
                arguments = (JsValue[]) callExpression.CachedArguments;
            }
            else
            {
                var allLiteral = true;
                if (callExpression.Arguments.Count > 0)
                {
                    arguments = _engine.JsValueArrayPool.RentArray(callExpression.Arguments.Count);
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

            var func = _engine.GetValue(callee);

            var r = callee as Reference;

            if (_maxRecursionDepth >= 0)
            {
                var stackItem = new CallStackElement(callExpression, func, r != null ? r.GetReferencedName() : "anonymous function");

                var recursionDepth = _engine.CallStack.Push(stackItem);

                if (recursionDepth > _maxRecursionDepth)
                {
                    _engine.CallStack.Pop();
                    throw new RecursionDepthOverflowException(_engine.CallStack, stackItem.ToString());
                }
            }

            if (ReferenceEquals(func, Undefined.Instance))
            {
                throw new JavaScriptException(_engine.TypeError, r == null ? "" : string.Format("Object has no method '{0}'", r.GetReferencedName()));
            }

            if (!func.IsObject())
            {
                if (_referenceResolver == null || !_referenceResolver.TryGetCallable(_engine, callee, out func))
                {
                    throw new JavaScriptException(_engine.TypeError,
                        r == null ? "" : $"Property '{r.GetReferencedName()}' of object is not a function");
                }
            }

            var callable = func.TryCast<ICallable>();
            if (callable == null)
            {
                throw new JavaScriptException(_engine.TypeError);
            }

            if (r != null)
            {
                if (r.IsPropertyReference())
                {
                    thisObject = r.GetBase();
                }
                else
                {
                    var env = r.GetBase().TryCast<EnvironmentRecord>();
                    thisObject = env.ImplicitThisValue();
                }
            }
            else
            {
                thisObject = Undefined.Instance;
            }

            // is it a direct call to eval ? http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.1.1
            if (r != null && r.GetReferencedName() == "eval" && callable is EvalFunctionInstance)
            {
                var value = ((EvalFunctionInstance) callable).Call(thisObject, arguments, true);
                _engine.ReferencePool.Return(r);
                return value;
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
                _engine.JsValueArrayPool.ReturnArray(arguments);
            }

            _engine.ReferencePool.Return(r);
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
            Reference r;

            switch (updateExpression.Operator)
            {
                case UnaryOperator.Increment:
                    r = value as Reference;
                    if (r != null
                        && r.IsStrict()
                        && r.GetBase() is EnvironmentRecord
                        && ("eval" == r.GetReferencedName() || "arguments" == r.GetReferencedName()))
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    var oldValue = TypeConverter.ToNumber(_engine.GetValue(value));
                    var newValue = oldValue + 1;
                    _engine.PutValue(r, newValue);

                    _engine.ReferencePool.Return(r);
                    return updateExpression.Prefix ? newValue : oldValue;

                case UnaryOperator.Decrement:
                    r = value as Reference;
                    if (r != null
                        && r.IsStrict()
                        && r.GetBase() is EnvironmentRecord
                        && ("eval" == r.GetReferencedName() || "arguments" == r.GetReferencedName()))
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    oldValue = TypeConverter.ToNumber(_engine.GetValue(value));
                    newValue = oldValue - 1;

                    _engine.PutValue(r, newValue);
                    _engine.ReferencePool.Return(r);

                    return updateExpression.Prefix ? newValue : oldValue;
                default:
                    throw new ArgumentException();
            }

        }

        public JsValue EvaluateThisExpression(ThisExpression thisExpression)
        {
            return _engine.ExecutionContext.ThisBinding;
        }

        public JsValue EvaluateNewExpression(NewExpression newExpression)
        {
            var arguments = _engine.JsValueArrayPool.RentArray(newExpression.Arguments.Count);
            BuildArguments(newExpression.Arguments, arguments, out _);

            // todo: optimize by defining a common abstract class or interface
            var callee = _engine.GetValue(EvaluateExpression(newExpression.Callee), true).TryCast<IConstructor>();

            if (callee == null)
            {
                throw new JavaScriptException(_engine.TypeError, "The object can't be used as constructor.");
            }

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments);

            _engine.JsValueArrayPool.ReturnArray(arguments);

            return instance;
        }

        public JsValue EvaluateArrayExpression(ArrayExpression arrayExpression)
        {
            var elements = arrayExpression.Elements;
            var count = elements.Count;
            
            var jsValues = _engine.JsValueArrayPool.RentArray(1);
            jsValues[0] = count;
            
            var a = _engine.Array.Construct(jsValues, (uint) count);
            for (var n = 0; n < count; n++)
            {
                var expr = elements[n];
                if (expr != null)
                {
                    var value = _engine.GetValue(EvaluateExpression((Expression) expr), true);
                    a.SetIndexValue((uint) n, value, updateLength: false);
                }
            }
            a.SetLength((uint) count);
            _engine.JsValueArrayPool.ReturnArray(jsValues);

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
                        if (r.IsStrict())
                        {
                            throw new JavaScriptException(_engine.SyntaxError);
                        }

                        _engine.ReferencePool.Return(r);
                        return true;
                    }
                    if (r.IsPropertyReference())
                    {
                        var o = TypeConverter.ToObject(_engine, r.GetBase());
                        var jsValue = o.Delete(r.GetReferencedName(), r.IsStrict());
                        _engine.ReferencePool.Return(r);
                        return jsValue;
                    }
                    if (r.IsStrict())
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    var referencedName = r.GetReferencedName();
                    _engine.ReferencePool.Return(r);

                    return bindings.DeleteBinding(referencedName);

                case UnaryOperator.Void:
                    _engine.GetValue(value);
                    return Undefined.Instance;

                case UnaryOperator.TypeOf:
                    r = value as Reference;
                    if (r != null)
                    {
                        if (r.IsUnresolvableReference())
                        {
                            _engine.ReferencePool.Return(r);
                            return "undefined";
                        }
                    }

                    var v = _engine.GetValue(value, true);

                    if (ReferenceEquals(v, Undefined.Instance))
                    {
                        return "undefined";
                    }
                    if (ReferenceEquals(v, Null.Instance))
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
                    throw new ArgumentException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BuildArguments(
            List<ArgumentListElement> expressionArguments, 
            JsValue[] targetArray,
            out bool cacheable)
        {
            cacheable = true;
            var count = expressionArguments.Count;

            for (var i = 0; i < count; i++)
            {
                var argument = (Expression) expressionArguments[i];
                targetArray[i] = _engine.GetValue(EvaluateExpression(argument), true);
                cacheable &= argument is Literal;
            }
        }
    }
}