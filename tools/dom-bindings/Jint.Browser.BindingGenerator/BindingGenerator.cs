using System.Reflection;
using System.Text;

namespace Jint.Browser.BindingGenerator;

/// <summary>What the generator needs to run.</summary>
public sealed class BindingGeneratorOptions
{
    /// <summary>Path to the pinned <c>AngleSharp.dll</c>.</summary>
    public required string CoreAssembly { get; init; }

    /// <summary>Path to the pinned <c>AngleSharp.Css.dll</c>.</summary>
    public required string CssAssembly { get; init; }

    /// <summary>Path to <c>overrides.json</c>.</summary>
    public required string OverridesPath { get; init; }
}

/// <summary>What one run of the generator produced.</summary>
/// <param name="Files">The generated files, keyed by file name.</param>
/// <param name="Report">The inventory, the counts, the skipped members and the diagnostics, as text.</param>
/// <param name="Diagnostics">
/// Everything a human has to read: a member name declared twice in one interface closure, an override entry
/// naming something the pinned assemblies no longer have. A non-empty list is not automatically a failure —
/// two of the entries are known and permanent — but a <em>new</em> one is.
/// </param>
/// <param name="Skipped">Every member left out, with the reason, as "Interface.member — reason".</param>
public sealed record BindingGeneratorResult(
    IReadOnlyDictionary<string, string> Files,
    string Report,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Skipped);

/// <summary>
/// The one entry point, shared by the command line and by the staleness test, which runs it in memory and
/// compares the result byte for byte with what is checked in.
/// </summary>
public static class BindingGenerator
{
    /// <summary>Generates every file, keyed by file name.</summary>
    public static IReadOnlyDictionary<string, string> Generate(BindingGeneratorOptions options)
        => Run(options).Files;

    /// <summary>Generates every file, plus the report, the diagnostics and the skip list.</summary>
    public static BindingGeneratorResult Run(BindingGeneratorOptions options)
    {
        var references = new List<string> { options.CoreAssembly, options.CssAssembly };
        references.AddRange(RuntimeAssemblies());

        var resolver = new PathAssemblyResolver(references.Distinct(StringComparer.OrdinalIgnoreCase));
        using var context = new MetadataLoadContext(resolver, "System.Private.CoreLib");

        var core = context.LoadFromAssemblyPath(options.CoreAssembly);
        var css = context.LoadFromAssemblyPath(options.CssAssembly);

        var overrides = Overrides.Load(options.OverridesPath);
        var model = new ModelBuilder([core, css], overrides).Build();
        var files = new Emitter(model).Emit();

        return new BindingGeneratorResult(
            files,
            Report(model, core, css),
            [.. model.Diagnostics.Order(StringComparer.Ordinal)],
            [.. model.Skipped.Select(s => s.Interface + "." + s.Member + " — " + s.Reason).Order(StringComparer.Ordinal)]);
    }

    private static IEnumerable<string> RuntimeAssemblies()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        foreach (var path in trusted.Split(Path.PathSeparator))
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static string Report(BindingModel model, Assembly core, Assembly css)
    {
        var builder = new StringBuilder();

        builder.Append("Assemblies\n");
        foreach (var assembly in new[] { core, css })
        {
            builder.Append("  ").Append(assembly.GetName().Name).Append(' ').Append(assembly.GetName().Version).Append('\n');
        }

        builder.Append("\nAttribute inventory\n");
        foreach (var (assembly, counts) in model.AttributeCounts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append("  ").Append(assembly).Append('\n');
            foreach (var (attribute, count) in counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                builder.Append("    ").Append(attribute.PadRight(32)).Append(count).Append('\n');
            }
        }

        builder.Append("\nGenerated\n");
        builder.Append("  interfaces         ").Append(model.Interfaces.Count).Append('\n');
        builder.Append("  operations         ").Append(model.Interfaces.Sum(i => i.Members.Count(m => m.Kind == MemberKind.Operation))).Append('\n');
        builder.Append("  attributes         ").Append(model.Interfaces.Sum(i => i.Members.Count(m => m.Kind == MemberKind.Attribute))).Append('\n');
        builder.Append("  writable attributes ").Append(model.Interfaces.Sum(i => i.Members.Count(m => m.Kind == MemberKind.Attribute && m.SetterBody is not null))).Append('\n');
        builder.Append("  constants          ").Append(model.Interfaces.Sum(i => i.Constants.Count)).Append('\n');
        builder.Append("  collections        ").Append(model.Interfaces.Count(i => i.Kind is WrapperKind.Collection or WrapperKind.NamedMap)).Append('\n');
        builder.Append("  string enums       ").Append(model.StringEnums.Count).Append('\n');

        // Which reflected attributes corrected a projection and which added a member is the one thing the
        // `reflected` list cannot say about itself, since the answer is the pinned assemblies' and not the
        // table's — an AngleSharp release that grows `HTMLElement.autofocus` moves a row from one column to
        // the other, and this is where a reader sees it.
        builder.Append("\nReflected attributes (").Append(model.Reflected.Count)
            .Append(", of which ").Append(model.Reflected.Count(r => r.Replaced)).Append(" replace a projection)\n");
        foreach (var reflected in model.Reflected.OrderBy(r => r.Qualified, StringComparer.Ordinal))
        {
            builder.Append("  ").Append(reflected.Qualified.PadRight(44)).Append(reflected.Replaced ? "replaces  " : "adds      ")
                .Append(reflected.Attribute.PadRight(20)).Append(reflected.Type).Append('\n');
        }

        builder.Append("\nSkipped members (").Append(model.Skipped.Count).Append(")\n");
        foreach (var skip in model.Skipped.OrderBy(s => s.Interface, StringComparer.Ordinal).ThenBy(s => s.Member, StringComparer.Ordinal))
        {
            builder.Append("  ").Append(skip.Interface).Append('.').Append(skip.Member).Append(" — ").Append(skip.Reason).Append('\n');
        }

        builder.Append("\nDiagnostics (").Append(model.Diagnostics.Count).Append(")\n");
        foreach (var diagnostic in model.Diagnostics.Order(StringComparer.Ordinal))
        {
            builder.Append("  ").Append(diagnostic).Append('\n');
        }

        return builder.ToString();
    }
}
