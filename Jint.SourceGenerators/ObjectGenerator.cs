using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Jint.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class ObjectGenerator : IIncrementalGenerator
{
    private const string JsObjectAttributeMetadataName = "Jint.JsObjectAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource(
            "JintAttributes.g.cs",
            SourceText.From(Attributes.Source, Encoding.UTF8)));

        IncrementalValuesProvider<ObjectDefinition?> objects = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                JsObjectAttributeMetadataName,
                predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax,
                transform: static (ctx, ct) => ObjectDefinition.From(ctx, ct));

        context.RegisterSourceOutput(objects.Where(static o => o is not null), static (spc, obj) =>
        {
            if (obj is null)
            {
                return;
            }

            foreach (var diagnostic in obj.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            if (obj.HasErrors)
            {
                return;
            }

            var source = Emitter.Emit(obj);
            spc.AddSource(obj.HintName, SourceText.From(source, Encoding.UTF8));
        });
    }
}
