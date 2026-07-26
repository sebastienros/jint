#nullable enable

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// The embedding shape that no other benchmark in this suite covers: a script projecting over
/// <b>host-supplied objects</b> — a custom <see cref="ObjectInstance"/> subclass that materializes
/// its properties on demand from a native record — instead of over plain script objects, with the
/// two engine-wide switches embedders routinely flip on top of it (a reference resolver and an
/// execution constraint).
///
/// <para>
/// The workload is a single projection loop that touches every read shape a data-shaping script
/// uses: member read (<c>it.kind</c>), member call (<c>it.name.toUpperCase()</c>), computed index
/// (<c>items[i]</c>), member write (into a fresh literal) and object-literal construction. It is
/// deliberately one script for all lanes so the parameter columns are the only thing that varies.
/// </para>
///
/// <para><b>What each parameter proves</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ReceiverKind"/> — <see cref="HostReceiverKind.PlainObject"/> is the script-object
/// floor. <see cref="HostReceiverKind.LazyHost"/> is the cost an embedder actually pays today:
/// every own-property read goes through the virtual <see cref="ObjectInstance.GetOwnProperty"/>
/// (the fast shape/dictionary lanes in <see cref="ObjectInstance.Get(JsValue, JsValue)"/> are not
/// reachable for a non-<c>PlainObject</c> receiver) and allocates a fresh
/// <see cref="PropertyDescriptor"/> per read. Expect the LazyHost row to be several times the
/// PlainObject row in both time and <c>Allocated</c>, with the allocation delta ≈
/// (descriptor size × own-property reads) — descriptor churn is half the story, so always read the
/// <c>Allocated</c> column next to <c>Mean</c> here.
/// </description></item>
/// <item><description>
/// <see cref="ResolverKind"/> — installing any <see cref="IReferenceResolver"/> sets
/// <c>Engine._customResolver</c>, which turns off the member-expression inline caches and the
/// call fast path engine-wide (see <c>JintMemberExpression</c> / <c>JintCallExpression</c>).
/// The <see cref="HostResolverKind.Unfiltered"/> row sizes that global de-optimization; it is
/// paid on every member read in the script, not only on the null-propagating ones the resolver
/// exists for.
/// </description></item>
/// <item><description>
/// <see cref="StatementLimit"/> — a statement limit is an <i>exact</i> constraint, so unlike a
/// timeout it cannot be amortized: it forces per-statement checks and disarms the tight-body loop
/// lane. Expect a visible regression on the <c>true</c> rows. See
/// <see cref="ConstrainedExecutionBenchmark"/> for the same effect measured in isolation.
/// </description></item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class HostObjectAccessBenchmark
{
    private const int ItemCount = 200;

    /// <summary>
    /// Large enough that the loop never trips it — the lane measures the cost of <i>having</i> an
    /// exact constraint registered, not the cost of hitting its limit. Deliberately not
    /// <see cref="int.MaxValue"/>, which registers no constraint at all (see
    /// <see cref="ConstraintsOptionsExtensions.MaxStatements"/>) and would have made the
    /// <c>StatementLimit=true</c> rows a second copy of the unconstrained ones.
    /// </summary>
    private const int StatementBudget = 100_000_000;

    private const string ProjectionSource = """
        function project(items) {
          var total = 0, out = [];
          for (var i = 0; i < items.length; i++) {
            var it = items[i];
            if (it.kind === 'a' && it.amount > 0) {
              total += it.amount * it.rate;
              out.push({ id: it.id, name: it.name.toUpperCase(), total: total });
            }
          }
          return total;
        }
        """;

    private Engine _engine = null!;
    private Prepared<Script> _projection;

    // Only the lanes that compile and run against today's public API are in [Params]; the members
    // awaiting a Jint feature are handled (and rejected) by GlobalSetup so that adding them here is
    // a one-line change once the feature lands.
    [Params(HostReceiverKind.PlainObject, HostReceiverKind.LazyHost)]
    public HostReceiverKind ReceiverKind { get; set; }

    [Params(HostResolverKind.None, HostResolverKind.Unfiltered)]
    public HostResolverKind ResolverKind { get; set; }

    [Params(false, true)]
    public bool StatementLimit { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        switch (ReceiverKind)
        {
            case HostReceiverKind.PlainObject:
            case HostReceiverKind.LazyHost:
                break;

            // TODO: enable once the host-object "ordinary access semantics" opt-in lands — the host
            // declares a fixed set of own properties up front so reads can take the ordinary
            // (shape/dictionary) lane instead of the virtual GetOwnProperty lane.
            case HostReceiverKind.LazyHostOrdinary:

            // TODO: enable once the descriptor-free read hook lands — a host hook that returns the
            // JsValue directly, so a read no longer allocates a PropertyDescriptor to carry it.
            case HostReceiverKind.LazyHostValueHook:

            // TODO: enable once the fixed-layout object factory lands — the host declares the layout
            // once and the engine allocates a flat slot array per instance.
            case HostReceiverKind.FixedLayout:
                throw new NotSupportedException($"{ReceiverKind} awaits a Jint feature that does not exist yet; see the TODO in {nameof(GlobalSetup)}.");

            default:
                throw new NotSupportedException(ReceiverKind.ToString());
        }

        if (ResolverKind == HostResolverKind.NullishOnly)
        {
            // TODO: enable once the resolver interest filter lands — a resolver that declares it only
            // cares about null/undefined bases, so the engine can keep its member inline caches armed
            // instead of de-optimizing every member read engine-wide.
            throw new NotSupportedException($"{ResolverKind} awaits a Jint feature that does not exist yet; see the TODO in {nameof(GlobalSetup)}.");
        }

        var options = new Options();
        if (ResolverKind == HostResolverKind.Unfiltered)
        {
            options.SetReferencesResolver(new UnfilteredNullPropagationResolver());
        }

        if (StatementLimit)
        {
            options.MaxStatements(StatementBudget);
        }

        _engine = new Engine(options);
        _engine.Execute(ProjectionSource);
        _engine.SetValue("items", BuildItems(_engine, ReceiverKind));

        _projection = Engine.PrepareScript("project(items);");
        _engine.Evaluate(_projection);
    }

    [Benchmark]
    public JsValue Projection() => _engine.Evaluate(_projection);

    private static JsValue BuildItems(Engine engine, HostReceiverKind kind)
    {
        if (kind == HostReceiverKind.PlainObject)
        {
            // Built from script so the baseline objects get the ordinary representation an object
            // literal produces (hidden-class shape, ordinary access lane) rather than a hand-built
            // one that might not match what real script data looks like.
            return engine.Evaluate($$"""
                (function () {
                  var a = [];
                  for (var i = 0; i < {{ItemCount}}; i++) {
                    a.push({
                      id: 'id-' + i,
                      kind: (i % 2 === 0) ? 'a' : 'b',
                      name: 'record name ' + i,
                      note: 'note for record ' + i,
                      amount: i + 1,
                      rate: 1.5
                    });
                  }
                  return a;
                })()
                """);
        }

        var items = new JsValue[ItemCount];
        for (var i = 0; i < items.Length; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            var text = new[]
            {
                "id-" + index,
                i % 2 == 0 ? "a" : "b",
                "record name " + index,
                "note for record " + index,
            };
            var numbers = new double[] { i + 1, 1.5 };
            items[i] = new LazyHostObject(engine, text, numbers);
        }

        return new JsArray(engine, items);
    }
}

