#nullable enable

using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Test262Harness;
using Zio;

namespace Jint.Tests.Test262;

internal sealed class Test262CorpusAcquisition
{
    private const int MaximumAttempts = 3;

    private readonly Func<string, string, Task<Test262Stream>> _loadStaged;
    private readonly Func<TimeSpan, Task> _delay;
    private readonly string _expectedDigest;
    private readonly Action<string> _log;

    internal Test262CorpusAcquisition(
        Func<string, string, Task<Test262Stream>> loadStaged,
        Func<TimeSpan, Task> delay,
        string expectedDigest,
        Action<string>? log = null)
    {
        _loadStaged = loadStaged;
        _delay = delay;
        _expectedDigest = expectedDigest;
        _log = log ?? (_ => { });
    }

    internal static Test262CorpusAcquisition CreateDefault(string expectedDigest, Action<string> log)
        => new(
            static (sha, stagingDirectory) => Test262StreamExtensions.FromGitHub(sha, stagingDirectory),
            Task.Delay,
            expectedDigest,
            log);

    internal async Task<Test262Stream> LoadAsync(string commitSha, string cacheDirectory, bool offline = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        Directory.CreateDirectory(cacheDirectory);
        var archive = GetArchivePath(commitSha, cacheDirectory);
        if (File.Exists(archive))
        {
            try
            {
                return LoadAndValidate(archive, commitSha);
            }
            catch (Exception exception) when (!offline)
            {
                _log($"Ignoring invalid Test262 cache entry {archive}: {exception.Message}");
            }
            catch (Exception exception)
            {
                throw new Test262CorpusUnavailableException(commitSha, 1, exception);
            }
        }
        else if (offline)
        {
            var exception = new FileNotFoundException(
                $"Offline Test262 mode requires the pinned archive at {archive}.",
                archive);
            throw new Test262CorpusUnavailableException(commitSha, 1, exception);
        }

        for (var attempt = 1; ; attempt++)
        {
            var stagingDirectory = Path.Combine(
                cacheDirectory,
                $".test262-{commitSha}-{Environment.ProcessId}-{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(stagingDirectory);
                var stream = await _loadStaged(commitSha, stagingDirectory);
                try
                {
                    Validate(stream, commitSha);
                }
                finally
                {
                    stream.Options.FileSystem.Dispose();
                }

                var stagedArchive = GetArchivePath(commitSha, stagingDirectory);
                if (!File.Exists(stagedArchive))
                {
                    throw new InvalidDataException("The staged Test262 download did not produce an archive.");
                }

                // The unique staging directory is under the cache root, so this publishes a completely
                // validated archive with one same-volume rename. Concurrent processes may replace it only
                // with another archive that passed the same pinned tree digest.
                try
                {
                    File.Move(stagedArchive, archive, overwrite: true);
                }
                catch (IOException) when (File.Exists(archive))
                {
                    // Another process can win the same atomic publish. Accept its archive only after
                    // independently proving that it is the same pinned tree.
                    return LoadAndValidate(archive, commitSha);
                }

                return LoadAndValidate(archive, commitSha);
            }
            catch (Exception exception) when (IsTransient(exception) && attempt < MaximumAttempts)
            {
                var delay = TimeSpan.FromSeconds(attempt);
                _log($"Test262 corpus acquisition for {commitSha} failed on attempt {attempt}/{MaximumAttempts}: "
                     + $"{exception.Message}. Retrying in {delay.TotalSeconds:0} second(s).");
                await _delay(delay);
            }
            catch (Exception exception)
            {
                throw new Test262CorpusUnavailableException(commitSha, attempt, exception);
            }
            finally
            {
                TryDeleteDirectory(stagingDirectory);
            }
        }
    }

    private Test262Stream LoadAndValidate(string archive, string commitSha)
    {
        var stream = Test262Stream.FromZipArchive(archive, $"test262-{commitSha}");
        try
        {
            Validate(stream, commitSha);
            return stream;
        }
        catch
        {
            stream.Options.FileSystem.Dispose();
            throw;
        }
    }

    private void Validate(Test262Stream stream, string commitSha)
    {
        var actualDigest = ComputeDigest(stream);
        if (!actualDigest.Equals(_expectedDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The Test262 content digest for {commitSha} was {actualDigest}; expected {_expectedDigest}.");
        }
    }

    private static string ComputeDigest(Test262Stream stream)
    {
        var fileSystem = stream.Options.FileSystem;
        var paths = fileSystem.EnumerateFiles("/harness", "*", SearchOption.AllDirectories)
            .Concat(fileSystem.EnumerateFiles("/test", "*", SearchOption.AllDirectories))
            .Select(static path => path.FullName)
            .OrderBy(static path => path, StringComparer.Ordinal);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> pathLength = stackalloc byte[sizeof(int)];

        foreach (var path in paths)
        {
            var encodedPath = Encoding.UTF8.GetBytes(path);
            BinaryPrimitives.WriteInt32LittleEndian(pathLength, encodedPath.Length);
            hash.AppendData(pathLength);
            hash.AppendData(encodedPath);

            using var content = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            hash.AppendData(SHA256.HashData(content));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // A uniquely named staging directory cannot affect another run or be mistaken for the cache.
        }
    }

    private static string GetArchivePath(string commitSha, string directory)
        => Path.Combine(directory, $"test262-{commitSha}.zip");

    private static bool IsTransient(Exception exception)
    {
        if (exception is HttpRequestException httpException)
        {
            return httpException.StatusCode is null
                   or HttpStatusCode.RequestTimeout
                   or HttpStatusCode.TooManyRequests
                   || (int)httpException.StatusCode >= 500;
        }

        return exception is IOException or InvalidDataException or SocketException or TimeoutException or TaskCanceledException;
    }
}

internal sealed class Test262CorpusUnavailableException : Exception
{
    internal Test262CorpusUnavailableException(string commitSha, int attempts, Exception innerException)
        : base($"Could not acquire the pinned Test262 corpus {commitSha} after {attempts} attempt(s).", innerException)
    {
        CommitSha = commitSha;
        Attempts = attempts;
    }

    internal string CommitSha { get; }

    internal int Attempts { get; }
}
