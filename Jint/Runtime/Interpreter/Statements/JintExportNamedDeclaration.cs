using Jint.Native;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclaration : JintStatement<ExportNamedDeclaration>
{
    private JintStatement? _declarationStatement;

    public JintExportNamedDeclaration(ExportNamedDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        if (_statement.Declaration != null)
        {
            _declarationStatement = Build(_statement.Declaration);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        _declarationStatement?.Execute(context);
        return new Completion(CompletionType.Normal, JsValue.Undefined, ((JintStatement) this)._statement);
    }
}
