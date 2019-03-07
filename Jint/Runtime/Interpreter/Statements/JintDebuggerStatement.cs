using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintDebuggerStatement : JintStatement<DebuggerStatement>
    {
        public JintDebuggerStatement(Engine engine, DebuggerStatement statement) : base(engine, statement)
        {
        }

        protected override Completion ExecuteInternal()
        {
            if (_engine.Options._IsDebuggerStatementAllowed)
            {
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Launch();
                }

                System.Diagnostics.Debugger.Break();
            }

            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}