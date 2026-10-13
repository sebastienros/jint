using System.Security.Cryptography;

namespace Jint.CldrDates.Generator;

/// <summary>
/// Writes and reads <c>DateTimeCalendarPatterns.bin</c>, the other calendars' counterpart of <c>DateTimePatterns.bin</c>.
/// The layout is described in <c>tools/cldr-dates/README.md</c>.
/// </summary>
/// <remarks>
/// It is a resource of its own so that a process formatting only Gregorian dates never reads it, and it follows
/// <c>DateTimePatterns.bin</c>'s locales: the same language blocks, the same locales in the same order, the same parents,
/// none of which it repeats. Within a block, each locale's records of every calendar are adjacent, which is what lets
/// deflate find what the calendars share — they are all the generic calendar's patterns with different eras and
/// months — at a quarter of what one block per calendar and language costs.
/// </remarks>
internal static class CalendarPatternFile
{
    internal const byte FormatVersion = 1;

    internal static ReadOnlySpan<byte> Magic => "JDTC"u8;

    internal static PatternFileResult Write(string cldrVersion, List<CalendarData> calendars, ParentLocales parents)
    {
        var byId = calendars.Select(c => c.Locales.ToDictionary(l => l.Id, StringComparer.Ordinal)).ToList();
        using var payload = new MemoryStream();
        using var blockArea = new MemoryStream();
        var blocks = new List<PatternBlock>();

        foreach (var group in calendars[0].Locales.Select(l => l.Id).GroupBy(PatternFile.LanguageOf).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            using var raw = new MemoryStream();
            var ids = group.Order(StringComparer.Ordinal).ToList();
            foreach (var id in ids)
            {
                for (var c = 0; c < calendars.Count; c++)
                {
                    var locale = byId[c][id];
                    PatternFile.WriteRecord(raw, locale, parents.ParentOf(id) is { } parent ? byId[c][parent] : null);
                    var ownKeys = calendars[c].OwnKeys[id];
                    PatternFile.WriteVarint(raw, ownKeys.Length);
                    foreach (var key in ownKeys)
                    {
                        PatternFile.WriteString(raw, key);
                    }
                }
            }

            var rawBytes = raw.ToArray();
            var compressed = PatternFile.Deflate(rawBytes);
            blocks.Add(new PatternBlock(group.Key, (int) blockArea.Length, compressed.Length, rawBytes.Length, ids));
            blockArea.Write(compressed);
            payload.Write(rawBytes);
        }

        using var index = new MemoryStream();
        PatternFile.WriteString(index, cldrVersion);
        PatternFile.WriteVarint(index, calendars.Count);
        foreach (var calendar in calendars)
        {
            var spec = calendar.Spec;
            PatternFile.WriteString(index, spec.CldrName);
            PatternFile.WriteVarint(index, spec.Identifiers.Length);
            foreach (var identifier in spec.Identifiers)
            {
                PatternFile.WriteString(index, identifier);
            }

            PatternFile.WriteVarint(index, spec.MonthCount);
            PatternFile.WriteVarint(index, spec.EraKeys.Length);
            foreach (var key in spec.EraKeys)
            {
                PatternFile.WriteString(index, key);
            }

            var names = spec.SlotNames;
            PatternFile.WriteVarint(index, names.Length);
            foreach (var name in names)
            {
                PatternFile.WriteString(index, name);
            }
        }

        PatternFile.WriteVarint(index, blocks.Count);
        foreach (var block in blocks)
        {
            PatternFile.WriteString(index, block.Language);
            PatternFile.WriteVarint(index, block.Offset);
            PatternFile.WriteVarint(index, block.CompressedLength);
            PatternFile.WriteVarint(index, block.RawLength);
            PatternFile.WriteVarint(index, block.Locales.Count);
        }

        var indexRaw = index.ToArray();
        var indexCompressed = PatternFile.Deflate(indexRaw);

        using var file = new MemoryStream();
        file.Write(Magic);
        file.WriteByte(FormatVersion);
        PatternFile.WriteVarint(file, indexRaw.Length);
        PatternFile.WriteVarint(file, indexCompressed.Length);
        file.Write(indexCompressed);
        blockArea.Position = 0;
        blockArea.CopyTo(file);

        var hashed = new MemoryStream();
        hashed.Write(indexRaw);
        payload.Position = 0;
        payload.CopyTo(hashed);

        return new PatternFileResult(file.ToArray(), indexRaw.Length, indexCompressed.Length, blocks, 0, Convert.ToHexStringLower(SHA256.HashData(hashed.ToArray())));
    }

