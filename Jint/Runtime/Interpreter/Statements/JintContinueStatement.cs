using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
    /// </summary>
    internal sealed class JintContinueStatement : JintStatement<ContinueStatement>
    {
        private readonly string _labelName;

        public JintContinueStatement(ContinueStatement statement) : base(statement)
        {
            _labelName = _statement.Label?.Name;
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            return new Completion(CompletionType.Continue, _labelName, Location);
        }
    }
}