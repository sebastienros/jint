using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Jint.SourceGenerators.Interop;

/// <summary>
/// Emits source-generated CLR interop for types annotated with <c>[Jint.JsAccessible]</c>.
/// </summary>
/// <remarks>
/// The whole pipeline collects before it emits, which is what lets the same annotated type be seen twice
/// without crashing the generator: <c>ForAttributeWithMetadataName</c> fires once per attributed
/// <em>declaration</em>, so a <c>partial class</c> carrying the attribute on two of its parts arrives twice —
/// and two <c>AddSource</c> calls under one hint name is an exception, not a diagnostic. Collecting is also
/// what produces the single per-assembly registration entry point.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class JsAccessibleGenerator : IIncrementalGenerator
{
    private const string JsAccessibleAttributeMetadataName = "Jint.JsAccessibleAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // TypeDeclarationSyntax rather than ClassDeclarationSyntax, which is what it used to be. A record is
        // a class, so [JsAccessible] compiles on one, but a RecordDeclarationSyntax is not a
        // ClassDeclarationSyntax — so an annotated record used to be dropped here, before anything could say
        // a word about it. It is still declined, now by IneligibleTypeReason and with a diagnostic.
        var types = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                JsAccessibleAttributeMetadataName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, ct) => AccessibleTypeResult.From(ctx, ct))
            .Where(static result => result is not null)
            .Collect();

        var rootNamespace = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) ? value : null)
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName))
            .Select(static (pair, _) => SanitizeNamespace(pair.Left) ?? SanitizeNamespace(pair.Right) ?? string.Empty);

        context.RegisterSourceOutput(types.Combine(rootNamespace), static (spc, pair) =>
        {
            var results = Deduplicate(pair.Left!);

            foreach (var result in results)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic);
                }
            }

            var definitions = ImmutableArray.CreateBuilder<AccessibleTypeDefinition>(results.Length);
            foreach (var result in results)
            {
                if (result.Definition is not null)
                {
                    definitions.Add(result.Definition);
                }
            }

            if (definitions.Count == 0)
            {
                return;
            }

            var emitted = definitions.ToImmutable();
            foreach (var definition in emitted)
            {
                spc.AddSource(definition.HintName, SourceText.From(JsAccessibleEmitter.EmitType(definition), Encoding.UTF8));
            }

            spc.AddSource(
                "JsAccessibleRegistration.g.cs",
                SourceText.From(JsAccessibleEmitter.EmitRegistration(pair.Right, emitted), Encoding.UTF8));
        });
    }

    /// <summary>
    /// One entry per annotated type, ordered by the path that names it so the emitted registration — and the
    /// order the diagnostics are reported in — is stable whatever order the compilation reported the
    /// declarations in.
    /// </summary>
    private static ImmutableArray<AccessibleTypeResult> Deduplicate(ImmutableArray<AccessibleTypeResult?> results)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<AccessibleTypeResult>(results.Length);
        foreach (var result in results)
        {
            if (result is not null && seen.Add(result.MetadataPath))
            {
                unique.Add(result);
            }
        }

        unique.Sort(static (a, b) => string.CompareOrdinal(a.MetadataPath, b.MetadataPath));
        return unique.ToImmutableArray();
    }

    /// <summary>
    /// The registration entry point is <c>public</c> so a host can call it across an assembly boundary, which
    /// makes its name part of that assembly's surface: it goes into the assembly's own root namespace rather
    /// than into a shared one, where two referenced assemblies would collide on it.
    /// </summary>
    private static string? SanitizeNamespace(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var sb = new StringBuilder(candidate!.Length);
        foreach (var segment in candidate.Split('.'))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            var sanitized = new StringBuilder(segment.Length);
            foreach (var ch in segment)
            {
                sanitized.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            }

            if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
            {
                sanitized.Insert(0, '_');
            }

            if (sb.Length > 0)
            {
                sb.Append('.');
            }

            sb.Append(sanitized);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}
