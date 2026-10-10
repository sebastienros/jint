using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Jint.Runtime;

namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR's Gregorian date and time patterns and names for every locale cldr-json carries, read from the embedded
/// <c>DateTimePatterns.bin</c>: the <c>[[LocaleData]]</c> an <c>Intl.DateTimeFormat</c> format record is chosen from
/// (https://tc39.es/ecma402/#sec-intl.datetimeformat-internal-slots).
/// </summary>
/// <remarks>
/// <para>
/// <c>Intl.DateTimeFormat</c> resolves a component bag and a <c>dateStyle</c>/<c>timeStyle</c> against it
/// (<see cref="DateTimePatternGenerator"/>), writes its names, and writes the <c>formatRange</c> of both through its
/// interval patterns (<see cref="DateTimeIntervalFormat"/>). The resource and its generator are described in
/// <c>DateTimePatternData.Data.cs</c> and <c>tools/cldr-dates/README.md</c>.
/// </para>
/// <para>
/// The records are deflated one block per language, and a block is inflated the first time a locale that needs it
/// is asked for: the locale's own language, the root's (every locale but the root is stored as what differs from
/// its CLDR parent), and a parent's in another language where CLDR says so (<c>nb</c> inherits from <c>no</c>,
/// <c>hi-Latn</c> from <c>en-IN</c>). Nothing else is read.
/// </para>
/// <para>
/// One <see cref="Shared"/> instance serves every engine in the process. The index, each block's records and each
/// locale's <see cref="DateTimePatternLocale"/> are published with <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>:
/// two engines asking for the same language at once may both inflate it, and both then use whichever was published
/// first. Everything published is immutable.
/// </para>
/// </remarks>
internal sealed partial class DateTimePatternData
{
    /// <summary>
    /// The root locale, as cldr-json names it: what a locale no data covers falls back to.
    /// </summary>
    internal const string Root = "und";

    private const string ResourceName = "Jint.Native.Intl.Data.DateTimePatterns.bin";
    private const byte FormatVersion = 2;

    internal static readonly DateTimePatternData Shared = new(OpenEmbeddedResource);

    private readonly Func<Stream> _openResource;
    private Index? _index;

    /// <param name="openResource">Opens a seekable stream over the resource; called once for the index and once per block inflated.</param>
    internal DateTimePatternData(Func<Stream> openResource)
    {
        _openResource = openResource;
    }

    /// <summary>
    /// Every locale the data carries, in ordinal order within each language block.
    /// </summary>
    internal IReadOnlyList<string> Locales => GetIndex().LocaleIds;

    /// <summary>
    /// The data for the CLDR locale that serves <paramref name="locale"/>; see <see cref="ResolveLocale"/>.
    /// </summary>
    internal DateTimePatternLocale GetLocale(string locale)
    {
        var index = GetIndex();
        return GetView(index, index.Ordinals[ResolveLocale(locale)]);
    }

