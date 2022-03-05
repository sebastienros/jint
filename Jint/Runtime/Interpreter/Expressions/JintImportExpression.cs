#nullable enable

using Esprima.Ast;
using Jint.Native.Promise;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintImportExpression : JintExpression
{
    private JintExpression _importExpression;

    public JintImportExpression(Import expression) : base(expression)
    {
        _initialized = false;
        _importExpression = null!;
    }

    protected override void Initialize(EvaluationContext context)
    {
        var expression = ((Import) _expression).Source;
        _importExpression = Build(context.Engine, expression!);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-import-calls
    /// </summary>
    protected override ExpressionResult EvaluateInternal(EvaluationContext context)
    {
        var module = context.Engine.GetActiveScriptOrModule().AsModule(context.Engine, context.LastSyntaxNode.Location);

        var argRef = _importExpression.Evaluate(context);
        context.Engine.RunAvailableContinuations();
        var value = context.Engine.GetValue(argRef.Value);
        var specifier = value.UnwrapIfPromise();

        if (specifier is ModuleNamespace)
        {
            // already resolved
            return NormalCompletion(value);
        }

        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
        var specifierString = TypeConverter.ToString(specifier);

        // 6.IfAbruptRejectPromise(specifierString, promiseCapability);
        context.Engine._host.ImportModuleDynamically(module, specifierString, promiseCapability);
        return NormalCompletion(promiseCapability.PromiseInstance);
    }
}