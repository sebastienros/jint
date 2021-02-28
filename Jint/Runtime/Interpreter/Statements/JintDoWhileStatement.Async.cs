using Esprima.Ast;
using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.1
    /// </summary>
    internal sealed partial class JintDoWhileStatement : JintStatement<DoWhileStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            JsValue v = Undefined.Instance;
            bool iterating;

            do
            {
                var completion = await _body.ExecuteAsync();
                if (!ReferenceEquals(completion.Value, null))
                {
                    v = completion.Value;
                }

                if (completion.Type != CompletionType.Continue || completion.Identifier != _labelSetName)
                {
                    if (completion.Type == CompletionType.Break && (completion.Identifier == null || completion.Identifier == _labelSetName))
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }

                    if (completion.Type != CompletionType.Normal)
                    {
                        return completion;
                    }
                }

                iterating = TypeConverter.ToBoolean(await _test.GetValueAsync());
            } while (iterating);

            return new Completion(CompletionType.Normal, v, null, Location);
        }
    }
}