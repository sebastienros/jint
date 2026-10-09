#if NET8_0_OR_GREATER

namespace Jint.WebApi.Fetch;

internal sealed partial class HttpResponseCache
{
    private string? _directory;
    private bool _temporary;
    private FileStream? _directoryLock;
    private bool _loading;

    private void OpenDisk(string? directory, bool temporary)
    {
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        // Fail construction if a second context/process is already using this partition. An explicit
        // identity opts into sequential persistence, never simultaneous sharing of a session's cache.
        _directory = directory;
        _temporary = temporary;
        _loading = true;
        try
        {
            _directoryLock = new FileStream(Path.Combine(directory, "cache.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            foreach (var path in Directory.EnumerateFiles(directory, "*.tmp")) TryDelete(path);
            foreach (var path in Directory.EnumerateFiles(directory, "*.entry"))
            {
                try
                {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    if (file.Length > _maxBytes || file.Length > _maxEntryBytes + 256L * 1024) { TryDelete(path); continue; }
                    if (file.Length < 36) throw new InvalidDataException();
                    var checksum = HashPrefix(file, file.Length - 32);
                    var storedChecksum = new byte[32];
                    file.ReadExactly(storedChecksum);
                    if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(checksum, storedChecksum)) throw new InvalidDataException();
                    file.Position = 0;
                    using var reader = new BinaryReader(file, System.Text.Encoding.UTF8);
                    if (reader.ReadInt32() != 1) throw new InvalidDataException();
                    var entry = new Entry
                    {
                        Key = ReadText(reader),
                        Partition = ReadText(reader),
                        Reason = ReadText(reader),
                        Received = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
                        InitialAgeSeconds = reader.ReadDouble(),
                        LifetimeSeconds = reader.ReadDouble(),
                        NoCache = reader.ReadBoolean(),
                        MustRevalidate = reader.ReadBoolean(),
                        RequestHeaders = ReadHeaders(reader),
                        ResponseHeaders = ReadHeaders(reader),
                    };
                    if (!double.IsFinite(entry.InitialAgeSeconds) || entry.InitialAgeSeconds is < 0 or > int.MaxValue
                        || !double.IsFinite(entry.LifetimeSeconds) || entry.LifetimeSeconds is < 0 or > int.MaxValue) throw new InvalidDataException();
                    var varyCount = reader.ReadInt32();
                    if (varyCount is < 0 or > 1024) throw new InvalidDataException();
                    entry.Vary = new string[varyCount];
                    for (var i = 0; i < varyCount; i++) entry.Vary[i] = ReadText(reader);
                    entry.ETag = ReadText(reader); if (entry.ETag.Length == 0) entry.ETag = null;
                    entry.LastModified = ReadText(reader); if (entry.LastModified.Length == 0) entry.LastModified = null;
                    var length = reader.ReadInt32();
                    if (length < 0 || length > _maxEntryBytes || length != file.Length - file.Position - 32) throw new InvalidDataException();
                    entry.Body = reader.ReadBytes(length);
                    entry.FileName = path;
                    using var probe = entry.Response(_clock.GetUtcNow());
                    Store(entry);
                    if (!_entries.TryGetValue(entry.Key, out var loaded) || !loaded.Contains(entry)) TryDelete(path);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or FormatException or OverflowException)
                {
                    TryDelete(path);
                }
            }
        }
        catch
        {
            CloseDisk();
            throw;
        }
        finally { _loading = false; }
    }

    private static byte[] HashPrefix(Stream stream, long count)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        Span<byte> buffer = stackalloc byte[8192];
        while (count > 0)
        {
            var read = stream.Read(buffer[..(int) Math.Min(count, buffer.Length)]);
            if (read == 0) throw new InvalidDataException();
            hash.AppendData(buffer[..read]);
            count -= read;
        }
        return hash.GetHashAndReset();
    }

    private static string ReadText(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length is < 0 or > 64 * 1024 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException();
        return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
    }
    private static void WriteText(BinaryWriter writer, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
    private static Dictionary<string, string[]> ReadHeaders(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count is < 0 or > 1024) throw new InvalidDataException();
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count; i++)
        {
            var name = ReadText(reader);
            var values = reader.ReadInt32();
            if (values is < 0 or > 1024) throw new InvalidDataException();
            var array = new string[values];
            for (var j = 0; j < values; j++) array[j] = ReadText(reader);
            headers.Add(name, array);
        }
        return headers;
    }
    private static void WriteHeaders(BinaryWriter writer, Dictionary<string, string[]> headers)
    {
        writer.Write(headers.Count);
        foreach (var header in headers)
        {
            WriteText(writer, header.Key);
            writer.Write(header.Value.Length);
            foreach (var value in header.Value) WriteText(writer, value);
        }
    }
    private void WriteDisk(Entry entry)
    {
        if (_directory is null || _loading) return;
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".entry");
        var temp = path + ".tmp";
        try
        {
            using (var writer = new BinaryWriter(File.Create(temp), System.Text.Encoding.UTF8))
            {
                writer.Write(1);
                WriteText(writer, entry.Key); WriteText(writer, entry.Partition); WriteText(writer, entry.Reason);
                writer.Write(entry.Received.UtcTicks); writer.Write(entry.InitialAgeSeconds); writer.Write(entry.LifetimeSeconds);
                writer.Write(entry.NoCache); writer.Write(entry.MustRevalidate);
                WriteHeaders(writer, entry.RequestHeaders); WriteHeaders(writer, entry.ResponseHeaders);
                writer.Write(entry.Vary.Length); foreach (var name in entry.Vary) WriteText(writer, name);
                WriteText(writer, entry.ETag ?? ""); WriteText(writer, entry.LastModified ?? "");
                writer.Write(entry.Body.Length); writer.Write(entry.Body);
            }
            using (var file = new FileStream(temp, FileMode.Open, FileAccess.ReadWrite))
            {
                var hash = HashPrefix(file, file.Length);
                file.Write(hash);
                // A cache may lose recent entries after a crash. Dispose flushes managed buffers before
                // atomic publication; the checksum rejects an incomplete entry on the next open.
            }
            File.Move(temp, path);
            entry.FileName = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cache IO is best effort after construction; a fetch must not fail because storage filled.
            TryDelete(temp);
        }
    }
    private static void DeleteDisk(Entry entry)
    {
        if (entry.FileName is { } path) File.Delete(path);
    }
    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    private void CloseDisk()
    {
        _directoryLock?.Dispose();
        _directoryLock = null;
        if (_temporary && _directory is { } directory)
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        _directory = null;
    }
}
#endif
