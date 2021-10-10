using System.Collections.Generic;
using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement<BlockStatement>
    {
        private JintStatementList _statementList;
        private List<Declaration> _lexicalDeclarations;

        public JintBlockStatement(BlockStatement blockStatement) : base(blockStatement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _statementList = new JintStatementList(_statement, _statement.Body);
            _lexicalDeclarations = HoistingScope.GetLexicalDeclarations(_statement);
        }

        protected override bool SupportsResume => true;

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            EnvironmentRecord oldEnv = null;
            var engine = context.Engine;
            if (_lexicalDeclarations != null)
            {
                oldEnv = engine.ExecutionContext.LexicalEnvironment;
                var blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                JintStatementList.BlockDeclarationInstantiation(engine, blockEnv, _lexicalDeclarations);
                engine.UpdateLexicalEnvironment(blockEnv);
            }

            var blockValue = _statementList.Execute(context);

            if (oldEnv is not null)
            {
                engine.UpdateLexicalEnvironment(oldEnv);
            }

            return blockValue;
        }
    }
}