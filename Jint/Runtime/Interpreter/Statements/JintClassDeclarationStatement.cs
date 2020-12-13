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
            _classDefinition = new ClassDefinition(className: null, classDeclaration.SuperClass, classDeclaration.Body);
        }

        protected override Completion ExecuteInternal()
        {
            var F = _classDefinition.BuildConstructor(_engine, _engine.ExecutionContext.LexicalEnvironment);

            if (_statement.Id != null)
            {
                _engine.ExecutionContext.LexicalEnvironment._record.CreateMutableBinding(_statement.Id.Name);
                _engine.ExecutionContext.LexicalEnvironment._record.InitializeBinding(_statement.Id.Name, F);
            }

            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}