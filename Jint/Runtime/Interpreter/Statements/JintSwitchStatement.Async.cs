using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
    /// </summary>
    internal sealed partial class JintSwitchStatement : JintStatement<SwitchStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var jsValue = await _discriminant.GetValueAsync();
            var r = await _switchBlock.ExecuteAsync(jsValue);
            if (r.Type == CompletionType.Break && r.Identifier == _statement.LabelSet?.Name)
            {
                return new Completion(CompletionType.Normal, r.Value, null, Location);
            }

            return r;
        }
    }
}