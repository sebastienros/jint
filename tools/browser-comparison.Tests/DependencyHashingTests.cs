using System.Security.Cryptography;
using BrowserComparison;

namespace BrowserComparison.Tests;

public sealed class DependencyHashingTests
{
    [Test]
    public async Task ParallelHashesMatchIndependentSerialReadsAndHaveStableCanonicalOrder()
    {
        var directory = CreateFiles();
        try
        {
            var files = Directory.GetFiles(directory).Order(StringComparer.Ordinal).ToArray();
            var expected = files.ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
            // Overlapping roots must not cause duplicate reads or entries.
            var first = await BinaryIdentity.HashRootsAsync([directory, files[0], directory], null);
            var second = await BinaryIdentity.HashRootsAsync([directory], null);
            Assert.That(first, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(expected));
            Assert.That(first.Keys.ToArray(), Is.EqualTo(files));
            Assert.That(second.Keys.ToArray(), Is.EqualTo(files));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HashWorkersAreBoundedAndAllReadersJoinBeforeSuccessOrFailure(bool fail)
    {
        var directory = CreateFiles();
        using var entered = new CountdownEvent(4);
        using var release = new ManualResetEventSlim();
        var active = 0;
        var maximum = 0;
        var calls = 0;
        var failure = new IOException("Synthetic dependency read failure");
        var task = BinaryIdentity.HashRootsAsync([directory], null, path =>
        {
            var call = Interlocked.Increment(ref calls);
            var concurrent = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref maximum); }
            while (concurrent > observed && Interlocked.CompareExchange(ref maximum, concurrent, observed) != observed);
            try
            {
                if (call <= 4)
                {
                    entered.Signal();
                    // A wedge ceiling for a broken handoff, never a performance assertion.
                    if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Hash worker barrier never released");
                }
                if (fail && call == 1) throw failure;
                var bytes = File.ReadAllBytes(path);
                return (Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });
        try
        {
            var allEntered = entered.Wait(TimeSpan.FromSeconds(30));
            release.Set();
            if (fail)
            {
                var error = Assert.ThrowsAsync<IOException>(async () => await task);
                Assert.That(error, Is.SameAs(failure));
                Assert.That(task.IsFaulted, Is.True);
            }
            else
            {
                var result = await task;
                Assert.That(result, Has.Count.EqualTo(96));
                Assert.That(calls, Is.EqualTo(96));
            }
            Assert.That(allEntered, Is.True, "Four independent workers must reach the barrier");
            Assert.That(maximum, Is.EqualTo(4));
            Assert.That(active, Is.Zero, "No reader may outlive the completed or faulted snapshot");
        }
        finally
        {
            release.Set();
            try { await task; }
            catch (IOException) when (fail) { }
            Directory.Delete(directory, true);
        }
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task CanonicalAliasesAreHashedExactlyOnce()
    {
        var directory = CreateFiles();
        try
        {
            var file = Directory.GetFiles(directory)[0];
            File.CreateSymbolicLink(Path.Combine(directory, "alias"), file);
            Directory.CreateSymbolicLink(Path.Combine(directory, "loop"), directory);
            var calls = 0;
            var result = await BinaryIdentity.HashRootsAsync([directory, file], null, path =>
            {
                Interlocked.Increment(ref calls);
                var bytes = File.ReadAllBytes(path);
                return (Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);
            });
            Assert.That(result, Has.Count.EqualTo(96));
            Assert.That(calls, Is.EqualTo(96));
            Assert.That(result.Keys, Does.Not.Contain(Path.Combine(directory, "alias")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task ProducerFailureWhileReadersAreActiveJoinsThemAndReturnsNoSnapshot()
    {
        var directory = CreateFiles(4);
        using var entered = new CountdownEvent(4);
        using var producerFailed = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var active = 0;
        var failure = new IOException("Synthetic dependency enumeration failure");
        IEnumerable<string> Roots()
        {
            yield return directory;
            if (!entered.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Readers never entered");
            producerFailed.Set();
            throw failure;
        }
        var task = BinaryIdentity.HashRootsAsync(Roots(), null, path =>
        {
            Interlocked.Increment(ref active);
            entered.Signal();
            try
            {
                if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Readers never released");
                var bytes = File.ReadAllBytes(path);
                return (Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });
        try
        {
            var failureReached = producerFailed.Wait(TimeSpan.FromSeconds(30));
            var activeAtFailure = Volatile.Read(ref active);
            var returnedBeforeReadersFinished = task.IsCompleted;
            release.Set();
            var error = Assert.ThrowsAsync<IOException>(async () => await task);
            Assert.That(error, Is.SameAs(failure));
            Assert.That(failureReached, Is.True);
            Assert.That(activeAtFailure, Is.EqualTo(4));
            Assert.That(returnedBeforeReadersFinished, Is.False);
            Assert.That(active, Is.Zero);
        }
        finally
        {
            release.Set();
            try { await task; }
            catch (IOException) { }
            Directory.Delete(directory, true);
        }
    }

    private static string CreateFiles(int count = 96)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        directory = BinaryIdentity.ResolvePath(directory, null);
        for (var i = 0; i < count; i++) File.WriteAllText(Path.Combine(directory, $"native-{i:D3}.so"), $"Independent native dependency {i}");
        return directory;
    }
}
