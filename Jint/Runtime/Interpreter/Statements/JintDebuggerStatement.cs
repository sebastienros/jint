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
            switch (_engine.Options._DebuggerStatementHandling)
            {
                case DebuggerStatementHandling.Clr:
                    if (!System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Launch();
                    }

                    System.Diagnostics.Debugger.Break();
                    break;
                case DebuggerStatementHandling.Jint:
                    _engine.DebugHandler?.Break(this._statement);
                    break;
                default:
                case DebuggerStatementHandling.Ignore:
                    break;
            }

            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}