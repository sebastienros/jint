using Esprima.Ast;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintIdentifierExpression : JintExpression
    {
        internal readonly EnvironmentRecord.BindingName _expressionName;
        private readonly JsValue _calculatedValue;

        public JintIdentifierExpression(Engine engine, Identifier expression) : base(engine, expression)
        {
            _expressionName = new EnvironmentRecord.BindingName(expression.Name);
            if (expression.Name == "undefined")
            {
                _calculatedValue = JsValue.Undefined;
            }
        }

        public bool HasEvalOrArguments
            => _expressionName.Key == KnownKeys.Eval || _expressionName.Key == KnownKeys.Arguments;

        protected override object EvaluateInternal()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            var identifierEnvironment = JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(_engine, env, _expressionName, strict, out var temp, out _)
                ? temp
                : JsValue.Undefined;

            return _engine._referencePool.Rent(identifierEnvironment, _expressionName.StringValue, strict, thisValue: null);
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            if (_calculatedValue is not null)
            {
                return _calculatedValue;
            }

            var strict = StrictModeScope.IsStrictModeCode;
            var env = _engine.ExecutionContext.LexicalEnvironment;

            JsValue value;
            if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                _engine,
                env,
                _expressionName,
                strict,
                out _,
                out value))
            {
                if (value is null)
                {
                    ExceptionHelper.ThrowReferenceError(_engine.Realm, _expressionName.Key.Name + " has not been initialized");
                }
            }
            else
            {
                var reference = _engine._referencePool.Rent(JsValue.Undefined, _expressionName.StringValue, strict, thisValue: null);
                value = _engine.GetValue(reference, true);
            }

            // make sure arguments access freezes state
            if (value is ArgumentsInstance argumentsInstance)
            {
                argumentsInstance.Materialize();
            }

            return value;
        }
    }
}