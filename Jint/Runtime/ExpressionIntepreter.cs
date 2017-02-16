using System;
using System.Linq;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Parser.Ast;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    public class ExpressionInterpreter
    {
        private readonly Engine _engine;

        public ExpressionInterpreter(Engine engine)
        {
            _engine = engine;
        }

        private object EvaluateExpression(Expression expression)
        {
            return _engine.EvaluateExpression(expression);
        }

        public JsValue EvaluateConditionalExpression(ConditionalExpression conditionalExpression)
        {
            var lref = _engine.EvaluateExpression(conditionalExpression.Test);
            if (TypeConverter.ToBoolean(_engine.GetValue(lref)))
            {
                var trueRef = _engine.EvaluateExpression(conditionalExpression.Consequent);
                return _engine.GetValue(trueRef);
            }
            else
            {
                var falseRef = _engine.EvaluateExpression(conditionalExpression.Alternate);
                return _engine.GetValue(falseRef);
            }
        }

        public JsValue EvaluateAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            var lref = EvaluateExpression(assignmentExpression.Left) as Reference;
            JsValue rval = _engine.GetValue(EvaluateExpression(assignmentExpression.Right));

            if (lref == null)
            {
                throw new JavaScriptException(_engine.ReferenceError);
            }

            if (assignmentExpression.Operator == AssignmentOperator.Assign) // "="
            {

                if(lref.IsStrict() && lref.GetBase().TryCast<EnvironmentRecord>() != null && (lref.GetReferencedName() == "eval" || lref.GetReferencedName() == "arguments"))
                {
                    throw new JavaScriptException(_engine.SyntaxError);
                }

                _engine.PutValue(lref, rval);
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
                        lval = TypeConverter.ToString(lprim) + TypeConverter.ToString(rprim);
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
                    if (lval == Undefined.Instance || rval == Undefined.Instance)
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
                    if (lval == Undefined.Instance || rval == Undefined.Instance)
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

            return lval;
        }

        private JsValue Divide(JsValue lval, JsValue rval)
        {
            if (lval == Undefined.Instance || rval == Undefined.Instance)
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
            var leftExpression = EvaluateExpression(expression.Left);
            JsValue left = _engine.GetValue(leftExpression);

            var rightExpression = EvaluateExpression(expression.Right);
            JsValue right = _engine.GetValue(rightExpression);

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
                    if (left == Undefined.Instance || right == Undefined.Instance)
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
                    if (left == Undefined.Instance || right == Undefined.Instance)
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
                    if (value == Undefined.Instance)
                    {
                        value = false;
                    }
                    break;

                case BinaryOperator.GreaterOrEqual:
                    value = Compare(left, right);
                    if (value == Undefined.Instance || value.AsBoolean())
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
                    if (value == Undefined.Instance)
                    {
                        value = false;
                    }
                    break;

                case BinaryOperator.LessOrEqual:
                    value = Compare(right, left, false);
                    if (value == Undefined.Instance || value.AsBoolean())
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

                    if (f == null)
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

        public JsValue EvaluateLogicalExpression(LogicalExpression logicalExpression)
        {
            var left = _engine.GetValue(EvaluateExpression(logicalExpression.Left));

            switch (logicalExpression.Operator)
            {

                case LogicalOperator.LogicalAnd:
                    if (!TypeConverter.ToBoolean(left))
                    {
                        return left;
                    }

                    return _engine.GetValue(EvaluateExpression(logicalExpression.Right));

                case LogicalOperator.LogicalOr:
                    if (TypeConverter.ToBoolean(left))
                    {
                        return left;
                    }

                    return _engine.GetValue(EvaluateExpression(logicalExpression.Right));

                default:
                    throw new NotImplementedException();
            }
        }

        public static bool Equal(JsValue x, JsValue y)
        {
            var typex = x.Type;
            var typey = y.Type;

            if (typex == typey)
            {
				return StrictlyEqual(x, y);
            }

            if (x == Null.Instance && y == Undefined.Instance)
            {
                return true;
            }

            if (x == Undefined.Instance && y == Null.Instance)
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

            if (typea == Types.None)
            {
                return true;
            }

            if (typea == Types.Number)
            {
                var nx = x.AsNumber();
                var ny = y.AsNumber();

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
                return x.AsString() == y.AsString();
            }

            if (typea == Types.Boolean)
            {
                return x.AsBoolean() == y.AsBoolean();
            }

			if (typea == Types.Object)
			{
				var xw = x.AsObject() as IObjectWrapper;

				if (xw != null)
				{
					var yw = y.AsObject() as IObjectWrapper;
					return Object.Equals(xw.Target, yw.Target);
				}
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

        public JsValue EvaluateLiteral(Literal literal)
        {
            if(literal.Cached)
            {
                return literal.CachedValue;
            }

            if (literal.Type == SyntaxNodes.RegularExpressionLiteral)
            {
				var regexp = _engine.RegExp.Construct(literal.Raw);

				if (regexp.Global)
				{
					// A Global regexp literal can't be cached or its state would evolve
					return regexp;
				}

				literal.CachedValue = regexp;
            }
            else
            {
                literal.CachedValue = JsValue.FromObject(_engine, literal.Value);
            }

            literal.Cached = true;
            return literal.CachedValue;
        }

        public JsValue EvaluateObjectExpression(ObjectExpression objectExpression)
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-11.1.5

            var obj = _engine.Object.Construct(Arguments.Empty);
            foreach (var property in objectExpression.Properties)
            {
                var propName = property.Key.GetKey();
                var previous = obj.GetOwnProperty(propName);
                PropertyDescriptor propDesc;

                switch (property.Kind)
                {
                    case PropertyKind.Data:
                        var exprValue = _engine.EvaluateExpression(property.Value);
                        var propValue = _engine.GetValue(exprValue);
                        propDesc = new PropertyDescriptor(propValue, true, true, true);
                        break;

                    case PropertyKind.Get:
                        var getter = property.Value as FunctionExpression;

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

                        propDesc = new PropertyDescriptor(get: get, set: null, enumerable: true, configurable:true);
                        break;

                    case PropertyKind.Set:
                        var setter = property.Value as FunctionExpression;

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
                        propDesc = new PropertyDescriptor(get:null, set: set, enumerable: true, configurable: true);
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
                        if (propDesc.Set != null && previous.Set != null)
                        {
                            throw new JavaScriptException(_engine.SyntaxError);
                        }

                        if (propDesc.Get != null && previous.Get != null)
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
            Expression expression = memberExpression.Property;


            if (!memberExpression.Computed) // index accessor ?
            {
                expression = new Literal { Type = SyntaxNodes.Literal, Value = memberExpression.Property.As<Identifier>().Name };
            }

            var propertyNameReference = EvaluateExpression(expression);
            var propertyNameValue = _engine.GetValue(propertyNameReference);
            TypeConverter.CheckObjectCoercible(_engine, baseValue);
            var propertyNameString = TypeConverter.ToString(propertyNameValue);

            return new Reference(baseValue, propertyNameString, StrictModeScope.IsStrictModeCode);
        }

        public JsValue EvaluateFunctionExpression(FunctionExpression functionExpression)
        {
            var funcEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            var envRec = (DeclarativeEnvironmentRecord)funcEnv.Record;

            if (functionExpression.Id != null && !String.IsNullOrEmpty(functionExpression.Id.Name))
            {
                envRec.CreateMutableBinding(functionExpression.Id.Name);
            }

            var closure = new ScriptFunctionInstance(
                _engine,
                functionExpression,
                funcEnv,
                functionExpression.Strict
                );

            if (functionExpression.Id != null && !String.IsNullOrEmpty(functionExpression.Id.Name))
            {
                envRec.InitializeImmutableBinding(functionExpression.Id.Name, closure);
            }

            return closure;
        }

        public JsValue EvaluateCallExpression(CallExpression callExpression)
        {
            var callee = EvaluateExpression(callExpression.Callee);

            if (_engine.Options._IsDebugMode)
            {
                _engine.DebugHandler.AddToDebugCallStack(callExpression);
            }

            JsValue thisObject;

            // todo: implement as in http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.4


            JsValue[] arguments;

            if (callExpression.Cached)
            {
                arguments = callExpression.CachedArguments;
            }
            else
            {
                arguments = callExpression.Arguments.Select(EvaluateExpression).Select(_engine.GetValue).ToArray();

                if (callExpression.CanBeCached)
                {
                    // The arguments array can be cached if they are all literals
                    if (callExpression.Arguments.All(x => x is Literal))
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

            if (_engine.Options._MaxRecursionDepth >= 0)
            {
                var stackItem = new CallStackElement(callExpression, func, r != null ? r.GetReferencedName() : "anonymous function");

                var recursionDepth = _engine.CallStack.Push(stackItem);

                if (recursionDepth > _engine.Options._MaxRecursionDepth)
                {
                    _engine.CallStack.Pop();
                    throw new RecursionDepthOverflowException(_engine.CallStack, stackItem.ToString());
                }
            }

            if (func == Undefined.Instance)
            {
                throw new JavaScriptException(_engine.TypeError, r == null ? "" : string.Format("Object has no method '{0}'", (callee as Reference).GetReferencedName()));
            }

            if (!func.IsObject())
            {
                throw new JavaScriptException(_engine.TypeError, r == null ? "" : string.Format("Property '{0}' of object is not a function", (callee as Reference).GetReferencedName()));
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
                return ((EvalFunctionInstance) callable).Call(thisObject, arguments, true);
            }

            var result = callable.Call(thisObject, arguments);

            if (_engine.Options._IsDebugMode)
            {
                _engine.DebugHandler.PopDebugCallStack();
            }

            if (_engine.Options._MaxRecursionDepth >= 0)
            {
                _engine.CallStack.Pop();
            }

            return result;
        }

        public JsValue EvaluateSequenceExpression(SequenceExpression sequenceExpression)
        {
            var result = Undefined.Instance;
            foreach (var expression in sequenceExpression.Expressions)
            {
                result = _engine.GetValue(_engine.EvaluateExpression(expression));
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
                        && (r.GetBase().TryCast<EnvironmentRecord>() != null)
                        && (Array.IndexOf(new[] { "eval", "arguments" }, r.GetReferencedName()) != -1))
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    var oldValue = TypeConverter.ToNumber(_engine.GetValue(value));
                    var newValue = oldValue + 1;
                    _engine.PutValue(r, newValue);

                    return updateExpression.Prefix ? newValue : oldValue;

                case UnaryOperator.Decrement:
                    r = value as Reference;
                    if (r != null
                        && r.IsStrict()
                        && (r.GetBase().TryCast<EnvironmentRecord>() != null)
                        && (Array.IndexOf(new[] { "eval", "arguments" }, r.GetReferencedName()) != -1))
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }

                    oldValue = TypeConverter.ToNumber(_engine.GetValue(value));
                    newValue = oldValue - 1;
                    _engine.PutValue(r, newValue);

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
            var arguments = newExpression.Arguments.Select(EvaluateExpression).Select(_engine.GetValue).ToArray();

            // todo: optimize by defining a common abstract class or interface
            var callee = _engine.GetValue(EvaluateExpression(newExpression.Callee)).TryCast<IConstructor>();

            if (callee == null)
            {
                throw new JavaScriptException(_engine.TypeError, "The object can't be used as constructor.");
            }

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments);

            return instance;
        }

        public JsValue EvaluateArrayExpression(ArrayExpression arrayExpression)
        {
            var a = _engine.Array.Construct(new JsValue[] { arrayExpression.Elements.Count() });
            var n = 0;
            foreach (var expr in arrayExpression.Elements)
            {
                if (expr != null)
                {
                    var value = _engine.GetValue(EvaluateExpression(expr));
                    a.DefineOwnProperty(n.ToString(),
                        new PropertyDescriptor(value, true, true, true), false);
                }
                n++;
            }

            return a;
        }

        public JsValue EvaluateUnaryExpression(UnaryExpression unaryExpression)
        {
            var value = _engine.EvaluateExpression(unaryExpression.Argument);
            Reference r;

            switch (unaryExpression.Operator)
            {
                case UnaryOperator.Plus:
                    return TypeConverter.ToNumber(_engine.GetValue(value));

                case UnaryOperator.Minus:
                    var n = TypeConverter.ToNumber(_engine.GetValue(value));
                    return double.IsNaN(n) ? double.NaN : n*-1;

                case UnaryOperator.BitwiseNot:
                    return ~TypeConverter.ToInt32(_engine.GetValue(value));

                case UnaryOperator.LogicalNot:
                    return !TypeConverter.ToBoolean(_engine.GetValue(value));

                case UnaryOperator.Delete:
                    r = value as Reference;
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

                        return true;
                    }
                    if (r.IsPropertyReference())
                    {
                        var o = TypeConverter.ToObject(_engine, r.GetBase());
                        return o.Delete(r.GetReferencedName(), r.IsStrict());
                    }
                    if (r.IsStrict())
                    {
                        throw new JavaScriptException(_engine.SyntaxError);
                    }
                    var bindings = r.GetBase().TryCast<EnvironmentRecord>();
                    return bindings.DeleteBinding(r.GetReferencedName());

                case UnaryOperator.Void:
                    _engine.GetValue(value);
                    return Undefined.Instance;

                case UnaryOperator.TypeOf:
                    r = value as Reference;
                    if (r != null)
                    {
                        if (r.IsUnresolvableReference())
                        {
                            return "undefined";
                        }
                    }
                    var v = _engine.GetValue(value);
                    if (v == Undefined.Instance)
                    {
                        return "undefined";
                    }
                    if (v == Null.Instance)
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
    }
}
