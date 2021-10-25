using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-try-statement
    /// </summary>
    internal sealed class JintTryStatement : JintStatement<TryStatement>
    {
        private JintStatement _block;
        private JintStatement _catch;
        private JintStatement _finalizer;

        public JintTryStatement(TryStatement statement) : base(statement)
        {

        }

        protected override void Initialize(EvaluationContext context)
        {
            _block = Build(_statement.Block);
            if (_statement.Finalizer != null)
            {
                _finalizer = Build(_statement.Finalizer);
            }
        }

        protected override bool SupportsResume => true;

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            int callStackSizeBeforeExecution = engine.CallStack.Count;

            var b = _block.Execute(context);

            if (b.Type == CompletionType.Throw)
            {
                // initialize lazily
                if (_statement.Handler is not null && _catch is null)
                {
                    _catch = Build(_statement.Handler.Body);
                }

                // execute catch
                if (_statement.Handler is not null)
                {
                    // Quick-patch for call stack not being unwinded when an exception is caught.
                    // Ideally, this should instead be solved by always popping the stack when returning
                    // from a call, regardless of whether it throws (i.e. CallStack.Pop() in finally clause
                    // in Engine.Call/Engine.Construct - however, that method currently breaks stack traces
                    // in error messages.
                    while (callStackSizeBeforeExecution < engine.CallStack.Count)
                    {
                        engine.CallStack.Pop();
                    }

                    // https://tc39.es/ecma262/#sec-runtime-semantics-catchclauseevaluation

                    var thrownValue = b.Value;
                    var oldEnv = engine.ExecutionContext.LexicalEnvironment;
                    var catchEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv, catchEnvironment: true);

                    var boundNames = new List<string>();
                    _statement.Handler.Param.GetBoundNames(boundNames);

                    foreach (var argName in boundNames)
                    {
                        catchEnv.CreateMutableBinding(argName, false);
                    }

                    engine.UpdateLexicalEnvironment(catchEnv);

                    var catchParam = _statement.Handler?.Param;
                    catchParam.BindingInitialization(context, thrownValue, catchEnv);

                    b = _catch.Execute(context);

                    engine.UpdateLexicalEnvironment(oldEnv);
                }
            }

            if (_finalizer != null)
            {
                var f = _finalizer.Execute(context);
                if (f.Type == CompletionType.Normal)
                {
                    return b;
                }

                return f.UpdateEmpty(Undefined.Instance);
            }

            return b.UpdateEmpty(Undefined.Instance);
        }
    }
}