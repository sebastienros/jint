using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>
    {
        public JintFunctionDeclarationStatement(Engine engine, FunctionDeclaration statement) : base(engine, statement)
        {
        }

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Normal, null, null, Location);
        }

        protected override Task<Completion> ExecuteInternalAsync() => Task.FromResult(ExecuteInternal());
    }
}