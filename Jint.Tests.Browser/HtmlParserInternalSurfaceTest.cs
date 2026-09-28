#if PUBLIC_API_BASELINES

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using VerifyNUnit;

namespace Jint.Tests.Browser;

/// <summary>
/// Snapshots every non-public Jint.HtmlParser type and member that Jint.Browser's compiled IL references, so
/// the integration contract the <c>InternalsVisibleTo</c> grant opens is a reviewed list rather than whatever
/// compiled.
/// </summary>
/// <remarks>
/// The grant exists because Browser owns bindings, page state and resources over the parser's native DOM,
/// CSSOM and selector machinery, and those hooks are not a stable public API yet. A new line in this baseline
/// is Browser reaching one more parser internal: acceptable when it is the parser's intended hook for that
/// job, and a reason to add or reuse one when it is an incidental helper instead. The read is metadata-only:
/// type and member references in Browser's IL, resolved against the parser assembly, so inlined constants
/// and enum values are not listed.
/// </remarks>
public class HtmlParserInternalSurfaceTest
{
    [Test]
    public async Task BrowserReachesOnlyTheReviewedParserInternals()
    {
        var parser = typeof(global::Jint.HtmlParser.Document).Assembly;
        var browser = typeof(global::Jint.Browser.Browser).Assembly;
        using var stream = File.OpenRead(browser.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();

        var parserName = parser.GetName().Name;
        var types = new Dictionary<EntityHandle, Type>();
        foreach (var handle in metadata.TypeReferences)
        {
            if (Resolve(metadata, handle, parser, parserName) is { } type) types[handle] = type;
        }

        var surface = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (_, type) in types)
        {
            if (!type.IsVisible) Entry(surface, type);
        }

        foreach (var handle in metadata.MemberReferences)
        {
            var reference = metadata.GetMemberReference(handle);
            if (ParentType(metadata, reference.Parent, types) is not { } type) continue;
            var name = metadata.GetString(reference.Name);
            var kind = reference.GetKind();
            var candidates = type.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            // Public members of a public type are the published API, not this grant.
            if (candidates.Length == 0 || type.IsVisible && candidates.Any(IsPublic)) continue;
            Entry(surface, type).Add(Member(name, kind));
        }

        var text = new StringBuilder();
        foreach (var (type, members) in surface)
        {
            text.Append(type).Append('\n');
            foreach (var member in members) text.Append("    ").Append(member).Append('\n');
        }

        await Verifier.Verify(text.ToString(), extension: "txt")
            .UseDirectory("Verify")
            .UseFileName("HtmlParserInternalSurfaceTest")
            .DisableRequireUniquePrefix();
    }

    private static SortedSet<string> Entry(SortedDictionary<string, SortedSet<string>> surface, Type type)
    {
        var name = (type.IsVisible ? "public " : "internal ") + type.FullName!.Replace('+', '.');
        if (!surface.TryGetValue(name, out var members)) surface.Add(name, members = new SortedSet<string>(StringComparer.Ordinal));
        return members;
    }

    private static Type? Resolve(MetadataReader metadata, TypeReferenceHandle handle, Assembly parser, string? parserName)
    {
        var reference = metadata.GetTypeReference(handle);
        var name = metadata.GetString(reference.Name);
        switch (reference.ResolutionScope.Kind)
        {
            case HandleKind.AssemblyReference:
                var assembly = metadata.GetAssemblyReference((AssemblyReferenceHandle) reference.ResolutionScope);
                if (metadata.GetString(assembly.Name) != parserName) return null;
                var ns = metadata.GetString(reference.Namespace);
                return parser.GetType(ns.Length == 0 ? name : ns + "." + name);
            case HandleKind.TypeReference:
                return Resolve(metadata, (TypeReferenceHandle) reference.ResolutionScope, parser, parserName)?
                    .GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
            default:
                return null;
        }
    }

    private static Type? ParentType(MetadataReader metadata, EntityHandle parent, Dictionary<EntityHandle, Type> types)
    {
        if (parent.Kind == HandleKind.TypeReference) return types.GetValueOrDefault(parent);
        if (parent.Kind != HandleKind.TypeSpecification) return null;
        // A member of a generic instantiation: the definition is the type reference after GENERICINST.
        var blob = metadata.GetBlobReader(metadata.GetTypeSpecification((TypeSpecificationHandle) parent).Signature);
        if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return null;
        blob.ReadSignatureTypeCode();
        return types.GetValueOrDefault(blob.ReadTypeHandle());
    }

    private static string Member(string name, MemberReferenceKind kind) => kind switch
    {
        MemberReferenceKind.Field => name + " (field)",
        _ when name is ".ctor" => "(constructor)",
        _ => name
    };

    private static bool IsPublic(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic,
        FieldInfo field => field.IsPublic,
        _ => false
    };
}

#endif