    /// <summary>
    /// The CLDR locale whose data serves the BCP 47 tag <paramref name="locale"/>: the tag itself if the data carries
    /// it, and otherwise the nearest locale that stands in for it, or <see cref="Root"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extensions are ignored, and variants too unless the data carries the tag with them (<c>ca-ES-valencia</c>).
    /// cldr-json leaves out the default-content locales — <c>de-DE</c>, <c>en-US</c>, <c>zh-Hant-TW</c> — because
    /// their data is their parent's, so a tag the data does not carry is maximized through the likely subtags to a
    /// language, script and region, and the forms CLDR names locales by are tried in turn.
    /// </para>
    /// <para>
    /// When the script is the language's likely one, CLDR usually leaves it out of the name (<c>de-AT</c>) but not
    /// always (<c>sr-Cyrl-BA</c>), so the order is language-region, language-script-region, language,
    /// language-script: <c>de-DE</c> is served by <c>de</c>, <c>sr-RS</c> by <c>sr</c> rather than <c>sr-Cyrl</c>,
    /// which has the same data. Any other script is never dropped, so the order is language-script-region,
    /// language-script: <c>sr-ME</c> (likely <c>sr-Latn-ME</c>) is served by <c>sr-Latn-ME</c>, and <c>zh-TW</c>
    /// (likely <c>zh-Hant-TW</c>) by <c>zh-Hant</c>, never by <c>zh</c>, which is Simplified.
    /// </para>
    /// </remarks>
    internal string ResolveLocale(string locale)
    {
        var index = GetIndex();
        var baseName = LikelySubtags.ExtractBaseName(locale);
        if (index.TryGetLocale(baseName, out var id))
        {
            return id;
        }

        LikelySubtags.ParseBaseName(baseName, out var language, out var script, out var region, out var variants);
        if (language is null)
        {
            return Root;
        }

        var withoutVariants = LikelySubtags.BuildBaseTag(language, script, region);
        if (variants.Count > 0 && index.TryGetLocale(withoutVariants, out id))
        {
            return id;
        }

        LikelySubtags.ParseBaseName(LikelySubtags.AddLikelySubtags(withoutVariants), out var maximizedLanguage, out var maximizedScript, out var maximizedRegion, out _);
        maximizedLanguage ??= language;
        maximizedScript ??= script;
        maximizedRegion ??= region;

        string? likelyScript = null;
        if (maximizedScript is not null)
        {
            LikelySubtags.ParseBaseName(LikelySubtags.AddLikelySubtags(maximizedLanguage), out _, out likelyScript, out _, out _);
        }

        if (maximizedScript is null || string.Equals(maximizedScript, likelyScript, StringComparison.Ordinal))
        {
            if (TryCandidate(index, maximizedLanguage, null, maximizedRegion, out id)
                || TryCandidate(index, maximizedLanguage, maximizedScript, maximizedRegion, out id)
                || TryCandidate(index, maximizedLanguage, null, null, out id)
                || TryCandidate(index, maximizedLanguage, maximizedScript, null, out id))
            {
                return id;
            }
        }
        else if (TryCandidate(index, maximizedLanguage, maximizedScript, maximizedRegion, out id)
                 || TryCandidate(index, maximizedLanguage, maximizedScript, null, out id))
        {
            return id;
        }

        return Root;
    }

    /// <summary>
    /// The locale <paramref name="locale"/>, which must be one the data carries, inherits from under CLDR's
    /// parent-locale rules (https://www.unicode.org/reports/tr35/#Parent_Locales), or <see langword="null"/> for the
    /// root.
    /// </summary>
    /// <remarks>
    /// The generator records every parent that is not the locale with its last subtag removed: the explicit table
    /// (<c>en-150</c> to <c>en-001</c>, <c>zh-Hant</c> to the root), the rule that a non-likely script inherits from
    /// the root (<c>zh-Latn</c>), and a truncation that would land on a default-content locale cldr-json leaves out
    /// (<c>ca-ES-valencia</c> to <c>ca</c>).
    /// </remarks>
    internal string? GetParentLocale(string locale)
    {
        var index = GetIndex();
        if (!index.TryGetLocale(locale, out var id))
        {
            Throw.ArgumentException("The date/time pattern data carries no locale " + locale + ".", nameof(locale));
        }

        return ParentOf(index, id);
    }

    /// <summary>
    /// Whether a locale of <paramref name="language"/> has been asked for, so its block is inflated.
    /// </summary>
    internal bool IsLanguageInflated(string language)
    {
        var index = GetIndex();
        for (var block = 0; block < index.Blocks.Length; block++)
        {
            if (string.Equals(index.Blocks[block].Language, language, StringComparison.Ordinal))
            {
                return Volatile.Read(ref index.BlockRecords[block]) is not null;
            }
        }

        return false;
    }

    private static bool TryCandidate(Index index, string language, string? script, string? region, out string id)
    {
        return index.TryGetLocale(LikelySubtags.BuildBaseTag(language, script, region), out id);
    }

    private static string? ParentOf(Index index, string id)
    {
        if (string.Equals(id, Root, StringComparison.Ordinal))
        {
            return null;
        }

        if (index.Parents.TryGetValue(id, out var parent))
        {
            return parent;
        }

        var dash = id.LastIndexOf('-');
        return dash < 0 ? Root : id.Substring(0, dash);
    }

