using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement<Statement>
    {
        private readonly JintStatementList _statementList;

        public JintBlockStatement(Engine engine, JintStatementList statementList) : base(engine, null)
        {
            _statementList = statementList;
        }

        // http://www.ecma-international.org/ecma-262/6.0/#sec-blockdeclarationinstantiation
        protected override Completion ExecuteInternal()
        {
            var env = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);

            _statementList.BlockDeclarationInstantiation(env._record);

            try
            {
                _engine.EnterExecutionContext(env, _engine.ExecutionContext.VariableEnvironment, _engine.ExecutionContext.ThisBinding);
                return _statementList.Execute();
            }
            finally
            {
                _engine.LeaveExecutionContext();
            }   
        }
    }
}