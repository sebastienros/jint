using Esprima.Ast;
using Esprima.Utils;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintClassExpression : JintExpression
    {
        private readonly ClassDefinition _classDefinition;

        public JintClassExpression(ClassExpression expression) : base(expression)
        {
            _classDefinition = new ClassDefinition(className: expression.Id?.Name, classSource: expression.ToString(), superClass: expression.SuperClass, body: expression.Body);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var env = context.Engine.ExecutionContext.LexicalEnvironment;
            return _classDefinition.BuildConstructor(context, env);
        }
    }
}
