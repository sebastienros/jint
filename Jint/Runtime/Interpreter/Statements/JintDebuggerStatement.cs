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
            // Handling for DebuggerStatementHandling.Jint has been moved to DebugHandler,
            // because it needs to suppress handling of steps and (non-source-triggered) breakpoints.
            if (_engine.Options._DebuggerStatementHandling == DebuggerStatementHandling.Clr)
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