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
        private JintStatement _body;
        private JintExpression _object;

        public JintWithStatement(WithStatement statement) : base(statement)
        {
        }

        protected override void Initialize(EvaluationContext context)
        {
            _body = Build(_statement.Body);
            _object = JintExpression.Build(context.Engine, _statement.Object);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var jsValue = _object.GetValue(context).Value;
            var engine = context.Engine;
            var obj = TypeConverter.ToObject(engine.Realm, jsValue);
            var oldEnv = engine.ExecutionContext.LexicalEnvironment;
            var newEnv = JintEnvironment.NewObjectEnvironment(engine, obj, oldEnv, provideThis: true, withEnvironment: true);
            engine.UpdateLexicalEnvironment(newEnv);

            Completion c;
            try
            {
                c = _body.Execute(context);
            }
            catch (JavaScriptException e)
            {
                c = new Completion(CompletionType.Throw, e.Error, _statement.Location);
            }
            finally
            {
                engine.UpdateLexicalEnvironment(oldEnv);
            }

            return c;
        }
    }
}