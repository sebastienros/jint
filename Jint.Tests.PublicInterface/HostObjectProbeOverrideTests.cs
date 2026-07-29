#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host object can override <see cref="ObjectInstance.ProbeOwnProperty"/> to answer "does this key exist,
/// and is it enumerable?" without building a <see cref="PropertyDescriptor"/>. These tests pin that every
/// enumeration-shaped operation routes through the override, and that the override staying consistent with
/// <see cref="ObjectInstance.GetOwnProperty"/> keeps all of them behaving exactly as the descriptor path did.
/// </summary>
public class HostObjectProbeOverrideTests
{
    /// <summary>
    /// A minimal host object backed by a dictionary. <see cref="GetOwnProperty"/> is deliberately expensive
    /// (it allocates a fresh descriptor and counts the call) so the tests can show the probe replacing it.
    /// </summary>
    private sealed class HostRecord : ObjectInstance
    {
        private readonly List<string> _order = new();
        private readonly Dictionary<string, JsValue> _values = new(System.StringComparer.Ordinal);
        private readonly HashSet<string> _hidden = new(System.StringComparer.Ordinal);

        public readonly List<string> DescriptorsBuiltFor = new();
        public int ProbeCalls;

        public int GetOwnPropertyCalls => DescriptorsBuiltFor.Count;

        public HostRecord(Engine engine) : base(engine)
        {
        }

        public HostRecord Add(string name, JsValue value, bool enumerable = true)
        {
            if (!_values.ContainsKey(name))
            {
                _order.Add(name);
            }

            _values[name] = value;
            if (!enumerable)
            {
                _hidden.Add(name);
            }

            return this;
        }

        public void ResetCounters()
        {
            DescriptorsBuiltFor.Clear();
            ProbeCalls = 0;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            DescriptorsBuiltFor.Add(property.ToString());

            if (property is JsString name && _values.TryGetValue(name.ToString(), out var value))
            {
                return new PropertyDescriptor(value, writable: true, enumerable: !_hidden.Contains(name.ToString()), configurable: true);
            }

            return PropertyDescriptor.Undefined;
        }

        // outside the Jint assembly a `protected internal` member is visible as `protected`
        protected override OwnPropertyProbe ProbeOwnProperty(JsValue property)
        {
            ProbeCalls++;

            if (property is not JsString name || !_values.ContainsKey(name.ToString()))
            {
                return OwnPropertyProbe.Missing;
            }

            return _hidden.Contains(name.ToString()) ? OwnPropertyProbe.NonEnumerable : OwnPropertyProbe.Enumerable;
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
        {
            var keys = new List<JsValue>(_order.Count);
            if ((types & Types.String) != Types.Empty)
            {
                foreach (var key in _order)
                {
                    keys.Add(new JsString(key));
                }
            }

            return keys;
        }
    }

    /// <summary>
    /// The probe exists so an existence question costs no descriptor at all. With host-contract verification on
    /// — a Debug build, or the shipped Release package with the <c>Jint.EnableHostContractVerification</c>
    /// switch set — the verifier re-asks <c>GetOwnProperty</c> once per probe to check the answer against it, so
    /// the "no descriptor" figure is a figure from an unverified build, exactly as the equivalent
    /// no-materialization figures for the shared-layout probe lane are.
    /// </summary>
    private static void AssertTheProbeAnsweredWithoutADescriptor(HostRecord host, params string[] keys)
    {
        foreach (var key in keys)
        {
#if DEBUG
            host.DescriptorsBuiltFor.Should().Contain(key, "the verifier re-asks GetOwnProperty once per probe");
#else
            host.DescriptorsBuiltFor.Should().NotContain(key, "an existence question must not build a descriptor");
#endif
        }
    }

    private static (Engine Engine, HostRecord Host) CreateEngine()
    {
        var engine = new Engine();
        var host = new HostRecord(engine)
            .Add("visible", 1)
            .Add("also", "two")
            .Add("secret", 3, enumerable: false);

        engine.SetValue("host", host);
        host.ResetCounters();
        return (engine, host);
    }

    [Fact]
    public void InOperatorUsesTheProbe()
    {
        var (engine, host) = CreateEngine();

        engine.Evaluate("'visible' in host").AsBoolean().Should().BeTrue();
        engine.Evaluate("'secret' in host").AsBoolean().Should().BeTrue();
        engine.Evaluate("'absent' in host").AsBoolean().Should().BeFalse();

        host.ProbeCalls.Should().Be(3);
#if DEBUG
        host.DescriptorsBuiltFor.Should().Equal("visible", "secret", "absent");
#else
        host.DescriptorsBuiltFor.Should().BeEmpty();
#endif
    }

