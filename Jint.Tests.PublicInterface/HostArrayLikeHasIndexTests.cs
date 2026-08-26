#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="ArrayLikeObject.HasIndex"/> is the existence-only counterpart of
/// <see cref="ArrayLikeObject.TryGetIndex"/>. A question that needs a yes/no rather than a value — <c>index in
/// list</c>, <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c>, the key enumerations, <c>delete</c>, the
/// per-element hole test the <c>Array.prototype</c> generics run — otherwise projects an element purely to throw
/// it away. A DOM host reported that as real cost: a <c>Set</c>-backed collection can answer containment in O(1)
/// with no allocation, while producing the element wraps a node.
///
/// <para>
/// The hook is pure opt-in: its base implementation is exactly the old behaviour (ask
/// <see cref="ArrayLikeObject.TryGetIndex"/>, discard the value), so a host that does not override it is never
/// asked anything new. <see cref="HostArrayLikeObjectTests"/> covers that host; this file covers the overriding
/// one.
/// </para>
///
/// <para>
/// <b>Debug builds verify the hook.</b> Every probe re-runs <c>TryGetIndex</c> and compares, so the agreement
/// tests below are live assertions when the suite runs against a Debug Jint and free when it does not. That is
/// also why the observation counts differ by configuration: under Debug the verifier's own <c>TryGetIndex</c>
/// call is counted, so "the value was not materialized" is expressed as
/// <c>TryGetIndexCalls == HasIndexCalls</c> there and <c>== 0</c> in Release, which is the configuration the
/// claim is about.
/// </para>
/// </summary>
public class HostArrayLikeHasIndexTests
{
    /// <summary>
    /// A collection whose containment test is genuinely cheaper than its element projection: presence is a
    /// <see cref="HashSet{T}"/> lookup, while producing the element allocates. Both operations are counted so a
    /// test can prove which one the engine asked for. Restricted to the public surface — the only Jint members
    /// it touches are the three <see cref="ArrayLikeObject"/> hooks and <see cref="ObjectInstance.Prototype"/>.
    /// </summary>
    private sealed class SparseHostList : ArrayLikeObject
    {
        private readonly HashSet<uint> _present = [];
        private readonly Dictionary<uint, string> _values = [];
        private uint _length;

