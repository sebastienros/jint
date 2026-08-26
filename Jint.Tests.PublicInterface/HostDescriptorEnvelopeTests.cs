#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the <b>reused-descriptor-envelope</b> contract: a host may keep <em>one</em>
/// <see cref="PropertyDescriptor"/> instance per property and re-point it at fresh data between reads, and
/// every read must observe the current value. It is the pattern an embedder reaches for when the same object
/// shape is handed to script over and over — one envelope per property, refilled per event, instead of a fresh
/// object and a fresh descriptor per event.
///
/// <para>
/// It works because the read inline caches cache the descriptor <em>reference</em>, never a value snapshot:
/// every caching lane returns through the engine's descriptor unwrap, which re-reads
/// <see cref="PropertyDescriptor.Value"/> — and the <see cref="PropertyFlag.CustomJsValue"/> flag, hence
/// <c>CustomValue</c> — on each hit. Nothing about a warm cache is allowed to change that, which is what these
/// tests exist to catch: they read the same property from the same AST node repeatedly, so a cache that
/// snapshotted the value would answer the second read with the first read's data.
/// </para>
///
/// <para>
/// Three receivers are covered, because they reach different caching lanes: a host
/// <see cref="ObjectInstance"/> subclass holding the envelope as its own property, a plain object carrying a
/// host-owned descriptor (the cheapest form — no subclass at all), and an ordinary object whose
/// <em>prototype</em> is the envelope-holding host.
/// </para>
///
/// <para>
/// These live in the public-interface suite on purpose: the project references Jint without any internals
/// access, so the whole pattern below is proven reachable by a third-party host.
/// </para>
/// </summary>
public class HostDescriptorEnvelopeTests
{
    private const int Events = 4;

    [Test]
    public void AReusedDescriptorIsReallyTheSameInstanceOnEveryProbe()
    {
        // The premise of everything below. If the host handed out a fresh descriptor per probe the tests
        // would pass for an uninteresting reason.
        var engine = new Engine();
        var host = new EnvelopeHostObject(engine);
        engine.SetValue("host", host);

        engine.Evaluate("host.body; host.body; host.body;");

        host.DistinctDescriptorsHandedOut.Should().Be(1);
    }

    [Test]
    public void AWarmMemberReadObservesEveryRefill()
    {
        var engine = new Engine();
        var host = new EnvelopeHostObject(engine);
        engine.SetValue("host", host);

        // One member-read node, read repeatedly, so every read after the first sees a warm site. The refill
        // happens between reads and moves nothing the engine can watch — only the Value of the descriptor the
        // host keeps handing back.
        engine.SetValue("refill", new Action<string>(host.Refill));

        var seen = engine.Evaluate($$"""
            var seen = [];
            for (var i = 0; i < {{Events}}; i++) {
                refill('event-' + i);
                seen.push(host.body);
            }
            seen.join(',');
            """);

        seen.Should().Be("event-0,event-1,event-2,event-3");
        host.DistinctDescriptorsHandedOut.Should().Be(1);
    }

    [Test]
    public void EveryReadShapeObservesTheRefill()
    {
        var engine = new Engine();
        var host = new EnvelopeHostObject(engine);
        engine.SetValue("host", host);

        for (var i = 0; i < Events; i++)
        {
            host.Refill("event-" + i);

            // the interpreter's member-read lane
            engine.Evaluate("host.body").Should().Be("event-" + i);
            // a computed key, which never reaches that lane and resolves through ObjectInstance.Get
            engine.Evaluate("host['bo' + 'dy']").Should().Be("event-" + i);
            // the base of a member call, which takes its own lane again
            engine.Evaluate("host.body.toUpperCase()").Should().Be(("event-" + i).ToUpperInvariant());
            // a host-side read, bypassing the interpreter entirely
            host.Get("body").Should().Be("event-" + i);
            // and the descriptor-only paths: enumeration and serialization
            engine.Evaluate("JSON.stringify(host)").Should().Be($$"""{"body":"event-{{i}}"}""");
            engine.Evaluate("Object.values(host).join('')").Should().Be("event-" + i);
        }

        host.DistinctDescriptorsHandedOut.Should().Be(1);
    }

