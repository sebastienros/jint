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
            _classDefinition = new ClassDefinition(className: classDeclaration.Id?.Name, classDeclaration.SuperClass, classDeclaration.Body);
        }

        protected override Completion ExecuteInternal()
        {
            var F = _classDefinition.BuildConstructor(_engine, _engine.ExecutionContext.LexicalEnvironment);

            if (_classDefinition._className != null)
            {
                _engine.ExecutionContext.LexicalEnvironment._record.CreateMutableBinding(_classDefinition._className);
                _engine.ExecutionContext.LexicalEnvironment._record.InitializeBinding(_classDefinition._className, F);
            }

            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}