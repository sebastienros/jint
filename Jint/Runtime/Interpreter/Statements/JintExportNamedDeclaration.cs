using Jint.Native;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclaration : JintStatement<ExportNamedDeclaration>
{
    private readonly JintStatement? _declarationStatement;

    public JintExportNamedDeclaration(ExportNamedDeclaration statement) : base(statement)
    {
        if (statement.Declaration != null)
        {
            _declarationStatement = Build(statement.Declaration);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var result = _declarationStatement?.Execute(context) ?? Completion.Empty();

        // Check for async/generator suspension
        if (context.IsSuspended())
        {
            return result;
        }

        return new Completion(CompletionType.Normal, JsValue.Undefined, ((JintStatement) this)._statement);
    }
}
