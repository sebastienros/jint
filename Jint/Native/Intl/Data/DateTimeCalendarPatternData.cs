using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Jint.Runtime;

namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR's date and time patterns and names for the calendars other than the Gregorian one, for every locale
/// <see cref="DateTimePatternData"/> carries, read from the embedded <c>DateTimeCalendarPatterns.bin</c>: what an
/// <c>Intl.DateTimeFormat</c> whose calendar is one of them resolves its format record against and writes its names
/// with (https://tc39.es/ecma402/#sec-intl.datetimeformat-internal-slots).
/// </summary>
/// <remarks>
/// <para>
/// Ten calendars are carried: <c>buddhist</c>, <c>japanese</c>, <c>roc</c>, <c>hebrew</c>, <c>persian</c>,
/// <c>indian</c>, <c>coptic</c>, <c>ethiopic</c>, <c>ethioaa</c> and the Islamic ones, which CLDR gives one set of
/// data. <c>chinese</c> and <c>dangi</c> are not: they write a related year and a year name, in a lane of their own.
/// The generator, <c>tools/cldr-dates</c>, writes each calendar the way ICU reads it, which is not always the way
/// cldr-json resolves it; <c>Jint.CldrDates.Generator/Calendars.cs</c> says where the two part.
/// </para>
/// <para>
/// A calendar's data is a <see cref="DateTimePatternLocale"/> like the Gregorian one, built over it: its weekday and
/// day-period names, field display names and the <c>dateTimeFormats</c> that join a date and a time in a range are the
/// Gregorian locale's, as they are in ICU, and the rest is the calendar's own. Its parent is the same locale's parent's
/// view of the same calendar, so the format matcher walks it as it walks the Gregorian data.
/// </para>
/// <para>
/// The resource follows <c>DateTimePatterns.bin</c>'s language blocks and locales without repeating them, and holds
/// each locale's records of every calendar side by side, which is what deflates them well. A block is inflated the
/// first time a calendar is asked for in its language, and only that calendar's records are read out of it. Everything
/// is published as <see cref="DateTimePatternData"/> publishes it: with <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>,
/// immutable once published.
/// </para>
/// </remarks>
internal sealed partial class DateTimeCalendarPatternData
{
    private const string ResourceName = "Jint.Native.Intl.Data.DateTimeCalendarPatterns.bin";
    private const byte FormatVersion = 1;

    /// <summary>The calendar slots before the months: date, time and atTime formats, appendItems and the interval fallback.</summary>
    private const int MonthsStart = 24;

    internal static readonly DateTimeCalendarPatternData Shared = new(OpenEmbeddedResource, DateTimePatternData.Shared);

    /// <summary>
    /// The ECMA-402 calendar identifiers the data carries, by the calendar's position in the resource, in the order the
    /// generator writes them; checked against the resource's own list when it is first read.
    /// </summary>
    private static readonly string[][] Identifiers =
    [
        ["buddhist"],
        ["japanese"],
        ["roc"],
        ["hebrew"],
        ["persian"],
        ["indian"],
        ["coptic"],
        ["ethiopic"],
        ["ethioaa"],
        ["islamic", "islamic-civil", "islamic-rgsa", "islamic-tbla", "islamic-umalqura"],
    ];

    private readonly Func<Stream> _openResource;
    private readonly DateTimePatternData _gregorian;
    private Index? _index;

    /// <param name="openResource">Opens a seekable stream over the resource; called once for the index and once per block inflated.</param>
    /// <param name="gregorian">The Gregorian data whose locales, parents and names this data is laid over.</param>
    internal DateTimeCalendarPatternData(Func<Stream> openResource, DateTimePatternData gregorian)
    {
        _openResource = openResource;
        _gregorian = gregorian;
    }

