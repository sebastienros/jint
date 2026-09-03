using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jint.Browser.BindingGenerator;

/// <summary>
/// The curated half of the binding: what AngleSharp's attributes cannot say, said once, with a reason on
/// every entry.
/// </summary>
/// <remarks>
/// Every entry names a member that has to exist in the pinned assemblies. <c>DomBindingsOverrideTests</c>
/// fails on one that does not, which is what stops the table quietly describing a version of AngleSharp that
/// is no longer referenced.
/// </remarks>
internal sealed class Overrides
{
    [JsonPropertyName("excludedInterfaces")]
    public List<ExcludedInterface> ExcludedInterfaces { get; init; } = [];

    [JsonPropertyName("manual")]
    public List<ManualEntry> Manual { get; init; } = [];

    [JsonPropertyName("skip")]
    public List<SkipEntry> Skip { get; init; } = [];

    [JsonPropertyName("hooks")]
    public List<HookEntry> Hooks { get; init; } = [];

    [JsonPropertyName("additions")]
    public List<AdditionEntry> Additions { get; init; } = [];

    [JsonPropertyName("nullableStrings")]
    public List<NullableStringEntry> NullableStrings { get; init; } = [];

    [JsonPropertyName("nullableParameters")]
    public List<NullableParameterEntry> NullableParameters { get; init; } = [];

    [JsonPropertyName("stringEnums")]
    public List<StringEnumEntry> StringEnums { get; init; } = [];

    [JsonPropertyName("constants")]
    public ConstantOverrides Constants { get; init; } = new();

    internal static Overrides Load(string path)
        => JsonSerializer.Deserialize(File.ReadAllText(path), OverridesJsonContext.Default.Overrides)
           ?? throw new InvalidOperationException("overrides.json is empty.");

    internal sealed class ExcludedInterface
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    internal sealed class ManualEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        /// <summary>The hand-written method group that builds the shape.</summary>
        [JsonPropertyName("shape")]
        public string Shape { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    internal sealed class SkipEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("member")]
        public string Member { get; init; } = "";

        /// <summary>Optional: <c>setter</c> skips only the write half of an attribute.</summary>
        [JsonPropertyName("half")]
        public string? Half { get; init; }

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    internal sealed class HookEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("member")]
        public string Member { get; init; } = "";

        /// <summary>Which half the hook replaces: <c>setter</c>, <c>getter</c> or <c>operation</c>.</summary>
        [JsonPropertyName("half")]
        public string Half { get; init; } = "operation";

        /// <summary>The method on <c>DomHostHooks</c> the generated body calls.</summary>
        [JsonPropertyName("hook")]
        public string Hook { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    /// <summary>
    /// A member the DOM standard puts on a generated interface and that AngleSharp's metadata cannot express
    /// - a callback parameter, a stringifier with no <c>[DomName]</c>, a member AngleSharp spells by another
    /// name, an event handler IDL attribute, an operation whose body is an event rather than a DOM call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry takes one of two forms, and exactly one: it either names a single <see cref="AdditionEntry.Member"/>
    /// and carries its <see cref="AdditionEntry.Body"/>, or it names an <see cref="AdditionEntry.Extend"/>
    /// method that is handed the half-built shape builder. <b>The member form is the one to reach for</b> - it
    /// goes through the model like any projected member, so the generated file names it, it sorts with the
    /// rest, and a member the pinned assemblies later grow under the same name is reported as a collision
    /// instead of quietly shadowed.
    /// </para>
    /// <para>
    /// The extend form exists for the one thing the member form cannot say: a <em>family</em> whose member
    /// list is computed rather than enumerated. HTML's event handler IDL attributes are that family - eighty
    /// -odd names on <c>HTMLElement</c> and as many again on <c>Document</c>, each an identically shaped
    /// accessor pair, which as member entries would be a hundred and seventy near-identical rows of a table
    /// whose whole purpose is to hold <em>decisions</em>. Its names are declared in C# where the generator
    /// cannot see them, so its collision check is <c>JsObjectShape.Builder</c>'s own refusal of a duplicate
    /// name, which <c>DomPrototypeTests</c> reaches for every interface.
    /// </para>
    /// <para>
    /// The member form's entries name something the pinned assemblies do <em>not</em> have; what is checked is
    /// the opposite - that the interface exists and that the member does not, so an entry can never quietly
    /// shadow a member AngleSharp grew.
    /// </para>
    /// </remarks>
    internal sealed class AdditionEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        /// <summary>The member form: the DOM name of the single member this entry declares.</summary>
        [JsonPropertyName("member")]
        public string Member { get; init; } = "";

        /// <summary><c>operation</c> (the default) or <c>attribute</c>. Member form only.</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; init; } = "operation";

        /// <summary>An operation's declared parameter count - its <c>length</c>. Member form only.</summary>
        [JsonPropertyName("length")]
        public int Length { get; init; }

        /// <summary>The C# statements of the operation or of the getter, one entry per line. Member form only.</summary>
        [JsonPropertyName("body")]
        public List<string> Body { get; init; } = [];

        /// <summary>The C# statements of the setter, for a writable attribute. Member form only.</summary>
        [JsonPropertyName("setter")]
        public List<string>? Setter { get; init; }

        /// <summary>
        /// The extend form: the hand-written method the emitter calls with the half-built builder, before
        /// <c>Build()</c>. It takes a <c>JsObjectShape.Builder</c> and returns nothing.
        /// </summary>
        [JsonPropertyName("extend")]
        public string Extend { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";

        /// <summary>Whether this entry hands the builder to a method rather than declaring one member.</summary>
        [JsonIgnore]
        public bool IsExtend => !string.IsNullOrEmpty(Extend);
    }

    internal sealed class NullableStringEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("member")]
        public string Member { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    /// <summary>
    /// One argument position that WebIDL declares nullable, which the emitter cannot see: it reads C#
    /// optionality — a default value in the signature — and not nullable-reference metadata.
    /// </summary>
    /// <remarks>
    /// Decoding the metadata instead was considered and is a different change: it would flip every parameter
    /// AngleSharp happens to annotate, all at once and unreviewably, where a table entry is one line naming
    /// the clause of the standard it comes from. Keyed on the argument index rather than the parameter name,
    /// because the index is what a script passes and what the emitted conversion takes.
    /// </remarks>
    internal sealed class NullableParameterEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("member")]
        public string Member { get; init; } = "";

        [JsonPropertyName("parameter")]
        public int Parameter { get; init; }

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    internal sealed class StringEnumEntry
    {
        [JsonPropertyName("enum")]
        public string Enum { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    internal sealed class ConstantOverrides
    {
        [JsonPropertyName("add")]
        public List<ConstantEntry> Add { get; init; } = [];

        [JsonPropertyName("skip")]
        public List<ConstantEntry> Skip { get; init; } = [];
    }

    internal sealed class ConstantEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("enum")]
        public string Enum { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }
}

[JsonSerializable(typeof(Overrides))]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
internal sealed partial class OverridesJsonContext : JsonSerializerContext;
