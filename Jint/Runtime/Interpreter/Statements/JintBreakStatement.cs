using Esprima.Ast;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
    /// </summary>
    internal sealed class JintBreakStatement : JintStatement<BreakStatement>
    {
        private readonly string _label;

        public JintBreakStatement(Engine engine, BreakStatement statement) : base(engine, statement)
        {
            _label = statement.Label?.Name;
        }

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Break, null, _label, Location);
        }

        protected override Task<Completion> ExecuteInternalAsync() => Task.FromResult(ExecuteInternal());
    }
}