        public SparseHostList(Engine engine, uint length) : base(engine)
        {
            _length = length;
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public int TryGetIndexCalls { get; private set; }

        public int HasIndexCalls { get; private set; }

        public void ResetCounters()
        {
            TryGetIndexCalls = 0;
            HasIndexCalls = 0;
        }

        public SparseHostList Put(uint index, string value)
        {
            _present.Add(index);
            _values[index] = value;
            if (index >= _length)
            {
                _length = index + 1;
            }

            return this;
        }

        public void Remove(uint index)
        {
            _present.Remove(index);
            _values.Remove(index);
        }

        public void SetLength(uint length) => _length = length;

        public override uint Length => _length;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            TryGetIndexCalls++;

            if (index < _length && _values.TryGetValue(index, out var item))
            {
                // the expensive half: a real host wraps a node here
                value = new string(item.ToCharArray());
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        protected override bool HasIndex(uint index)
        {
            HasIndexCalls++;
            return index < _length && _present.Contains(index);
        }
    }

    /// <summary>
    /// Answers <see langword="true"/> for one index its <see cref="TryGetIndex"/> does not serve — a contract
    /// violation, used only to pin what a violating host observes.
    /// </summary>
    private sealed class OverclaimingHostList : ArrayLikeObject
    {
        public OverclaimingHostList(Engine engine) : base(engine)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => 3;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < 2)
            {
                value = "item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        // index 2 is a hole for TryGetIndex but claimed present here
        protected override bool HasIndex(uint index) => index < 3;
    }

    /// <summary>
    /// Denies an index its <see cref="TryGetIndex"/> does serve — the opposite violation.
    /// </summary>
    private sealed class UnderclaimingHostList : ArrayLikeObject
    {
        public UnderclaimingHostList(Engine engine) : base(engine)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => 3;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < 3)
            {
                value = "item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        // index 1 is served by TryGetIndex but denied here
        protected override bool HasIndex(uint index) => index < 3 && index != 1;
    }

    private static Engine CreateEngine(out SparseHostList list)
    {
        var engine = new Engine();
        list = new SparseHostList(engine, 0);
        list.Put(0, "a").Put(1, "b").Put(2, "c");
        engine.SetValue("list", list);
        return engine;
    }

    /// <summary>
    /// Whether Jint's host-contract verifiers are running: always in a Debug build, and in Release when
    /// <c>Jint.EnableHostContractVerification</c> was set before the first use of any Jint type — which is what
    /// this repository's Release verification leg does (<c>JINT_HOST_CONTRACT_VERIFICATION=1</c>). Static so
    /// <see cref="IgnoreUnlessAttribute" /> can read it while the test tree is built.
    /// </summary>
    public static bool Verifying => HostContractVerificationSwitch.Enabled;

    /// <inheritdoc cref="Verifying" />
    public static bool NotVerifying => !Verifying;

    /// <summary>
    /// The claim the hook exists for. Unverified, no element is produced at all for an existence question; while
    /// verifying, the only <c>TryGetIndex</c> calls are the verifier's, one per probe.
    /// </summary>
    private static void AssertNoValueWasMaterialized(SparseHostList list)
    {
        list.HasIndexCalls.Should().BeGreaterThan(0, "the existence question must reach the containment hook");
        if (Verifying)
        {
            list.TryGetIndexCalls.Should().Be(list.HasIndexCalls, "the agreement verifier re-runs TryGetIndex once per probe");
        }
        else
        {
            list.TryGetIndexCalls.Should().Be(0, "an existence question must not project an element");
        }
    }

    [TestCase("0 in list", true)]
    [TestCase("2 in list", true)]
    [TestCase("3 in list", false)]
    [TestCase("list.hasOwnProperty(0)", true)]
    [TestCase("list.hasOwnProperty('1')", true)]
    [TestCase("list.hasOwnProperty(9)", false)]
    [TestCase("list.propertyIsEnumerable(0)", true)]
    [TestCase("list.propertyIsEnumerable(9)", false)]
    public void AnExistenceQuestionAsksTheContainmentHookAndNeverProjectsAnElement(string script, bool expected)
    {
        var engine = CreateEngine(out var list);
        engine.Evaluate(script);

        list.ResetCounters();
        engine.Evaluate(script).Should().Be(expected);

        AssertNoValueWasMaterialized(list);
    }

    [TestCase("Object.keys(list).join(',')", "0,1,2")]
    [TestCase("Object.getOwnPropertyNames(list).join(',')", "0,1,2,length")]
    [TestCase("(function () { var k = []; for (var n in list) { k.push(n); } return k.join(','); })()", "0,1,2")]
    public void KeyEnumerationAsksTheContainmentHookAndNeverProjectsAnElement(string script, string expected)
    {
        var engine = CreateEngine(out var list);
        engine.Evaluate(script);

        list.ResetCounters();
        engine.Evaluate(script).Should().Be(expected);

        AssertNoValueWasMaterialized(list);
    }

    [Test]
    public void DeleteAsksTheContainmentHookAndNeverProjectsAnElement()
    {
        var engine = CreateEngine(out var list);

        list.ResetCounters();
        engine.Evaluate("delete list[0]").Should().Be(false);
        engine.Evaluate("delete list[9]").Should().Be(true);

        AssertNoValueWasMaterialized(list);
    }

    [Test]
    public void ReadsStillGoThroughTheValueHookAndNeverAskTheContainmentHookFirst()
    {
        // The counterpart claim: adding the hook must not put an extra call on a read path. A question that
        // needs the value asks for the value, once.
        var engine = CreateEngine(out var list);
        engine.Evaluate("list[1]");

        list.ResetCounters();
        engine.Evaluate("list[1]").Should().Be("b");

        list.TryGetIndexCalls.Should().Be(1);
        list.HasIndexCalls.Should().Be(0);

        list.ResetCounters();
        engine.Evaluate("var s = ''; for (var i = 0; i < list.length; i++) { s += list[i]; } s;").Should().Be("abc");
        list.TryGetIndexCalls.Should().Be(3);
        list.HasIndexCalls.Should().Be(0);
    }

    [Test]
    public void AnOverridingHostKeepsEveryDerivedAnswerCoherent()
    {
        // The positive agreement case, and the one that matters most under Debug: the verifier runs on every
        // probe here, so this test is a live check of the whole matrix against a well-behaved override.
        var engine = CreateEngine(out var list);
        list.Remove(1);
        list.SetLength(4);

        engine.Evaluate("list.length").Should().Be(4);
        engine.Evaluate("list[0]").Should().Be("a");
        engine.Evaluate("list[1]").Should().Be(JsValue.Undefined);
        engine.Evaluate("1 in list").Should().Be(false);
        engine.Evaluate("3 in list").Should().Be(false);
        engine.Evaluate("list.hasOwnProperty(2)").Should().Be(true);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,2");
        engine.Evaluate("Object.getOwnPropertyNames(list).join(',')").Should().Be("0,2,length");
        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a",null,"c",null]""");
        engine.Evaluate("Object.getOwnPropertyDescriptor(list, 1)").Should().Be(JsValue.Undefined);
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(list, 2))")
            .Should().Be("""{"value":"c","writable":false,"enumerable":true,"configurable":true}""");
        engine.Evaluate("delete list[1]").Should().Be(true);
        engine.Evaluate("delete list[2]").Should().Be(false);
    }

