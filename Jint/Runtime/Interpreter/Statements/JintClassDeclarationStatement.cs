using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintClassDeclarationStatement : JintStatement<ClassDeclaration>
{
    private readonly ClassDefinition _classDefinition;

    public JintClassDeclarationStatement(ClassDeclaration classDeclaration) : base(classDeclaration)
    {
        _classDefinition = new ClassDefinition(className: classDeclaration.Id?.Name, classDeclaration);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var value = _classDefinition.BuildConstructor(context, env);

        var classBinding = _classDefinition._className;
        if (classBinding != null)
        {
            env.InitializeBinding(classBinding, value, DisposeHint.Normal);
        }

        return new Completion(CompletionType.Normal, JsEmpty.Instance, _statement);
    }
}
