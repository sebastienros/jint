#nullable enable

using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportAllDeclarationStatement : JintExportDeclarationStatement<ExportAllDeclaration>
{
    public JintExportAllDeclarationStatement(ExportAllDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        InitializeDeclaration(context.Engine, _statement.Exported);
    }
}