    [Fact]
    public void HasOwnPropertyUsesTheProbe()
    {
        var (engine, host) = CreateEngine();

        engine.Evaluate("host.hasOwnProperty('visible')").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.hasOwnProperty('secret')").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.hasOwnProperty('absent')").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.hasOwn(host, 'visible')").AsBoolean().Should().BeTrue();

        host.ProbeCalls.Should().Be(4);
        // the only descriptors built are for resolving `host.hasOwnProperty` itself (a miss on the host,
        // then the prototype); never for the probed keys
        AssertTheProbeAnsweredWithoutADescriptor(host, "visible", "secret", "absent");
    }

    [Fact]
    public void PropertyIsEnumerableUsesTheProbe()
    {
        var (engine, host) = CreateEngine();

        engine.Evaluate("host.propertyIsEnumerable('visible')").AsBoolean().Should().BeTrue();
        engine.Evaluate("host.propertyIsEnumerable('secret')").AsBoolean().Should().BeFalse();
        engine.Evaluate("host.propertyIsEnumerable('absent')").AsBoolean().Should().BeFalse();

        host.ProbeCalls.Should().Be(3);
        AssertTheProbeAnsweredWithoutADescriptor(host, "visible", "secret", "absent");
    }

    [Fact]
    public void ObjectKeysValuesEntriesFilterThroughTheProbe()
    {
        var (engine, host) = CreateEngine();

        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("visible,also");
        engine.Evaluate("Object.values(host).join(',')").AsString().Should().Be("1,two");
        engine.Evaluate("JSON.stringify(Object.entries(host))").AsString().Should().Be("""[["visible",1],["also","two"]]""");

        host.ProbeCalls.Should().Be(9);
    }

    [Fact]
    public void ObjectAssignAndSpreadCopyOnlyProbeEnumerableKeys()
    {
        var (engine, host) = CreateEngine();

        engine.Evaluate("JSON.stringify(Object.assign({}, host))").AsString().Should().Be("""{"visible":1,"also":"two"}""");
        engine.Evaluate("JSON.stringify({ ...host })").AsString().Should().Be("""{"visible":1,"also":"two"}""");

        host.ProbeCalls.Should().Be(6);
    }

    [Fact]
    public void JsonStringifySkipsNonEnumerableViaTheProbe()
    {
        var (engine, host) = CreateEngine();

        engine.Evaluate("JSON.stringify(host)").AsString().Should().Be("""{"visible":1,"also":"two"}""");

        host.ProbeCalls.Should().Be(3);
    }

    [Fact]
    public void ForInEnumerationAgreesWithTheProbe()
    {
        var (engine, _) = CreateEngine();

        engine.Evaluate("var keys = []; for (var k in host) { keys.push(k); } keys.join(',')")
            .AsString().Should().Be("visible,also");
    }

    [Fact]
    public void ProbeStaysConsistentWithGetOwnProperty()
    {
        var (engine, host) = CreateEngine();

        // the same three keys, once through the probe-backed operations and once through
        // getOwnPropertyDescriptor, which goes to GetOwnProperty
        foreach (var key in new[] { "visible", "also", "secret", "absent" })
        {
            var exists = engine.Evaluate($"Object.getOwnPropertyDescriptor(host, '{key}') !== undefined").AsBoolean();
            var enumerable = engine.Evaluate($"!!(Object.getOwnPropertyDescriptor(host, '{key}') || {{}}).enumerable").AsBoolean();

            engine.Evaluate($"host.hasOwnProperty('{key}')").AsBoolean().Should().Be(exists, key);
            engine.Evaluate($"host.propertyIsEnumerable('{key}')").AsBoolean().Should().Be(enumerable, key);
        }

        host.GetOwnPropertyCalls.Should().BeGreaterThan(0);
        host.ProbeCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ObjectDefinePropertiesReadsTheSourceThroughTheProbe()
    {
        var engine = new Engine();

        // Object.defineProperties filters its descriptor-bag argument by enumerability, which is the
        // probe's job: the non-enumerable entry must not become a definition on the target
        var bag = new HostRecord(engine)
            .Add("alpha", engine.Evaluate("({ value: 1, enumerable: true })"))
            .Add("beta", engine.Evaluate("({ value: 2, enumerable: true })"))
            .Add("gamma", engine.Evaluate("({ value: 3, enumerable: true })"), enumerable: false);

        engine.SetValue("bag", bag);
        bag.ResetCounters();

        var result = engine.Evaluate("Object.keys(Object.defineProperties({}, bag)).join(',')").AsString();

        result.Should().Be("alpha,beta");
        bag.ProbeCalls.Should().Be(3);
    }

    /// <summary>
    /// A host whose probe contradicts its own <see cref="ObjectInstance.GetOwnProperty"/> for the same key.
    /// The engine trusts the probe without re-verifying it, so this is the one host hook whose failure mode is
    /// a key silently vanishing from every enumeration rather than a wrong value somewhere visible.
    /// </summary>
    private sealed class LyingProbeHost : ObjectInstance
    {
        private readonly string _key;
        private readonly OwnPropertyProbe _lie;

        public LyingProbeHost(Engine engine, string key, OwnPropertyProbe lie) : base(engine)
        {
            _key = key;
            _lie = lie;
        }

        // "present, enumerable" for exactly two names, whatever the probe claims
        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            var name = property.ToString();
            if (name == "honest" || name == _key)
            {
                return new PropertyDescriptor(name, writable: true, enumerable: true, configurable: true);
            }

            return PropertyDescriptor.Undefined;
        }

        protected override OwnPropertyProbe ProbeOwnProperty(JsValue property)
            => property.ToString() == _key ? _lie : base.ProbeOwnProperty(property);

        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
            => new() { new JsString("honest"), new JsString(_key) };
    }

#if DEBUG
    /// <summary>
    /// With verification on — a Debug build, or the shipped Release package with the
    /// <c>Jint.EnableHostContractVerification</c> switch set before first use — a probe that disagrees with
    /// <c>GetOwnProperty</c> for the same key throws on the spot, naming the host, the key and both answers,
    /// instead of quietly dropping the key. This is the hook that had no verifier at all.
    /// </summary>
    [Theory]
    [InlineData(OwnPropertyProbe.Missing)]
    [InlineData(OwnPropertyProbe.NonEnumerable)]
    public void VerificationTurnsASilentProbeDisagreementIntoAThrow(OwnPropertyProbe lie)
    {
        var engine = new Engine();
        engine.SetValue("host", new LyingProbeHost(engine, "lied", lie));

        var act = () => engine.Evaluate("Object.keys(host).join(',')");

        act.Should().Throw<InvalidOperationException>().WithMessage("*ProbeOwnProperty*lied*");
    }

