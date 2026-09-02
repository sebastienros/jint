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

    [JsonPropertyName("nullableStrings")]
    public List<NullableStringEntry> NullableStrings { get; init; } = [];

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

    internal sealed class NullableStringEntry
    {
        [JsonPropertyName("interface")]
        public string Interface { get; init; } = "";

        [JsonPropertyName("member")]
        public string Member { get; init; } = "";

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
