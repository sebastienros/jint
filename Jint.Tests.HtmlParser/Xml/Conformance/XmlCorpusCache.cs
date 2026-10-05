#nullable enable
using System.Security.Cryptography;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

/// <summary>
/// On-demand, digest-pinned download cache for the W3C corpus. Nothing here is committed: the archive is
/// fetched once into the gitignored <c>Cache/</c> folder and reused until its bytes stop matching the pin.
/// </summary>
internal static class XmlCorpusCache
{
    internal const string ArchiveUrl = "https://www.w3.org/XML/Test/xmlts20130923.tar.gz";
    internal const string ArchiveFileName = "xmlts20130923.tar.gz";
    private const int DownloadAttempts = 3;

    internal static string Directory => Path.Combine(XmlCorpus.Root, "Cache");

    internal static byte[] Archive() => GetOrDownload(Path.Combine(Directory, ArchiveFileName), ArchiveUrl, XmlCorpus.ArchiveDigest);

    private static byte[] GetOrDownload(string path, string url, string sha256)
    {
        if (TryReadVerified(path, sha256) is { } cached) return cached;

        Exception? last = null;
        for (var attempt = 1; attempt <= DownloadAttempts; attempt++)
        {
            try
            {
                var bytes = Download(url);
                var actual = Hash(bytes);
                if (actual != sha256)
                    throw new InvalidDataException($"SHA-256 mismatch for {url}: {actual}");
                WriteAtomically(path, bytes);
                return bytes;
            }
            catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or InvalidDataException)
            {
                last = error;
                if (attempt < DownloadAttempts) Thread.Sleep(TimeSpan.FromSeconds(attempt * 2));
            }
        }
        throw new InvalidDataException(
            $"Cannot download the pinned W3C XML corpus from {url} ({last?.Message}). " +
            $"Place the file with SHA-256 {sha256} at {path} to run offline.", last);
    }

    /// <summary>Returns the cached bytes when they match the pin; otherwise <c>null</c>, so a stale or torn file is replaced.</summary>
    internal static byte[]? TryReadVerified(string path, string sha256)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        return Hash(bytes) == sha256 ? bytes : null;
    }

    /// <summary>
    /// Writes through a unique sibling and renames it into place, so the net8.0 and net10.0 test processes
    /// that share this folder never observe a partial file.
    /// </summary>
    internal static void WriteAtomically(string path, byte[] bytes)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            try
            {
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A concurrent writer won the race (or holds the file open on Windows); its bytes are equivalent.
                if (TryReadVerified(path, Hash(bytes)) is null) throw;
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static byte[] Download(string url)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        return DownloadAsync(client, url, TimeSpan.FromMinutes(5)).GetAwaiter().GetResult();
    }

    // ResponseHeadersRead ends HttpClient.Timeout coverage at the headers. This deadline belongs to the
    // whole attempt, including reading the body, and cancellation is propagated to every async read.
    internal static async Task<byte[]> DownloadAsync(HttpClient client, string url, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        var cancellationToken = deadline.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
