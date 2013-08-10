using System;
using System.Linq;
using Jint.Native;
using Jint.Native.Function;
using Jint.Parser.Ast;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
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

        public object EvaluateConditionalExpression(ConditionalExpression conditionalExpression)
        {
            var test = _engine.EvaluateExpression(conditionalExpression.Test);
            var evaluate = TypeConverter.ToBoolean(test) ? conditionalExpression.Consequent : conditionalExpression.Alternate;
            
            return _engine.EvaluateExpression(evaluate);
        }

        public object EvaluateAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            object right = _engine.GetValue(EvaluateExpression(assignmentExpression.Right));

            var r = (Reference)EvaluateExpression(assignmentExpression.Left);

            object value = _engine.GetValue(r);
            var type = TypeConverter.GetType(value);

            switch (assignmentExpression.Operator)
            {
                case "=":
                    value = right;
                    break;

                case "+=":
                    switch (type)
                    {
                        case TypeCode.String:
                            value = TypeConverter.ToString(_engine.GetValue(r)) + right;
                            break;

                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(_engine.GetValue(r)) + TypeConverter.ToNumber(right);
                            break;
                    }
                    
                    break;

                case "-=":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(_engine.GetValue(r)) + TypeConverter.ToNumber(right);
                            break;
                    }
                    break;

                case "*=":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(_engine.GetValue(r)) *TypeConverter.ToNumber(right);
                            break;
                    }
                    break;

                case "/=":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(_engine.GetValue(r)) / TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
            }

            _engine.SetValue(r, value);

            return value;
        }

        public object EvaluateBinaryExpression(BinaryExpression expression)
        {
            object left = _engine.GetValue(EvaluateExpression(expression.Left));
            object right = _engine.GetValue(EvaluateExpression(expression.Right));
            object value = Undefined.Instance;
            var type = TypeConverter.GetType(left);

            switch (expression.Operator)
            {
                case "+":
                    switch (type)
                    {
                        case TypeCode.String:
                            value = TypeConverter.ToString(left) + right;
                            break;

                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) + TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "-":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "*":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "/":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) / TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "==":
                    value = left.Equals(right);
                    break;
                case "!=":
                    value = !left.Equals(right);
                    break;
                case ">":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) > TypeConverter.ToNumber(right);
                            break;
                    }
                    break;

                case ">=":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) >= TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "<":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) < TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "<=":
                    switch (type)
                    {
                        case TypeCode.Double:
                            value = TypeConverter.ToNumber(left) <= TypeConverter.ToNumber(right);
                            break;
                    }
                    break;
                case "===":
                    value = left.GetType() == right.GetType() && left == right;
                    break;
                case "instanceof":
                    var f = (FunctionInstance)right;
                    value = f.HasInstance(left);
                    break;
                default:
                    throw new NotImplementedException("");
            }

            return value;
        }

        public object EvaluateIdentifier(Identifier identifier)
        {
            switch (identifier.Name)
            {
                case "undefined":
                    return Undefined.Instance;
                case "null":
                    return Null.Instance;
            }

            return _engine.CurrentExecutionContext.LexicalEnvironment.GetIdentifierReference(identifier.Name, false);
        }

        public object EvaluateLiteral(Literal literal)
        {
            return literal.Value;
        }

        public object EvaluateObjectExpression(ObjectExpression objectExpression)
        {
            var value = _engine.Object.Construct(new dynamic[0]);

            foreach (var property in objectExpression.Properties)
            {
                switch (property.Kind)
                {
                    case PropertyKind.Data:
                        value.DefineOwnProperty(property.Key.GetKey(), new DataDescriptor(property.Value), false);
                        break;
                    case PropertyKind.Get:
                        throw new NotImplementedException();
                        break;
                    case PropertyKind.Set:
                        throw new NotImplementedException();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return value;
        }

        public object EvaluateMemberExpression(MemberExpression memberExpression)
        {
            var baseValue = _engine.GetValue(EvaluateExpression(memberExpression.Object));

            string propertyName =
                !memberExpression.Computed
                    ? memberExpression.Property.As<Identifier>().Name // o.foo 
                    : EvaluateExpression(memberExpression.Property).ToString(); // o['foo']

            return new Reference(baseValue, propertyName, false);
        }

        public object EvaluateFunctionExpression(FunctionExpression functionExpression)
        {
            string identifier = functionExpression.Id != null ? functionExpression.Id.Name : null;
            return new ScriptFunctionInstance(
                _engine,
                functionExpression.Body, 
                identifier, 
                functionExpression.Parameters.ToArray(), 
                _engine.Function.Prototype,
                _engine.Object.Construct(new dynamic[0]),
                LexicalEnvironment.NewDeclarativeEnvironment(_engine.CurrentExecutionContext.LexicalEnvironment)
                );
        }

        public object EvaluateCallExpression(CallExpression callExpression)
        {
            /// todo: read the spec as this is made up
            
            var arguments = callExpression.Arguments.Select(EvaluateExpression);
            var result = EvaluateExpression(callExpression.Callee);
            var r = result as Reference;
            if (r != null)
            {
                // x.hasOwnProperty
                var callee = (FunctionInstance)_engine.GetValue(r);
                return callee.Call(r.GetBase(), arguments.ToArray());
            }
            else
            {
                // assert(...)
                var callee = (FunctionInstance)_engine.GetValue(result);
                return callee.Call(_engine.CurrentExecutionContext.ThisBinding, arguments.ToArray());
            }

        }

        public object EvaluateSequenceExpression(SequenceExpression sequenceExpression)
        {
            foreach (var expression in sequenceExpression.Expressions)
            {
                _engine.EvaluateExpression(expression);
            }

            return Undefined.Instance;
        }

        public object EvaluateUpdateExpression(UpdateExpression updateExpression)
        {
            var r = EvaluateExpression(updateExpression.Argument) as Reference;

            var value = _engine.GetValue(r);
            var old = value;

            switch (updateExpression.Operator)
            {
                case "++" :
                    value = TypeConverter.ToNumber(value) + 1;
                    break;
                case "--":
                    value = TypeConverter.ToNumber(value) - 1;
                    break;
                default:
                    throw new ArgumentException();
            }

            _engine.SetValue(r, value);

            return updateExpression.Prefix ? value : old;
        }

        public object EvaluateThisExpression(ThisExpression thisExpression)
        {
            return _engine.CurrentExecutionContext.ThisBinding;
        }

        public object EvaluateNewExpression(NewExpression newExpression)
        {
            var arguments = newExpression.Arguments.Select(EvaluateExpression).ToArray();
            
            // todo: optimize by defining a common abstract class or interface
            var callee = (IConstructor)_engine.GetValue(EvaluateExpression(newExpression.Callee));

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments);

            // initializes the new instance by executing the Function
            callee.Call(instance, arguments.ToArray());

            return instance;
        }

        public object EvaluateArrayExpression(ArrayExpression arrayExpression)
        {
            var arguments = arrayExpression.Elements.Select(EvaluateExpression).ToArray();

            // construct the new instance using the Function's constructor method
            var instance = _engine.Array.Construct(arguments);

            return instance;
        }
    }
}
