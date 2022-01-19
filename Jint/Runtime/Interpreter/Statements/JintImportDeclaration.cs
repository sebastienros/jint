using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Promise;

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
        var referencingScriptOrModule = context.Engine.GetActiveScriptOrModule().AsModule() ?? throw new InvalidOperationException("The current referencing script or module must be a module");
        var specifier = _statement.Source.StringValue;
        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
        var specifierString = TypeConverter.ToString(specifier);

        // TODO: This comment was in @lahma's code: 6.IfAbruptRejectPromise(specifierString, promiseCapability);
        context.Engine._host.ImportModuleDynamically(referencingScriptOrModule, specifierString, promiseCapability);
        return NormalCompletion(promiseCapability.PromiseInstance);
    }
}