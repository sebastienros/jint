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
            _classDefinition = new ClassDefinition(className: classDeclaration.Id?.Name, classDeclaration.SuperClass, classDeclaration.Body);
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            var engine = context.Engine;
            var env = engine.ExecutionContext.LexicalEnvironment;
            var completion = _classDefinition.BuildConstructor(context, env);

            if (completion.IsAbrupt())
            {
                return completion;
            }

            var classBinding = _classDefinition._className;
            if (classBinding != null)
            {
                env.InitializeBinding(classBinding, completion.Value!);
            }

            return new Completion(CompletionType.Normal, null!, null, Location);
        }
    }
}
