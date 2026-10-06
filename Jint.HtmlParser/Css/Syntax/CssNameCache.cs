namespace Jint.HtmlParser.Css.Syntax;

// Shares the strings of short, frequently repeated tokens (property names, keywords, units,
// small numbers, indentation) across parses. Slots are overwritten on collision and read
// without locking: a torn read only costs a miss, because every hit compares the characters.
internal static class CssNameCache
{
    private const int MaxLength = 32;
    private const int Mask = 1023;
    private static readonly string?[] Entries = new string?[Mask + 1];

    internal static string Get(ReadOnlySpan<char> value)
    {
        switch (value.Length)
        {
            case 0:
                return string.Empty;
            case > MaxLength:
                return value.ToString();
        }

        var hash = (uint) value.Length;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619;
        }
        ref var slot = ref Entries[(int) (hash & Mask)];
        var cached = slot;
        if (cached is not null && value.SequenceEqual(cached))
        {
            return cached;
        }
        var created = value.ToString();
        slot = created;
        return created;
    }
}