/// <summary>Receiver shapes the projection loop reads from.</summary>
public enum HostReceiverKind
{
    /// <summary>Plain script object — the floor every host shape is measured against.</summary>
    PlainObject,

    /// <summary>
    /// Custom <see cref="ObjectInstance"/> subclass projecting from a native record through the
    /// virtual <see cref="ObjectInstance.GetOwnProperty"/>. This is what embedders write today.
    /// </summary>
    LazyHost,

    /// <summary>Awaits the host-object "ordinary access semantics" opt-in. Not runnable yet.</summary>
    LazyHostOrdinary,

    /// <summary>Awaits the descriptor-free read hook. Not runnable yet.</summary>
    LazyHostValueHook,

    /// <summary>Awaits the fixed-layout object factory. Not runnable yet.</summary>
    FixedLayout,
}

/// <summary>Reference-resolver configurations layered on top of the receiver.</summary>
public enum HostResolverKind
{
    /// <summary>Engine default resolver — member inline caches stay armed.</summary>
    None,

    /// <summary>
    /// A null-propagating resolver of the kind embedders install so that <c>a.b.c</c> over missing
    /// data yields undefined instead of throwing. It carries no interest filter, so the engine
    /// de-optimizes every member read.
    /// </summary>
    Unfiltered,

    /// <summary>Awaits the resolver interest filter. Not runnable yet.</summary>
    NullishOnly,
}

