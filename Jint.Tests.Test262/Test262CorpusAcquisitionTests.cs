#nullable enable

using System.IO.Compression;
using System.Net;
using System.Net.Http;
using Jint.Tests.Test262;
using Test262Harness;

namespace Jint.Tests.Test262Infrastructure;

[TestFixture]
public sealed class Test262CorpusAcquisitionTests
{
    private const string TinyCorpusDigest = "c1467ff005a7345aa858548da8503b8be23fe0c52b38a8055568386dbf02c7cd";

    [Test]
    public async Task TransientFailureStagesAndPublishesTheSamePinnedCorpus()
    {
        const string sha = "0123456789abcdef";
        var cacheDirectory = NewCacheDirectory();
        var calls = new List<(string Sha, string StagingDirectory)>();
        var delays = new List<TimeSpan>();
        var acquisition = new Test262CorpusAcquisition(
            (actualSha, stagingDirectory) =>
            {
                calls.Add((actualSha, stagingDirectory));
                if (calls.Count == 1)
                {
                    return Task.FromException<Test262Stream>(new HttpRequestException("temporary DNS failure"));
                }

                var archive = Path.Combine(stagingDirectory, $"test262-{actualSha}.zip");
                CreateArchive(archive, actualSha);
                return Task.FromResult(Test262Stream.FromZipArchive(archive, $"test262-{actualSha}"));
            },
            delay =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            TinyCorpusDigest);

        try
        {
            var stream = await acquisition.LoadAsync(sha, cacheDirectory);
            using (stream.Options.FileSystem)
            {
                Assert.That(stream.Options.FileSystem.DirectoryExists("/test"), Is.True);
                Assert.That(File.Exists(Path.Combine(cacheDirectory, $"test262-{sha}.zip")), Is.True);
                Assert.That(calls, Has.Count.EqualTo(2));
                Assert.That(calls.Select(static call => call.Sha), Is.All.EqualTo(sha));
                Assert.That(calls.Select(static call => call.StagingDirectory), Is.All.Not.EqualTo(cacheDirectory));
                Assert.That(calls.Select(static call => call.StagingDirectory).Distinct().Count(), Is.EqualTo(2));
                Assert.That(Directory.EnumerateDirectories(cacheDirectory), Is.Empty);
                Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(1) }));
            }
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task IndependentAcquisitionsPublishSafelyToTheSameCache()
    {
        const string sha = "0123456789abcdef";
        var cacheDirectory = NewCacheDirectory();
        var bothDownloadsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;

        async Task<Test262Stream> Stage(
            string actualSha,
            string stagingDirectory,
            string marker,
            bool waitForRelease)
        {
            var archive = Path.Combine(stagingDirectory, $"test262-{actualSha}.zip");
            CreateArchive(archive, actualSha, marker);
            if (Interlocked.Increment(ref readyCount) == 2)
            {
                bothDownloadsReady.SetResult();
            }

            await bothDownloadsReady.Task;
            if (waitForRelease)
            {
                await releaseSecondDownload.Task;
            }

            return Test262Stream.FromZipArchive(archive, $"test262-{actualSha}");
        }

        var first = new Test262CorpusAcquisition(
            (actualSha, stagingDirectory) => Stage(actualSha, stagingDirectory, "first", waitForRelease: false),
            _ => Task.CompletedTask,
            TinyCorpusDigest);
        var second = new Test262CorpusAcquisition(
            (actualSha, stagingDirectory) => Stage(actualSha, stagingDirectory, "second", waitForRelease: true),
            _ => Task.CompletedTask,
            TinyCorpusDigest);
        Test262Stream? firstStream = null;
        Test262Stream? secondStream = null;

        try
        {
            var firstTask = first.LoadAsync(sha, cacheDirectory);
            var secondTask = second.LoadAsync(sha, cacheDirectory);
            firstStream = await firstTask;
            releaseSecondDownload.SetResult();
            secondStream = await secondTask;

            Assert.That(File.Exists(Path.Combine(cacheDirectory, $"test262-{sha}.zip")), Is.True);
            Assert.That(Directory.EnumerateDirectories(cacheDirectory), Is.Empty);

            var offline = new Test262CorpusAcquisition(
                (_, _) => throw new InvalidOperationException("A published cache entry must not download."),
                _ => Task.CompletedTask,
                TinyCorpusDigest);
            var offlineStream = await offline.LoadAsync(sha, cacheDirectory, offline: true);
            using (offlineStream.Options.FileSystem)
            {
                Assert.That(offlineStream, Is.Not.Null);
            }

            firstStream.Options.FileSystem.Dispose();
            firstStream = null;
            secondStream.Options.FileSystem.Dispose();
            secondStream = null;

            var archive = Path.Combine(cacheDirectory, $"test262-{sha}.zip");
            using var zip = ZipFile.OpenRead(archive);
            Assert.That(zip.GetEntry("first"), Is.Not.Null);
            Assert.That(zip.GetEntry("second"), Is.Null);
        }
        finally
        {
            releaseSecondDownload.TrySetResult();
            firstStream?.Options.FileSystem.Dispose();
            secondStream?.Options.FileSystem.Dispose();
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task InvalidOnlineCacheIsRemovedBeforePublishingItsReplacement()
    {
        const string sha = "0123456789abcdef";
        var cacheDirectory = NewCacheDirectory();
        var archive = Path.Combine(cacheDirectory, $"test262-{sha}.zip");
        CreatePartialArchive(archive, sha);
        var acquisition = new Test262CorpusAcquisition(
            (actualSha, stagingDirectory) =>
            {
                var stagedArchive = Path.Combine(stagingDirectory, $"test262-{actualSha}.zip");
                CreateArchive(stagedArchive, actualSha);
                return Task.FromResult(Test262Stream.FromZipArchive(stagedArchive, $"test262-{actualSha}"));
            },
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            var stream = await acquisition.LoadAsync(sha, cacheDirectory);
            using (stream.Options.FileSystem)
            {
                Assert.That(stream.Options.FileSystem.DirectoryExists("/test/language"), Is.True);
            }
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task OfflineModeUsesOnlyTheArchiveNamedForThePinnedCommit()
    {
        const string sha = "0123456789abcdef";
        var cacheDirectory = NewCacheDirectory();
        CreateArchive(Path.Combine(cacheDirectory, $"test262-{sha}.zip"), sha);
        var downloadCalls = 0;
        var acquisition = new Test262CorpusAcquisition(
            (_, _) =>
            {
                downloadCalls++;
                throw new InvalidOperationException("The network must not be used in offline mode.");
            },
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            var stream = await acquisition.LoadAsync(sha, cacheDirectory, offline: true);
            using (stream.Options.FileSystem)
            {
                Assert.That(stream.Options.FileSystem.DirectoryExists("/harness"), Is.True);
                Assert.That(stream.Options.FileSystem.DirectoryExists("/test"), Is.True);
                Assert.That(downloadCalls, Is.Zero);
            }
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public void OfflineModeFailsClosedWhenThePinnedArchiveIsAbsent()
    {
        var cacheDirectory = NewCacheDirectory();
        var downloadCalls = 0;
        var acquisition = new Test262CorpusAcquisition(
            (_, _) =>
            {
                downloadCalls++;
                throw new InvalidOperationException("The network must not be used in offline mode.");
            },
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            var exception = Assert.ThrowsAsync<Test262CorpusUnavailableException>(() =>
                acquisition.LoadAsync("0123456789abcdef", cacheDirectory, offline: true));

            Assert.That(exception!.InnerException, Is.TypeOf<FileNotFoundException>());
            Assert.That(downloadCalls, Is.Zero);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public void OfflineModeRejectsAPartialArchiveWithTheExactRootDirectories()
    {
        const string sha = "0123456789abcdef";
        var cacheDirectory = NewCacheDirectory();
        CreatePartialArchive(Path.Combine(cacheDirectory, $"test262-{sha}.zip"), sha);
        var acquisition = new Test262CorpusAcquisition(
            (_, _) => throw new InvalidOperationException("The network must not be used in offline mode."),
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            var exception = Assert.ThrowsAsync<Test262CorpusUnavailableException>(() =>
                acquisition.LoadAsync(sha, cacheDirectory, offline: true));

            Assert.That(exception!.InnerException, Is.TypeOf<InvalidDataException>());
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public void OfflineModeRejectsAnArchiveWhoseRootDoesNotMatchThePinnedCommit()
    {
        const string sha = "0123456789abcdef";
        var cacheDirectory = NewCacheDirectory();
        CreateArchive(Path.Combine(cacheDirectory, $"test262-{sha}.zip"), "different-commit");
        var acquisition = new Test262CorpusAcquisition(
            (_, _) => throw new InvalidOperationException("The network must not be used in offline mode."),
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            Assert.ThrowsAsync<Test262CorpusUnavailableException>(() =>
                acquisition.LoadAsync(sha, cacheDirectory, offline: true));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public void PermanentTransientFailureStopsAtTheBound()
    {
        const string sha = "fedcba9876543210";
        var cacheDirectory = NewCacheDirectory();
        var attempts = 0;
        var acquisition = new Test262CorpusAcquisition(
            (_, _) =>
            {
                attempts++;
                return Task.FromException<Test262Stream>(new HttpRequestException("DNS unavailable"));
            },
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            var exception = Assert.ThrowsAsync<Test262CorpusUnavailableException>(() =>
                acquisition.LoadAsync(sha, cacheDirectory));

            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(exception!.CommitSha, Is.EqualTo(sha));
            Assert.That(exception.Attempts, Is.EqualTo(3));
            Assert.That(exception.InnerException, Is.TypeOf<HttpRequestException>());
            Assert.That(Directory.EnumerateDirectories(cacheDirectory), Is.Empty);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public void PermanentHttpFailureIsReportedWithoutRetrying()
    {
        var cacheDirectory = NewCacheDirectory();
        var attempts = 0;
        var acquisition = new Test262CorpusAcquisition(
            (_, _) =>
            {
                attempts++;
                return Task.FromException<Test262Stream>(
                    new HttpRequestException("not found", null, HttpStatusCode.NotFound));
            },
            _ => Task.CompletedTask,
            TinyCorpusDigest);

        try
        {
            var exception = Assert.ThrowsAsync<Test262CorpusUnavailableException>(() =>
                acquisition.LoadAsync("0123456789abcdef", cacheDirectory));

            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(exception!.Attempts, Is.EqualTo(1));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task UnavailableCorpusProducesOneFailureAcrossConcurrentGeneratedCalls()
    {
        const int callCount = 16;
        var stream = Test262Stream.FromFileSystem(
            new UnavailableTest262FileSystem(new HttpRequestException("DNS unavailable")));

        var calls = Enumerable.Range(0, callCount).Select(_ => Task.Run(() =>
        {
            try
            {
                stream.GetTestFile("language/example.js");
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }));
        var exceptions = await Task.WhenAll(calls);

        Assert.That(exceptions.Count(static exception => exception is AssertionException), Is.EqualTo(1));
        Assert.That(exceptions.Count(static exception => exception is IgnoreException), Is.EqualTo(callCount - 1));
    }

    private static string NewCacheDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void CreateArchive(string archive, string sha, string? marker = null)
    {
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        if (marker is not null)
        {
            zip.CreateEntry(marker);
        }

        zip.CreateEntry($"test262-{sha}/");
        zip.CreateEntry($"test262-{sha}/harness/");
        zip.CreateEntry($"test262-{sha}/harness/assert.js");
        zip.CreateEntry($"test262-{sha}/test/");
        zip.CreateEntry($"test262-{sha}/test/language/");
        zip.CreateEntry($"test262-{sha}/test/language/example.js");
    }

    private static void CreatePartialArchive(string archive, string sha)
    {
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        zip.CreateEntry($"test262-{sha}/");
        zip.CreateEntry($"test262-{sha}/harness/");
        zip.CreateEntry($"test262-{sha}/harness/assert.js");
        zip.CreateEntry($"test262-{sha}/test/");
    }
}