    /// <summary>
    /// Decodes <paramref name="file"/> the way the loader does, independently of the writer, resolving every locale of
    /// every calendar through <paramref name="parents"/>, and requires each to come back exactly as it went in.
    /// </summary>
    internal static void VerifyRoundTrip(byte[] file, List<CalendarData> expected, ParentLocales parents)
    {
        var reader = new PatternFile.Reader(file);
        if (!reader.ReadBytes(4).SequenceEqual(Magic.ToArray()) || reader.ReadByte() != FormatVersion)
        {
            throw new InvalidDataException("calendar round trip: bad header");
        }

        var indexRawLength = reader.ReadVarint();
        var indexCompressedLength = reader.ReadVarint();
        var index = new PatternFile.Reader(PatternFile.Inflate(reader.ReadBytes(indexCompressedLength), indexRawLength));
        var blockArea = reader.Position;

        index.ReadString();
        var calendarCount = index.ReadVarint();
        if (calendarCount != expected.Count)
        {
            throw new InvalidDataException("calendar round trip: calendar count differs");
        }

        var slotCounts = new int[calendarCount];
        for (var c = 0; c < calendarCount; c++)
        {
            var spec = expected[c].Spec;
            var ok = string.Equals(index.ReadString(), spec.CldrName, StringComparison.Ordinal);
            var identifiers = new string[index.ReadVarint()];
            for (var i = 0; i < identifiers.Length; i++)
            {
                identifiers[i] = index.ReadString();
            }

            ok &= identifiers.SequenceEqual(spec.Identifiers, StringComparer.Ordinal) && index.ReadVarint() == spec.MonthCount;
            var eraKeys = new string[index.ReadVarint()];
            for (var i = 0; i < eraKeys.Length; i++)
            {
                eraKeys[i] = index.ReadString();
            }

            var names = new string[index.ReadVarint()];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = index.ReadString();
            }

            if (!ok || !eraKeys.SequenceEqual(spec.EraKeys, StringComparer.Ordinal) || !names.SequenceEqual(spec.SlotNames, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"calendar round trip: {spec.CldrName}'s index entry differs");
            }

            slotCounts[c] = names.Length;
        }

        var records = new Dictionary<(int Calendar, string Locale), DecodedRecord>();
        var localeOrder = expected[0].Locales.Select(l => l.Id).GroupBy(PatternFile.LanguageOf).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        var blockCount = index.ReadVarint();
        if (blockCount != localeOrder.Count)
        {
            throw new InvalidDataException("calendar round trip: block count differs");
        }

        for (var b = 0; b < blockCount; b++)
        {
            if (!string.Equals(index.ReadString(), localeOrder[b].Key, StringComparison.Ordinal))
            {
                throw new InvalidDataException("calendar round trip: languages differ");
            }

            var offset = index.ReadVarint();
            var compressedLength = index.ReadVarint();
            var rawLength = index.ReadVarint();
            var ids = localeOrder[b].Order(StringComparer.Ordinal).ToList();
            if (index.ReadVarint() != ids.Count)
            {
                throw new InvalidDataException("calendar round trip: a block's locale count differs");
            }

            var block = new PatternFile.Reader(PatternFile.Inflate(file.AsSpan(blockArea + offset, compressedLength).ToArray(), rawLength));
            foreach (var id in ids)
            {
                for (var c = 0; c < calendarCount; c++)
                {
                    records.Add((c, id), DecodedRecord.Read(block, slotCounts[c]));
                }
            }

            if (!block.AtEnd)
            {
                throw new InvalidDataException("calendar round trip: trailing bytes in a block");
            }
        }

        if (!index.AtEnd)
        {
            throw new InvalidDataException("calendar round trip: trailing bytes in the index");
        }

