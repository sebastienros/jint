#nullable enable

using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportDefaultDeclarationStatement : JintExportDeclarationStatement<ExportDefaultDeclaration>
{
    public JintExportDefaultDeclarationStatement(ExportDefaultDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        InitializeDeclaration(context.Engine, _statement.Declaration);
    }
}