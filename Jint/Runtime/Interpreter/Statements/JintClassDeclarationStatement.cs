#nullable enable

using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintClassDeclarationStatement : JintStatement<ClassDeclaration>
    {
        private readonly ClassDefinition _classDefinition;

        public JintClassDeclarationStatement(ClassDeclaration classDeclaration) : base(classDeclaration)
        {
            _classDefinition = new ClassDefinition(className: classDeclaration.Id, classDeclaration.SuperClass, classDeclaration.Body);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var env = engine.ExecutionContext.LexicalEnvironment;
            var F = _classDefinition.BuildConstructor(context, env);

            var classBinding = _classDefinition._className;
            if (classBinding != null)
            {
                env.CreateMutableBinding(classBinding);
                env.InitializeBinding(classBinding, F);
            }

            return Completion.Empty();
        }
    }
}