#nullable enable

using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintClassDeclarationStatement : JintStatement<ClassDeclaration>
    {
        private readonly ClassDefinition _classDefinition;

        public JintClassDeclarationStatement(Engine engine, ClassDeclaration classDeclaration) : base(engine, classDeclaration)
        {
            _classDefinition = new ClassDefinition(className: classDeclaration.Id, classDeclaration.SuperClass, classDeclaration.Body);
        }

        protected override Completion ExecuteInternal()
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var F = _classDefinition.BuildConstructor(_engine, env);

            var classBinding = _classDefinition._className;
            if (classBinding != null)
            {
                env._record.CreateMutableBinding(classBinding);
                env._record.InitializeBinding(classBinding, F);
            }

            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}