using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintClassExpression : JintExpression
    {
        public JintClassExpression(Engine engine, ClassExpression expression) : base(engine, expression)
        {
        }

        protected override object EvaluateInternal()
        {
            var env = LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment);

            var classExpression = (ClassExpression) _expression;
            var name = classExpression.Id?.Name;
            
            var closure = new ClassConstructorInstance(
                _engine,
                classExpression.SuperClass,
                classExpression.Body,
                env,
                name);
            
            if (name is not null)
            {
                var envRec = (DeclarativeEnvironmentRecord) env._record;
                envRec.CreateMutableBindingAndInitialize(name, canBeDeleted: false, closure);
            }

            return closure;
        }
    }
}