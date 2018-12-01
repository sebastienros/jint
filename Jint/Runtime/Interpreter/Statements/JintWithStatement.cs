using Esprima.Ast;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
    /// </summary>
    internal sealed class JintWithStatement : JintStatement<WithStatement>
    {
        private readonly JintStatement _body;
        private readonly JintExpression _object;

        public JintWithStatement(Engine engine, WithStatement statement) : base(engine, statement)
        {
            _body = Build(engine, statement.Body);
            _object = JintExpression.Build(engine, _statement.Object);
        }

        protected override Completion ExecuteInternal()
        {
            var jsValue = _object.GetValue();
            var obj = TypeConverter.ToObject(_engine, jsValue);
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var newEnv = LexicalEnvironment.NewObjectEnvironment(_engine, obj, oldEnv, true);
            _engine.UpdateLexicalEnvironment(newEnv);

            Completion c;
            try
            {
                c = _body.Execute();
            }
            catch (JavaScriptException e)
            {
                c = new Completion(CompletionType.Throw, e.Error, null, _statement.Location);
            }
            finally
            {
                _engine.UpdateLexicalEnvironment(oldEnv);
            }

            return c;
        }
    }
}