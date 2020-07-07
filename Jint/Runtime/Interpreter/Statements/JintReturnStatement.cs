using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
    /// </summary>
    internal sealed class JintReturnStatement : JintStatement<ReturnStatement>
    {
        private readonly JintExpression _argument;

        public JintReturnStatement(Engine engine, ReturnStatement statement) : base(engine, statement)
        {
            _argument = _statement.Argument != null
                ? JintExpression.Build(engine, _statement.Argument)
                : null;
        }

        protected override Completion ExecuteInternal()
        {
            var jsValue = _argument?.GetValue() ?? Undefined.Instance;
            return new Completion(CompletionType.Return, jsValue, null, Location);
        }

        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var jsValue = _argument != null
                    ? await _argument?.GetValueAsync() ?? Undefined.Instance
                    : null;
            return new Completion(CompletionType.Return, jsValue, null, Location);
        }
    }
}