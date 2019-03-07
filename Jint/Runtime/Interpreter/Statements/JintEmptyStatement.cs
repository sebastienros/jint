using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintEmptyStatement : JintStatement<EmptyStatement>
    {
        public JintEmptyStatement(Engine engine, EmptyStatement statement) : base(engine, statement)
        {
        }

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}