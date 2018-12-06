using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
    /// </summary>
    internal sealed class JintThrowStatement : JintStatement<ThrowStatement>
    {
        private readonly JintExpression _argument;

        public JintThrowStatement(Engine engine, ThrowStatement statement) : base(engine, statement)
        {
            _argument = JintExpression.Build(engine, _statement.Argument);
        }

        protected override Completion ExecuteInternal()
        {
            var jsValue = _argument.GetValue();
            return new Completion(CompletionType.Throw, jsValue, null, _statement.Location);
        }
    }
}