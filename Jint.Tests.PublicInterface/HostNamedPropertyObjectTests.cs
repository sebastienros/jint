#nullable enable

using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="NamedPropertyObject"/> is the supported way to project a host record — named values held in
/// native state — into script. A subclass declares only <c>NameCount</c>, <c>NameAt</c> and
/// <c>TryGetNamedValue</c>; the base class derives the entire JS-visible property model from those three, so
/// the five hooks a host used to have to keep mutually consistent (<c>GetOwnProperty</c>,
/// <c>ProbeOwnProperty</c>, <c>GetOwnPropertyKeys</c>, <c>TryGetOwnPropertyValue</c>, <c>GetOwnProperties</c>)
/// cannot disagree.
///
/// <para>
/// These tests live in the project <b>without</b> <c>InternalsVisibleTo</c>, so the fact that the record below
/// compiles at all is the reachability proof. The enumeration group is the one that matters most: a real
/// integrator once overrode <c>GetOwnProperties</c> instead of <c>GetOwnPropertyKeys</c> and shipped an object
/// invisible to <c>Object.keys</c>, <c>for..in</c>, spread, <c>Object.assign</c> and <c>JSON.stringify</c>.
/// Neither method exists to be overridden here.
/// </para>
///
/// <para>
/// A build with host-contract verification on (a Debug Jint, or Release with the
/// <c>Jint.EnableHostContractVerification</c> switch — which is what <c>JINT_HOST_CONTRACT_VERIFICATION=1</c>
/// turns on here) re-derives this class's own answers through <c>GetOwnProperty</c> on every access, so
/// running this suite that way is the coherence checker for the whole matrix. Every assertion below is written
/// to hold in both configurations.
/// </para>
/// </summary>
public class HostNamedPropertyObjectTests
{
    private static Engine EngineWith(out HostNamedRecord host, params string[] names)
    {
        var engine = new Engine();
        host = new HostNamedRecord(engine);
        foreach (var name in names)
        {
            host.Add(name, "value-of-" + name);
        }

        engine.SetValue("host", host);
        return engine;
    }

    [Test]
    public void AProjectedNameReads()
    {
        var engine = EngineWith(out _, "alpha", "beta");

        engine.Evaluate("host.alpha").Should().Be("value-of-alpha");
        engine.Evaluate("host['beta']").Should().Be("value-of-beta");
    }

    [Test]
    public void ANameTheProjectionDoesNotCarryResolvesOnThePrototype()
    {
        var engine = EngineWith(out _, "alpha");
        engine.Execute("Object.prototype.inherited = 'from-prototype';");

        engine.Evaluate("host.inherited").Should().Be("from-prototype");
        engine.Evaluate("host.hasOwnProperty('inherited')").Should().BeFalse();
        engine.Evaluate("'inherited' in host").Should().BeTrue();
    }

    [Test]
    public void ANameNobodyCarriesIsUndefined()
    {
        var engine = EngineWith(out _, "alpha");

        engine.Evaluate("host.missing").Should().BeUndefined();
        engine.Evaluate("'missing' in host").Should().BeFalse();
        engine.Evaluate("host.hasOwnProperty('missing')").Should().BeFalse();
    }

    [Test]
    public void ExistenceQuestionsAnswerFromTheProjection()
    {
        var engine = EngineWith(out var host, "alpha");
        host.Add("hidden", "value-of-hidden", enumerable: false);

        engine.Evaluate("'alpha' in host").Should().BeTrue();
        engine.Evaluate("'hidden' in host").Should().BeTrue();
        engine.Evaluate("host.hasOwnProperty('alpha')").Should().BeTrue();
        engine.Evaluate("host.hasOwnProperty('hidden')").Should().BeTrue();
        engine.Evaluate("host.propertyIsEnumerable('alpha')").Should().BeTrue();
        engine.Evaluate("host.propertyIsEnumerable('hidden')").Should().BeFalse();
    }

    [Test]
    public void ObjectKeysValuesAndEntriesSeeTheProjection()
    {
        var engine = EngineWith(out var host, "alpha", "beta");
        host.Add("hidden", "value-of-hidden", enumerable: false);

        engine.Evaluate("Object.keys(host).join()").Should().Be("alpha,beta");
        engine.Evaluate("Object.values(host).join()").Should().Be("value-of-alpha,value-of-beta");
        engine.Evaluate("JSON.stringify(Object.entries(host))")
            .Should().Be("""[["alpha","value-of-alpha"],["beta","value-of-beta"]]""");
    }

    [Test]
    public void GetOwnPropertyNamesListsNonEnumerableNamesToo()
    {
        var engine = EngineWith(out var host, "alpha");
        host.Add("hidden", "value-of-hidden", enumerable: false);

        engine.Evaluate("Object.getOwnPropertyNames(host).join()").Should().Be("alpha,hidden");
    }

