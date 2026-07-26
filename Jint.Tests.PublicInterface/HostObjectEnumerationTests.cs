using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The read path resolves a <see cref="PropertyAccessSemantics.Ordinary"/> host object from a single
/// own-property probe instead of routing through <c>Get</c>. These tests pin the enumeration and
/// existence-check paths — which never took that shortcut — against the same object, so a future change to the
/// read lane cannot desync the two. Each case runs over both derived outcomes: a host that overrides only
/// <c>GetOwnProperty</c> (derived ordinary, short lane) and one that adds a pure pass-through <c>Get</c>
/// override (derived exotic, full pipeline). The two answer identically by construction, so any difference an
/// assertion below can see is the engine's doing, not the host's.
/// </summary>
public class HostObjectEnumerationTests
{
    private static Engine CreateEngineWithHost(bool overridesGet)
    {
        var engine = new Engine();
        var host = overridesGet ? new PassThroughGetHostObject(engine) : new ProjectedHostObject(engine);
        host.Project("alpha", 1).Project("beta", "two");

        engine.SetValue("host", host);
        engine.Execute("Object.prototype.inheritedOnly = 'from-prototype';");
        return engine;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistenceChecksSeeExactlyTheProjectedProperties(bool overridesGet)
    {
        var engine = CreateEngineWithHost(overridesGet);

        engine.Evaluate("'alpha' in host").Should().Be(true);
        engine.Evaluate("'absent' in host").Should().Be(false);
        engine.Evaluate("'inheritedOnly' in host").Should().Be(true);

        engine.Evaluate("host.hasOwnProperty('alpha')").Should().Be(true);
        engine.Evaluate("host.hasOwnProperty('absent')").Should().Be(false);
        engine.Evaluate("host.hasOwnProperty('inheritedOnly')").Should().Be(false);

        engine.Evaluate("host.propertyIsEnumerable('alpha')").Should().Be(true);
        engine.Evaluate("host.propertyIsEnumerable('absent')").Should().Be(false);
        engine.Evaluate("host.propertyIsEnumerable('inheritedOnly')").Should().Be(false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReflectionOverAHostObjectMatchesItsReads(bool overridesGet)
    {
        var engine = CreateEngineWithHost(overridesGet);

        engine.Evaluate("Object.keys(host).join(',')").Should().Be("alpha,beta");
        engine.Evaluate("Object.values(host).join(',')").Should().Be("1,two");
        engine.Evaluate("Object.entries(host).map(function (e) { return e[0] + '=' + e[1]; }).join(',')").Should().Be("alpha=1,beta=two");
        engine.Evaluate("Object.getOwnPropertyNames(host).join(',')").Should().Be("alpha,beta");

        // for-in walks the prototype chain, so it also picks the inherited name up.
        engine.Evaluate("var seen = []; for (var k in host) { seen.push(k + ':' + host[k]); } seen.join(',');")
            .Should().Be("alpha:1,beta:two,inheritedOnly:from-prototype");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyingAHostObjectProducesItsProjectedValues(bool overridesGet)
    {
        var engine = CreateEngineWithHost(overridesGet);

        engine.Evaluate("JSON.stringify(Object.assign({}, host))").Should().Be("""{"alpha":1,"beta":"two"}""");
        engine.Evaluate("JSON.stringify({ ...host })").Should().Be("""{"alpha":1,"beta":"two"}""");
        engine.Evaluate("JSON.stringify(host)").Should().Be("""{"alpha":1,"beta":"two"}""");
        engine.Evaluate("JSON.stringify(host, ['beta'])").Should().Be("""{"beta":"two"}""");
    }
}
