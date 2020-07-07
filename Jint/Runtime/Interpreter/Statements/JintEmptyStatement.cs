using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintEmptyStatement : JintStatement<EmptyStatement>
    {
        public JintEmptyStatement(Engine engine, EmptyStatement statement) : base(engine, statement)
        {
        }

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Normal, null, null, Location);
        }

        protected override Task<Completion> ExecuteInternalAsync() => Task.FromResult(ExecuteInternal());
    }
}