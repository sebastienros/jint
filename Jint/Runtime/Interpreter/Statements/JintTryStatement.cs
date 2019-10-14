using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
    /// </summary>
    internal sealed class JintTryStatement : JintStatement<TryStatement>
    {
        private readonly JintStatement _block;
        private readonly JintStatement _catch;
        private readonly string _catchParamName;
        private readonly JintStatement _finalizer;

        public JintTryStatement(Engine engine, TryStatement statement) : base(engine, statement)
        {
            _block = Build(engine, statement.Block);
            if (_statement.Handler != null)
            {
                _catch = Build(engine, _statement.Handler.Body);
                _catchParamName = ((Esprima.Ast.Identifier) _statement.Handler.Param).Name;
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
                    var oldEnv = _engine.ExecutionContext;
                    var lex = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv.LexicalEnvironment);
                    lex._record.CreateMutableBinding(_catchParamName, c);
                    b = _engine.WithExecutionContext(lex, oldEnv.VariableEnvironment, oldEnv.ThisBinding, () =>
                    {
                        return _catch.Execute();
                    }).GetAwaiter().GetResult();

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
    }
}