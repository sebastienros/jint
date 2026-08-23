#nullable enable

using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// What one operation costs a host record built on <see cref="NamedPropertyObject"/>, counted in calls to the
/// hooks the host itself wrote. The sibling of <see cref="HostObjectProbeCountTests"/>, which measures the
/// same thing for a hand-written <see cref="ObjectInstance"/> subclass in <c>GetOwnProperty</c> calls.
///
/// <para>
/// The two metrics meet at <see cref="HostNamedRecord.FlagReads"/>. A name's attributes are needed at exactly
/// two moments — building a <c>PropertyDescriptor</c>, and answering an <c>OwnPropertyProbe</c> — so a
/// scenario that reads no flags built neither. That is the headline: <b>every read row below costs zero
/// descriptors and zero probes</b>, which is the column the hand-written host reaches only by finding, and
/// correctly implementing, <c>TryGetOwnPropertyValue</c>. Here it is what deriving from the base class gives
/// you.
/// </para>
///
/// <para>
/// The value-hook column is not uniformly one, and the row that is not is worth reading twice. A name absent
/// <em>everywhere</em> costs <b>two</b> <c>TryGetNamedValue</c> calls: the first establishes the own miss, the
/// interpreter then finds nothing on the direct prototype either and falls back to the full
/// <c>ObjectInstance.Get</c>, which re-establishes the same miss on the way to the prototype walk. It is the
/// hook-call twin of the "a name absent everywhere costs 2" row the descriptor column has always had, it
/// predates this class and is not specific to it, and removing it means changing the interpreter's shared
/// own-miss fallback rather than anything here.
/// </para>
///
/// <para>
/// Jint's host-contract verifiers re-derive every one of these answers through <c>GetOwnProperty</c> and
/// compare, so they cost hook calls of their own. The unverified column is the one an embedder pays and the
/// one every cost claim is about; never quote a verified figure as a cost.
/// </para>
///
/// <para>
/// The counts are assertions about behaviour rather than about desirability. Update them deliberately — a
/// silent change in either direction is what this test exists to catch.
/// </para>
/// </summary>
public class HostNamedPropertyProbeCountTests
{
    /// <summary>What one operation costs, in calls to each of the three host hooks.</summary>
    private readonly record struct HookCalls(int Values, int Existence, int Flags);

    private static HookCalls Measure(string script, out Engine engine, bool withPrototypeMember = false)
    {
        engine = new Engine();
        var host = new HostNamedRecord(engine);
        host.Add("alpha", "value-of-alpha");
        host.Add("beta", "value-of-beta");
        engine.SetValue("host", host);

        if (withPrototypeMember)
        {
            engine.Execute("Object.prototype.inherited = 'from-prototype';");
        }

        host.ResetCounters();
        engine.Evaluate(script);

        return new HookCalls(host.ValueReads, host.ExistenceProbes, host.FlagReads);
    }

    private static HookCalls Expected(HookCalls unverified, HookCalls verified)
        => HostContractVerificationSwitch.Enabled ? verified : unverified;

    [Fact]
    public void AnOwnNameReadCostsOneValueProjectionAndNoDescriptor()
    {
        var calls = Measure("host.alpha;", out var engine);

        engine.Evaluate("host.alpha").Should().Be("value-of-alpha");
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 1, Existence: 0, Flags: 0),
            verified: new HookCalls(Values: 5, Existence: 0, Flags: 3)));
    }

    [Fact]
    public void EachReadCostsItsOwnProjection()
    {
        var calls = Measure("host.alpha; host.beta; host.alpha;", out _);

        // Nothing is cached across reads for a host receiver, in this column or any other.
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 3, Existence: 0, Flags: 0),
            verified: new HookCalls(Values: 15, Existence: 0, Flags: 9)));
    }

    [Fact]
    public void ANameAbsentEverywhereCostsTwoProjections()
    {
        var calls = Measure("host.missing;", out var engine);

        engine.Evaluate("host.missing").Should().BeUndefined();

        // See the type's remarks: the second call is the interpreter's shared own-miss fallback re-entering
        // ObjectInstance.Get, not anything this class does. Still no descriptor and no probe.
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 2, Existence: 0, Flags: 0),
            verified: new HookCalls(Values: 7, Existence: 0, Flags: 0)));
    }

    [Fact]
    public void ANameOnThePrototypeCostsOneProjection()
    {
        var calls = Measure("host.inherited;", out var engine, withPrototypeMember: true);

        engine.Evaluate("host.inherited").Should().Be("from-prototype");

        // The own miss has to be re-established on every read — a projection that starts carrying the name
        // must shadow the prototype from the very next one — and the projection answers that for free.
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 1, Existence: 0, Flags: 0),
            verified: new HookCalls(Values: 5, Existence: 0, Flags: 0)));
    }

    [Fact]
    public void AMemberCallBaseCostsOneProjection()
    {
        var calls = Measure("host.alpha.toUpperCase();", out var engine);

        engine.Evaluate("host.alpha.toUpperCase()").Should().Be("VALUE-OF-ALPHA");

        // The base of a member call takes a different lane than a plain read — it resolves straight through
        // ObjectInstance.Get — and the projection answers it there too.
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 1, Existence: 0, Flags: 0),
            verified: new HookCalls(Values: 2, Existence: 0, Flags: 1)));
    }

    [Fact]
    public void AnExistenceQuestionProjectsNoValue()
    {
        var calls = Measure("'alpha' in host;", out _);

        // The existence hook answers it, and the one flag read is the probe deciding enumerable from
        // non-enumerable. No value is produced to be discarded.
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 0, Existence: 1, Flags: 1),
            verified: new HookCalls(Values: 1, Existence: 1, Flags: 1)));
    }

    [Fact]
    public void KeyEnumerationProjectsNoValues()
    {
        var calls = Measure("Object.keys(host);", out var engine);

        engine.Evaluate("Object.keys(host).join()").Should().Be("alpha,beta");

        // One probe and one flag read per name — the filter Object.keys needs — and not one value projected.
        calls.Should().Be(Expected(
            unverified: new HookCalls(Values: 0, Existence: 2, Flags: 2),
            verified: new HookCalls(Values: 4, Existence: 2, Flags: 2)));
    }
}
