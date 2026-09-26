namespace Jint.HtmlParser.Css.Values.References;

internal enum CssSubstitutionResultKind { Uninitialized, Tokens, GuaranteedInvalid, PendingFeature }

internal readonly struct CssSubstitutionContext
{
    internal CssSubstitutionContext(string propertyName, CssReferenceUse use, bool isAnimatable)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (use is < CssReferenceUse.PropertyValue or > CssReferenceUse.DescriptorValue)
            throw new ArgumentOutOfRangeException(nameof(use));
        if (use == CssReferenceUse.CustomPropertyValue)
        {
            if (!CssSubstitutionArguments.IsCustomName(propertyName) || !isAnimatable)
                throw new ArgumentException("A custom-property context must be animatable and have a custom name.");
        }
        else
        {
            if (propertyName.Length == 0 || CssSubstitutionArguments.IsCustomName(propertyName))
                throw new ArgumentException("An ordinary property name is required.", nameof(propertyName));
            for (var i = 0; i < propertyName.Length; i++)
            {
                if (propertyName[i] is >= 'A' and <= 'Z')
                    throw new ArgumentException("Ordinary property names must be ASCII-canonical.", nameof(propertyName));
            }
        }
        if (use == CssReferenceUse.DescriptorValue && isAnimatable)
            throw new ArgumentException("Descriptor contexts are not animatable.", nameof(isAnimatable));
        PropertyName = propertyName;
        Use = use;
        IsAnimatable = isAnimatable;
    }

    internal string PropertyName { get; }
    internal CssReferenceUse Use { get; }
    internal bool IsAnimatable { get; }
    internal bool IsValid => PropertyName is not null;
}

internal readonly struct CssSubstitutionResult
{
    private readonly CssSubstitutedValue? _value;
    private readonly string? _pendingFeature;

    private CssSubstitutionResult(CssSubstitutionResultKind kind, CssSubstitutedValue? value,
        string? pendingFeature)
    {
        Kind = kind;
        _value = value;
        _pendingFeature = pendingFeature;
    }

    internal CssSubstitutionResultKind Kind { get; }
    internal CssSubstitutedValue Value => Kind == CssSubstitutionResultKind.Tokens
        ? _value! : throw new InvalidOperationException();
    internal string PendingFeature => Kind == CssSubstitutionResultKind.PendingFeature
        ? _pendingFeature! : throw new InvalidOperationException();

    internal static CssSubstitutionResult Tokens(CssSubstitutedValue value) =>
        new(CssSubstitutionResultKind.Tokens, value ?? throw new ArgumentNullException(nameof(value)), null);
    internal static CssSubstitutionResult Invalid() =>
        new(CssSubstitutionResultKind.GuaranteedInvalid, null, null);
    internal static CssSubstitutionResult Pending(string feature) =>
        new(CssSubstitutionResultKind.PendingFeature, null,
            !string.IsNullOrEmpty(feature) ? feature : throw new ArgumentException("A feature is required.", nameof(feature)));
}
