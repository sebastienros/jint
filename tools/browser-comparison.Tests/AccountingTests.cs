using BrowserComparison;

namespace BrowserComparison.Tests;

public sealed class AccountingTests
{
    [TestCase("browser: ws://127.0.0.1:4567/devtools/browser/abc", "ws://127.0.0.1:4567/devtools/browser/abc")]
    [TestCase("DevTools listening on ws://127.0.0.1:4567/devtools/browser/abc", "ws://127.0.0.1:4567/devtools/browser/abc")]
    [TestCase("$msg=\"server running\" address=127.0.0.1:49367", "ws://127.0.0.1:49367")]
    [TestCase("config address=127.0.0.1:49367", null)]
    public void ReadinessRequiresAnAnnouncement(string line, string? expected)
    {
        Assert.That(BrowserAdapter.ParseEndpoint(line), Is.EqualTo(expected));
    }

    [Test]
    public void ScopeEntryTreatsExecutableAndArgumentsAsData()
    {
        var options = new AdapterOptions("probe", "jint", "/tmp/a path/$(untrusted)", AccountingDirectory: "/tmp/scope with spaces");
        var start = BrowserAdapter.CreateStartInfo(options, ["literal;argument", "`data`"]);
        Assert.That(start.FileName, Is.EqualTo("/bin/sh"));
        Assert.That(start.ArgumentList.Skip(3), Is.EqualTo(new[] { "/tmp/scope with spaces", "/tmp/a path/$(untrusted)", "literal;argument", "`data`" }));
        Assert.That(start.ArgumentList[1], Does.Contain("exec \"$@\""));
    }

