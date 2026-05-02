using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Jint.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class AccessibleGenerator : IIncrementalGenerator
{
    private const string JsAccessibleAttributeMetadataName = "Jint.JsAccessibleAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<AccessibleTypeDefinition?> types = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                JsAccessibleAttributeMetadataName,
                predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax,
                transform: static (ctx, ct) => AccessibleTypeDefinition.From(ctx, ct));

        context.RegisterSourceOutput(types.Where(static t => t is not null), static (spc, t) =>
        {
            if (t is null) return;
            foreach (var diagnostic in t.Diagnostics) spc.ReportDiagnostic(diagnostic);
            if (t.HasErrors) return;

            var source = AccessibleEmitter.Emit(t);
            spc.AddSource(t.HintName, SourceText.From(source, Encoding.UTF8));
        });
    }
}
