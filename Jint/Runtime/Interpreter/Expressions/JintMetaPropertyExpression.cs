using Esprima.Ast;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Expressions
{
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
            if (expression.Meta.Name == "new" && expression.Property.Name == "target")
            {
                return context.Engine.GetNewTarget();
            }

            if (expression.Meta.Name == "import" && expression.Property.Name == "meta")
            {
                var module = (SourceTextModuleRecord) context.Engine.ExecutionContext.ScriptOrModule!;
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
}
