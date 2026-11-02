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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-bindingclassdeclarationevaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var env = engine.ExecutionContext.LexicalEnvironment;
        var value = _classDefinition.BuildConstructor(context, env);

        var classBinding = _classDefinition._className;
        if (classBinding != null)
        {
            // A class whose heritage or computed key suspended is not defined yet, and its binding stays in its
            // temporal dead zone until the resume defines it.
            if (_classDefinition._maySuspend && context.IsSuspended())
            {
                return new Completion(CompletionType.Normal, JsEmpty.Instance, _statement);
            }

            env.InitializeBinding(classBinding, value, DisposeHint.Normal);
        }

        return new Completion(CompletionType.Normal, JsEmpty.Instance, _statement);
    }
}
