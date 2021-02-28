using Esprima.Ast;
using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
    /// </summary>
    internal sealed partial class JintReturnStatement : JintStatement<ReturnStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var jsValue = _argument != null
                    ? await _argument?.GetValueAsync() ?? Undefined.Instance
                    : null;
            return new Completion(CompletionType.Return, jsValue, null, Location);
        }
    }
}