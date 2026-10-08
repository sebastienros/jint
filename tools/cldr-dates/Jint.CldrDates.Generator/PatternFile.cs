using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Jint.CldrDates.Generator;

internal sealed record PatternBlock(string Language, int Offset, int CompressedLength, int RawLength, List<string> Locales);

internal sealed record PatternFileResult(byte[] Bytes, int IndexRawLength, int IndexCompressedLength, List<PatternBlock> Blocks, int ParentCount, string PayloadSha256);

/// <summary>
/// Writes and reads <c>DateTimePatterns.bin</c>. The layout is described in <c>tools/cldr-dates/README.md</c>.
/// </summary>
internal static class PatternFile
{
    /// <summary>
    /// 2 since the records carry intervalFormats (issue #4158's formatRange); 1 had none.
    /// </summary>
    internal const byte FormatVersion = 2;

    internal static ReadOnlySpan<byte> Magic => "JDTP"u8;

    internal static PatternFileResult Write(string cldrVersion, List<CldrLocale> locales, ParentLocales parents)
    {
        var byId = locales.ToDictionary(l => l.Id, StringComparer.Ordinal);
        using var payload = new MemoryStream();
        using var blockArea = new MemoryStream();
        var blocks = new List<PatternBlock>();

        foreach (var group in locales.GroupBy(l => LanguageOf(l.Id)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            using var raw = new MemoryStream();
            var ids = new List<string>();
            foreach (var locale in group.OrderBy(l => l.Id, StringComparer.Ordinal))
            {
                ids.Add(locale.Id);
                WriteRecord(raw, locale, parents.ParentOf(locale.Id) is { } parent ? byId[parent] : null);
            }

            var rawBytes = raw.ToArray();
            var compressed = Deflate(rawBytes);
            blocks.Add(new PatternBlock(group.Key, (int) blockArea.Length, compressed.Length, rawBytes.Length, ids));
            blockArea.Write(compressed);
            payload.Write(rawBytes);
        }

        using var index = new MemoryStream();
        WriteString(index, cldrVersion);
        WriteVarint(index, SlotLayout.Names.Length);
        foreach (var name in SlotLayout.Names)
        {
            WriteString(index, name);
        }

        WriteVarint(index, blocks.Count);
        foreach (var block in blocks)
        {
            WriteString(index, block.Language);
            WriteVarint(index, block.Offset);
            WriteVarint(index, block.CompressedLength);
            WriteVarint(index, block.RawLength);
            WriteVarint(index, block.Locales.Count);
            foreach (var id in block.Locales)
            {
                WriteString(index, id);
            }
        }

        var exceptions = locales
            .Select(l => (l.Id, Parent: parents.ParentOf(l.Id)))
            .Where(p => p.Parent is not null && !string.Equals(p.Parent, ParentLocales.Truncate(p.Id), StringComparison.Ordinal))
            .ToList();
        WriteVarint(index, exceptions.Count);
        foreach (var (id, parent) in exceptions)
        {
            WriteString(index, id);
            WriteString(index, parent!);
        }

        var indexRaw = index.ToArray();
        var indexCompressed = Deflate(indexRaw);

        using var file = new MemoryStream();
        file.Write(Magic);
        file.WriteByte(FormatVersion);
        WriteVarint(file, indexRaw.Length);
        WriteVarint(file, indexCompressed.Length);
        file.Write(indexCompressed);
        blockArea.Position = 0;
        blockArea.CopyTo(file);

        var hashed = new MemoryStream();
        hashed.Write(indexRaw);
        payload.Position = 0;
        payload.CopyTo(hashed);

        return new PatternFileResult(file.ToArray(), indexRaw.Length, indexCompressed.Length, blocks, exceptions.Count, Convert.ToHexStringLower(SHA256.HashData(hashed.ToArray())));
    }

    internal static string LanguageOf(string id)
    {
        var dash = id.IndexOf('-', StringComparison.Ordinal);
        return dash < 0 ? id : id[..dash];
    }

    /// <summary>
    /// A record: the slots that differ from the CLDR parent (every slot for the root, which has none) as gap-coded indexes and
    /// values; then the availableFormats entries that are new or differ; then the skeletons the parent has and this
    /// locale does not; then the same two lists for the interval patterns, keyed by skeleton and field.
    /// </summary>
    private static void WriteRecord(Stream stream, CldrLocale locale, CldrLocale? parent)
    {
        var slots = new List<int>();
        for (var slot = 0; slot < locale.Slots.Length; slot++)
        {
            if (parent is null || !string.Equals(parent.Slots[slot], locale.Slots[slot], StringComparison.Ordinal))
            {
                slots.Add(slot);
            }
        }

        WriteVarint(stream, slots.Count);
        var previous = -1;
        foreach (var slot in slots)
        {
            WriteVarint(stream, slot - previous - 1);
            WriteString(stream, locale.Slots[slot]);
            previous = slot;
        }

        var changed = locale.Formats
            .Where(f => parent is null || !parent.Formats.TryGetValue(f.Key, out var pattern) || !string.Equals(pattern, f.Value, StringComparison.Ordinal))
            .ToList();
        WriteVarint(stream, changed.Count);
        foreach (var (skeleton, pattern) in changed)
        {
            WriteString(stream, skeleton);
            WriteString(stream, pattern);
        }

        var removed = parent is null ? [] : parent.Formats.Keys.Where(k => !locale.Formats.ContainsKey(k)).ToList();
        WriteVarint(stream, removed.Count);
        foreach (var skeleton in removed)
        {
            WriteString(stream, skeleton);
        }

        var changedIntervals = locale.Intervals
            .Where(i => parent is null || !parent.Intervals.TryGetValue(i.Key, out var pattern) || !string.Equals(pattern, i.Value, StringComparison.Ordinal))
            .ToList();
        WriteVarint(stream, changedIntervals.Count);
        foreach (var (key, pattern) in changedIntervals)
        {
            WriteString(stream, key.Skeleton);
            stream.WriteByte(checked((byte) key.Field));
            WriteString(stream, pattern);
        }

        var removedIntervals = parent is null ? [] : parent.Intervals.Keys.Where(k => !locale.Intervals.ContainsKey(k)).ToList();
        WriteVarint(stream, removedIntervals.Count);
        foreach (var key in removedIntervals)
        {
            WriteString(stream, key.Skeleton);
            stream.WriteByte(checked((byte) key.Field));
        }
    }

    /// <summary>
    /// Decodes <paramref name="file"/> the way the loader does, independently of the writer, and requires every locale
    /// to come back exactly as it went in.
    /// </summary>
    internal static void VerifyRoundTrip(byte[] file, List<CldrLocale> expected)
    {
        var reader = new Reader(file);
        if (!reader.ReadBytes(4).SequenceEqual(Magic.ToArray()) || reader.ReadByte() != FormatVersion)
        {
            throw new InvalidDataException("round trip: bad header");
        }

        var indexRawLength = reader.ReadVarint();
        var indexCompressedLength = reader.ReadVarint();
        var index = new Reader(Inflate(reader.ReadBytes(indexCompressedLength), indexRawLength));
        var blockArea = reader.Position;

        index.ReadString();
        var slotCount = index.ReadVarint();
        for (var i = 0; i < slotCount; i++)
        {
            if (!string.Equals(index.ReadString(), SlotLayout.Names[i], StringComparison.Ordinal))
            {
                throw new InvalidDataException("round trip: slot names differ");
            }
        }

        var records = new Dictionary<string, DecodedRecord>(StringComparer.Ordinal);
        var blockCount = index.ReadVarint();
        for (var b = 0; b < blockCount; b++)
        {
            index.ReadString();
            var offset = index.ReadVarint();
            var compressedLength = index.ReadVarint();
            var rawLength = index.ReadVarint();
            var localeCount = index.ReadVarint();
            var block = new Reader(Inflate(file.AsSpan(blockArea + offset, compressedLength).ToArray(), rawLength));
            for (var l = 0; l < localeCount; l++)
            {
                var id = index.ReadString();
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

                records.Add(id, record);
            }

            if (!block.AtEnd)
            {
                throw new InvalidDataException("round trip: trailing bytes in a block");
            }
        }

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var parentCount = index.ReadVarint();
        for (var i = 0; i < parentCount; i++)
        {
            parents.Add(index.ReadString(), index.ReadString());
        }

        if (!index.AtEnd)
        {
            throw new InvalidDataException("round trip: trailing bytes in the index");
        }

        var resolved = new Dictionary<string, (string[] Slots, SortedDictionary<string, string> Formats, SortedDictionary<IntervalKey, string> Intervals)>(StringComparer.Ordinal);
        foreach (var locale in expected)
        {
            var (slots, formats, intervals) = Resolve(locale.Id);
            if (!slots.SequenceEqual(locale.Slots, StringComparer.Ordinal) || !formats.SequenceEqual(locale.Formats) || !intervals.SequenceEqual(locale.Intervals))
            {
                throw new InvalidDataException($"round trip: {locale.Id} does not decode to what was written");
            }
        }

        (string[] Slots, SortedDictionary<string, string> Formats, SortedDictionary<IntervalKey, string> Intervals) Resolve(string id)
        {
            if (resolved.TryGetValue(id, out var done))
            {
                return done;
            }

            var record = records[id];
            var parent = parents.TryGetValue(id, out var listed) ? listed : ParentLocales.Truncate(id);
            string[] slots;
            SortedDictionary<string, string> formats;
            SortedDictionary<IntervalKey, string> intervals;
            if (string.Equals(id, ParentLocales.Root, StringComparison.Ordinal))
            {
                slots = record.Slots;
                formats = new SortedDictionary<string, string>(record.Set, StringComparer.Ordinal);
                intervals = new SortedDictionary<IntervalKey, string>(record.SetIntervals);
            }
            else
            {
                var inherited = Resolve(parent);
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

    private sealed class DecodedRecord(string[] slots)
    {
        internal string[] Slots { get; } = slots;

        internal Dictionary<string, string> Set { get; } = new(StringComparer.Ordinal);

        internal List<string> Removed { get; } = [];

        internal Dictionary<IntervalKey, string> SetIntervals { get; } = [];

        internal List<IntervalKey> RemovedIntervals { get; } = [];
    }

    internal static byte[] Inflate(byte[] compressed, int rawLength)
    {
        using var inflate = new DeflateStream(new MemoryStream(compressed), CompressionMode.Decompress);
        var raw = new byte[rawLength];
        inflate.ReadExactly(raw);
        if (inflate.ReadByte() != -1)
        {
            throw new InvalidDataException("inflated past the recorded length");
        }

        return raw;
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        return output.ToArray();
    }

    private static void WriteVarint(Stream stream, int value)
    {
        var remaining = checked((uint) value);
        while (remaining >= 0x80)
        {
            stream.WriteByte((byte) (remaining | 0x80));
            remaining >>= 7;
        }

        stream.WriteByte((byte) remaining);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarint(stream, bytes.Length);
        stream.Write(bytes);
    }

    private sealed class Reader(byte[] bytes)
    {
        internal int Position { get; private set; }

        internal bool AtEnd => Position == bytes.Length;

        internal byte ReadByte() => bytes[Position++];

        internal byte[] ReadBytes(int count)
        {
            var result = bytes.AsSpan(Position, count).ToArray();
            Position += count;
            return result;
        }

        internal int ReadVarint()
        {
            var result = 0;
            for (var shift = 0; ; shift += 7)
            {
                var b = ReadByte();
                result |= (b & 0x7F) << shift;
                if (b < 0x80)
                {
                    return result;
                }
            }
        }

        internal string ReadString()
        {
            var length = ReadVarint();
            var value = Encoding.UTF8.GetString(bytes, Position, length);
            Position += length;
            return value;
        }
    }
}
