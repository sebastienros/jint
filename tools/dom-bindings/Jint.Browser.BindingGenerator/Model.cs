using System.Reflection;

namespace Jint.Browser.BindingGenerator;

/// <summary>How an interface's instances are wrapped. Mirrors <c>Jint.Browser.Dom.DomWrapperKind</c>.</summary>
internal enum WrapperKind
{
    Object,
    Node,
    Collection,
    NamedMap,
    HtmlCollection,
}

/// <summary>What a generated member is, in WebIDL's vocabulary.</summary>
internal enum MemberKind
{
    Operation,
    Attribute,
}

/// <summary>One JavaScript-visible member of one interface.</summary>
internal sealed class MemberModel
{
    internal required string DomName { get; init; }

    internal required MemberKind Kind { get; init; }

    /// <summary>The C# expression body of the getter / the operation, given <c>thisObj</c> and <c>args</c>.</summary>
    internal required string Body { get; init; }

    /// <summary>The C# statements of the setter, or <see langword="null"/> for a read-only attribute.</summary>
    internal string? SetterBody { get; init; }

    /// <summary>An operation's declared parameter count — its <c>length</c>.</summary>
    internal int Length { get; init; }

    /// <summary>Where the member came from, for the emitted comment and for collision reporting.</summary>
    internal required string Origin { get; init; }
}

/// <summary>A WebIDL constant — <c>Node.ELEMENT_NODE</c>.</summary>
internal sealed record ConstantModel(string Name, long Value, string Origin);

/// <summary>One interface the generator emits.</summary>
internal sealed class InterfaceModel
{
    internal required string DomName { get; init; }

    internal required Type ClrType { get; init; }

    internal InterfaceModel? Parent { get; set; }

    internal bool RootsAtEventTarget { get; set; }

    internal bool HasInterfaceObject { get; set; } = true;

    internal WrapperKind Kind { get; set; } = WrapperKind.Object;

    internal List<MemberModel> Members { get; } = [];

    internal List<ConstantModel> Constants { get; } = [];

    /// <summary>The generated <c>DomCollectionAccessor</c> class body, or <see langword="null"/>.</summary>
    internal string? AccessorClass { get; set; }

    /// <summary>The C# expression naming this interface's accessor singleton, or <see langword="null"/>.</summary>
    internal string? AccessorReference { get; set; }

    /// <summary>The field name this interface gets on <c>DomInterfaces</c>.</summary>
    internal required string FieldName { get; init; }

    /// <summary>Which generated file the shape builder lands in.</summary>
    internal required string Group { get; init; }

    /// <summary>
    /// The hand-written method group that builds this interface's shape, for an interface the generator
    /// cannot express. The registry still carries the interface, so its place in the prototype chain, its
    /// interface object and its type-map entry are generated exactly like everyone else's.
    /// </summary>
    internal string? ManualShape { get; set; }

    /// <summary>
    /// The hand-written method the emitter hands the half-built builder to, for an interface that carries
    /// members no AngleSharp member could ever project — an event handler IDL attribute, an activation
    /// behaviour, a member the runtime rather than the DOM answers. Unlike <see cref="ManualShape"/> this
    /// adds to the generated members instead of replacing them, so the interface keeps everything the
    /// attributes did say and the shape stays one shape.
    /// </summary>
    internal string? ShapeAdditions { get; set; }

    public override string ToString() => DomName;
}

/// <summary>Everything the emitter needs, plus what the report prints.</summary>
internal sealed class BindingModel
{
    internal List<InterfaceModel> Interfaces { get; } = [];

    /// <summary>Enums whose values cross as strings, keyed by CLR full name.</summary>
    internal Dictionary<string, EnumModel> StringEnums { get; } = new(StringComparer.Ordinal);

    /// <summary>Closed <c>IHtmlCollection&lt;T&gt;</c> constructions seen in a member signature.</summary>
    internal SortedSet<string> HtmlCollectionElements { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The namespaces holding the extension classes a generated member calls. They are emitted as <c>using</c>
    /// directives and the calls are written in extension form, because AngleSharp and AngleSharp.Css both
    /// declare an <c>AngleSharp.Dom.ElementExtensions</c> — naming either by its full name is CS0433, and
    /// extension-method lookup resolves it by which one declares the method.
    /// </summary>
    internal SortedSet<string> ExtensionNamespaces { get; } = new(StringComparer.Ordinal);

    /// <summary>Members left out, and why. Every entry is reported.</summary>
    internal List<SkipRecord> Skipped { get; } = [];

    /// <summary>Anything the generator wants a human to read: collisions, unmatched overrides.</summary>
    internal List<string> Diagnostics { get; } = [];

    /// <summary>Attribute usage counts per assembly, for the inventory.</summary>
    internal Dictionary<string, Dictionary<string, int>> AttributeCounts { get; } = new(StringComparer.Ordinal);
}

/// <summary>A skipped member and the reason.</summary>
internal sealed record SkipRecord(string Interface, string Member, string Reason);

/// <summary>A WebIDL string enumeration projected from a CLR enum.</summary>
internal sealed class EnumModel
{
    internal required string ClrFullName { get; init; }

    internal required string HelperName { get; init; }

    internal required List<(string FieldName, string Literal)> Values { get; init; }
}
