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

        public JintIdentifierExpression(Identifier expression) : base(expression)
        {
            _expressionName = new EnvironmentRecord.BindingName(expression.Name);
            if (expression.Name == "undefined")
            {
                _calculatedValue = JsValue.Undefined;
            }
        }

        public bool HasEvalOrArguments
            => _expressionName.Key == KnownKeys.Eval || _expressionName.Key == KnownKeys.Arguments;

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var env = engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            var identifierEnvironment = JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(engine, env, _expressionName, strict, out var temp, out _)
                ? temp
                : JsValue.Undefined;

            return NormalCompletion(engine._referencePool.Rent(identifierEnvironment, _expressionName.StringValue, strict, thisValue: null));
        }

        public override Completion GetValue(EvaluationContext context)
        {
            // need to notify correct node when taking shortcut
            context.LastSyntaxNode = _expression;

            if (_calculatedValue is not null)
            {
                return Completion.Normal(_calculatedValue, _expression.Location);
            }

            var strict = StrictModeScope.IsStrictModeCode;
            var engine = context.Engine;
            var env = engine.ExecutionContext.LexicalEnvironment;

            if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                engine,
                env,
                _expressionName,
                strict,
                out _,
                out var value))
            {
                if (value is null)
                {
                    ExceptionHelper.ThrowReferenceError(engine.Realm, _expressionName.Key.Name + " has not been initialized");
                }
            }
            else
            {
                var reference = engine._referencePool.Rent(JsValue.Undefined, _expressionName.StringValue, strict, thisValue: null);
                value = engine.GetValue(reference, true);
            }

            // make sure arguments access freezes state
            if (value is ArgumentsInstance argumentsInstance)
            {
                argumentsInstance.Materialize();
            }

            return Completion.Normal(value, _expression.Location);
        }
    }
}