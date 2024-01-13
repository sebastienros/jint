using Esprima.Ast;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement<BlockStatement>
    {
        private JintStatementList? _statementList;
        private JintStatement? _singleStatement;
        private List<Declaration>? _lexicalDeclarations;

        public JintBlockStatement(BlockStatement blockStatement) : base(blockStatement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _lexicalDeclarations = HoistingScope.GetLexicalDeclarations(_statement);
            if (_statement.Body.Count == 1)
            {
                _singleStatement = Build(_statement.Body[0]);
            }
            else
            {
                _statementList = new JintStatementList(_statement, _statement.Body);
            }
        }

        /// <summary>
        /// Optimized for direct access without virtual dispatch.
        /// </summary>
        public Completion ExecuteBlock(EvaluationContext context)
        {
            if (_statementList is null && _singleStatement is null)
            {
                Initialize(context);
            }

            Environment? oldEnv = null;
            var engine = context.Engine;
            if (_lexicalDeclarations != null)
            {
                oldEnv = engine.ExecutionContext.LexicalEnvironment;
                var blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                JintStatementList.BlockDeclarationInstantiation(blockEnv, _lexicalDeclarations);
                engine.UpdateLexicalEnvironment(blockEnv);
            }

            Completion blockValue;
            if (_singleStatement is not null)
            {
                blockValue = ExecuteSingle(context);
            }
            else
            {
                blockValue = _statementList!.Execute(context);
            }

            if (oldEnv is not null)
            {
                engine.UpdateLexicalEnvironment(oldEnv);
            }

            return blockValue;
        }

        private Completion ExecuteSingle(EvaluationContext context)
        {
            Completion blockValue;
            try
            {
                blockValue = _singleStatement!.Execute(context);
                if (context.Engine._error is not null)
                {
                    blockValue = JintStatementList.HandleError(context.Engine, _singleStatement);
                }
            }
            catch (Exception ex)
            {
                if (ex is JintException)
                {
                    blockValue = JintStatementList.HandleException(context, ex, _singleStatement);
                }
                else
                {
                    throw;
                }
            }

            return blockValue;
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            return ExecuteBlock(context);
        }
    }
}
