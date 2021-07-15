using Esprima.Ast;
using Jint.Runtime.Debugger;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintDebuggerStatement : JintStatement<DebuggerStatement>
    {
        public JintDebuggerStatement(Engine engine, DebuggerStatement statement) : base(engine, statement)
        {
        }

        protected override Completion ExecuteInternal()
        {
            switch (_engine.Options.Debugger.StatementHandling)
            {
                case DebuggerStatementHandling.Clr:
                    if (!System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Launch();
                    }

                    System.Diagnostics.Debugger.Break();
                    break;
                case DebuggerStatementHandling.Script:
                    _engine.DebugHandler?.OnBreak(_statement);
                    break;
                case DebuggerStatementHandling.Ignore:
                    break;
            }

            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}