    [Fact]
    public void VerificationAlsoCoversTheExistenceQuestions()
    {
        var engine = new Engine();
        engine.SetValue("host", new LyingProbeHost(engine, "lied", OwnPropertyProbe.Missing));

        ((Action) (() => engine.Evaluate("'lied' in host"))).Should().Throw<InvalidOperationException>();
        ((Action) (() => engine.Evaluate("host.hasOwnProperty('lied')"))).Should().Throw<InvalidOperationException>();
        ((Action) (() => engine.Evaluate("host.propertyIsEnumerable('lied')"))).Should().Throw<InvalidOperationException>();
        ((Action) (() => engine.Evaluate("JSON.stringify(host)"))).Should().Throw<InvalidOperationException>();
        ((Action) (() => engine.Evaluate("JSON.stringify({ ...host })"))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AnHonestOverrideIsUnaffectedByVerification()
    {
        // the whole Debug run of this file is the real assertion; this one states it directly
        var (engine, host) = CreateEngine();

        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("visible,also");
        engine.Evaluate("'secret' in host").AsBoolean().Should().BeTrue();
        host.ProbeCalls.Should().Be(4);
    }
#else
    /// <summary>
    /// What the same host does when nothing is verifying: the lie is believed. Pinned so the damage is on the
    /// record — a wrong <c>Missing</c> costs the key everywhere existence is asked, while a read still produces
    /// it, and nothing anywhere reports a problem.
    /// </summary>
    [Fact]
    public void WithoutVerificationAWrongMissingSilentlyDropsTheKey()
    {
        var engine = new Engine();
        engine.SetValue("host", new LyingProbeHost(engine, "lied", OwnPropertyProbe.Missing));

        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("honest");
        engine.Evaluate("'lied' in host").AsBoolean().Should().BeFalse();
        engine.Evaluate("host.hasOwnProperty('lied')").AsBoolean().Should().BeFalse();
        engine.Evaluate("JSON.stringify(host)").AsString().Should().Be("""{"honest":"honest"}""");

        // ...while the property is plainly there
        engine.Evaluate("host.lied").AsString().Should().Be("lied");
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, 'lied') !== undefined").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void WithoutVerificationAWrongEnumerabilitySilentlyHidesTheKeyFromCopies()
    {
        var engine = new Engine();
        engine.SetValue("host", new LyingProbeHost(engine, "lied", OwnPropertyProbe.NonEnumerable));

        engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("honest");
        engine.Evaluate("JSON.stringify({ ...host })").AsString().Should().Be("""{"honest":"honest"}""");

        // but it exists, and getOwnPropertyDescriptor says it is enumerable
        engine.Evaluate("'lied' in host").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(host, 'lied').enumerable").AsBoolean().Should().BeTrue();
    }
#endif
}
