#nullable enable

using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Modules;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportDefaultDeclarationStatement : JintExportDeclarationStatement<ExportDefaultDeclaration>
{
    private JintExpression? _init;

    public JintExportDefaultDeclarationStatement(ExportDefaultDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _init = JintExpression.Build(context.Engine, (Expression) _statement.Declaration);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var module = context.Engine.GetActiveScriptOrModule() as JsModule;
        if (module == null) throw new JavaScriptException("Export can only be used in a module");

        var completion = _init?.GetValue(context) ?? Completion.Empty();
        module._environment.CreateImmutableBindingAndInitialize("*default*", true, completion.Value);

        return Completion.Empty();
    }
}