using Esprima.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal class JintSuperExpression : JintExpression
    {
        public JintSuperExpression(Engine engine, Super expression) : base(engine, expression)
        {
        }

        protected override object EvaluateInternal()
        {
            var envRec = (FunctionEnvironmentRecord) _engine.GetThisEnvironment();
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return superConstructor;
        }
    }
}