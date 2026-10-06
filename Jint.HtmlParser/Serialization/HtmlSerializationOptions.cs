namespace Jint.HtmlParser;

/// <summary>Immutable HTML escaping context and shadow-root selection for serialization.</summary>
public sealed class HtmlSerializationOptions
{
    internal static readonly HtmlSerializationOptions Default = new();
    private readonly IReadOnlyList<ShadowRoot> _shadowRoots;

    /// <summary>Copies and deduplicates explicit root identities. Null entries are rejected.</summary>
    /// <remarks>The caller's sequence is enumerated here, before any serialization operation.</remarks>
    public HtmlSerializationOptions(bool scriptingEnabled = false, bool serializableShadowRoots = false,
        IEnumerable<ShadowRoot>? shadowRoots = null)
    {
        ScriptingEnabled = scriptingEnabled;
        SerializableShadowRoots = serializableShadowRoots;
        if (shadowRoots is null)
        {
            _shadowRoots = Array.AsReadOnly(Array.Empty<ShadowRoot>());
            return;
        }

        var seen = new HashSet<ShadowRoot>(ReferenceEqualityComparer.Instance);
        var copy = new List<ShadowRoot>();
        foreach (var root in shadowRoots)
        {
            if (root is null) throw new ArgumentException("A shadow root sequence cannot contain null.", nameof(shadowRoots));
            if (seen.Add(root)) copy.Add(root);
        }

        _shadowRoots = copy.AsReadOnly();
    }

    /// <summary>Controls noscript escaping; never executes script.</summary>
    public bool ScriptingEnabled { get; }
    /// <summary>Includes reachable attached roots marked serializable.</summary>
    public bool SerializableShadowRoots { get; }
    /// <summary>Explicitly selected roots, including closed roots; unreachable roots are not appended.</summary>
    public IReadOnlyList<ShadowRoot> ShadowRoots => _shadowRoots;

    /// <summary>An optional consumer view over descendants; null serializes the tree unchanged.</summary>
    internal Serialization.HtmlSerializationFilter? Filter { get; init; }
}
