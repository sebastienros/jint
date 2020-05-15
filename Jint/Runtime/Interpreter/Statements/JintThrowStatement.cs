using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
    /// </summary>
    internal sealed class JintThrowStatement : JintStatement<ThrowStatement>
    {
        private JintExpression _argument;

        public JintThrowStatement(Engine engine, ThrowStatement statement) : base(engine, statement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _argument = JintExpression.Build(_engine, _statement.Argument);
        }

        protected override Completion ExecuteInternal()
        {
            var jsValue = _argument.GetValue();
            return new Completion(CompletionType.Throw, jsValue, null, _statement.Location);
        }

        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var jsValue = await _argument.GetValueAsync();
            return new Completion(CompletionType.Throw, jsValue, null, _statement.Location);
        }
    }
}