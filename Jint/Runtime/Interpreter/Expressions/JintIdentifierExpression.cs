using Esprima.Ast;
using Jint.Native;
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
            => _expressionName.StringValue._value == KnownKeys.Eval || _expressionName.Key == KnownKeys.Arguments;

        protected override object EvaluateInternal()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            var identifierEnvironment = LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(env, _expressionName, strict, out var temp, out _)
                ? temp
                : JsValue.Undefined;

            return _engine._referencePool.Rent(identifierEnvironment, _expressionName.StringValue, strict, thisValue: null);
        }

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;

            if (!(_calculatedValue is null))
            {
                return _calculatedValue;
            }

            var strict = StrictModeScope.IsStrictModeCode;
            var env = _engine.ExecutionContext.LexicalEnvironment;
            return LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                _expressionName,
                strict,
                out _,
                out var value)
                ? value ?? ExceptionHelper.ThrowReferenceError<JsValue>(_engine, _expressionName.Key.Name + " has not been initialized")
                : _engine.GetValue(_engine._referencePool.Rent(JsValue.Undefined, _expressionName.StringValue, strict, thisValue: null), true);
        }
    }
}