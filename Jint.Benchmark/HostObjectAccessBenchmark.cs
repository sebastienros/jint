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
/// floor. <see cref="HostReceiverKind.LazyHost"/> is what an embedder pays for a custom subclass
/// that overrides <see cref="ObjectInstance.GetOwnProperty"/> and nothing else: the engine derives
/// <see cref="PropertyAccessSemantics.Ordinary"/> for it, so an own-property read costs one virtual
/// probe (the fast shape/dictionary lanes in <see cref="ObjectInstance.Get(JsValue, JsValue)"/> are
/// not reachable for a non-<c>PlainObject</c> receiver) and the fresh
/// <see cref="PropertyDescriptor"/> that probe returns. Descriptor churn is half the story, so
/// always read the <c>Allocated</c> column next to <c>Mean</c> here — the allocation delta against
/// PlainObject is ≈ (descriptor size × own-property reads).
/// <see cref="HostReceiverKind.LazyHostExotic"/> is the identical projection behind an override of
/// <c>Get</c> that only delegates to <c>base.Get</c>. Nothing about the values changes; the engine
/// derives <see cref="PropertyAccessSemantics.Exotic"/> from the override alone and routes every
/// read through the full <c>Get</c> pipeline. The LazyHost → LazyHostExotic delta is therefore the
/// price of being exotic, paid by a host that genuinely needs to observe every read.
/// <see cref="HostReceiverKind.FixedLayout"/> is the same host records built through
/// <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/>: a declared layout
/// resolves to one interned hidden class, so every item in the batch shares it and reads take the
/// ordinary shape-keyed lane. The LazyHost → FixedLayout delta is therefore the whole cost of the
/// custom-subclass representation — the virtual <see cref="ObjectInstance.GetOwnProperty"/> call
/// <i>and</i> the per-read <see cref="PropertyDescriptor"/>, which the shaped form removes
/// together. Its useful control is <see cref="HostReceiverKind.PlainObject"/>: the two build the
/// same key order into the same representation, so FixedLayout landing on the PlainObject row is
/// the expected result and any gap is host-side projection cost, not access cost.
/// <see cref="HostReceiverKind.LazyHostValueHook"/> is the LazyHost record again, this time also
/// overriding <see cref="ObjectInstance.TryGetOwnPropertyValue"/> so the projected value is handed
/// over directly. The probe and the descriptor both disappear — including on the reads the member
/// lane never sees, such as the computed <c>items[i]</c> — so this row is where <c>Allocated</c>
/// should close on the PlainObject floor while the receiver stays a custom subclass. The
/// LazyHost → LazyHostValueHook delta separates "no descriptor churn" from the "fewer virtual
/// calls" the derivation already bought, instead of conflating the two.
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

    /// <summary>
    /// Declared once and shared by every item the <see cref="HostReceiverKind.FixedLayout"/> lane
    /// builds — which is the whole point of the API: one layout resolves to one interned hidden
    /// class per engine, so the projection loop stays monomorphic across the batch instead of
    /// meeting a new shape per item. The names are <see cref="LazyHostObject"/>'s own field order,
    /// which is also the order the <see cref="HostReceiverKind.PlainObject"/> literal uses, so every
    /// receiver kind presents identical own-key order.
    /// </summary>
    private static readonly JsObjectLayout _itemLayout = new(LazyHostObject.FieldNames);

    private Engine _engine = null!;
    private Prepared<Script> _projection;

    // Every lane compiles and runs against today's public API now that the value hook has landed.
    [Params(
        HostReceiverKind.PlainObject,
        HostReceiverKind.LazyHost,
        HostReceiverKind.LazyHostExotic,
        HostReceiverKind.LazyHostValueHook,
        HostReceiverKind.FixedLayout)]
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
            case HostReceiverKind.LazyHostExotic:
            case HostReceiverKind.LazyHostValueHook:
            case HostReceiverKind.FixedLayout:
                break;

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

        // Every host lane starts from the identical native record and runs it through the identical
        // projection, so the only things that vary between them are the object representation the
        // projected values end up in and what that representation declares about its own access.
        var fixedLayout = kind == HostReceiverKind.FixedLayout;
        var slots = fixedLayout ? new JsValue[_itemLayout.Count] : [];

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

            if (!fixedLayout)
            {
                items[i] = kind switch
                {
                    HostReceiverKind.LazyHostExotic => new ExoticLazyHostObject(engine, text, numbers),
                    HostReceiverKind.LazyHostValueHook => new ValueAnsweringLazyHostObject(engine, text, numbers),
                    _ => new LazyHostObject(engine, text, numbers),
                };
                continue;
            }

            for (var slot = 0; slot < slots.Length; slot++)
            {
                slots[slot] = LazyHostObject.ProjectSlot(slot, text, numbers);
            }

            // Create copies the values into the object's own slots, so one buffer serves the batch.
            items[i] = JsObject.Create(engine, _itemLayout, slots);
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
    /// virtual <see cref="ObjectInstance.GetOwnProperty"/>, overriding nothing else and declaring
    /// nothing. The engine derives <see cref="PropertyAccessSemantics.Ordinary"/> for it, so this is
    /// what an embedder gets without doing anything.
    /// </summary>
    LazyHost,

    /// <summary>
    /// The same projection behind an override of <see cref="ObjectInstance.Get(JsValue, JsValue)"/>
    /// that only delegates to the base implementation. The override alone makes the engine derive
    /// <see cref="PropertyAccessSemantics.Exotic"/>, so this row is the cost of being exotic with
    /// every other variable held fixed.
    /// </summary>
    LazyHostExotic,

    /// <summary>
    /// The same projection with one added override of
    /// <see cref="ObjectInstance.TryGetOwnPropertyValue"/>, so an own read hands the value straight
    /// over and materializes no <see cref="PropertyDescriptor"/> at all — on the computed
    /// <c>items[i]</c> reads too, which never reach the interpreter's member lane.
    /// </summary>
    LazyHostValueHook,

    /// <summary>
    /// The same native records, projected once into ordinary objects built straight into the hidden
    /// class through <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/>.
    /// No custom subclass, so reads take the shape-keyed lane and allocate no descriptor.
    /// </summary>
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
/// Written the only way the public API allows: override the virtual <see cref="GetOwnProperty"/> and
/// hand back a <see cref="PropertyDescriptor"/>. The projected <see cref="JsValue"/> is memoized per
/// field (an embedder that re-projected on every read would also defeat every downstream identity
/// cache, which would measure something else), but the descriptor itself is rebuilt on every call
/// because the public API offers no way to avoid it. That per-read descriptor is the allocation this
/// lane exists to size.
/// </para>
///
/// <para>
/// It overrides <see cref="GetOwnProperty"/> and nothing else, so the engine derives
/// <see cref="PropertyAccessSemantics.Ordinary"/> for it without the host declaring anything. The two
/// subclasses each add exactly one override on top of the identical projection code, which is what
/// makes the deltas between the rows readable: <see cref="ExoticLazyHostObject"/> adds a pass-through
/// <c>Get</c> (the <see cref="HostReceiverKind.LazyHostExotic"/> row, so the delta is what being
/// derived exotic costs) and <see cref="ValueAnsweringLazyHostObject"/> adds
/// <see cref="ObjectInstance.TryGetOwnPropertyValue"/> (the
/// <see cref="HostReceiverKind.LazyHostValueHook"/> row, so the delta is the descriptor).
/// </para>
///
/// <para>
/// <see cref="GetOwnPropertyCallCount"/> is the regression guard for the "more than one virtual
/// GetOwnProperty call per own-property read" finding — see the probe-count test in
/// Jint.Tests.PublicInterface.
/// </para>
/// </summary>
internal class LazyHostObject : ObjectInstance
{
    // id, kind, name, note come from the text array; amount, rate from the numbers array.
    private const int SlotId = 0;
    private const int SlotKind = 1;
    private const int SlotName = 2;
    private const int SlotNote = 3;
    private const int SlotAmount = 4;
    private const int SlotRate = 5;
    private const int SlotCount = 6;

