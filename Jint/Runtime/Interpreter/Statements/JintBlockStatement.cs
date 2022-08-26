using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement<BlockStatement>
    {
        private JintStatementList _statementList = null!;
        private List<Declaration>? _lexicalDeclarations;

        public JintBlockStatement(BlockStatement blockStatement) : base(blockStatement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _statementList = new JintStatementList(_statement, _statement.Body);
            _lexicalDeclarations = HoistingScope.GetLexicalDeclarations(_statement);
        }

        internal override bool SupportsResume => true;

        /// <summary>
        /// Optimized for direct access without virtual dispatch.
        /// </summary>
        public Completion ExecuteBlock(EvaluationContext context)
        {
            if (_statementList is null)
            {
                _statementList = new JintStatementList(_statement, _statement.Body);
                _lexicalDeclarations = HoistingScope.GetLexicalDeclarations(_statement);
            }

            EnvironmentRecord? oldEnv = null;
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

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            return ExecuteBlock(context);
        }
    }
}