    /// <summary>
    /// The position of the calendar whose data serves the ECMA-402 identifier <paramref name="calendar"/>, or -1 for a
    /// calendar the data does not carry — <c>gregory</c>, <c>iso8601</c>, <c>chinese</c>, <c>dangi</c>, a host's — which
    /// is written with the Gregorian data. Answered without reading the resource.
    /// </summary>
    internal static int CalendarOf(string? calendar)
    {
        if (calendar is null)
        {
            return -1;
        }

        for (var i = 0; i < Identifiers.Length; i++)
        {
            foreach (var identifier in Identifiers[i])
            {
                if (string.Equals(identifier, calendar, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// The CLDR era keys the calendar at <paramref name="calendar"/> carries names for, in the order
    /// <see cref="DateTimePatternLocale.GetEraNames"/> holds them.
    /// </summary>
    internal ReadOnlySpan<string> EraKeys(int calendar) => GetIndex().Calendars[calendar].EraKeys;

    /// <summary>
    /// The data of the calendar at <paramref name="calendar"/> (<see cref="CalendarOf"/>) for the CLDR locale that
    /// serves the BCP 47 tag <paramref name="locale"/> (<see cref="DateTimePatternData.ResolveLocale"/>).
    /// </summary>
    internal DateTimePatternLocale GetLocale(string locale, int calendar)
    {
        var index = GetIndex();
        if ((uint) calendar >= (uint) index.Calendars.Length)
        {
            Throw.ArgumentOutOfRangeException(nameof(calendar), "The date/time pattern data carries no such calendar.");
        }

        var gregorian = _gregorian.GetLocale(locale);
        return GetLevel(index, calendar, index.Ordinals[gregorian.Locale]).View;
    }

    private Level GetLevel(Index index, int calendar, int ordinal)
    {
        ref var slot = ref index.Levels[calendar * index.LocaleIds.Length + ordinal];
        var level = Volatile.Read(ref slot);
        if (level is not null)
        {
            return level;
        }

        var id = index.LocaleIds[ordinal];
        var record = GetRecord(index, calendar, ordinal);
        var layout = index.Calendars[calendar];
        var parentId = _gregorian.GetParentLocale(id);
        string[] slots;
        DateTimeSkeletonPattern[] formats;
        DateTimeIntervalPattern[] intervals;
        Level? parent = null;
        if (parentId is null)
        {
            if (record.Record.SlotIndexes.Length != layout.SlotCount || record.Record.RemovedSkeletons.Length != 0 || record.Record.RemovedIntervals.Length != 0)
            {
                Throw.InvalidOperationException("The root's calendar pattern record is incomplete.");
            }

            slots = record.Record.SlotValues;
            formats = record.Record.SetFormats;
            intervals = record.Record.SetIntervals;
        }
        else
        {
            parent = GetLevel(index, calendar, index.Ordinals[parentId]);
            slots = (string[]) parent.Slots.Clone();
            for (var i = 0; i < record.Record.SlotIndexes.Length; i++)
            {
                slots[record.Record.SlotIndexes[i]] = record.Record.SlotValues[i];
            }

            formats = DateTimePatternLocale.Merge(parent.Formats, record.Record.SetFormats, record.Record.RemovedSkeletons);
            intervals = DateTimePatternLocale.MergeIntervals(parent.Intervals, record.Record.SetIntervals, record.Record.RemovedIntervals);
        }

        // The Gregorian locale's values with the calendar's patterns over them. The dateTimeFormats stay the Gregorian
        // ones, which is what ICU's DateIntervalFormat joins a range's date and time with whatever the calendar.
        var gregorianSlots = _gregorian.GetLocale(id).CopySlots();
        System.Array.Copy(slots, 0, gregorianSlots, DateTimePatternLocale.DateFormatsStart, 4);
        System.Array.Copy(slots, 4, gregorianSlots, DateTimePatternLocale.TimeFormatsStart, 4);
        System.Array.Copy(slots, 8, gregorianSlots, DateTimePatternLocale.AtTimeFormatsStart, 4);

        System.Array.Copy(slots, 12, gregorianSlots, DateTimePatternLocale.AppendItemsStart, 11);
        gregorianSlots[DateTimePatternLocale.IntervalFallbackSlot] = slots[23];

        string[]? monthNames = null;
        if (layout.MonthCount > 0)
        {
            monthNames = new string[layout.MonthCount * 6];
            System.Array.Copy(slots, MonthsStart, monthNames, 0, monthNames.Length);
        }

        var eraNames = new string[layout.EraKeys.Length * 3];
        System.Array.Copy(slots, MonthsStart + layout.MonthCount * 6, eraNames, 0, eraNames.Length);

        var view = new DateTimePatternLocale(
            id,
            gregorianSlots,
            formats,
            parent?.View,
            record.Record.SetFormats,
            intervals,
            layout.Name,
            record.OwnKeys,
            monthNames,
            layout.MonthCount,
            eraNames,
            layout.EraKeys.Length);
        level = new Level(slots, formats, intervals, view);
        return Interlocked.CompareExchange(ref slot, level, null) ?? level;
    }

    private CalendarRecord GetRecord(Index index, int calendar, int ordinal)
    {
        var block = index.LocaleBlocks[ordinal];
        ref var recordsSlot = ref index.Records[block * index.Calendars.Length + calendar];
        var records = Volatile.Read(ref recordsSlot);
        if (records is null)
        {
            var bytes = Volatile.Read(ref index.BlockBytes[block]);
            if (bytes is null)
            {
                ref readonly var entry = ref index.Blocks[block];
                var inflated = Inflate(entry.Offset, entry.CompressedLength, entry.RawLength);
                bytes = Interlocked.CompareExchange(ref index.BlockBytes[block], inflated, null) ?? inflated;
            }

            var read = ReadRecords(index, in index.Blocks[block], bytes, calendar);
            records = Interlocked.CompareExchange(ref recordsSlot, read, null) ?? read;
        }

        return records[ordinal - index.Blocks[block].FirstLocale];
    }

    /// <summary>
    /// The records of one calendar out of an inflated block, which holds every calendar's record of each locale in turn;
    /// the other calendars' are stepped over without being read.
    /// </summary>
    private static CalendarRecord[] ReadRecords(Index index, in BlockEntry block, byte[] bytes, int calendar)
    {
        var reader = new ByteReader(bytes);
        var records = new CalendarRecord[block.LocaleCount];
        for (var locale = 0; locale < records.Length; locale++)
        {
            for (var c = 0; c < index.Calendars.Length; c++)
            {
                if (c == calendar)
                {
                    records[locale] = ReadRecord(ref reader, index.Calendars[c].SlotCount);
                }
                else
                {
                    SkipRecord(ref reader);
                }
            }
        }

        if (!reader.AtEnd)
        {
            Throw.InvalidOperationException("The calendar pattern data is corrupt.");
        }

        return records;
    }

    private static CalendarRecord ReadRecord(ref ByteReader reader, int slotCount)
    {
        var count = reader.ReadVarint();
        var slotIndexes = new byte[count];
        var slotValues = new string[count];
        var slot = -1;
        for (var j = 0; j < count; j++)
        {
            slot += reader.ReadVarint() + 1;
            if (slot >= slotCount || slot > byte.MaxValue)
            {
                Throw.InvalidOperationException("The calendar pattern data is corrupt.");
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
                Throw.InvalidOperationException("The calendar pattern data is corrupt.");
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
                Throw.InvalidOperationException("The calendar pattern data is corrupt.");
            }
        }

        var removedIntervals = new DateTimeIntervalPattern[reader.ReadVarint()];
        for (var j = 0; j < removedIntervals.Length; j++)
        {
            var skeleton = reader.ReadString();
            removedIntervals[j] = new DateTimeIntervalPattern(skeleton, reader.ReadField(), "");
        }

        var ownKeys = new string[reader.ReadVarint()];
        for (var j = 0; j < ownKeys.Length; j++)
        {
            ownKeys[j] = reader.ReadString();
        }

        return new CalendarRecord(new DateTimePatternRecord(slotIndexes, slotValues, set, removed, setIntervals, removedIntervals), ownKeys);
    }

    private static void SkipRecord(ref ByteReader reader)
    {
        var count = reader.ReadVarint();
        for (var j = 0; j < count; j++)
        {
            reader.ReadVarint();
            reader.SkipString();
        }

        count = reader.ReadVarint();
        for (var j = 0; j < count; j++)
        {
            reader.SkipString();
            reader.SkipString();
        }

        count = reader.ReadVarint();
        for (var j = 0; j < count; j++)
        {
            reader.SkipString();
        }

        count = reader.ReadVarint();
        for (var j = 0; j < count; j++)
        {
            reader.SkipString();
            reader.ReadField();
            reader.SkipString();
        }

        count = reader.ReadVarint();
        for (var j = 0; j < count; j++)
        {
            reader.SkipString();
            reader.ReadField();
        }

        count = reader.ReadVarint();
        for (var j = 0; j < count; j++)
        {
            reader.SkipString();
        }
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
            if (stream.ReadByte() != 'J' || stream.ReadByte() != 'D' || stream.ReadByte() != 'T' || stream.ReadByte() != 'C' || stream.ReadByte() != FormatVersion)
            {
                Throw.InvalidOperationException("The embedded calendar pattern data has an unknown format.");
            }

            rawLength = ReadVarint(stream);
            compressedLength = ReadVarint(stream);
            indexStart = stream.Position;
        }

        var reader = new ByteReader(Inflate(indexStart, compressedLength, rawLength));
        if (!string.Equals(reader.ReadString(), CldrVersion, StringComparison.Ordinal)
            || !string.Equals(CldrVersion, DateTimePatternData.CldrVersion, StringComparison.Ordinal))
        {
            Throw.InvalidOperationException("The embedded calendar pattern data does not match DateTimeCalendarPatternData.Data.cs; regenerate both with tools/cldr-dates.");
        }

        var calendars = new CalendarLayout[reader.ReadVarint()];
        if (calendars.Length != CalendarCount || calendars.Length != Identifiers.Length)
        {
            Throw.InvalidOperationException("The embedded calendar pattern data does not match DateTimeCalendarPatternData.Data.cs; regenerate both with tools/cldr-dates.");
        }

        for (var c = 0; c < calendars.Length; c++)
        {
            var name = reader.ReadString();
            var identifiers = new string[reader.ReadVarint()];
            for (var i = 0; i < identifiers.Length; i++)
            {
                identifiers[i] = reader.ReadString();
            }

            var monthCount = reader.ReadVarint();
            var eraKeys = new string[reader.ReadVarint()];
            for (var i = 0; i < eraKeys.Length; i++)
            {
                eraKeys[i] = reader.ReadString();
            }

            var slotNames = new string[reader.ReadVarint()];
            for (var i = 0; i < slotNames.Length; i++)
            {
                slotNames[i] = reader.ReadString();
            }

            CheckLayout(identifiers, Identifiers[c], monthCount, eraKeys, slotNames);
            calendars[c] = new CalendarLayout(name, monthCount, eraKeys, slotNames.Length);
        }

        var localeIds = _gregorian.Locales;
        var blockAreaStart = indexStart + compressedLength;
        var blocks = new BlockEntry[reader.ReadVarint()];
        var localeBlocks = new int[localeIds.Count];
        var first = 0;
        for (var block = 0; block < blocks.Length; block++)
        {
            var language = reader.ReadString();
            var offset = blockAreaStart + reader.ReadVarint();
            var blockCompressedLength = reader.ReadVarint();
            var blockRawLength = reader.ReadVarint();
            var localeCount = reader.ReadVarint();
            if (first + localeCount > localeIds.Count || !string.Equals(LanguageOf(localeIds[first]), language, StringComparison.Ordinal))
            {
                Throw.InvalidOperationException("The embedded calendar pattern data does not follow DateTimePatterns.bin's locales; regenerate both with tools/cldr-dates.");
            }

            blocks[block] = new BlockEntry(offset, blockCompressedLength, blockRawLength, first, localeCount);
            for (var i = 0; i < localeCount; i++)
            {
                localeBlocks[first + i] = block;
            }

            first += localeCount;
        }

        if (!reader.AtEnd || first != localeIds.Count || first != LocaleCount)
        {
            Throw.InvalidOperationException("The embedded calendar pattern data does not match DateTimeCalendarPatternData.Data.cs; regenerate both with tools/cldr-dates.");
        }

        return new Index(calendars, blocks, localeIds, localeBlocks);
    }

    /// <summary>
    /// The index's own account of each calendar must be the one this class reads: the identifiers it serves, and its
    /// slots — the fixed ones by name, then the months and eras it says it has.
    /// </summary>
    private static void CheckLayout(string[] identifiers, string[] expected, int monthCount, string[] eraKeys, string[] slotNames)
    {
        var ok = identifiers.AsSpan().SequenceEqual(expected)
                 && monthCount is 0 or 12 or 13 or 14
                 && eraKeys.Length > 0
                 && slotNames.Length == MonthsStart + monthCount * 6 + eraKeys.Length * 3
                 && string.Equals(slotNames[0], "dateFormats/full", StringComparison.Ordinal)
                 && string.Equals(slotNames[4], "timeFormats/full", StringComparison.Ordinal)
                 && string.Equals(slotNames[8], "dateTimeFormats-atTime/standard/full", StringComparison.Ordinal)
                 && string.Equals(slotNames[12], "dateTimeFormats/appendItems/Era", StringComparison.Ordinal)
                 && string.Equals(slotNames[23], "dateTimeFormats/intervalFormats/intervalFormatFallback", StringComparison.Ordinal)
                 && string.Equals(slotNames[MonthsStart + monthCount * 6], "eras/eraAbbr/" + eraKeys[0], StringComparison.Ordinal);
        if (ok && monthCount > 0)
        {
            ok = string.Equals(slotNames[MonthsStart], "months/format/abbreviated/1", StringComparison.Ordinal)
                 && string.Equals(slotNames[MonthsStart + monthCount * 3], "months/stand-alone/abbreviated/1", StringComparison.Ordinal)
                 && (monthCount != 14 || string.Equals(slotNames[MonthsStart + 13], "months/format/abbreviated/7-yeartype-leap", StringComparison.Ordinal));
        }

        if (!ok)
        {
            Throw.InvalidOperationException("The embedded calendar pattern data has a layout DateTimeCalendarPatternData does not read; update both together.");
        }
    }

    private static string LanguageOf(string locale)
    {
        var dash = locale.IndexOf('-');
        return dash < 0 ? locale : locale.Substring(0, dash);
    }

    private byte[] Inflate(long offset, int compressedLength, int rawLength)
    {
        using var stream = _openResource();
        if (offset + compressedLength > stream.Length)
        {
            Throw.InvalidOperationException("The embedded calendar pattern data is truncated.");
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
                Throw.InvalidOperationException("The embedded calendar pattern data is truncated.");
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

        Throw.InvalidOperationException("The embedded calendar pattern data is corrupt.");
        return 0;
    }

    private static Stream OpenEmbeddedResource()
    {
        // As DateTimePatternData's: a constant name on the declaring type's own assembly, which trimming and Native AOT keep.
        return typeof(DateTimeCalendarPatternData).Assembly.GetManifestResourceStream(ResourceName)
               ?? throw new InvalidOperationException("Could not load the embedded Intl calendar pattern data.");
    }

    /// <summary>One calendar's account of itself in the index.</summary>
    private sealed class CalendarLayout
    {
        internal CalendarLayout(string name, int monthCount, string[] eraKeys, int slotCount)
        {
            Name = name;
            MonthCount = monthCount;
            EraKeys = eraKeys;
            SlotCount = slotCount;
        }

        /// <summary>CLDR's name for the calendar.</summary>
        internal string Name { get; }

        /// <summary>How many month names it has, 0 for the Gregorian months.</summary>
        internal int MonthCount { get; }

        internal string[] EraKeys { get; }

        internal int SlotCount { get; }
    }

    /// <summary>One locale's resolved data for one calendar: its own slots, formats and intervals, and the view over them.</summary>
    private sealed class Level
    {
        internal Level(string[] slots, DateTimeSkeletonPattern[] formats, DateTimeIntervalPattern[] intervals, DateTimePatternLocale view)
        {
            Slots = slots;
            Formats = formats;
            Intervals = intervals;
            View = view;
        }

        internal string[] Slots { get; }

        internal DateTimeSkeletonPattern[] Formats { get; }

        internal DateTimeIntervalPattern[] Intervals { get; }

        internal DateTimePatternLocale View { get; }
    }

    /// <summary>A record of the resource: <c>DateTimePatterns.bin</c>'s, and the availableFormats keys that are the calendar's own.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly struct CalendarRecord
    {
        internal CalendarRecord(DateTimePatternRecord record, string[] ownKeys)
        {
            Record = record;
            OwnKeys = ownKeys;
        }

        internal DateTimePatternRecord Record { get; }

        internal string[] OwnKeys { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BlockEntry
    {
        internal BlockEntry(long offset, int compressedLength, int rawLength, int firstLocale, int localeCount)
        {
            Offset = offset;
            CompressedLength = compressedLength;
            RawLength = rawLength;
            FirstLocale = firstLocale;
            LocaleCount = localeCount;
        }

        internal long Offset { get; }

        internal int CompressedLength { get; }

        internal int RawLength { get; }

        internal int FirstLocale { get; }

        internal int LocaleCount { get; }
    }

    private sealed class Index
    {
        internal Index(CalendarLayout[] calendars, BlockEntry[] blocks, IReadOnlyList<string> localeIds, int[] localeBlocks)
        {
            Calendars = calendars;
            Blocks = blocks;
            LocaleIds = new string[localeIds.Count];
            Ordinals = new Dictionary<string, int>(localeIds.Count, StringComparer.Ordinal);
            for (var i = 0; i < localeIds.Count; i++)
            {
                LocaleIds[i] = localeIds[i];
                Ordinals.Add(localeIds[i], i);
            }

            LocaleBlocks = localeBlocks;
            BlockBytes = new byte[]?[blocks.Length];
            Records = new CalendarRecord[]?[blocks.Length * calendars.Length];
            Levels = new Level?[calendars.Length * localeIds.Count];
        }

        internal CalendarLayout[] Calendars { get; }

        internal BlockEntry[] Blocks { get; }

        internal string[] LocaleIds { get; }

        internal Dictionary<string, int> Ordinals { get; }

        internal int[] LocaleBlocks { get; }

        /// <summary>Each block, inflated, once a calendar is first asked for in its language.</summary>
        internal readonly byte[]?[] BlockBytes;

        /// <summary>Each block's records of each calendar, once read.</summary>
        internal readonly CalendarRecord[]?[] Records;

        /// <summary>Each locale's data of each calendar, once built.</summary>
        internal readonly Level?[] Levels;
    }

    private struct ByteReader
    {
        private readonly byte[] _bytes;
        private int _position;

        internal ByteReader(byte[] bytes)
        {
            _bytes = bytes;
            _position = 0;
        }

        internal readonly bool AtEnd => _position == _bytes.Length;

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

            Throw.InvalidOperationException("The embedded calendar pattern data is corrupt.");
            return 0;
        }

        /// <summary>An interval pattern's field: one of <c>G y M d a h m</c>.</summary>
        internal char ReadField()
        {
            if ((uint) _position >= (uint) _bytes.Length || "GyMdahm".IndexOf((char) _bytes[_position]) < 0)
            {
                Throw.InvalidOperationException("The embedded calendar pattern data is corrupt.");
            }

            return (char) _bytes[_position++];
        }

        internal string ReadString()
        {
            var length = ReadVarint();
            if (length > _bytes.Length - _position)
            {
                Throw.InvalidOperationException("The embedded calendar pattern data is corrupt.");
            }

            var value = Encoding.UTF8.GetString(_bytes, _position, length);
            _position += length;
            return value;
        }

        internal void SkipString()
        {
            var length = ReadVarint();
            if (length > _bytes.Length - _position)
            {
                Throw.InvalidOperationException("The embedded calendar pattern data is corrupt.");
            }

            _position += length;
        }
    }
}
