using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintIfStatement : JintStatement<IfStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            Completion result;
            if (TypeConverter.ToBoolean(await _test.GetValueAsync()))
            {
                result = await _statementConsequent.ExecuteAsync();
            }
            else if (_alternate != null)
            {
                result = await _alternate.ExecuteAsync();
            }
            else
            {
                return new Completion(CompletionType.Normal, null, null, Location);
            }

            return result;
        }
    }
}