    [Test]
    public void TheContainmentHookIsConsultedLive()
    {
        var engine = CreateEngine(out var list);

        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1,2");

        list.Remove(1);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,2");
        engine.Evaluate("1 in list").Should().Be(false);

        list.Put(1, "B");
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1,2");
        engine.Evaluate("1 in list").Should().Be(true);
        engine.Evaluate("list[1]").Should().Be("B");
    }

    /// <summary>
    /// The other side of the same coin: with verification on — a Debug build, or the shipped Release package
    /// with the <c>Jint.EnableHostContractVerification</c> switch set before first use — the damage pinned below
    /// never gets the chance to happen, because the first probe that disagrees with <c>TryGetIndex</c> throws
    /// and names the host, the index and both answers.
    /// </summary>
    [IgnoreUnless(nameof(Verifying), "host-contract verification is off in this run")]
    [TestCase(true)]
    [TestCase(false)]
    public void VerificationTurnsASilentIndexDisagreementIntoAThrow(bool overclaiming)
    {
        var engine = new Engine();
        ArrayLikeObject list = overclaiming ? new OverclaimingHostList(engine) : new UnderclaimingHostList(engine);
        engine.SetValue("list", list);

        var act = () => engine.Evaluate("Object.keys(list).join(',')");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HasIndex*");
    }

    /// <summary>
    /// What a host that breaks the agreement observes when nothing is verifying. These are <b>not</b> a
    /// specification of desired behaviour — they pin the damage so it is on the record and reviewable, exactly
    /// as the <c>ProbeOwnProperty</c> contract does for a wrong <c>Missing</c>. The engine trusts the hook and
    /// does not re-verify it on the hot path, so a violation is silent unless verification is turned on.
    /// <para>
    /// Guarded to the unverified configuration on purpose: with the verifier running, the first disagreeing
    /// probe throws, which is what the theory above asserts instead.
    /// </para>
    /// </summary>
    [Test, IgnoreUnless(nameof(NotVerifying), "host-contract verification is on in this run")]
    public void AHostClaimingAnIndexItCannotServeAdvertisesAKeyThatReadsAsUndefined()
    {
        var engine = new Engine();
        engine.SetValue("list", new OverclaimingHostList(engine));

        // the engine believes the hook, so the key is advertised everywhere existence is asked...
        engine.Evaluate("2 in list").Should().Be(true);
        engine.Evaluate("list.hasOwnProperty(2)").Should().Be(true);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,1,2");
        engine.Evaluate("delete list[2]").Should().Be(false);

        // ...but reading it goes to TryGetIndex, which does not serve it, so the read resolves as an own miss
        engine.Evaluate("list[2]").Should().Be(JsValue.Undefined);
        engine.Evaluate("Object.getOwnPropertyDescriptor(list, 2)").Should().Be(JsValue.Undefined);

        // and the two disagree in a way script can see directly
        engine.Evaluate("Object.keys(list).length !== Object.values(list).filter(function (v) { return v !== undefined; }).length")
            .Should().Be(true);
    }

    [Test, IgnoreUnless(nameof(NotVerifying), "host-contract verification is on in this run")]
    public void AHostDenyingAnIndexItDoesServeHidesTheElementFromEveryEnumeration()
    {
        var engine = new Engine();
        engine.SetValue("list", new UnderclaimingHostList(engine));

        // the element is silently dropped from existence checks and enumeration...
        engine.Evaluate("1 in list").Should().Be(false);
        engine.Evaluate("list.hasOwnProperty(1)").Should().Be(false);
        engine.Evaluate("Object.keys(list).join(',')").Should().Be("0,2");
        engine.Evaluate("delete list[1]").Should().Be(true);

        // ...while the read still produces it, which is the incoherence the contract forbids
        engine.Evaluate("list[1]").Should().Be("item1");
        engine.Evaluate("JSON.stringify(list)").Should().Be("""["item0","item1","item2"]""");
    }
}
