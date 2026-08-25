using Microsoft.CodeAnalysis;

namespace Jint.SourceGenerators.Interop;

/// <summary>
/// One reported decline, as the pipeline carries it.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately <b>not</b> <c>Jint.SourceGenerators.DiagnosticInfo</c>, which the other generator
/// uses. That type rebuilds its location from a file path and a span, through
/// <c>Location.Create(string, TextSpan, LinePositionSpan)</c> — and the location that produces has no
/// <c>SourceTree</c>. The compiler resolves an <c>.editorconfig</c> severity <em>per syntax tree</em>, so a
/// tree-less diagnostic silently skips that lookup and keeps its default severity no matter what the
/// consumer writes. It cost nothing there, where every descriptor is an error already. Here the whole
/// point is that a host can promote one, so the real <see cref="Microsoft.CodeAnalysis.Location"/> is
/// carried instead.
/// </para>
/// <para>
/// A <c>Location</c> is not the value-equatable thing an incremental model wants, which is why
/// <c>JsAccessibleGenerator</c> keeps the reporting output and the emitting output apart: the emitting one
/// selects <c>AccessibleTypeDefinition</c>, which carries no location, so an edit that moves a declaration
/// without changing what is generated still re-uses the cached source.
/// </para>
/// </remarks>
internal sealed record class AccessibleDiagnostic(
    DiagnosticDescriptor Descriptor,
    Location? Location,
    EquatableArray<string> MessageArgs)
{
    public AccessibleDiagnostic(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs)
        : this(descriptor, location, messageArgs.ToEquatableArray())
    {
    }

    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(Descriptor, Location, args);
    }
}

/// <summary>
/// What the <c>[JsAccessible]</c> generator could not take, and why. Every one of these says the same
/// thing about behaviour — the member keeps the reflection path it had before the type was annotated, and
/// nothing about it changes — so none of them is a defect and none defaults above
/// <see cref="DiagnosticSeverity.Info"/>. They exist because the alternative is silence: the feature's
/// whole claim is that a member is reached without reflection, and until now nothing told a host which of
/// their members had not been.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately no attribute property that promotes these. Whether a build tolerates a member
/// falling back is a property of the build, not of one annotated type, and Roslyn already has the knob:
/// <c>dotnet_diagnostic.JINT033.severity = error</c> in an <c>.editorconfig</c>, which is verified to
/// reach a generator-reported diagnostic. That also gives a host what a boolean could not — per-rule
/// severity, so "an overload of mine may stay reflected, but a parameter I could have typed
/// <c>JsValue</c> may not" is expressible.
/// </para>
/// <para>
/// The ids are split by what a host would do about them rather than by where the decline is coded, since
/// the severity knob is per id: rename (JINT032), change a signature (JINT033), change an accessor
/// (JINT034), and the two nobody can do anything about but should know (JINT035, JINT036).
/// </para>
/// </remarks>
internal static class JsAccessibleDiagnosticDescriptors
{
    private const string Category = "Jint.SourceGenerators";

    public static readonly DiagnosticDescriptor TypeCannotBeRegistered = new(
        id: "JINT030",
        title: "[JsAccessible] type cannot be registered",
        messageFormat: "Type '{0}' is annotated with [JsAccessible] but cannot be registered, so every member of it is reached through reflection: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeRegistersNothing = new(
        id: "JINT031",
        title: "[JsAccessible] type registers no member",
        messageFormat: "Type '{0}' is annotated with [JsAccessible] but no member of it can be generated, so the annotation registers nothing and every member is reached through reflection",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodNameIsNotUnique = new(
        id: "JINT032",
        title: "[JsAccessible] method name is not unique",
        messageFormat: "Method '{0}' on '{1}' is reached through reflection: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodSignatureNotGenerated = new(
        id: "JINT033",
        title: "[JsAccessible] method signature is outside the generated lane",
        messageFormat: "Method '{0}' on '{1}' is reached through reflection: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberDeclarationNotGenerated = new(
        id: "JINT034",
        title: "[JsAccessible] member declaration is outside the generated lane",
        messageFormat: "{0} '{1}' on '{2}' is reached through reflection: {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberTypeCannotBeNamed = new(
        id: "JINT035",
        title: "[JsAccessible] member type cannot be named in generated code",
        messageFormat: "{0} '{1}' on '{2}' is reached through reflection: {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IndexerIsProbedFirst = new(
        id: "JINT036",
        title: "[JsAccessible] indexer is probed ahead of every generated member",
        messageFormat: "Indexer 'this[{0}]' on '{1}' is reached through reflection, and it is probed ahead of the declared members: every name the indexer answers for resolves through it, and the generated lane is not consulted for that name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}
