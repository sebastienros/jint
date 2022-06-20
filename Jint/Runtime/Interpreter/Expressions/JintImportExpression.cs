using Esprima.Ast;
using Jint.Native.Promise;

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
        _importExpression = Build(context.Engine, expression);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-import-calls
    /// </summary>
    protected override ExpressionResult EvaluateInternal(EvaluationContext context)
    {
        var referencingScriptOrModule = context.Engine.GetActiveScriptOrModule();
        var argRef = _importExpression.Evaluate(context);
        var specifier = context.Engine.GetValue(argRef.Value); //.UnwrapIfPromise();
        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
        var specifierString = TypeConverter.ToString(specifier);
        context.Engine._host.ImportModuleDynamically(referencingScriptOrModule, specifierString, promiseCapability);
        context.Engine.RunAvailableContinuations();
        return NormalCompletion(promiseCapability.PromiseInstance);
    }
}