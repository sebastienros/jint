using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintLabeledStatement : JintStatement<LabeledStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            // TODO: Esprima added Statement.Label, maybe not necessary as this line is finding the
            // containing label and could keep a table per program with all the labels
            // labeledStatement.Body.LabelSet = labeledStatement.Label;
            var result = await _body.ExecuteAsync();
            if (result.Type == CompletionType.Break && result.Identifier == _labelName)
            {
                var value = result.Value;
                return new Completion(CompletionType.Normal, value, null, Location);
            }

            return result;
        }
    }
}