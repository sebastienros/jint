using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintIdentifierExpression : JintExpression
    {
        private readonly Key _expressionName;
        private JsString _expressionNameJsValue; 
        private readonly JsValue _calculatedValue;

        public JintIdentifierExpression(Engine engine, Identifier expression) : base(engine, expression)
        {
            _expressionName = expression.Name;
            if (expression.Name == "undefined")
            {
                _calculatedValue = JsValue.Undefined;
            }
        }

        public string ExpressionName => _expressionName.Name;

        public bool HasEvalOrArguments
            => ExpressionName == CommonProperties.Eval || ExpressionName == CommonProperties.Arguments;

        protected override object EvaluateInternal()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            var identifierEnvironment = LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(env, _expressionName, strict, out var temp, out _)
                ? temp
                : JsValue.Undefined;

            var property = _expressionNameJsValue ??= new JsString(_expressionName.Name);
            return _engine._referencePool.Rent(identifierEnvironment, property, strict);
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
            var property = _expressionNameJsValue ??= new JsString(_expressionName);
            return TryGetIdentifierEnvironmentWithBindingValue(strict, _expressionName, out _, out var value)
                ? value ?? ExceptionHelper.ThrowReferenceError<JsValue>(_engine, _expressionName + " has not been initialized")
                : _engine.GetValue(_engine._referencePool.Rent(JsValue.Undefined, property, strict), true);
        }
    }
}