using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintMetaPropertyExpression : JintExpression
{
    public JintMetaPropertyExpression(MetaProperty expression) : base(expression)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-meta-properties
    /// </summary>
    protected override object EvaluateInternal(EvaluationContext context)
    {
        var expression = (MetaProperty) _expression;
        if (string.Equals(expression.Meta.Name, "new", StringComparison.Ordinal) && string.Equals(expression.Property.Name, "target", StringComparison.Ordinal))
        {
            return context.Engine.GetNewTarget();
        }

        if (string.Equals(expression.Meta.Name, "import", StringComparison.Ordinal) && string.Equals(expression.Property.Name, "meta", StringComparison.Ordinal))
        {
            var module = (SourceTextModule) context.Engine.ExecutionContext.ScriptOrModule!;
            var importMeta = module.ImportMeta;
            if (importMeta is null)
            {
                importMeta = context.Engine.Realm.Intrinsics.Object.Construct(0);
                var importMetaValues = context.Engine._host.GetImportMetaProperties(module);
                foreach (var p in importMetaValues)
                {
                    importMeta.CreateDataPropertyOrThrow(p.Key, p.Value);
                }

                context.Engine._host.FinalizeImportMeta(importMeta, module);
                module.ImportMeta = importMeta;
            }

            return importMeta;
        }

        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }
}