    [Test]
    public void ForInWalksTheProjectionAndThenThePrototype()
    {
        var engine = EngineWith(out var host, "alpha", "beta");
        host.Add("hidden", "value-of-hidden", enumerable: false);
        engine.Execute("Object.prototype.inherited = 'from-prototype';");

        engine.Evaluate("var seen = []; for (var k in host) { seen.push(k); } seen.join()")
            .Should().Be("alpha,beta,inherited");
    }

    [Test]
    public void SpreadAndObjectAssignCopyTheProjection()
    {
        var engine = EngineWith(out var host, "alpha", "beta");
        host.Add("hidden", "value-of-hidden", enumerable: false);

        engine.Evaluate("JSON.stringify({ ...host })")
            .Should().Be("""{"alpha":"value-of-alpha","beta":"value-of-beta"}""");
        engine.Evaluate("JSON.stringify(Object.assign({}, host))")
            .Should().Be("""{"alpha":"value-of-alpha","beta":"value-of-beta"}""");
    }

    [Test]
    public void JsonStringifySerializesTheProjection()
    {
        var engine = EngineWith(out var host, "alpha", "beta");
        host.Add("hidden", "value-of-hidden", enumerable: false);

        engine.Evaluate("JSON.stringify(host)")
            .Should().Be("""{"alpha":"value-of-alpha","beta":"value-of-beta"}""");
    }

    [Test]
    public void TheProjectionIsLive()
    {
        var engine = EngineWith(out var host, "alpha");

        engine.Evaluate("Object.keys(host).join()").Should().Be("alpha");

        host.Add("beta", "value-of-beta");
        engine.Evaluate("Object.keys(host).join()").Should().Be("alpha,beta");
        engine.Evaluate("host.beta").Should().Be("value-of-beta");

        host.Remove("alpha");
        engine.Evaluate("Object.keys(host).join()").Should().Be("beta");
        engine.Evaluate("host.alpha").Should().BeUndefined();
        engine.Evaluate("'alpha' in host").Should().BeFalse();
    }

    [Test]
    public void GetOwnPropertyDescriptorReportsTheProjectionShape()
    {
        var engine = EngineWith(out var host, "alpha");
        host.Add("hidden", "value-of-hidden", enumerable: false);

        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(host, 'alpha'))")
            .Should().Be("""{"value":"value-of-alpha","writable":false,"enumerable":true,"configurable":true}""");
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, 'hidden').enumerable").Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, 'missing')").Should().BeUndefined();
    }

    [Test]
    public void AProjectedNameIsReadOnly()
    {
        var engine = EngineWith(out _, "alpha");

        engine.Evaluate("host.alpha = 'written'; host.alpha").Should().Be("value-of-alpha");
        Invoking(() => engine.Evaluate("'use strict'; host.alpha = 'written';"))
            .Should().Throw<JavaScriptException>();
    }

    [Test]
    public void DeleteOfAProjectedNameIsRefused()
    {
        var engine = EngineWith(out _, "alpha");

        engine.Evaluate("delete host.alpha").Should().BeFalse();
        engine.Evaluate("host.alpha").Should().Be("value-of-alpha");
        Invoking(() => engine.Evaluate("'use strict'; delete host.alpha;"))
            .Should().Throw<JavaScriptException>();
    }

    [Test]
    public void DefiningOverAProjectedNameIsRefused()
    {
        var engine = EngineWith(out _, "alpha");

        Invoking(() => engine.Evaluate("Object.defineProperty(host, 'alpha', { value: 1 });"))
            .Should().Throw<JavaScriptException>();
        engine.Evaluate("host.alpha").Should().Be("value-of-alpha");
    }

    [Test]
    public void NamesTheProjectionDoesNotCarryStayOrdinary()
    {
        var engine = EngineWith(out _, "alpha");

        engine.Evaluate("host.expando = 42; host.expando").Should().Be(42);
        engine.Evaluate("Object.getOwnPropertyNames(host).join()").Should().Be("alpha,expando");
        engine.Evaluate("delete host.expando").Should().BeTrue();
        engine.Evaluate("host.expando").Should().BeUndefined();
    }

    /// <summary>
    /// A name written as an expando before the projection carried it is shadowed by the projection once it
    /// appears — and listed once, not twice.
    /// </summary>
    [Test]
    public void AProjectionThatGrowsOverAnExpandoShadowsItWithoutDuplicatingTheKey()
    {
        var engine = EngineWith(out var host, "alpha");
        engine.Execute("host.later = 'expando';");

        engine.Evaluate("Object.getOwnPropertyNames(host).join()").Should().Be("alpha,later");
        engine.Evaluate("host.later").Should().Be("expando");

        host.Add("later", "value-of-later");

        engine.Evaluate("Object.getOwnPropertyNames(host).join()").Should().Be("alpha,later");
        engine.Evaluate("Object.keys(host).join()").Should().Be("alpha,later");
        engine.Evaluate("host.later").Should().Be("value-of-later");
    }

    [Test]
    public void SymbolKeysStayOrdinary()
    {
        var engine = EngineWith(out _, "alpha");

        engine.Evaluate("host[Symbol.toStringTag] = 'HostRecord'; Object.prototype.toString.call(host)")
            .Should().Be("[object HostRecord]");
        engine.Evaluate("Object.getOwnPropertySymbols(host).length").Should().Be(1);
        engine.Evaluate("Object.keys(host).join()").Should().Be("alpha");
    }

    [Test]
    public void ANumericKeyResolvesTheSameAsItsStringForm()
    {
        var engine = EngineWith(out var host);
        host.Add("7", "value-of-7");

        engine.Evaluate("host[7]").Should().Be("value-of-7");
        engine.Evaluate("host['7']").Should().Be("value-of-7");
        engine.Evaluate("7 in host").Should().BeTrue();
    }

    [Test]
    public void CanonicalIndexNamesEnumerateAscendingAndFirst()
    {
        var engine = EngineWith(out var host);
        host.Add("beta", "value-of-beta").Add("2", "value-of-2").Add("1", "value-of-1").Add("alpha", "value-of-alpha");

        engine.Evaluate("Object.keys(host).join()").Should().Be("1,2,beta,alpha");
    }

    [Test]
    public void TheRecordIsNotArrayLike()
    {
        var engine = EngineWith(out _, "alpha");

        engine.Evaluate("Array.isArray(host)").Should().BeFalse();
        engine.Evaluate("host.length").Should().BeUndefined();
    }

    /// <summary>
    /// The existence-only hook is pure opt-in and, when overridden, is what every yes/no question uses — no
    /// value is projected to be thrown away.
    /// </summary>
    [Test]
    public void ExistenceQuestionsUseTheExistenceHookAndProjectNoValue()
    {
        var engine = EngineWith(out var host, "alpha", "beta");

        // `in` only — a `host.hasOwnProperty(...)` call would first *read* the method off the prototype,
        // which is a member read on the host and costs a projection of its own.
        host.ResetCounters();
        engine.Evaluate("'alpha' in host && 'beta' in host").Should().BeTrue();

        host.ExistenceProbes.Should().Be(2);
        if (!HostContractVerificationSwitch.Enabled)
        {
            host.ValueReads.Should().Be(0);
        }
    }
}