    /// <summary>
    /// Own-property names in slot order — the single source of truth for this record's shape, shared
    /// with the <see cref="HostReceiverKind.FixedLayout"/> lane's <see cref="JsObjectLayout"/> so the
    /// two receivers cannot drift apart in key set or key order.
    /// </summary>
    internal static readonly string[] FieldNames = ["id", "kind", "name", "note", "amount", "rate"];

    private static readonly JsValue[] _keys = Array.ConvertAll(FieldNames, static name => (JsValue) new JsString(name));

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
        // data-descriptor form an embedder can hand back, so this allocation is only avoidable by
        // answering through TryGetOwnPropertyValue instead.
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

    protected JsValue Project(int slot)
    {
        var projected = _projected[slot];
        if (projected is not null)
        {
            return projected;
        }

        projected = ProjectSlot(slot, _text, _numbers);
        _projected[slot] = projected;
        return projected;
    }

    /// <summary>
    /// The one place a native record field becomes a <see cref="JsValue"/>. Static and shared with
    /// the <see cref="HostReceiverKind.FixedLayout"/> lane, which builds its objects eagerly from
    /// the same records — so the two host receivers hand the script value-for-value identical
    /// properties and the only difference left between them is the representation carrying those
    /// values.
    /// </summary>
    internal static JsValue ProjectSlot(int slot, string[] text, double[] numbers) => slot switch
    {
        SlotAmount => JsNumber.Create(numbers[0]),
        SlotRate => JsNumber.Create(numbers[1]),
        // The one field the script calls a string method on is handed over unmaterialized, so
        // the String.prototype receiver inline cache is on the measured path.
        SlotName => LazyHostString.FromUtf8(text[SlotName]),
        _ => new JsString(text[slot]),
    };

