using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintIdentifierExpression : JintExpression
    {
        private readonly Key _expressionName;
        private readonly JsValue _calculatedValue;

        public JintIdentifierExpression(Engine engine, Esprima.Ast.Identifier expression) : base(engine, expression)
        {
            _expressionName = expression.Name;
            if (expression.Name == "undefined")
            {
                _calculatedValue = JsValue.Undefined;
            }
        }

        public ref readonly Key ExpressionName => ref _expressionName;

        protected override object EvaluateInternal()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            return LexicalEnvironment.GetIdentifierReference(env, _expressionName, strict);
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
            return TryGetIdentifierEnvironmentWithBindingValue(strict, _expressionName, out _, out var value)
                ? value
                : _engine.GetValue(new Reference(JsValue.Undefined, _expressionName, strict), true);
        }
    }
}