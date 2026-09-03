using Jint.DevTools.ProtocolGenerator;

// The generator's Protocol type, which an unqualified name in this namespace would bind to the namespace.
using ProtocolDescription = Jint.DevTools.ProtocolGenerator.Protocol;

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
    /// <remarks>
    /// Every file but one, and the one is the point of the test below it: <c>Jint.g.cs</c> is generated from
    /// a description this repository writes, so the Chrome commit is not where it came from.
    /// </remarks>
    [Test]
    public void EveryFileGeneratedFromTheVendoredDescriptionNamesThePinnedCommit()
    {
        var pin = ProtocolPin.Read(Path.Combine(RepositoryPaths.ProtocolDirectory, "pin.json"));

        foreach (var path in Directory.GetFiles(RepositoryPaths.GeneratedDirectory, "*.g.cs"))
        {
            var text = File.ReadAllText(path);
            if (text.Contains(ProtocolDescription.OwnFile + " - this repository", StringComparison.Ordinal))
            {
                continue;
            }

            text.Should().Contain(pin.Commit, "'{0}' says which protocol commit it came from", Path.GetFileName(path));
        }
    }

    /// <summary>
    /// Every generated file says which description it was read from, and which part of the manifest shaped
    /// it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provenance that names the wrong document is worse than none, and that is what
    /// <see href="https://github.com/sebastienros/jint/issues/3683">#3683</see> found: <c>Jint.g.cs</c> is
    /// generated from <c>jint_protocol.json</c>, which is this repository's own file, and its header cited
    /// the Chrome commit its twenty-one neighbours come from. A reader had no way to tell the one file whose
    /// contents a protocol bump cannot move from the ones it can.
    /// </para>
    /// <para>
    /// The manifest line is the other half. <c>TheCheckedInCodeIsWhatTheEmitterProduces</c> catches a stale
    /// file in a build; somebody reading a diff has only what the file says about itself, and "generated
    /// from the Audits entries at sha256:<i>x</i>" is checkable against a manifest in a way that "generated
    /// from manifest.json" is not.
    /// </para>
    /// </remarks>
    [Test]
    public void EveryGeneratedFileNamesTheDescriptionAndTheManifestItCameFrom()
    {
        var manifest = GenerationManifest.Read(RepositoryPaths.ManifestPath);

        foreach (var path in Directory.GetFiles(RepositoryPaths.GeneratedDirectory, "*.g.cs"))
        {
            var name = Path.GetFileName(path);
            var header = string.Join("\n", File.ReadAllLines(path).Take(12));
            var domain = Path.GetFileNameWithoutExtension(name).Replace(".g", "", StringComparison.Ordinal);

            header.Should().Contain("source:", "'{0}' says which description it was generated from", name);
            header.Should().Contain("manifest: tools/devtools-protocol/manifest.json", "'{0}' says which manifest it was generated from", name);

            if (manifest.GeneratedDomainNames.Contains(domain, StringComparer.Ordinal))
            {
                header.Should().Contain(
                    domain + " entries, sha256:" + manifest.DigestOf(domain),
                    "'{0}' carries a digest of the manifest entries that shaped it",
                    name);
            }
            else
            {
                header.Should().Contain(
                    "whole file, sha256:" + manifest.Digest,
                    "'{0}' is generated from all of the manifest, so it carries a digest of all of it",
                    name);
            }
        }
    }

    /// <summary>
    /// The <c>Jint</c> domain's file names our own description and does not claim to come from Chrome's.
    /// </summary>
    [Test]
    public void TheJintDomainNamesItsOwnDescriptionAndNotTheChromePin()
    {
        var pin = ProtocolPin.Read(Path.Combine(RepositoryPaths.ProtocolDirectory, "pin.json"));
        var header = string.Join("\n", File.ReadAllLines(Path.Combine(RepositoryPaths.GeneratedDirectory, "Jint.g.cs")).Take(12));

        header.Should().Contain("tools/devtools-protocol/" + ProtocolDescription.OwnFile);
        header.Should().NotContain(pin.Commit, "the Jint domain is not vendored from Chrome and a bump cannot move it");
    }

    /// <summary>
    /// A domain whose manifest entry names its members generates those and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second item of <see href="https://github.com/sebastienros/jint/issues/3683">#3683</see>: a domain
    /// used to be generated whole or not at all, so <c>Audits</c> cost 143 KB of data transfer objects for
    /// an <c>enable</c> and a <c>disable</c> that are accepted no-ops. What a client sees is unchanged - a
    /// command with no virtual falls to the dispatch default, which answers the same <c>-32601</c> - so this
    /// asserts the shape of the generated code, and <c>ACommandAPartialDomainDoesNotGenerateIsStillMethodNotFound</c> asserts
    /// that the answer did not move.
    /// </para>
    /// <para>
    /// Both directions matter. A command the entry names and the emitter did not write would be one the
    /// manifest says is implemented and nothing can override; one it wrote and the entry does not name is
    /// the saving quietly not being made.
    /// </para>
    /// </remarks>
    [Test]
    public void APartialDomainGeneratesTheCommandsItsEntryNamesAndNoOthers()
    {
        var protocol = ProtocolDescription.Read(RepositoryPaths.ProtocolDirectory);
        var manifest = GenerationManifest.Read(RepositoryPaths.ManifestPath);
        var partial = manifest.GeneratedDomains.Where(domain => !domain.IsWhole).ToArray();

        partial.Should().NotBeEmpty("the mechanism is only exercised while something uses it");

        foreach (var entry in partial)
        {
            var generated = File.ReadAllText(Path.Combine(RepositoryPaths.GeneratedDirectory, entry.Name + ".g.cs"));

            foreach (var command in protocol.Domain(entry.Name).Commands)
            {
                // The emitter's own Naming.Pascal, which is internal to the generator: only the first
                // character moves, because the protocol's casing carries information (consoleAPICalled).
                var declaration = " " + char.ToUpperInvariant(command.Name[0]) + command.Name[1..] + "Async(";
                generated.Contains(declaration, StringComparison.Ordinal).Should().Be(
                    entry.GeneratesCommand(command.Name),
                    "'{0}.{1}' is {2} by the manifest's entry for '{0}'",
                    entry.Name,
                    command.Name,
                    entry.GeneratesCommand(command.Name) ? "generated" : "not generated");
            }
        }
    }

    /// <summary>
    /// The emitter refuses a manifest that implements a command its own entry does not generate.
    /// </summary>
    /// <remarks>
    /// Without this the mistake is silent in the worst way: the command has no virtual, so the override that
    /// answers it does not compile - or, for a page-level domain checked by another suite, the manifest and
    /// <c>Schema.getDomains</c> claim a command that answers <c>-32601</c>. Failing in the generator is what
    /// makes the two lists one statement.
    /// </remarks>
    [Test]
    public void TheEmitterRefusesAnImplementedCommandItsDomainDoesNotGenerate()
    {
        var manifest = File.ReadAllText(RepositoryPaths.ManifestPath)
            .Replace("\"getMetrics\"]", "]", StringComparison.Ordinal);

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, manifest);

        try
        {
            var refusal = Assert.Throws<ProtocolGeneratorException>(
                () => ProtocolEmitter.Emit(RepositoryPaths.ProtocolDirectory, path));

            refusal!.Message.Should().Contain("Performance.getMetrics");
            refusal.Message.Should().Contain("does not generate");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
