using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintProgram : JintStatement<Program>
    {
        private readonly JintStatementList _list;

        public JintProgram(Engine engine, Program statement) : base(engine, statement)
        {
            _list = new JintStatementList(_engine, _statement.Body);
        }

        public override Completion Execute()
        {
            return _list.Execute();
        }
    }
}