    private DateTimePatternLocale GetView(Index index, int ordinal)
    {
        var view = Volatile.Read(ref index.Views[ordinal]);
        if (view is not null)
        {
            return view;
        }

        var id = index.LocaleIds[ordinal];
        var record = GetRecord(index, ordinal);
        var parent = ParentOf(index, id);
        if (parent is null)
        {
            if (record.SlotIndexes.Length != DateTimePatternLocale.SlotCount || record.RemovedSkeletons.Length != 0 || record.RemovedIntervals.Length != 0)
            {
                Throw.InvalidOperationException("The root's date/time pattern record is incomplete.");
            }

            view = new DateTimePatternLocale(id, record.SlotValues, record.SetFormats, parent: null, record.SetFormats, record.SetIntervals);
        }
        else
        {
            view = GetView(index, index.Ordinals[parent]).Derive(id, in record);
        }

        return Interlocked.CompareExchange(ref index.Views[ordinal], view, null) ?? view;
    }

    private DateTimePatternRecord GetRecord(Index index, int ordinal)
    {
        var block = index.LocaleBlocks[ordinal];
        var records = Volatile.Read(ref index.BlockRecords[block]);
        if (records is null)
        {
            var inflated = ReadBlock(in index.Blocks[block]);
            records = Interlocked.CompareExchange(ref index.BlockRecords[block], inflated, null) ?? inflated;
        }

        return records[ordinal - index.Blocks[block].FirstLocale];
    }

    private DateTimePatternRecord[] ReadBlock(in BlockEntry block)
    {
        var reader = new ByteReader(Inflate(block.Offset, block.CompressedLength, block.RawLength));
        var records = new DateTimePatternRecord[block.LocaleCount];
        for (var i = 0; i < records.Length; i++)
        {
            var slotCount = reader.ReadVarint();
            var slotIndexes = new byte[slotCount];
            var slotValues = new string[slotCount];
            var slot = -1;
            for (var j = 0; j < slotCount; j++)
            {
                slot += reader.ReadVarint() + 1;
                if (slot >= DateTimePatternLocale.SlotCount)
                {
                    Throw.InvalidOperationException("The date/time pattern data is corrupt.");
                }

                slotIndexes[j] = (byte) slot;
                slotValues[j] = reader.ReadString();
            }

            var set = new DateTimeSkeletonPattern[reader.ReadVarint()];
            for (var j = 0; j < set.Length; j++)
            {
                set[j] = new DateTimeSkeletonPattern(reader.ReadString(), reader.ReadString());
                if (j > 0 && string.CompareOrdinal(set[j - 1].Skeleton, set[j].Skeleton) >= 0)
                {
                    Throw.InvalidOperationException("The date/time pattern data is corrupt.");
                }
            }

            var removed = new string[reader.ReadVarint()];
            for (var j = 0; j < removed.Length; j++)
            {
                removed[j] = reader.ReadString();
            }

            var setIntervals = new DateTimeIntervalPattern[reader.ReadVarint()];
            for (var j = 0; j < setIntervals.Length; j++)
            {
                var skeleton = reader.ReadString();
                setIntervals[j] = new DateTimeIntervalPattern(skeleton, reader.ReadField(), reader.ReadString());
                if (j > 0 && DateTimeIntervalPattern.Compare(in setIntervals[j - 1], in setIntervals[j]) >= 0)
                {
                    Throw.InvalidOperationException("The date/time pattern data is corrupt.");
                }
            }

            var removedIntervals = new DateTimeIntervalPattern[reader.ReadVarint()];
            for (var j = 0; j < removedIntervals.Length; j++)
            {
                var skeleton = reader.ReadString();
                removedIntervals[j] = new DateTimeIntervalPattern(skeleton, reader.ReadField(), "");
            }

            records[i] = new DateTimePatternRecord(slotIndexes, slotValues, set, removed, setIntervals, removedIntervals);
        }

        if (!reader.AtEnd)
        {
            Throw.InvalidOperationException("The date/time pattern data is corrupt.");
        }

        return records;
    }

    private Index GetIndex()
    {
        var index = Volatile.Read(ref _index);
        if (index is not null)
        {
            return index;
        }

        var read = ReadIndex();
        return Interlocked.CompareExchange(ref _index, read, null) ?? read;
    }

