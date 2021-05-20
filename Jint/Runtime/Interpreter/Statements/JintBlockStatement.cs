using System.Collections.Generic;
using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement<BlockStatement>
    {
        private JintStatementList _statementList;
        private List<Declaration> _lexicalDeclarations;

        public JintBlockStatement(Engine engine, BlockStatement blockStatement) : base(engine, blockStatement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _statementList = new JintStatementList(_engine, _statement);
            _lexicalDeclarations = HoistingScope.GetLexicalDeclarations(_statement);
        }

        // http://www.ecma-international.org/ecma-262/6.0/#sec-blockdeclarationinstantiation
        protected override Completion ExecuteInternal()
        {
            EnvironmentRecord oldEnv = null;
            if (_lexicalDeclarations != null)
            {
                oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                var blockEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
                JintStatementList.BlockDeclarationInstantiation(blockEnv, _lexicalDeclarations);
                _engine.UpdateLexicalEnvironment(blockEnv);
            }

            var blockValue = _statementList.Execute();

            if (oldEnv is not null)
            {
                _engine.UpdateLexicalEnvironment(oldEnv);
            }
            
            return blockValue;
        }
    }
}