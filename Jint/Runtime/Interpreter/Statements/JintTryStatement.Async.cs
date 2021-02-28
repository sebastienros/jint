using Esprima.Ast;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
    /// </summary>
    internal sealed partial class JintTryStatement : JintStatement<TryStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var b = await _block.ExecuteAsync();
            if (b.Type == CompletionType.Throw)
            {
                // execute catch
                if (_catch != null)
                {
                    var c = b.Value;
                    var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                    var catchEnvRecord = (DeclarativeEnvironmentRecord)catchEnv._record;
                    catchEnvRecord.CreateMutableBindingAndInitialize(_catchParamName, canBeDeleted: false, c);

                    _engine.UpdateLexicalEnvironment(catchEnv);
                    b = await _catch.ExecuteAsync();
                    _engine.UpdateLexicalEnvironment(oldEnv);
                }
            }

            if (_finalizer != null)
            {
                var f = await _finalizer.ExecuteAsync();
                if (f.Type == CompletionType.Normal)
                {
                    return b;
                }

                return f;
            }

            return b;
        }
    }
}