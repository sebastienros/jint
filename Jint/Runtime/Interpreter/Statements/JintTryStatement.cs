using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
    /// </summary>
    internal sealed class JintTryStatement : JintStatement<TryStatement>
    {
        private readonly JintStatement _block;
        private JintStatement _catch;
        private readonly JintStatement _finalizer;

        public JintTryStatement(Engine engine, TryStatement statement) : base(engine, statement)
        {
            _block = Build(engine, statement.Block);
            if (statement.Finalizer != null)
            {
                _finalizer = Build(engine, _statement.Finalizer);
            }
        }

        protected override Completion ExecuteInternal()
        {
            int callStackSizeBeforeExecution = _engine.CallStack.Count;

            var b = _block.Execute();
            if (b.Type == CompletionType.Throw)
            {
                // initialize lazily
                if (_statement.Handler is not null && _catch is null)
                {
                    _catch = Build(_engine, _statement.Handler.Body);
                }

                // execute catch
                if (_statement.Handler is not null)
                {
                    // Quick-patch for call stack not being unwinded when an exception is caught.
                    // Ideally, this should instead be solved by always popping the stack when returning
                    // from a call, regardless of whether it throws (i.e. CallStack.Pop() in finally clause
                    // in Engine.Call/Engine.Construct - however, that method currently breaks stack traces
                    // in error messages.
                    while (callStackSizeBeforeExecution < _engine.CallStack.Count)
                    {
                        _engine.CallStack.Pop();
                    }

                    // https://tc39.es/ecma262/#sec-runtime-semantics-catchclauseevaluation

                    var thrownValue = b.Value;
                    var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var catchEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, oldEnv, catchEnvironment: true);

                    var boundNames = new List<string>();
                    _statement.Handler.Param.GetBoundNames(boundNames);

                    foreach (var argName in boundNames)
                    {
                        catchEnv.CreateMutableBinding(argName, false);
                    }

                    _engine.UpdateLexicalEnvironment(catchEnv);

                    var catchParam = _statement.Handler?.Param;
                    catchParam.BindingInitialization(_engine, thrownValue, catchEnv);

                    b = _catch.Execute();

                    _engine.UpdateLexicalEnvironment(oldEnv);
                }
            }

            if (_finalizer != null)
            {
                var f = _finalizer.Execute();
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