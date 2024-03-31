namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportAllDeclaration : JintStatement<ExportAllDeclaration>
{
    public JintExportAllDeclaration(ExportAllDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        return Completion.Empty();
    }
}