    private Index ReadIndex()
    {
        int rawLength;
        int compressedLength;
        long indexStart;
        using (var stream = _openResource())
        {
            if (stream.ReadByte() != 'J' || stream.ReadByte() != 'D' || stream.ReadByte() != 'T' || stream.ReadByte() != 'P' || stream.ReadByte() != FormatVersion)
            {
                Throw.InvalidOperationException("The embedded date/time pattern data has an unknown format.");
            }

            rawLength = ReadVarint(stream);
            compressedLength = ReadVarint(stream);
            indexStart = stream.Position;
        }

        var reader = new ByteReader(Inflate(indexStart, compressedLength, rawLength));
        if (!string.Equals(reader.ReadString(), CldrVersion, StringComparison.Ordinal))
        {
            Throw.InvalidOperationException("The embedded date/time pattern data does not match DateTimePatternData.Data.cs; regenerate both with tools/cldr-dates.");
        }

        var slotNames = new string[reader.ReadVarint()];
        for (var i = 0; i < slotNames.Length; i++)
        {
            slotNames[i] = reader.ReadString();
        }

        CheckLayout(slotNames);

        var blockAreaStart = indexStart + compressedLength;
        var blocks = new BlockEntry[reader.ReadVarint()];
        var localeIds = new List<string>(LocaleCount);
        var localeBlocks = new List<int>(LocaleCount);
        for (var block = 0; block < blocks.Length; block++)
        {
            var language = reader.ReadString();
            var offset = blockAreaStart + reader.ReadVarint();
            var blockCompressedLength = reader.ReadVarint();
            var blockRawLength = reader.ReadVarint();
            var localeCount = reader.ReadVarint();
            blocks[block] = new BlockEntry(language, offset, blockCompressedLength, blockRawLength, localeIds.Count, localeCount);
            for (var i = 0; i < localeCount; i++)
            {
                localeIds.Add(reader.ReadString());
                localeBlocks.Add(block);
            }
        }

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var parentCount = reader.ReadVarint();
        for (var i = 0; i < parentCount; i++)
        {
            parents.Add(reader.ReadString(), reader.ReadString());
        }

        if (!reader.AtEnd || localeIds.Count != LocaleCount)
        {
            Throw.InvalidOperationException("The embedded date/time pattern data does not match DateTimePatternData.Data.cs; regenerate both with tools/cldr-dates.");
        }

        return new Index(blocks, localeIds.ToArray(), localeBlocks.ToArray(), parents);
    }

