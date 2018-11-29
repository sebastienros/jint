using Esprima;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintBlockStatement : JintStatement
    {
        private readonly JintStatementList _statementList;

        public JintBlockStatement(Engine engine, JintStatementList statementList)
        {
            _statementList = statementList;
        }

        public override Completion Execute()
        {
            return _statementList.Execute();
        }

        public override Location Location => null;
    }
}