using System.Text;

namespace Jint.Native.Intl;

/// <summary>
/// Reads and rewrites the Unicode locale extension sequence — the <c>-u-</c> singleton and the
/// attributes and keywords after it — of a BCP 47 language tag.
/// </summary>
/// <remarks>
/// <para>
/// One scanner for every Intl consumer, because the subtag rule is easy to write and easy to write
/// wrong: https://unicode.org/reports/tr35/#unicode_locale_extensions says a keyword is a two-character
/// key followed by zero or more 3-to-8-character type subtags, so a value ends at the next two-character
/// subtag and a sequence ends at the next singleton. Reading one key at a time by hand cost every
/// formatter that did so any tag carrying more than one key.
/// </para>
/// <para>
/// Nothing here validates the tag: the reading entry points answer for whatever a structurally valid tag
/// carries, and answer nothing for a tag that carries no <c>-u-</c> sequence at all.
/// </para>
/// </remarks>
internal static class UnicodeExtension
{
    /// <summary>
    /// Reads the value of one Unicode extension key, lowercased, or null when the tag has no such key.
    /// </summary>
    /// <remarks>
    /// A key written with no value at all — the <c>kn</c> of <c>de-DE-u-kn</c> — answers with the empty
    /// string, which is how a caller tells "present, and true by UTS 35 §3.2.1" from "absent".
    /// </remarks>
    internal static string? GetKeywordValue(string locale, string key)
    {
        foreach (var keyword in EnumerateKeywords(locale))
        {
            if (keyword.KeyIs(key))
            {
                return ToLowerString(keyword.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates the keywords of the tag's Unicode extension sequence, in the order they appear.
    /// </summary>
    internal static KeywordEnumerator EnumerateKeywords(string locale)
    {
        return TryLocateSequence(locale, out _, out var contentStart, out var contentEnd)
            ? new KeywordEnumerator(locale.AsSpan(contentStart, contentEnd - contentStart))
            : default;
    }

    /// <summary>
    /// Returns the tag's Unicode extension sequence, from its <c>u</c> singleton to the next one, or null.
    /// </summary>
    internal static string? GetSequence(string locale)
    {
        return TryLocateSequence(locale, out var sequenceStart, out _, out var contentEnd)
            ? locale.Substring(sequenceStart + 1, contentEnd - sequenceStart - 1)
            : null;
    }

    /// <summary>
    /// Returns the tag with its whole Unicode extension sequence removed, keeping every other extension.
    /// </summary>
    internal static string RemoveSequence(string locale)
    {
        if (!TryLocateSequence(locale, out var sequenceStart, out _, out var contentEnd))
        {
            return locale;
        }

        if (contentEnd >= locale.Length)
        {
            return locale.Substring(0, sequenceStart);
        }

        return string.Concat(locale.AsSpan(0, sequenceStart), locale.AsSpan(contentEnd));
    }

    /// <summary>
    /// Returns the tag carrying <paramref name="key"/> with <paramref name="value"/>, or without the key
    /// when the value is null, keeping every other keyword and extension.
    /// </summary>
    /// <remarks>
    /// Keys come back in ascending order, which is the canonical form
    /// https://unicode.org/reports/tr35/#Canonical_Unicode_Locale_Identifiers asks for; attributes are
    /// kept ahead of them. Removing the last keyword removes the <c>-u-</c> singleton with it.
    /// </remarks>
    internal static string WithKeyword(string locale, string key, string? value)
    {
        if (!TryLocateSequence(locale, out var sequenceStart, out var contentStart, out var contentEnd))
        {
            // No sequence to merge into: insert one, or leave a tag that already lacks the key alone.
            if (value is null)
            {
                return locale;
            }

            var keyword = value.Length == 0 ? "-u-" + key : "-u-" + key + "-" + value;
            var insertAt = SingletonInsertionPoint(locale);
            return insertAt >= locale.Length
                ? locale + keyword
                : locale.Insert(insertAt, keyword);
        }

        var attributes = new List<string>();
        var keywords = new List<KeyValuePair<string, string>>();
        var written = false;

        foreach (var attribute in EnumerateAttributes(locale.AsSpan(contentStart, contentEnd - contentStart)))
        {
            attributes.Add(attribute.ToString());
        }

        foreach (var keyword in new KeywordEnumerator(locale.AsSpan(contentStart, contentEnd - contentStart)))
        {
            if (keyword.KeyIs(key))
            {
                if (value is not null && !written)
                {
                    keywords.Add(new KeyValuePair<string, string>(key, value));
                    written = true;
                }

                continue;
            }

            keywords.Add(new KeyValuePair<string, string>(ToLowerString(keyword.Key), ToLowerString(keyword.Value)));
        }

        if (value is not null && !written)
        {
            keywords.Add(new KeyValuePair<string, string>(key, value));
        }

        keywords.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));

        var builder = new ValueStringBuilder(stackalloc char[64]);
        builder.Append(locale.AsSpan(0, sequenceStart));

        if (attributes.Count > 0 || keywords.Count > 0)
        {
            builder.Append("-u");
            foreach (var attribute in attributes)
            {
                builder.Append('-');
                builder.Append(attribute);
            }

            foreach (var keyword in keywords)
            {
                builder.Append('-');
                builder.Append(keyword.Key);
                if (keyword.Value.Length > 0)
                {
                    builder.Append('-');
                    builder.Append(keyword.Value);
                }
            }
        }

        if (contentEnd < locale.Length)
        {
            builder.Append(locale.AsSpan(contentEnd));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether a subtag of a Unicode extension sequence is a key rather than a value or an attribute.
    /// </summary>
    /// <remarks>
    /// https://unicode.org/reports/tr35/#unicode_locale_extensions: <c>key = alphanum alpha</c>, and every
    /// other subtag of the sequence is 3 to 8 characters, so length alone separates the two — the second
    /// character is checked because the grammar says so, not because anything hangs on it.
    /// </remarks>
    internal static bool IsKey(ReadOnlySpan<char> subtag)
    {
        return subtag.Length == 2 && IsAlphanumeric(subtag[0]) && IsAlpha(subtag[1]);
    }

    /// <summary>
    /// Locates the tag's Unicode extension sequence: <paramref name="sequenceStart"/> is the index of the
    /// hyphen before its <c>u</c> singleton, and the content between the other two indexes is everything
    /// after <c>-u-</c> and before the next singleton.
    /// </summary>
    private static bool TryLocateSequence(string locale, out int sequenceStart, out int contentStart, out int contentEnd)
    {
        sequenceStart = 0;
        contentStart = 0;
        contentEnd = 0;

        var length = locale.Length;
        var index = 0;
        while (index < length)
        {
            var end = SubtagEnd(locale, index);
            if (end - index == 1)
            {
                var singleton = locale[index];

                // A private-use sequence runs to the end of the tag, so nothing after it is an extension.
                if (singleton is 'x' or 'X')
                {
                    return false;
                }

                if (singleton is 'u' or 'U')
                {
                    // The singleton has to be preceded by a hyphen and followed by content: a tag that
                    // starts with it is not a tag, and one that ends with it carries no keyword.
                    if (index == 0 || end >= length)
                    {
                        return false;
                    }

                    sequenceStart = index - 1;
                    contentStart = end + 1;
                    contentEnd = SequenceEnd(locale, contentStart);
                    return contentStart < contentEnd;
                }
            }

            index = end + 1;
        }

        return false;
    }

    /// <summary>
    /// The index at which the sequence starting at <paramref name="start"/> ends: the hyphen before the
    /// next singleton, or the end of the tag.
    /// </summary>
    private static int SequenceEnd(string locale, int start)
    {
        var length = locale.Length;
        var index = start;
        while (index < length)
        {
            var end = SubtagEnd(locale, index);
            if (end - index == 1)
            {
                return index - 1;
            }

            index = end + 1;
        }

        return length;
    }

    private static int SubtagEnd(string locale, int start)
    {
        var index = locale.IndexOf('-', start);
        return index < 0 ? locale.Length : index;
    }

    private static int SubtagEnd(ReadOnlySpan<char> content, int start)
    {
        var index = content.Slice(start).IndexOf('-');
        return index < 0 ? content.Length : start + index;
    }

    /// <summary>
    /// Where a new <c>-u-</c> sequence belongs in a tag that has none: ahead of every extension whose
    /// singleton sorts after <c>u</c>, which is what keeps a private-use sequence last.
    /// </summary>
    private static int SingletonInsertionPoint(string locale)
    {
        var length = locale.Length;
        var index = 0;
        while (index < length)
        {
            var end = SubtagEnd(locale, index);
            if (end - index == 1 && char.ToLowerInvariant(locale[index]) > 'u')
            {
                return index - 1;
            }

            index = end + 1;
        }

        return length;
    }

    private static AttributeEnumerator EnumerateAttributes(ReadOnlySpan<char> content) => new(content);

    private static string ToLowerString(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (c is >= 'A' and <= 'Z')
            {
                return value.ToString().ToLowerInvariant();
            }
        }

        return value.ToString();
    }

    private static bool IsAlpha(char c) => (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z');

    private static bool IsAlphanumeric(char c) => IsAlpha(c) || (c is >= '0' and <= '9');

    /// <summary>
    /// One keyword of a Unicode extension sequence: a key, and the value subtags that follow it.
    /// </summary>
    internal readonly ref struct Keyword
    {
        internal Keyword(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        /// The two-character key, as written in the tag.
        /// </summary>
        internal ReadOnlySpan<char> Key { get; }

        /// <summary>
        /// The value subtags, hyphen-joined as written, and empty for a key written without one.
        /// </summary>
        internal ReadOnlySpan<char> Value { get; }

        /// <summary>
        /// Whether this keyword's key is <paramref name="key"/>, which callers pass in lowercase.
        /// </summary>
        internal bool KeyIs(string key) => Key.Equals(key.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Walks the keywords of a Unicode extension sequence's content, skipping its leading attributes.
    /// </summary>
    internal ref struct KeywordEnumerator
    {
        private readonly ReadOnlySpan<char> _content;
        private int _index;

        internal KeywordEnumerator(ReadOnlySpan<char> content)
        {
            _content = content;
            _index = 0;
            Current = default;
        }

        public Keyword Current { get; private set; }

        public readonly KeywordEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            while (_index < _content.Length)
            {
                var keyEnd = SubtagEnd(_content, _index);
                var key = _content.Slice(_index, keyEnd - _index);
                _index = keyEnd + 1;

                if (!IsKey(key))
                {
                    // An attribute, or a value subtag of a key this walk has already reported.
                    continue;
                }

                // The value runs to the next key, so every subtag until then belongs to this one. A key
                // written last leaves _index one past the content, which is an empty value, not a slice.
                var valueStart = _index <= _content.Length ? _index : _content.Length;
                var valueEnd = valueStart;
                while (_index < _content.Length)
                {
                    var subtagEnd = SubtagEnd(_content, _index);
                    if (IsKey(_content.Slice(_index, subtagEnd - _index)))
                    {
                        break;
                    }

                    valueEnd = subtagEnd;
                    _index = subtagEnd + 1;
                }

                Current = new Keyword(key, _content.Slice(valueStart, valueEnd - valueStart));
                return true;
            }

            Current = default;
            return false;
        }
    }

    /// <summary>
    /// Walks the attributes of a Unicode extension sequence's content — the subtags before its first key.
    /// </summary>
    private ref struct AttributeEnumerator
    {
        private readonly ReadOnlySpan<char> _content;
        private int _index;

        internal AttributeEnumerator(ReadOnlySpan<char> content)
        {
            _content = content;
            _index = 0;
            Current = default;
        }

        public ReadOnlySpan<char> Current { get; private set; }

        public readonly AttributeEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_index >= _content.Length)
            {
                return false;
            }

            var end = SubtagEnd(_content, _index);
            var subtag = _content.Slice(_index, end - _index);
            if (IsKey(subtag))
            {
                _index = _content.Length;
                return false;
            }

            _index = end + 1;
            Current = subtag;
            return true;
        }
    }
}