    protected static int SlotOf(string name) => name switch
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
/// The <see cref="HostReceiverKind.LazyHostExotic"/> receiver: identical projection, identical values,
/// one added override of <see cref="ObjectInstance.Get(JsValue, JsValue)"/> that does nothing but
/// delegate. The engine derives <see cref="PropertyAccessSemantics.Exotic"/> from the presence of that
/// override alone, which is the point — a host does not have to behave exotically to be treated as
/// exotic, it only has to reserve the right to. A host in this position that knows it is ordinary
/// declares so through <c>SetPropertyAccessSemantics</c> and lands back on the LazyHost row.
/// </summary>
internal sealed class ExoticLazyHostObject : LazyHostObject
{
    public ExoticLazyHostObject(Engine engine, string[] text, double[] numbers) : base(engine, text, numbers)
    {
    }

    public override JsValue Get(JsValue property, JsValue receiver) => base.Get(property, receiver);
}

/// <summary>
/// The <see cref="HostReceiverKind.LazyHostValueHook"/> receiver: identical projection, identical
/// values, one added override of <see cref="ObjectInstance.TryGetOwnPropertyValue"/>. It answers off
/// the same <c>SlotOf</c> the descriptor lane uses, so its <c>false</c> means exactly what
/// <see cref="LazyHostObject.GetOwnProperty"/> returning <c>Undefined</c> means — which is the
/// contract, and the reason the engine may skip the probe rather than merely postpone it.
/// <para>
/// Overriding it is the whole opt-in; the engine derives the flag from the type, so the
/// <see cref="HostReceiverKind.LazyHost"/> row above is untouched by this class existing.
/// </para>
/// </summary>
internal sealed class ValueAnsweringLazyHostObject : LazyHostObject
{
    public ValueAnsweringLazyHostObject(Engine engine, string[] text, double[] numbers) : base(engine, text, numbers)
    {
    }

    // Spelled `protected internal override` because this project is a friend assembly of Jint; an
    // embedder outside it writes `protected override`. Same member either way — the keyword is the
    // only difference, and it is the one place in this file where the friend relationship shows.
    protected internal override bool TryGetOwnPropertyValue(JsValue property, JsValue receiver, out JsValue value)
    {
        if (property.IsString())
        {
            var slot = SlotOf(property.ToString());
            if (slot >= 0)
            {
                value = Project(slot);
                return true;
            }
        }

        // Not this record's key — and GetOwnProperty would say the same, which is what makes
        // answering false here honest rather than a deferral.
        value = JsValue.Undefined;
        return false;
    }
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
/// The overrides below are exactly the ones the base class's subclassing contract asks of a value
/// carrying a null backing field: <see cref="ToString"/> for correctness, the rest to keep reads
/// from materializing. Nothing here needs to work around the base class any more — the members
/// that used to read the backing field directly now route through <see cref="ToString"/> — so a
/// concatenation lane over this receiver would be sound. It is deliberately not added: the
/// projection script is shared by every parameter combination so that the columns are the only
/// thing that varies, and string concatenation is a string-representation question rather than a
/// host-object-access one. The lazy string is already on the measured path through
/// <c>it.name.toUpperCase()</c>.
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
