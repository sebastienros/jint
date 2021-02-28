using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintExpressionStatement : JintStatement<ExpressionStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync() {
            var value = await _expression.GetValueAsync();
            return new Completion(CompletionType.Normal, value, null, Location);
        }
    }
}