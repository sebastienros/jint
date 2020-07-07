using Esprima.Ast;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
    /// </summary>
    internal sealed class JintTryStatement : JintStatement<TryStatement>
    {
        private readonly JintStatement _block;
        private readonly JintStatement _catch;
        private readonly Key _catchParamName;
        private readonly JintStatement _finalizer;

        public JintTryStatement(Engine engine, TryStatement statement) : base(engine, statement)
        {
            _block = Build(engine, statement.Block);
            if (_statement.Handler != null)
            {
                _catch = Build(engine, _statement.Handler.Body);
                _catchParamName = ((Identifier) _statement.Handler.Param).Name;
            }

            if (statement.Finalizer != null)
            {
                _finalizer = Build(engine, _statement.Finalizer);
            }
        }

        protected override Completion ExecuteInternal()
        {
            var b = _block.Execute();
            if (b.Type == CompletionType.Throw)
            {
                // execute catch
                if (_catch != null)
                {
                    var c = b.Value;
                    var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                    var catchEnvRecord = (DeclarativeEnvironmentRecord) catchEnv._record;
                    catchEnvRecord.CreateMutableBindingAndInitialize(_catchParamName, canBeDeleted: false, c);

                    _engine.UpdateLexicalEnvironment(catchEnv);
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

                return f;
            }

            return b;
        }

        protected async override Task<Completion> ExecuteInternalAsync()
        {
            var b = await _block.ExecuteAsync();
            if (b.Type == CompletionType.Throw)
            {
                // execute catch
                if (_catch != null)
                {
                    var c = b.Value;
                    var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                    var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                    var catchEnvRecord = (DeclarativeEnvironmentRecord)catchEnv._record;
                    catchEnvRecord.CreateMutableBindingAndInitialize(_catchParamName, canBeDeleted: false, c);

                    _engine.UpdateLexicalEnvironment(catchEnv);
                    b = await _catch.ExecuteAsync();
                    _engine.UpdateLexicalEnvironment(oldEnv);
                }
            }

            if (_finalizer != null)
            {
                var f = await _finalizer.ExecuteAsync();
                if (f.Type == CompletionType.Normal)
                {
                    return b;
                }

                return f;
            }

            return b;
        }
    }
}