/// <summary>
/// The null-propagating resolver shape embedders install verbatim: any nullish property base
/// resolves to undefined rather than throwing, and calling through a nullish base yields a no-op
/// function. Deliberately unfiltered — that is the point of the lane.
/// </summary>
internal sealed class UnfilteredNullPropagationResolver : IReferenceResolver
{
    public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
    {
        value = reference.Base;
        return true;
    }

    public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
        => value.IsNull() || value.IsUndefined();

    public bool TryGetCallable(Engine engine, object callee, out JsValue value)
    {
        value = new ClrFunction(engine, "anonymous", static (thisObj, _) => thisObj);
        return true;
    }

    public bool CheckCoercible(JsValue value) => true;
}

/// <summary>
/// A host object that projects a small native record (a <see cref="string"/> array plus a
/// <see cref="double"/> array — the shape a column store, a document field set or a decoded row
/// arrives in) into JavaScript properties on demand.
///
/// <para>
/// This is deliberately the <b>today</b> baseline, written the only way the current public API
/// allows: override the virtual <see cref="GetOwnProperty"/> and hand back a
/// <see cref="PropertyDescriptor"/>. The projected <see cref="JsValue"/> is memoized per field
/// (an embedder that re-projected on every read would also defeat every downstream identity cache,
/// which would measure something else), but the descriptor itself is rebuilt on every call because
/// the public API offers no way to avoid it. That per-read descriptor is the allocation this lane
/// exists to size.
/// </para>
///
/// <para>
/// <see cref="GetOwnPropertyCallCount"/> is the regression guard for the "more than one virtual
/// GetOwnProperty call per own-property read" finding — see the probe-count test in
/// Jint.Tests.PublicInterface.
/// </para>
/// </summary>
internal sealed class LazyHostObject : ObjectInstance
{
    // id, kind, name, note come from the text array; amount, rate from the numbers array.
    private const int SlotId = 0;
    private const int SlotKind = 1;
    private const int SlotName = 2;
    private const int SlotNote = 3;
    private const int SlotAmount = 4;
    private const int SlotRate = 5;
    private const int SlotCount = 6;

    private static readonly JsValue[] _keys =
    [
        new JsString("id"),
        new JsString("kind"),
        new JsString("name"),
        new JsString("note"),
        new JsString("amount"),
        new JsString("rate"),
    ];

    private readonly string[] _text;
    private readonly double[] _numbers;
    private readonly JsValue?[] _projected = new JsValue?[SlotCount];

    public LazyHostObject(Engine engine, string[] text, double[] numbers) : base(engine)
    {
        _text = text;
        _numbers = numbers;
    }

    /// <summary>
    /// How many times the engine asked this instance for an own property. Exposed so a test can
    /// pin today's probes-per-read ratio.
    /// </summary>
    public int GetOwnPropertyCallCount { get; private set; }

    public void ResetGetOwnPropertyCallCount() => GetOwnPropertyCallCount = 0;

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        GetOwnPropertyCallCount++;

        if (!property.IsString())
        {
            return PropertyDescriptor.Undefined;
        }

        var slot = SlotOf(property.ToString());
        if (slot < 0)
        {
            return PropertyDescriptor.Undefined;
        }

