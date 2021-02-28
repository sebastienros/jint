using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
    /// </summary>
    internal sealed partial class JintThrowStatement : JintStatement<ThrowStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var jsValue = await _argument.GetValueAsync();
            return new Completion(CompletionType.Throw, jsValue, null, _statement.Location);
        }
    }
}