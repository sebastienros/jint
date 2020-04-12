using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement<BlockStatement>
    {
        private JintStatementList _statementList;
        private HoistingScope _hoistingScope;

        public JintBlockStatement(Engine engine, BlockStatement blockStatement) : base(engine, blockStatement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _statementList = new JintStatementList(_engine, _statement, _statement.Body);
            _hoistingScope = HoistingScope.Hoist(_statement, HoistingScope.HoistingMode.Block);
        }

        // http://www.ecma-international.org/ecma-262/6.0/#sec-blockdeclarationinstantiation
        protected override Completion ExecuteInternal()
        {
            var env = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);
            JintStatementList.BlockDeclarationInstantiation(_engine, env._record, _hoistingScope._lexicalDeclarations);
            _engine.EnterExecutionContext(env, _engine.ExecutionContext.VariableEnvironment, _engine.ExecutionContext.ThisBinding);

            try
            {
                return _statementList.Execute();
            }
            finally
            {
                _engine.LeaveExecutionContext();
            }   
        }
    }
}