/// <summary>
/// The minimum viable host record on <see cref="NamedPropertyObject"/>: an ordered name list plus a value
/// map, which is what a projecting embedder actually has. Written against the public surface only — the sole
/// Jint members it touches are the base class's five hooks. The counters are what
/// <see cref="HostNamedPropertyProbeCountTests"/> measures; <see cref="FlagReads"/> counts every point at
/// which a <c>PropertyDescriptor</c> or an <c>OwnPropertyProbe</c> is actually built, because those are the
/// only two things that need to know a name's attributes.
/// </summary>
internal sealed class HostNamedRecord : NamedPropertyObject
{
    private readonly List<string> _names = new();
    private readonly Dictionary<string, JsValue> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);

    public HostNamedRecord(Engine engine) : base(engine)
    {
    }

    /// <summary>Number of <c>TryGetNamedValue</c> calls since <see cref="ResetCounters"/>.</summary>
    public int ValueReads { get; private set; }

    /// <summary>Number of <c>HasName</c> calls since <see cref="ResetCounters"/>.</summary>
    public int ExistenceProbes { get; private set; }

    /// <summary>Number of <c>IsNameEnumerable</c> calls since <see cref="ResetCounters"/>.</summary>
    public int FlagReads { get; private set; }

    public void ResetCounters()
    {
        ValueReads = 0;
        ExistenceProbes = 0;
        FlagReads = 0;
    }

    public HostNamedRecord Add(string name, JsValue value, bool enumerable = true)
    {
        if (!_values.ContainsKey(name))
        {
            _names.Add(name);
        }

        _values[name] = value;
        if (!enumerable)
        {
            _hidden.Add(name);
        }

        return this;
    }

    public void Remove(string name)
    {
        _names.Remove(name);
        _values.Remove(name);
        _hidden.Remove(name);
    }

    public override int NameCount => _names.Count;

    public override string NameAt(int index) => _names[index];

    public override bool TryGetNamedValue(string name, out JsValue value)
    {
        ValueReads++;

        if (_values.TryGetValue(name, out var found))
        {
            value = found;
            return true;
        }

        value = JsValue.Undefined;
        return false;
    }

    // outside the Jint assembly a `protected internal` member is visible as `protected`
    protected override bool HasName(string name)
    {
        ExistenceProbes++;
        return _values.ContainsKey(name);
    }

    protected override bool IsNameEnumerable(string name)
    {
        FlagReads++;
        return !_hidden.Contains(name);
    }
}