        for (var c = 0; c < calendarCount; c++)
        {
            var resolved = new Dictionary<string, (string[] Slots, SortedDictionary<string, string> Formats, SortedDictionary<IntervalKey, string> Intervals)>(StringComparer.Ordinal);
            foreach (var locale in expected[c].Locales)
            {
                var (slots, formats, intervals) = Resolve(c, locale.Id);
                if (!slots.SequenceEqual(locale.Slots, StringComparer.Ordinal)
                    || !formats.SequenceEqual(locale.Formats)
                    || !intervals.SequenceEqual(locale.Intervals)
                    || !records[(c, locale.Id)].OwnKeys.SequenceEqual(expected[c].OwnKeys[locale.Id], StringComparer.Ordinal))
                {
                    throw new InvalidDataException($"calendar round trip: {locale.Id} {expected[c].Spec.CldrName} does not decode to what was written");
                }
            }

            (string[] Slots, SortedDictionary<string, string> Formats, SortedDictionary<IntervalKey, string> Intervals) Resolve(int calendar, string id)
            {
                if (resolved.TryGetValue(id, out var done))
                {
                    return done;
                }

                var record = records[(calendar, id)];
                var parent = parents.ParentOf(id);
                string[] slots;
                SortedDictionary<string, string> formats;
                SortedDictionary<IntervalKey, string> intervals;
                if (parent is null)
                {
                    slots = record.Slots;
                    formats = new SortedDictionary<string, string>(record.Set, StringComparer.Ordinal);
                    intervals = new SortedDictionary<IntervalKey, string>(record.SetIntervals);
                }
                else
                {
                    var inherited = Resolve(calendar, parent);
                    slots = [.. inherited.Slots];
                    for (var i = 0; i < slots.Length; i++)
                    {
                        slots[i] = record.Slots[i] ?? slots[i];
                    }

                    formats = new SortedDictionary<string, string>(inherited.Formats, StringComparer.Ordinal);
                    foreach (var skeleton in record.Removed)
                    {
                        formats.Remove(skeleton);
                    }

                    foreach (var (skeleton, pattern) in record.Set)
                    {
                        formats[skeleton] = pattern;
                    }

                    intervals = new SortedDictionary<IntervalKey, string>(inherited.Intervals);
                    foreach (var key in record.RemovedIntervals)
                    {
                        intervals.Remove(key);
                    }

                    foreach (var (key, pattern) in record.SetIntervals)
                    {
                        intervals[key] = pattern;
                    }
                }

                resolved[id] = (slots, formats, intervals);
                return (slots, formats, intervals);
            }
        }
    }

    private sealed class DecodedRecord(string[] slots)
    {
        internal string[] Slots { get; } = slots;

        internal Dictionary<string, string> Set { get; } = new(StringComparer.Ordinal);

        internal List<string> Removed { get; } = [];

        internal Dictionary<IntervalKey, string> SetIntervals { get; } = [];

        internal List<IntervalKey> RemovedIntervals { get; } = [];

        internal List<string> OwnKeys { get; } = [];

        internal static DecodedRecord Read(PatternFile.Reader block, int slotCount)
        {
            var record = new DecodedRecord(new string[slotCount]);
            var count = block.ReadVarint();
            var slot = -1;
            for (var i = 0; i < count; i++)
            {
                slot += block.ReadVarint() + 1;
                record.Slots[slot] = block.ReadString();
            }

            count = block.ReadVarint();
            for (var i = 0; i < count; i++)
            {
                record.Set.Add(block.ReadString(), block.ReadString());
            }

            count = block.ReadVarint();
            for (var i = 0; i < count; i++)
            {
                record.Removed.Add(block.ReadString());
            }

            count = block.ReadVarint();
            for (var i = 0; i < count; i++)
            {
                var skeleton = block.ReadString();
                record.SetIntervals.Add(new IntervalKey(skeleton, (char) block.ReadByte()), block.ReadString());
            }

            count = block.ReadVarint();
            for (var i = 0; i < count; i++)
            {
                var skeleton = block.ReadString();
                record.RemovedIntervals.Add(new IntervalKey(skeleton, (char) block.ReadByte()));
            }

            count = block.ReadVarint();
            for (var i = 0; i < count; i++)
            {
                record.OwnKeys.Add(block.ReadString());
            }

            return record;
        }
    }
}
