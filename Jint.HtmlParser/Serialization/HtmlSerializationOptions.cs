namespace Jint.HtmlParser.Serialization;

// A call receives a stable identity snapshot, never the caller's mutable sequence.
internal sealed class HtmlSerializationOptions
{
    internal static readonly HtmlSerializationOptions Default = new();
    private readonly IReadOnlyList<ShadowRoot> _shadowRoots;

    internal HtmlSerializationOptions(bool scriptingEnabled = false, bool serializableShadowRoots = false,
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

    internal bool ScriptingEnabled { get; }
    internal bool SerializableShadowRoots { get; }
    internal IReadOnlyList<ShadowRoot> ShadowRoots => _shadowRoots;
}
