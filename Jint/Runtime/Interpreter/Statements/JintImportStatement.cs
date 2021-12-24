using Esprima.Ast;
using Jint.Native.Promise;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintImportStatement : JintStatement<ImportDeclaration>
{
    public JintImportStatement(ImportDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _statement.Specifiers;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var referencingScriptOrModule = context.Engine.GetActiveScriptOrModule();
        var argRef = evaluating AssignmentExpression;
        var specifier = argRef.GetValue();
        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
        var specifierString = TypeConverter.ToString(specifier);

        promiseCapability.Deconstruct();
        if (spc)

        6.IfAbruptRejectPromise(specifierString, promiseCapability);
        context.Engine._host.ImportModuleDynamically(referencingScriptOrModule, specifierString, promiseCapability);
        return NormalCompletion(promiseCapability.PromiseInstance);
    }
}