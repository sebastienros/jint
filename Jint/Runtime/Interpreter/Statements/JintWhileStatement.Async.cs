using Esprima.Ast;
using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
    /// </summary>
    internal sealed partial class JintWhileStatement : JintStatement<WhileStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var v = Undefined.Instance;
            while (true)
            {
                var jsValue = await _test.GetValueAsync();
                if (!TypeConverter.ToBoolean(jsValue))
                {
                    return new Completion(CompletionType.Normal, v, null, Location);
                }

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
            }
        }
    }
}