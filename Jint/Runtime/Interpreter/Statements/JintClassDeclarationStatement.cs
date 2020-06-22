#nullable enable

using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
    /// </summary>
    internal sealed class JintClassDeclarationStatement : JintStatement<ClassDeclaration>
    {
        public JintClassDeclarationStatement(Engine engine, ClassDeclaration classDeclaration) : base(engine, classDeclaration)
        {
        }

        protected override Completion ExecuteInternal()
        {

            var F = new ClassConstructorInstance(
                _engine, 
                _statement.SuperClass,
                _statement.Body,
                _engine.ExecutionContext.LexicalEnvironment);

            if (_statement.Id != null)
            {
                _engine.ExecutionContext.LexicalEnvironment._record.CreateMutableBinding(_statement.Id.Name);
                _engine.ExecutionContext.LexicalEnvironment._record.InitializeBinding(_statement.Id.Name, F);
            }

            return new Completion(CompletionType.Return, JsValue.Undefined, null, Location);
        }
    }
}