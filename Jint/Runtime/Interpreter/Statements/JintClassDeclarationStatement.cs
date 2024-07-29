using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Statements;

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
        var value = _classDefinition.BuildConstructor(context, env);

        if (context.IsAbrupt())
        {
            return new Completion(context.Completion, value, _statement);
        }

        var classBinding = _classDefinition._className;
        if (classBinding != null)
        {
            env.InitializeBinding(classBinding, value);
        }

        return new Completion(CompletionType.Normal, JsEmpty.Instance, _statement);
    }
}