    /// <summary>
    /// The first slot of each group the offsets in <see cref="DateTimePatternLocale"/> name must be the value the
    /// resource has there, or the resource was generated with a different layout.
    /// </summary>
    private static void CheckLayout(string[] slotNames)
    {
        if (slotNames.Length != DateTimePatternLocale.SlotCount
            || slotNames.Length != SlotCount
            || !string.Equals(slotNames[DateTimePatternLocale.DateTimeFormatsStart], "dateTimeFormats/full", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.AtTimeFormatsStart], "dateTimeFormats-atTime/standard/full", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.DateFormatsStart], "dateFormats/full", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.TimeFormatsStart], "timeFormats/full", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.AppendItemsStart], "dateTimeFormats/appendItems/Era", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.FieldDisplayNamesStart], "fields/era/displayName", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.MonthsStart], "months/format/abbreviated/1", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.WeekdaysStart], "days/format/abbreviated/sun", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.ErasStart], "eras/eraAbbr/0", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.DayPeriodsStart], "dayPeriods/format/abbreviated/am", StringComparison.Ordinal)
            || !string.Equals(slotNames[DateTimePatternLocale.IntervalFallbackSlot], "dateTimeFormats/intervalFormats/intervalFormatFallback", StringComparison.Ordinal))
        {
            Throw.InvalidOperationException("The embedded date/time pattern data has a slot layout DateTimePatternLocale does not read; update both together.");
        }
    }

    private byte[] Inflate(long offset, int compressedLength, int rawLength)
    {
        using var stream = _openResource();
        if (offset + compressedLength > stream.Length)
        {
            Throw.InvalidOperationException("The embedded date/time pattern data is truncated.");
        }

        stream.Position = offset;
        using var inflate = new DeflateStream(stream, CompressionMode.Decompress);
        var raw = new byte[rawLength];
        var read = 0;
        while (read < raw.Length)
        {
            var count = inflate.Read(raw, read, raw.Length - read);
            if (count == 0)
            {
                Throw.InvalidOperationException("The embedded date/time pattern data is truncated.");
            }

            read += count;
        }

        return raw;
    }

    private static int ReadVarint(Stream stream)
    {
        var result = 0;
        for (var shift = 0; shift < 32; shift += 7)
        {
            var b = stream.ReadByte();
            if (b < 0)
            {
                break;
            }

            result |= (b & 0x7F) << shift;
            if (b < 0x80)
            {
                return result;
            }
        }

        Throw.InvalidOperationException("The embedded date/time pattern data is corrupt.");
        return 0;
    }

    private static Stream OpenEmbeddedResource()
    {
        // A constant name looked up on the declaring type's own assembly: nothing here depends on metadata a trimmer
        // could remove, and trimming and Native AOT keep an assembly's manifest resources.
        return typeof(DateTimePatternData).Assembly.GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException("Could not load the embedded Intl date/time pattern data.");
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BlockEntry
    {
        internal BlockEntry(string language, long offset, int compressedLength, int rawLength, int firstLocale, int localeCount)
        {
            Language = language;
            Offset = offset;
            CompressedLength = compressedLength;
            RawLength = rawLength;
            FirstLocale = firstLocale;
            LocaleCount = localeCount;
        }

        internal string Language { get; }

        internal long Offset { get; }

        internal int CompressedLength { get; }

        internal int RawLength { get; }

        internal int FirstLocale { get; }

        internal int LocaleCount { get; }
    }

    private sealed class Index
    {
        internal Index(BlockEntry[] blocks, string[] localeIds, int[] localeBlocks, Dictionary<string, string> parents)
        {
            Blocks = blocks;
            LocaleIds = localeIds;
            LocaleBlocks = localeBlocks;
            Parents = parents;
            Ordinals = new Dictionary<string, int>(localeIds.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < localeIds.Length; i++)
            {
                Ordinals.Add(localeIds[i], i);
            }

            BlockRecords = new DateTimePatternRecord[]?[blocks.Length];
            Views = new DateTimePatternLocale?[localeIds.Length];
        }

        internal BlockEntry[] Blocks { get; }

        internal string[] LocaleIds { get; }

        internal int[] LocaleBlocks { get; }

        internal Dictionary<string, string> Parents { get; }

        internal Dictionary<string, int> Ordinals { get; }

        /// <summary>
        /// Each block's records, published once it is inflated.
        /// </summary>
        internal readonly DateTimePatternRecord[]?[] BlockRecords;

        /// <summary>
        /// Each locale's resolved data, published once it is built.
        /// </summary>
        internal readonly DateTimePatternLocale?[] Views;

        /// <summary>
        /// Looks a tag up case-insensitively and answers the locale as the data names it.
        /// </summary>
        internal bool TryGetLocale(string tag, out string id)
        {
            if (Ordinals.TryGetValue(tag, out var ordinal))
            {
                id = LocaleIds[ordinal];
                return true;
            }

            id = null!;
            return false;
        }
    }

    private sealed class ByteReader
    {
        private readonly byte[] _bytes;
        private int _position;

        internal ByteReader(byte[] bytes)
        {
            _bytes = bytes;
        }

        internal bool AtEnd => _position == _bytes.Length;

        internal int ReadVarint()
        {
            var result = 0;
            for (var shift = 0; shift < 32; shift += 7)
            {
                if ((uint) _position >= (uint) _bytes.Length)
                {
                    break;
                }

                var b = _bytes[_position++];
                result |= (b & 0x7F) << shift;
                if (b < 0x80)
                {
                    return result;
                }
            }

            Throw.InvalidOperationException("The embedded date/time pattern data is corrupt.");
            return 0;
        }

        /// <summary>An interval pattern's field: one of <c>G y M d a h m</c>.</summary>
        internal char ReadField()
        {
            if ((uint) _position >= (uint) _bytes.Length || "GyMdahm".IndexOf((char) _bytes[_position]) < 0)
            {
                Throw.InvalidOperationException("The date/time pattern data is corrupt.");
            }

            return (char) _bytes[_position++];
        }

        internal string ReadString()
        {
            var length = ReadVarint();
            if (length > _bytes.Length - _position)
            {
                Throw.InvalidOperationException("The embedded date/time pattern data is corrupt.");
            }

            var value = Encoding.UTF8.GetString(_bytes, _position, length);
            _position += length;
            return value;
        }
    }
}
