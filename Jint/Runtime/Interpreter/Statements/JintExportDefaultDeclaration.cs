#nullable enable

using Esprima.Ast;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportDefaultDeclaration : JintStatement<ExportDefaultDeclaration>
{
    private JintExpression? _init;

    public JintExportDefaultDeclaration(ExportDefaultDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _init = JintExpression.Build(context.Engine, (Expression) _statement.Declaration);
    }

    // https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var module = context.Engine.GetActiveScriptOrModule().AsModule(context.Engine, context.LastSyntaxNode.Location);

        var completion = _init?.GetValue(context) ?? Completion.Empty();
        module._environment.CreateImmutableBindingAndInitialize("*default*", true, completion.Value);

        return Completion.Empty();
    }
}