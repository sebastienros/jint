namespace Jint.HtmlParser;

/// <summary>Options for a native DOM mutation registration.</summary>
public sealed class MutationObserverOptions
{
    public bool ChildList { get; init; }
    public bool Subtree { get; init; }
    public bool? Attributes { get; init; }
    public bool? CharacterData { get; init; }
    public bool? AttributeOldValue { get; init; }
    public bool? CharacterDataOldValue { get; init; }
    public IReadOnlyList<string>? AttributeFilter { get; init; }
}

internal sealed class NormalizedMutationOptions
{
    private NormalizedMutationOptions() { }

    internal bool ChildList { get; private init; }
    internal bool Subtree { get; private init; }
    internal bool Attributes { get; private init; }
    internal bool CharacterData { get; private init; }
    internal bool AttributeOldValue { get; private init; }
    internal bool CharacterDataOldValue { get; private init; }
    internal HashSet<string>? AttributeFilter { get; private init; }

    internal static NormalizedMutationOptions Create(MutationObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var attributes = options.Attributes ?? (options.AttributeOldValue is not null || options.AttributeFilter is not null);
        var characterData = options.CharacterData ?? (options.CharacterDataOldValue is not null);
        if ((!options.ChildList && !attributes && !characterData) ||
            (options.Attributes == false && (options.AttributeOldValue == true || options.AttributeFilter is not null)) ||
            (options.CharacterData == false && options.CharacterDataOldValue == true))
        {
            throw new ArgumentException("The requested mutation types and old-value/filter options are inconsistent.", nameof(options));
        }

        HashSet<string>? filter = null;
        if (options.AttributeFilter is { } input)
        {
            filter = new HashSet<string>(StringComparer.Ordinal);
            foreach (var localName in input)
            {
                if (localName is null)
                {
                    throw new ArgumentException("An attribute filter cannot contain null.", nameof(options));
                }

                filter.Add(localName);
            }
        }

        return new NormalizedMutationOptions
        {
            ChildList = options.ChildList,
            Subtree = options.Subtree,
            Attributes = attributes,
            CharacterData = characterData,
            AttributeOldValue = options.AttributeOldValue == true,
            CharacterDataOldValue = options.CharacterDataOldValue == true,
            AttributeFilter = filter
        };
    }

    internal bool Matches(MutationRecordKind kind, string? attributeName, string? attributeNamespace)
        => kind switch
        {
            MutationRecordKind.ChildList => ChildList,
            MutationRecordKind.CharacterData => CharacterData,
            _ => Attributes && (AttributeFilter is null ||
                attributeNamespace is null && attributeName is not null && AttributeFilter.Contains(attributeName))
        };
}
