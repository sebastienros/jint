using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public sealed class ArrowFunctionInstance : FunctionInstance
    {
        private readonly JsValue _thisObj;
        private readonly ArrowFunctionExpression _arrowFunctionExpression;
        private readonly HoistingScope _hoistingScope;

        public ArrowFunctionInstance(Engine engine, JsValue thisObj, 
            LexicalEnvironment scope, ArrowFunctionExpression arrowFunctionExpression, bool strict) 
            : base(engine, arrowFunctionExpression.Params.Select(x=>x.As<Identifier>().Name).ToArray(),
                  scope, strict)
        {
            _hoistingScope = new HoistingScope();// TODO: How should we get the real value here?
            _thisObj = thisObj;
            _arrowFunctionExpression = arrowFunctionExpression;

            Extensible = true;
            Prototype = engine.Function.PrototypeObject;

            DefineOwnProperty("length", new PropertyDescriptor(new JsValue(FormalParameters.Length), false, false, false), false);

            var proto = engine.Object.Construct(Arguments.Empty);
            proto.DefineOwnProperty("constructor", new PropertyDescriptor(this, true, false, true), false);
            DefineOwnProperty("prototype", new PropertyDescriptor(proto, true, false, false), false);

            if (strict)
            {
                var thrower = engine.Function.ThrowTypeError;
                DefineOwnProperty("caller", new PropertyDescriptor(thrower, thrower, false, false), false);
                DefineOwnProperty("arguments", new PropertyDescriptor(thrower, thrower, false, false), false);
            }
        }

        public override JsValue Call(JsValue passThisObjIsIgnored, JsValue[] arguments)
        {
            Engine.DeclarationBindingInstantiation(
                DeclarationBindingType.FunctionCode,
                _hoistingScope.FunctionDeclarations,
                _hoistingScope.VariableDeclarations,
                this,
                arguments);

            Engine.EnterExecutionContext(Scope, Scope, _thisObj);
            try
            {
                if (_arrowFunctionExpression.Expression)
                {
                    var value = Engine.EvaluateExpression(_arrowFunctionExpression.Body.As<Expression>());
                    return Engine.GetValue(value);
                }


                var result = Engine.ExecuteStatement(_arrowFunctionExpression.Body.As<Statement>());

                if (result.Type == Completion.Throw)
                {
                    JavaScriptException ex = new JavaScriptException(result.GetValueOrDefault())
                        .SetCallstack(Engine, result.Location);
                    throw ex;
                }
                return result.Value;
            }
            finally
            {
                Engine.LeaveExecutionContext();
            }
        }
    }
}