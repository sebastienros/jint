using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintScript : JintStatement<Script>
    {
        private readonly JintStatementList _list;

        public JintScript(Script script) : base(script)
        {
            _list = new JintStatementList(script);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            return _list.Execute(context);
        }
    }
}