    [Test]
    public void AReusedDescriptorSurvivesTheSameSiteSeeingOtherReceivers()
    {
        // A member-read site that alternates between two envelopes must not serve either one's value for the
        // other: whatever the site cached belongs to a receiver, and the receiver changes under it.
        var engine = new Engine();
        var first = new EnvelopeHostObject(engine);
        var second = new EnvelopeHostObject(engine);
        engine.SetValue("first", first);
        engine.SetValue("second", second);

        first.Refill("first-0");
        second.Refill("second-0");

        var seen = engine.Evaluate("""
            var hosts = [first, second];
            var seen = [];
            for (var i = 0; i < 6; i++) {
                seen.push(hosts[i % 2].body);
            }
            seen.join(',');
            """);

        seen.Should().Be("first-0,second-0,first-0,second-0,first-0,second-0");
    }

    [Test]
    public void ARefilledEnvelopeIsObservedThroughAPrototypeToo()
    {
        // The envelope living on a prototype rather than on the receiver, which is the lane that really does
        // cache a descriptor reference across reads. It must re-read the value on every hit as well.
        var engine = new Engine();
        var envelope = new EnvelopeHostObject(engine);
        engine.SetValue("envelope", envelope);
        engine.SetValue("refill", new Action<string>(envelope.Refill));

        var seen = engine.Evaluate($$"""
            var receiver = Object.create(envelope);
            var seen = [];
            for (var i = 0; i < {{Events}}; i++) {
                refill('event-' + i);
                seen.push(receiver.body);
            }
            seen.join(',');
            """);

        seen.Should().Be("event-0,event-1,event-2,event-3");
        envelope.DistinctDescriptorsHandedOut.Should().Be(1);
    }

    [Test]
    public void ACustomValueDescriptorIsConsultedOnEveryRead()
    {
        // The documented lazy-value hook: one descriptor instance whose value is produced on demand from
        // native state. PropertyFlag.CustomJsValue is re-read per cache hit, so the override really is asked
        // on every read rather than once at install time.
        var engine = new Engine();
        var host = new LazyEnvelopeHostObject(engine);
        engine.SetValue("host", host);
        engine.SetValue("refill", new Action<string>(host.Refill));

        var seen = engine.Evaluate($$"""
            var seen = [];
            for (var i = 0; i < {{Events}}; i++) {
                refill('event-' + i);
                seen.push(host.body);
            }
            seen.join(',');
            """);

        seen.Should().Be("event-0,event-1,event-2,event-3");
        host.DistinctDescriptorsHandedOut.Should().Be(1);

        // at least one consult per read; the engine is free to ask more often (a data-descriptor check does)
        host.CustomValueReads.Should().BeGreaterThanOrEqualTo(Events);
    }

    [Test]
    public void ACustomValueDescriptorStaysLazyOnTheOtherReadShapes()
    {
        var engine = new Engine();
        var host = new LazyEnvelopeHostObject(engine);
        engine.SetValue("host", host);

        host.Refill("first");
        engine.Evaluate("host['bo' + 'dy']").Should().Be("first");
        engine.Evaluate("host.body.toUpperCase()").Should().Be("FIRST");
        engine.Evaluate("JSON.stringify(host)").Should().Be("""{"body":"first"}""");

        host.Refill("second");
        engine.Evaluate("host['bo' + 'dy']").Should().Be("second");
        engine.Evaluate("host.body.toUpperCase()").Should().Be("SECOND");
        engine.Evaluate("JSON.stringify(host)").Should().Be("""{"body":"second"}""");
    }

