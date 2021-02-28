using System.Threading.Tasks;
using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed partial class JintBlockStatement : JintStatement<BlockStatement>
    {
        protected async override Task<Completion> ExecuteInternalAsync()
        {
            LexicalEnvironment oldEnv = null;
            if (_lexicalDeclarations != null)
            {
                oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                var blockEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
                JintStatementList.BlockDeclarationInstantiation(blockEnv, _lexicalDeclarations);
                _engine.UpdateLexicalEnvironment(blockEnv);
            }

            var blockValue = await _statementList.ExecuteAsync();

            if (oldEnv != null)
            {
                _engine.UpdateLexicalEnvironment(oldEnv);
            }

            return blockValue;
        }
    }
}