        // Fresh descriptor per call: the public PropertyDescriptor surface has no reusable
        // data-descriptor form an embedder can hand back, so this allocation is unavoidable today.
        return new PropertyDescriptor(Project(slot), writable: true, enumerable: true, configurable: true);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>(SlotCount);
        if ((types & Types.String) != Types.Empty)
        {
            keys.AddRange(_keys);
        }

        return keys;
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(
                _keys[slot],
                new PropertyDescriptor(Project(slot), writable: true, enumerable: true, configurable: true));
        }
    }

    private JsValue Project(int slot)
    {
        var projected = _projected[slot];
        if (projected is not null)
        {
            return projected;
        }

        projected = slot switch
        {
            SlotAmount => JsNumber.Create(_numbers[0]),
            SlotRate => JsNumber.Create(_numbers[1]),
            // The one field the script calls a string method on is handed over unmaterialized, so
            // the String.prototype receiver inline cache is on the measured path.
            SlotName => LazyHostString.FromUtf8(_text[SlotName]),
            _ => new JsString(_text[slot]),
        };

        _projected[slot] = projected;
        return projected;
    }

    private static int SlotOf(string name) => name switch
    {
        "id" => SlotId,
        "kind" => SlotKind,
        "name" => SlotName,
        "note" => SlotNote,
        "amount" => SlotAmount,
        "rate" => SlotRate,
        _ => -1,
    };
}

/// <summary>
/// A string whose characters live in a host-owned encoded buffer and are only decoded when the
/// engine genuinely needs a flat <see cref="string"/> — the shape a host uses when most projected
/// strings are compared or discarded rather than read.
///
/// <para>
/// <b>Public surface only.</b> Jint grants this project <c>InternalsVisibleTo</c>, but this class
/// restricts itself to members an embedder in an unrelated assembly could also override —
/// otherwise the lane would measure a host object no embedder can actually build.
/// </para>
///
/// <para>
/// <b>Hazard:</b> the base class stores its flat value in an internal field that a subclass cannot
/// populate, and a handful of base members read that field directly instead of going through
/// <see cref="ToString"/>. Everything reachable through the public surface is overridden below,
/// but <c>JsString.EnsureCapacity</c> is <c>internal virtual</c> and so <b>not overridable by an
/// embedder</b> — it dereferences the null backing value, so <c>String.prototype.concat</c> on an
/// instance of this class currently throws a <see cref="NullReferenceException"/>. The projection
/// workload deliberately avoids <c>concat</c>; drop this note (and feel free to add a concat lane)
/// once the base class routes <c>EnsureCapacity</c> through <c>ToString()</c> the way
/// <c>JsString.SlicedString</c> does.
/// </para>
/// </summary>
internal sealed class LazyHostString : JsString
{
    private readonly byte[] _utf8;
    private readonly int _length;
    private string? _materialized;

    private LazyHostString(byte[] utf8, int length) : base(null!)
    {
        _utf8 = utf8;
        _length = length;
    }

    public static LazyHostString FromUtf8(string value) => new(Encoding.UTF8.GetBytes(value), value.Length);

    /// <summary>How many times the engine forced a decode. A read-mostly host wants this at zero.</summary>
    public int MaterializeCount { get; private set; }

    public override string ToString()
    {
        if (_materialized is null)
        {
            MaterializeCount++;
            _materialized = Encoding.UTF8.GetString(_utf8);
        }

        return _materialized;
    }

    public override int Length => _length;

    public override char this[int index] => ToString()[index];

    public override bool Equals(string? other)
        => other is not null && _length == other.Length && string.Equals(ToString(), other, StringComparison.Ordinal);

    public override bool Equals(JsString? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Length is answered without decoding, so a mismatched compare stays materialization-free.
        return _length == other.Length && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);
    }

    // IsLooselyEqual is deliberately not overridden: the base implementation routes a JsString
    // comparand through the virtual Equals(JsString) overridden above and everything else through
    // ToString(), so it never touches the null backing field.

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ToString());
}
