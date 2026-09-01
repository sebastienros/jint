using Jint.DevTools.ProtocolGenerator;

namespace Jint.Tests.DevTools.Protocol;

/// <summary>
/// The checked-in code under <c>Jint.DevTools/Protocol/Generated/</c> is what the emitter produces from the
/// vendored protocol and the manifest today.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the output is checked in at all.</b> A Roslyn source generator cannot do this job: the
/// <c>System.Text.Json</c> context has to be generated <i>over</i> the data transfer objects and generators
/// do not chain, and <c>Jint.SourceGenerators</c> is <c>netstandard2.0</c> without <c>System.Text.Json</c>.
/// Checked-in output also means a protocol bump arrives as a diff a reviewer can read, which for a surface
/// this size is the difference between reviewing the change and trusting it.
/// </para>
/// <para>
/// <b>What this test buys.</b> Checked-in generated code rots the moment somebody edits its input without
/// re-running the tool — the manifest, the pin, or the vendored JSON. Running the emitter in memory and
/// diffing costs milliseconds and makes that a build failure instead of a surface that quietly describes
/// last month's protocol.
/// </para>
/// </remarks>
public class GeneratedProtocolIsCurrentTests
{
    [Test]
    public void TheCheckedInCodeIsWhatTheEmitterProduces()
    {
        var emitted = ProtocolEmitter.Emit(RepositoryPaths.ProtocolDirectory, RepositoryPaths.ManifestPath);
        var onDisk = Directory.GetFiles(RepositoryPaths.GeneratedDirectory, "*.g.cs")
            .ToDictionary(path => Path.GetFileName(path), path => File.ReadAllText(path), StringComparer.Ordinal);

        var missing = emitted.Keys.Except(onDisk.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var extra = onDisk.Keys.Except(emitted.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var different = emitted.Keys
            .Intersect(onDisk.Keys, StringComparer.Ordinal)
            .Where(name => !string.Equals(RepositoryPaths.NormalizeNewlines(emitted[name]), RepositoryPaths.NormalizeNewlines(onDisk[name]), StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length == 0 && extra.Length == 0 && different.Length == 0)
        {
            return;
        }

        var complaints = new List<string>();
        complaints.AddRange(missing.Select(name => $"  {name} is not checked in."));
        complaints.AddRange(extra.Select(name => $"  {name} is checked in and the emitter no longer produces it."));
        complaints.AddRange(different.Select(name => $"  {name} differs from what the emitter produces."));

        Assert.Fail($"""
            The generated protocol code is not what tools/devtools-protocol produces from the pinned
            description and manifest.json today. Something upstream of it changed - the manifest, the pin,
            the vendored JSON, or the emitter - and the code was not regenerated.

            {string.Join(Environment.NewLine, complaints)}

            Regenerate, then read the diff:

                dotnet run --project tools/devtools-protocol/Jint.DevTools.ProtocolGenerator -c Release -- --protocol tools/devtools-protocol --manifest tools/devtools-protocol/manifest.json --output Jint.DevTools/Protocol/Generated
            """);
    }

    /// <summary>
    /// The emitter is deterministic, which is what makes the diff above mean "the inputs changed".
    /// </summary>
    [Test]
    public void TwoRunsOfTheEmitterProduceTheSameBytes()
    {
        var first = ProtocolEmitter.Emit(RepositoryPaths.ProtocolDirectory, RepositoryPaths.ManifestPath);
        var second = ProtocolEmitter.Emit(RepositoryPaths.ProtocolDirectory, RepositoryPaths.ManifestPath);

        second.Keys.Should().BeEquivalentTo(first.Keys);
        foreach (var (name, content) in first)
        {
            second[name].Should().Be(content, "'{0}' is emitted the same way every run", name);
        }
    }

    /// <summary>
    /// The pin the emitted files stamp is the pin on disk, so a bump that forgot to regenerate is caught
    /// even when nothing else about the protocol moved.
    /// </summary>
    [Test]
    public void TheGeneratedCodeNamesThePinnedCommit()
    {
        var pin = ProtocolPin.Read(Path.Combine(RepositoryPaths.ProtocolDirectory, "pin.json"));

        foreach (var path in Directory.GetFiles(RepositoryPaths.GeneratedDirectory, "*.g.cs"))
        {
            File.ReadAllText(path).Should().Contain(pin.Commit, "'{0}' says which protocol commit it came from", Path.GetFileName(path));
        }
    }
}
