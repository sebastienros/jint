#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The shape every option group has, seen from outside the assembly: a read-only property that materializes
/// its group on first access, and does so once however many engine builds race for it.
/// </summary>
public class HostOptionsShapeTests
{
    [Test]
    public void AGroupIsTheSameInstanceEveryTimeItIsRead()
    {
        var options = new Options();

        ReferenceEquals(options.Constraints, options.Constraints).Should().BeTrue();
        ReferenceEquals(options.Interop, options.Interop).Should().BeTrue();
        ReferenceEquals(options.Json, options.Json).Should().BeTrue();
        ReferenceEquals(options.Debugger, options.Debugger).Should().BeTrue();
        ReferenceEquals(options.Modules, options.Modules).Should().BeTrue();
    }

    [Test]
    public async Task ConcurrentReadersOfAGroupAllSeeOneInstance()
    {
        // Options is documented as safe to share between engines being constructed concurrently, and every
        // engine build reads several groups. A plain `??=` would hand two racing builds their own instance
        // each, and only one of them would see a later host mutation of the group.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var options = new Options();
            using var start = new ManualResetEventSlim(false);
            var seen = new Options.ConstraintOptions[8];

            var readers = new Task[seen.Length];
            for (var i = 0; i < readers.Length; i++)
            {
                var slot = i;
                readers[slot] = Task.Run(() =>
                {
                    start.Wait();
                    seen[slot] = options.Constraints;
                });
            }

            start.Set();
            await Task.WhenAll(readers);

            seen.Should().OnlyContain(x => ReferenceEquals(x, options.Constraints));
        }
    }

    [Test]
    public void TheJsonGroupIsConfiguredThroughItsMembers()
    {
        // In 4.16.x Json was the only group with a public setter, which is why the untrusted-code profile's
        // private snapshot had to hand-copy it. It is read-only like every other group now.
        var options = new Options();
        options.Json.MaxParseDepth = 3;
        options.Json.MaxParseDepth.Should().Be(3);

        var engine = new Engine(options);
        engine.Evaluate("JSON.parse('{\"a\":{\"b\":1}}').a.b").AsNumber().Should().Be(1);
        Invoking(() => engine.Evaluate("JSON.parse('{\"a\":{\"b\":{\"c\":{\"d\":1}}}}')"))
            .Should().Throw<JavaScriptException>();
    }

    [Test]
    public void ASaturatedRegexTimeoutMeansUntimedRatherThanUnconstructible()
    {
        // "A limit that cannot be reached is not a limit". Regex accepts a match timeout up to int.MaxValue
        // milliseconds, so TimeSpan.MaxValue could not be one; in 4.16.x it threw out of the Regex constructor
        // on the .NET path while Jint's own engine quietly treated it as untimed.
        foreach (var timeout in new[] { TimeSpan.MaxValue, TimeSpan.Zero, TimeSpan.FromDays(1000) })
        {
            var engine = new Engine(options => options.Constraints.RegexTimeout = timeout);
            engine.Evaluate("/a(b)c/.exec('abc')[1]").AsString().Should().Be("b");
        }
    }

    [Test]
    public void AConfiguredRegexTimeoutStillReadsBackAsItWasAssigned()
    {
        // The normalization happens where the timeout is used, so a security report describes what the host
        // actually typed.
        var options = new Options();
        options.Constraints.RegexTimeout = TimeSpan.MaxValue;

        options.Constraints.RegexTimeout.Should().Be(TimeSpan.MaxValue);
        options.ValidateSecurityConfiguration().Diagnostics
            .Should().Contain(x => x.Code == SecurityDiagnosticCodes.RegexTimeoutExcessive);
    }
}
