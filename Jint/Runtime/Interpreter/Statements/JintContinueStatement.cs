using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
    /// </summary>
    internal sealed class JintContinueStatement : JintStatement<ContinueStatement>
    {
        private readonly string _labelName;

        public JintContinueStatement(Engine engine, ContinueStatement statement) : base(engine, statement)
        {
            _labelName = _statement.Label?.Name;
        }

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Continue, null, _labelName, Location);
        }
    }
}