using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintIdentifierExpression : JintExpression
    {
        private readonly string _expressionName;

        public JintIdentifierExpression(Engine engine, Identifier expression) : base(engine, expression)
        {
            _expressionName = expression.Name;
        }

        protected override object EvaluateInternal() => GetIdentifierReference();

        public override JsValue GetValue()
        {
            // need to notify correct node when taking shortcut
            _engine._lastSyntaxNode = _expression;
            var value = GetIdentifierReference();
            return _engine.GetValue(value, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Reference GetIdentifierReference()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            return LexicalEnvironment.GetIdentifierReference(env, _expressionName, strict);
        }
    }
}