    [Test]
    public void ExitedDescendantsRetainKernelCpuAndPeak()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "cpu.stat"), "usage_usec 123456\nuser_usec 120000\nsystem_usec 3456\n");
            File.WriteAllText(Path.Combine(directory, "cgroup.events"), "populated 0\nfrozen 0\n");
            File.WriteAllText(Path.Combine(directory, "memory.current"), "0\n");
            File.WriteAllText(Path.Combine(directory, "memory.peak"), "33554432\n");
            File.WriteAllText(Path.Combine(directory, "cgroup.procs"), "");
            var value = ScopeSnapshot.Read(directory)!;
            Assert.That(value.CpuMicroseconds, Is.EqualTo(123456));
            Assert.That(value.LifetimePeakChargedBytes, Is.EqualTo(33554432));
            Assert.That(value.CurrentChargedBytes, Is.Zero);
            Assert.That(value.Members, Is.Empty);
            Assert.That(value.Populated, Is.False);
            var diagnosticMiss = ScopeSnapshot.Read(directory, _ => throw new IOException("membership unavailable"))!;
            Assert.That(diagnosticMiss.CpuMicroseconds, Is.EqualTo(123456));
            Assert.That(diagnosticMiss.LifetimePeakChargedBytes, Is.EqualTo(33554432));
            Assert.That(diagnosticMiss.Members.Single().Status, Is.EqualTo("membership-unavailable"));
            File.Delete(Path.Combine(directory, "cpu.stat"));
            Assert.That(() => ScopeSnapshot.Read(directory), Throws.TypeOf<FileNotFoundException>());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase("stable", "observed-non-atomic", "membership-final")]
    [TestCase("reused", "identity-changed", "stat-recheck")]
    [TestCase("left", "membership-lost", "membership-after-stat")]
    [TestCase("left-final", "membership-lost", "membership-final")]
    [TestCase("io", "identity-unavailable", "stat-initial")]
    [TestCase("denied", "identity-unavailable", "stat-initial")]
    [TestCase("partial", "partial-identity", "stat-initial")]
    [TestCase("partial-recheck", "partial-identity", "stat-recheck")]
    [TestCase("membership-error", "membership-unavailable", "membership-initial")]
    public void PidDiagnosticsRecheckMembershipAndDistinguishIdentityMisses(string scenario, string status, string phase)
    {
        var memberships = 0;
        var identities = 0;
        string Read(string path)
        {
            if (path.EndsWith("cgroup.procs", StringComparison.Ordinal))
            {
                memberships++;
                if (scenario == "membership-error") throw new IOException("unreadable membership");
                return (scenario == "left" && memberships == 2) || (scenario == "left-final" && memberships == 3) ? "" : "42\n";
            }
            Assert.That(path, Is.EqualTo("/proc/42/stat"));
            identities++;
            if (scenario == "io") throw new IOException("procfs read failed; exit not established");
            if (scenario == "denied") throw new UnauthorizedAccessException("protected process");
            if (scenario == "partial" || (scenario == "partial-recheck" && identities == 2)) return "42 (worker) S 0";
            return "42 (worker (name)) " + string.Join(' ', Enumerable.Repeat("0", 19)) + " "
                + (scenario == "reused" && identities == 2 ? "200" : "100");
        }
        var observation = ScopeMemberObservation.Read("synthetic-scope", Read).Single();
        Assert.That(observation.Status, Is.EqualTo(status));
        Assert.That(observation.Phase, Is.EqualTo(phase));
        if (scenario == "stable")
        {
            Assert.That(memberships, Is.EqualTo(3));
            Assert.That(identities, Is.EqualTo(2));
            Assert.That(observation.KernelStartTime, Is.EqualTo("100"));
        }
        if (scenario is "io" or "denied")
            Assert.That(observation.Error, Does.Contain(scenario == "io" ? "IOException" : "UnauthorizedAccessException"));
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task OwnedLaunchFailureIsReapedAndDisposalIsRepeatable()
    {
        var adapter = new BrowserAdapter(new AdapterOptions("fails", "lightpanda", "/bin/sh", ["-c", "exit 23"]));
        try
        {
            Assert.That(async () => await adapter.StartAsync(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(adapter.ProcessId, Is.Not.Null);
        }
        finally
        {
            await adapter.DisposeAsync();
            await adapter.DisposeAsync();
        }
        Assert.That(() => System.Diagnostics.Process.GetProcessById(adapter.ProcessId!.Value), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public async Task MemoryTimelineRetainsEveryStageThroughFinalCleanup()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "memory.current"), "1000");
        try
        {
            await using var sampler = new ScopeMemorySampler(directory);
            foreach (var stage in new[] { "launch", "preparation", "work", "teardown" }) sampler.Stage(stage);
            await sampler.DisposeAsync();
            var samples = sampler.Snapshot();
            foreach (var stage in new[] { "launch", "preparation", "work", "teardown" })
                Assert.That(samples.Count(x => x.Stage == stage), Is.GreaterThanOrEqualTo(2));
            Assert.That(samples.All(x => double.IsFinite(x.ElapsedMilliseconds) && x.ElapsedMilliseconds >= 0), Is.True);
            Assert.That(samples.Select(x => x.ElapsedMilliseconds), Is.Ordered);
            Assert.That(samples[^1].Stage, Is.EqualTo("teardown"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task DependencyManifestIncludesNativeHelpersAndRuntimeResources()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "Frameworks"));
        try
        {
            var helper = Path.Combine(directory, "Frameworks", "helper-runtime.so");
            File.WriteAllText(Path.Combine(directory, "browser"), "synthetic executable");
            File.WriteAllText(helper, "synthetic native runtime");
            File.WriteAllText(Path.Combine(directory, "snapshot.bin"), "synthetic snapshot");
            var before = await BinaryIdentity.HashRootsAsync([directory]);
            Assert.That(before, Has.Count.EqualTo(3));
            File.WriteAllText(helper, "changed runtime with unchanged browser executable");
            var after = await BinaryIdentity.HashRootsAsync([directory]);
            Assert.That(after[helper], Is.Not.EqualTo(before[helper]));
            Assert.That(after[Path.Combine(directory, "browser")], Is.EqualTo(before[Path.Combine(directory, "browser")]));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase("cancellation")]
    [TestCase("timeout")]
    [TestCase("assertion")]
    [TestCase("launch")]
    [Platform(Exclude = "Win")]
    public async Task ActualAdapterFailureAlwaysRecordsAndCleansItsOwnedProcessScope(string failure)
    {
        var root = Environment.GetEnvironmentVariable("JINT_BROWSER_COMPARISON_CGROUP_ROOT");
        var scope = root is null ? null : Path.Combine(root, "jint-comparison-" + Guid.NewGuid().ToString("N"));
        if (scope is not null) Directory.CreateDirectory(scope);
        var journal = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        using var cancel = new CancellationTokenSource();
        var script = failure == "launch" ? (scope is null ? "exit 23" : "sleep 600 & exit 23")
            : "sleep 600 & printf 'DevTools listening on ws://127.0.0.1:1\\n'; wait";
        var options = new AdapterOptions("probe", "lightpanda", "/bin/sh", ["-c", script], AccountingDirectory: scope);
        var adapter = new BrowserAdapter(options, (_, token) =>
        {
            if (failure == "cancellation") cancel.Cancel();
            return failure is "cancellation" or "timeout" ? new TaskCompletionSource<PuppeteerSharp.IBrowser?>().Task
                : Task.FromResult<PuppeteerSharp.IBrowser?>(null);
        }, connectionTimeout: TimeSpan.Zero);
        var run = new OwnedRun(options, adapter, journal);
        try
        {
            Assert.That(async () => await run.ExecuteAsync<int>(_ => throw new InvalidOperationException("checksum mismatch"), cancel.Token),
                Throws.TypeOf<AggregateException>());
            Assert.That(run.CleanupSucceeded, Is.True);
            Assert.That(adapter.ProcessId, Is.Not.Null);
            Assert.That(() => System.Diagnostics.Process.GetProcessById(adapter.ProcessId!.Value), Throws.TypeOf<ArgumentException>());
            using var record = System.Text.Json.JsonDocument.Parse(File.ReadAllText(journal));
            Assert.That(record.RootElement.GetProperty("Terminal").GetBoolean(), Is.True);
            Assert.That(record.RootElement.GetProperty("CleanupSucceeded").GetBoolean(), Is.True);
            var terminal = record.RootElement.GetProperty("Events").EnumerateArray().Last();
            Assert.That(terminal.GetProperty("State").GetString(), Is.EqualTo("failed"));
            Assert.That(terminal.GetProperty("Failure").GetProperty("Type").GetString(), Is.EqualTo(failure switch
            {
                "cancellation" => typeof(TaskCanceledException).FullName,
                "timeout" => typeof(TimeoutException).FullName,
                _ => typeof(InvalidOperationException).FullName,
            }));
            if (scope is not null)
            {
                Assert.That(ScopeSnapshot.Read(scope)!.Populated, Is.False);
                Assert.That(run.Memory.Snapshot().Select(x => x.Stage), Does.Contain("launch").And.Contain("teardown"));
            }
        }
        finally
        {
            await adapter.DisposeAsync();
            File.Delete(journal);
            if (scope is not null) Directory.Delete(scope);
        }
    }

    [Test]
    public void ColdLaneCannotReuseAProcessAcrossIterations()
    {
        var configuration = new Configuration(1, [new AdapterOptions("test", "jint", Environment.ProcessPath)], Iterations: 2);
        Assert.That(configuration.Validate, Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void WorkloadsHaveIndependentResultsAndNamedCoverage()
    {
        Assert.That(Workloads.Names, Has.Length.EqualTo(5));
        foreach (var name in Workloads.Names)
        {
            var workload = Workloads.Get(name);
            Assert.That(workload.Html, Does.StartWith("<!doctype html>"));
            Assert.That(workload.Expected, Is.Not.Empty);
        }
        Assert.That(() => Workloads.Get("unsupported"), Throws.TypeOf<ArgumentException>());
    }
}
