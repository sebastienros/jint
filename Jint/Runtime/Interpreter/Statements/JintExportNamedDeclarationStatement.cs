#nullable enable

using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclarationStatement : JintExportDeclarationStatement<ExportNamedDeclaration>
{
    public JintExportNamedDeclarationStatement(ExportNamedDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        InitializeDeclaration(context.Engine, _statement.Declaration);
    }
}