namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintImportDeclaration : JintStatement<ImportDeclaration>
{
    public JintImportDeclaration(ImportDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        // just to ensure module context or valid
        context.Engine.GetActiveScriptOrModule().AsModule(context.Engine, context.LastSyntaxElement.Location);
        return Completion.Empty();
    }
}
