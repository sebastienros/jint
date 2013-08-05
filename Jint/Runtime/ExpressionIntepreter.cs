using System;
using System.Linq;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
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

        private dynamic EvaluateExpression(Expression expression)
        {
            return _engine.EvaluateExpression(expression);
        }

        public dynamic EvaluateConditionalExpression(ConditionalExpression conditionalExpression)
        {
            var test = _engine.EvaluateExpression(conditionalExpression.Test);
            var evaluate = test ? conditionalExpression.Consequent : conditionalExpression.Alternate;
            
            return _engine.EvaluateExpression(evaluate);
        }

        public dynamic EvaluateAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            dynamic right = _engine.GetValue(EvaluateExpression(assignmentExpression.Right));

            Reference r = EvaluateExpression(assignmentExpression.Left);

            dynamic value = Undefined.Instance;

            switch (assignmentExpression.Operator)
            {
                case "=":
                    value = right;
                    break;

                case "+=":
                    value = _engine.GetValue(r) + right;
                    break;

                case "-=":
                    value = _engine.GetValue(r) - right;
                    break;

                case "*=":
                    value = _engine.GetValue(r) * right;
                    break;

                case "/=":
                    value = _engine.GetValue(r) / right;
                    break;
            }

            _engine.SetValue(r, value);

            return value;
        }

        private static dynamic add(dynamic left, dynamic right)
        {
            return left + right;
        }

        public dynamic EvaluateBinaryExpression(BinaryExpression expression)
        {
            dynamic left = _engine.GetValue(EvaluateExpression(expression.Left));
            dynamic right = _engine.GetValue(EvaluateExpression(expression.Right));
            dynamic value;

            switch (expression.Operator)
            {
                case "+":
                    value = left + right;
                    break;
                case "-":
                     value = left - right;
                    break;
                case "*":
                    value = left * right;
                    break;
                case "/":
                    value = left / right;
                    break;
                case "==":
                    value = left.Equals(right);
                    break;
                case "!=":
                    value = !left.Equals(right);
                    break;
                case ">":
                    value = left > right;
                    break;
                case ">=":
                    value = left >= right;
                    break;
                case "<":
                    value = left < right;
                    break;
                case "<=":
                    value = left <= right;
                    break;
                case "===":
                    value = left.GetType() == right.GetType() && left == right;
                    break;
                case "instanceof":
                    FunctionInstance f = right;
                    value = right.HasInstance(left);
                    break;
                default:
                    throw new NotImplementedException("");
            }

            return value;
        }

        public dynamic EvaluateIdentifier(Identifier identifier)
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

        public dynamic EvaluateLiteral(Literal literal)
        {
            return literal.Value;
        }

        public dynamic EvaluateObjectExpression(ObjectExpression objectExpression)
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

        public dynamic EvaluateMemberExpression(MemberExpression memberExpression)
        {
            var baseValue = _engine.GetValue(EvaluateExpression(memberExpression.Object));

            string propertyName =
                !memberExpression.Computed
                    ? memberExpression.Property.As<Identifier>().Name // o.foo 
                    : EvaluateExpression(memberExpression.Property).ToString(); // o['foo']

            return new Reference(baseValue, propertyName, false);
        }

        public dynamic EvaluateFunctionExpression(FunctionExpression functionExpression)
        {
            string identifier = functionExpression.Id != null ? functionExpression.Id.Name : null;
            return new ScriptFunctionInstance(
                functionExpression.Body, 
                identifier, 
                functionExpression.Parameters.ToArray(), 
                _engine.Function.Prototype,
                _engine.Object.Construct(new dynamic[0]),
                LexicalEnvironment.NewDeclarativeEnvironment(_engine.CurrentExecutionContext.LexicalEnvironment)
                );
        }

        public dynamic EvaluateCallExpression(CallExpression callExpression)
        {
            /// todo: read the spec as this is made up
            
            var arguments = callExpression.Arguments.Select(EvaluateExpression);
            var r = EvaluateExpression(callExpression.Callee);
            if (r is Reference)
            {
                // x.hasOwnProperty
                FunctionInstance callee = _engine.GetValue(r);
                return callee.Call(_engine, r.GetBase(), arguments.ToArray());
            }
            else
            {
                // assert(...)
                FunctionInstance callee = _engine.GetValue(r);
                return callee.Call(_engine, _engine.CurrentExecutionContext.ThisBinding, arguments.ToArray());
            }

        }

        public dynamic EvaluateSequenceExpression(SequenceExpression sequenceExpression)
        {
            foreach (var expression in sequenceExpression.Expressions)
            {
                _engine.EvaluateExpression(expression);
            }

            return Undefined.Instance;
        }

        public dynamic EvaluateUpdateExpression(UpdateExpression updateExpression)
        {
            Reference r = EvaluateExpression(updateExpression.Argument);

            var value = _engine.GetValue(r);
            var old = value;

            switch (updateExpression.Operator)
            {
                case "++" :
                    value += 1;
                    break;
                case "--":
                    value -= 1;
                    break;
                default:
                    throw new ArgumentException();
            }

            _engine.SetValue(r, value);

            return updateExpression.Prefix ? value : old;
        }

        public dynamic EvaluateThisExpression(ThisExpression thisExpression)
        {
            return _engine.CurrentExecutionContext.ThisBinding;
        }

        public dynamic EvaluateNewExpression(NewExpression newExpression)
        {
            var arguments = newExpression.Arguments.Select(EvaluateExpression).ToArray();
            
            // todo: optimize by defining a common abstract class or interface
            dynamic callee = _engine.GetValue(EvaluateExpression(newExpression.Callee));

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments);

            // initializes the new instance by executing the Function
            callee.Call(_engine, instance, arguments.ToArray());

            return instance;
        }
    }
}