    [Test]
    public void APlainObjectCarryingAHostOwnedDescriptorObservesEveryRefill()
    {
        // The same pattern without a subclass at all: a plain object with a host-owned descriptor installed
        // on it, re-pointed per event. The host keeps the descriptor, the engine keeps the object — and the
        // read cache, which for a plain receiver really does hold on to that descriptor, must still re-read
        // its value.
        var engine = new Engine();
        var body = new PropertyDescriptor(JsValue.Undefined, PropertyFlag.ConfigurableEnumerableWritable);
        var envelope = new JsObject(engine);
        envelope.FastSetProperty("body", body);
        engine.SetValue("envelope", envelope);
        engine.SetValue("refill", new Action<string>(value => body.Value = value));

        var seen = engine.Evaluate($$"""
            var seen = [];
            for (var i = 0; i < {{Events}}; i++) {
                refill('event-' + i);
                seen.push(envelope.body);
            }
            seen.join(',');
            """);

        seen.Should().Be("event-0,event-1,event-2,event-3");

        // and the object still behaves like an ordinary one around the reused slot
        engine.Evaluate("JSON.stringify(envelope)").Should().Be("""{"body":"event-3"}""");
        engine.Evaluate("envelope.body = 'written'; envelope.body;").Should().Be("written");
        body.Value.Should().Be("written");
    }
}

/// <summary>
/// A host object that owns exactly one <see cref="PropertyDescriptor"/> for its only property and re-points it
/// at new data between reads, instead of allocating a descriptor per probe. Counts how many distinct
/// descriptor instances it has handed out so a test can prove the reuse is real.
/// </summary>
internal class EnvelopeHostObject : ObjectInstance
{
    /// <summary>The name the single envelope is published under.</summary>
    protected const string BodyName = "body";

    private readonly PropertyDescriptor _body;
    private readonly HashSet<PropertyDescriptor> _handedOut = new HashSet<PropertyDescriptor>();

    public EnvelopeHostObject(Engine engine)
        : this(engine, new PropertyDescriptor(JsValue.Undefined, PropertyFlag.ConfigurableEnumerableWritable))
    {
    }

    protected EnvelopeHostObject(Engine engine, PropertyDescriptor body) : base(engine)
    {
        _body = body;
    }

    public int DistinctDescriptorsHandedOut => _handedOut.Count;

    /// <summary>
    /// Re-points the envelope at the next event's data. Not a JavaScript-visible write: nothing the engine
    /// watches moves, which is exactly why a value-snapshotting cache would go stale here.
    /// </summary>
    public virtual void Refill(string value) => _body.Value = value;

    private static bool IsBody(JsValue property)
        => property.IsString() && string.Equals(property.ToString(), BodyName, StringComparison.Ordinal);

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (!IsBody(property))
        {
            return PropertyDescriptor.Undefined;
        }

        _handedOut.Add(_body);
        return _body;
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>(1);
        if ((types & Types.String) != Types.Empty)
        {
            keys.Add(new JsString(BodyName));
        }

        return keys;
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        _handedOut.Add(_body);
        yield return new KeyValuePair<JsValue, PropertyDescriptor>(new JsString(BodyName), _body);
    }
}

/// <summary>
/// The same envelope, one difference: its single descriptor produces the value on demand through
/// <c>CustomValue</c> — the supported lazy-value hook — rather than storing one. The descriptor instance never
/// changes and neither does anything on it; only the native field behind it does.
/// </summary>
internal sealed class LazyEnvelopeHostObject : EnvelopeHostObject
{
    private readonly LazyBodyDescriptor _descriptor;

    public LazyEnvelopeHostObject(Engine engine) : this(engine, new LazyBodyDescriptor())
    {
    }

    private LazyEnvelopeHostObject(Engine engine, LazyBodyDescriptor descriptor) : base(engine, descriptor)
    {
        _descriptor = descriptor;
    }

    public int CustomValueReads => _descriptor.Reads;

    public override void Refill(string value) => _descriptor.Refill(value);

    private sealed class LazyBodyDescriptor : PropertyDescriptor
    {
        private string _value = "";

        public LazyBodyDescriptor()
            : base(null, PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.CustomJsValue)
        {
        }

        public int Reads { get; private set; }

        public void Refill(string value) => _value = value;

        // Outside the Jint assembly a protected-internal member is inherited as protected, so that is what an
        // embedder writes here.
        protected override JsValue? CustomValue
        {
            get
            {
                Reads++;
                return _value;
            }
            set => _value = value?.ToString() ?? "";
        }
    }
}
