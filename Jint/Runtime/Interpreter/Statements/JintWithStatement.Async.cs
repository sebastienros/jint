using Esprima.Ast;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
    /// </summary>
    internal sealed partial class JintWithStatement : JintStatement<WithStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var jsValue = await _object.GetValueAsync();
            var obj = TypeConverter.ToObject(_engine, jsValue);
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var newEnv = LexicalEnvironment.NewObjectEnvironment(_engine, obj, oldEnv, true);
            _engine.UpdateLexicalEnvironment(newEnv);

            Completion c;
            try
            {
                c = await _body.ExecuteAsync();
            }
            catch (JavaScriptException e)
            {
                c = new Completion(CompletionType.Throw, e.Error, null, _statement.Location);
            }
            finally
            {
                _engine.UpdateLexicalEnvironment(oldEnv);
            }

            return c;
        }
    }
}