using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
    /// </summary>
    internal sealed class JintReturnStatement : JintStatement<ReturnStatement>
    {
        private JintExpression _argument;

        public JintReturnStatement(ReturnStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _argument = _statement.Argument != null
                ? JintExpression.Build(context.Engine, _statement.Argument)
                : null;
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var jsValue = _argument?.GetValue(context).Value ?? Undefined.Instance;
            return new Completion(CompletionType.Return, jsValue, null, Location);
        }
    }
}