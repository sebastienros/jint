#nullable enable

using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintClassDeclarationStatement : JintStatement<ClassDeclaration>
    {
        protected override Task<Completion> ExecuteInternalAsync() => Task.FromResult(ExecuteInternal());
    }
}