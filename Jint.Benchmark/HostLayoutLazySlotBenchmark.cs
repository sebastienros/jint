#nullable enable

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Benchmark;

/// <summary>
/// The embedding shape lazy layout slots exist for: a <b>batch</b> of host records with a fixed member set,
/// most of it cheap fields and a few members each decoding a part of that item's raw payload, over which a
/// script touches only some of the expensive ones. Modelled on a real adoption's envelope — 15 members, 4 of
/// them decoded on demand — with the script reading one decoded member per item, so 3 of every 4 decodes are
/// avoidable and the question is only whether the representation lets a host avoid them without losing the
/// shared hidden class.
///
/// <para><b>What each lane proves</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="EnvelopeKind.LayoutEager"/> is today's shaped answer: one
/// <see cref="JsObjectLayout"/>, every value supplied at <c>JsObject.Create</c> time. The batch keeps one
/// interned hidden class and the projection loop stays monomorphic — but every item pays all four decodes,
/// including the three nothing reads. This is the row the lazy lane must beat, and the gap between them is
/// (avoided decodes × decode cost), nothing else.
/// </description></item>
/// <item><description>
/// <see cref="EnvelopeKind.LayoutLazy"/> is the same layout with those four members declared through
/// <c>JsObjectLayout.Builder.AddLazy</c> and the raw payload handed over as the per-object state. Same
/// hidden class as the eager lane — a lazy layout interns the very shape its eager twin does — so the read
/// lanes are identical and only the decode count differs. Read <c>Allocated</c> next to <c>Mean</c>: one
/// sentinel per item is the storage this lane adds, against three decoded objects per item it removes.
/// </description></item>
/// <item><description>
/// <see cref="EnvelopeKind.DictionaryCustomValue"/> is what a host had to write before: the same 11 cheap
/// members through <c>FastSetDataProperty</c> and the same 4 lazy ones as
/// <see cref="PropertyFlag.CustomJsValue"/> descriptors through <c>FastSetProperty</c>. It decodes exactly
/// as little as the lazy layout does, so its distance from <see cref="EnvelopeKind.LayoutLazy"/> is purely
/// the price of the dictionary representation: a per-item property dictionary and 15 descriptors on the
/// build side, and a batch that never shares a hidden class — so the projection loop's member sites see a
/// new object with a new descriptor set per item — on the read side.
/// </description></item>
/// </list>
///
/// <para>
/// Both benchmarks rebuild the batch, because that is the point: a lazy slot memoizes, so a second pass over
/// the same items would decode nothing and measure the wrong thing. <see cref="Build"/> isolates creation;
/// <see cref="BuildAndProject"/> adds the script that decides which members are ever observed.
/// </para>
///
/// <para>
/// The host types here are deliberately restricted to what a third-party embedder can reach, even though
/// this project has <c>InternalsVisibleTo</c>: <see cref="RawEnvelope"/> is a plain CLR record,
/// <see cref="LazyMemberDescriptor"/> overrides only <c>PropertyDescriptor.CustomValue</c>, and the batch is
/// assembled from script through a host delegate. No internal member participates, so the numbers are the
/// ones an embedder would see. Two places the restriction bites, both recorded here rather than quietly
/// worked around: the batch array is built by script calling <c>makeItem(i)</c> instead of through an
/// internal array intrinsic, which adds one host-delegate call per item — identical in every lane, so a
/// constant offset and never a lane difference; and the descriptor override has to be spelled
/// <c>protected internal</c> because this project is a friend assembly, where an embedder writes
/// <c>protected</c> for the same member.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class HostLayoutLazySlotBenchmark
{
    private const int ItemCount = 500;

    /// <summary>Cheap members, supplied as values in every lane.</summary>
    private static readonly string[] EagerNames =
    [
        "id", "type", "stream", "sequence", "created", "correlationId",
        "causationId", "contentType", "isJson", "position", "commitPosition"
    ];

    /// <summary>The members that decode a part of the payload on demand.</summary>
    private static readonly string[] LazyNames = ["body", "metadata", "headers", "annotations"];

    /// <summary>
    /// One item's undecoded payload: four encoded member bodies, plus the cheap fields. Decoding is a
    /// deliberate stand-in for the real thing (a JSON parse) — it walks the encoded text once and builds one
    /// object per member, which is the cost profile that matters here and keeps the benchmark free of any
    /// dependency on the JSON built-in's own performance.
    /// </summary>
    private sealed class RawEnvelope
    {
        internal RawEnvelope(int index)
        {
            Index = index;
            Encoded = new string[LazyNames.Length];
            for (var i = 0; i < Encoded.Length; i++)
            {
                Encoded[i] = $"f0={i}-{index};f1=value-{index};f2=other-{index};f3={index * 7};f4=tail-{index}";
            }
        }

        internal int Index { get; }
        internal string[] Encoded { get; }

        internal JsValue Decode(JsObject owner, int member)
        {
            var text = Encoded[member];
            var entries = new List<KeyValuePair<string, JsValue>>(5);
            var start = 0;
            while (start < text.Length)
            {
                var end = text.IndexOf(';', start);
                if (end < 0)
                {
                    end = text.Length;
                }

                var pair = text.Substring(start, end - start);
                var eq = pair.IndexOf('=');
                entries.Add(new KeyValuePair<string, JsValue>(pair.Substring(0, eq), JsString.Create(pair.Substring(eq + 1))));
                start = end + 1;
            }

            return JsObject.CreateFromEntries(owner.Engine, entries);
        }
    }

    /// <summary>
    /// The pre-existing workaround: a lazy member installed as a raw descriptor. Memoizes into the inherited
    /// value field on first read, exactly as a host would write it.
    /// </summary>
    private sealed class LazyMemberDescriptor : PropertyDescriptor
    {
        private readonly JsObject _owner;
        private readonly RawEnvelope _raw;
        private readonly int _member;
        private JsValue? _resolved;

        internal LazyMemberDescriptor(JsObject owner, RawEnvelope raw, int member)
            : base(null, PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.CustomJsValue)
        {
            _owner = owner;
            _raw = raw;
            _member = member;
        }

        // Spelled `protected internal` only because this project is a friend assembly of Jint; a real
        // embedder writes `protected override` for the same member (a protected internal member is seen as
        // protected from outside the assembly). Nothing else about the type differs.
        protected internal override JsValue? CustomValue
        {
            get => _resolved ??= _raw.Decode(_owner, _member);
            set => _resolved = value;
        }
    }

    // Declared once for the process, as a host would. The eager and the lazy layout carry the same names in
    // the same order, so they resolve to the very same interned hidden class in a given engine.
    private static readonly JsObjectLayout EagerLayout = new([.. EagerNames, .. LazyNames]);

    private static readonly JsObjectLayout LazyLayout = BuildLazyLayout();

    private static JsObjectLayout BuildLazyLayout()
    {
        var builder = JsObjectLayout.CreateBuilder();
        foreach (var name in EagerNames)
        {
            builder.Add(name);
        }

        // One static factory per member index, so nothing engine-affine is captured: everything item-specific
        // arrives through the state argument.
        builder.AddLazy(LazyNames[0], static (o, state) => ((RawEnvelope) state!).Decode(o, 0));
        builder.AddLazy(LazyNames[1], static (o, state) => ((RawEnvelope) state!).Decode(o, 1));
        builder.AddLazy(LazyNames[2], static (o, state) => ((RawEnvelope) state!).Decode(o, 2));
        builder.AddLazy(LazyNames[3], static (o, state) => ((RawEnvelope) state!).Decode(o, 3));
        return builder.Build();
    }

    private const string BuildSource = """
        function build(n) {
          var items = new Array(n);
          for (var i = 0; i < n; i++) { items[i] = makeItem(i); }
          return items;
        }
        function project(n) {
          var items = build(n);
          var total = 0;
          for (var i = 0; i < items.length; i++) {
            var e = items[i];
            if (e.type === 'a') { total += e.sequence + e.body.f0.length; }
          }
          return total;
        }
        """;

    private Engine _engine = null!;
    private Prepared<Script> _build;
    private Prepared<Script> _project;

    [Params(EnvelopeKind.LayoutEager, EnvelopeKind.LayoutLazy, EnvelopeKind.DictionaryCustomValue)]
    public EnvelopeKind Kind { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();
        _engine.Execute(BuildSource);

        var kind = Kind;
        var engine = _engine;
        _engine.SetValue("makeItem", new Func<int, JsValue>(index => MakeItem(engine, kind, index)));

        _build = Engine.PrepareScript($"build({ItemCount}).length;");
        _project = Engine.PrepareScript($"project({ItemCount});");

        // Warm the handler-tree caches so the measured runs are the steady state, and prove both lanes work.
        _engine.Evaluate(_build);
        _engine.Evaluate(_project);
    }

    /// <summary>Creation only: no member is ever observed, so the eager lane's decodes are pure waste.</summary>
    [Benchmark]
    public JsValue Build() => _engine.Evaluate(_build);

    /// <summary>Creation plus the projection loop, which observes one decoded member of every item.</summary>
    [Benchmark]
    public JsValue BuildAndProject() => _engine.Evaluate(_project);

    private static JsValue MakeItem(Engine engine, EnvelopeKind kind, int index)
    {
        var raw = new RawEnvelope(index);
        return kind switch
        {
            EnvelopeKind.LayoutEager => CreateEager(engine, raw),
            EnvelopeKind.LayoutLazy => CreateLazy(engine, raw),
            EnvelopeKind.DictionaryCustomValue => CreateDictionary(engine, raw),
            _ => throw new NotSupportedException(kind.ToString())
        };
    }

    private static JsObject CreateEager(Engine engine, RawEnvelope raw)
    {
        var values = new JsValue[EagerNames.Length + LazyNames.Length];
        FillEager(values, raw);

        // Decoding needs the object the members belong to, and Create has not produced it yet, so the eager
        // lane builds the shell first and fills the decoded members straight into their slots. That is the
        // ordinary write path, so the object stays shaped — this lane is not handicapped by the ordering.
        var obj = JsObject.Create(engine, EagerLayout, values);
        for (var i = 0; i < LazyNames.Length; i++)
        {
            obj.Set(LazyNames[i], raw.Decode(obj, i));
        }

        return obj;
    }

    private static JsObject CreateLazy(Engine engine, RawEnvelope raw)
    {
        var values = new JsValue[EagerNames.Length + LazyNames.Length];
        FillEager(values, raw);
        return JsObject.Create(engine, LazyLayout, values, raw);
    }

    private static JsObject CreateDictionary(Engine engine, RawEnvelope raw)
    {
        var obj = new JsObject(engine);
        var values = new JsValue[EagerNames.Length + LazyNames.Length];
        FillEager(values, raw);
        for (var i = 0; i < EagerNames.Length; i++)
        {
            obj.FastSetDataProperty(EagerNames[i], values[i]);
        }

        for (var i = 0; i < LazyNames.Length; i++)
        {
            obj.FastSetProperty(LazyNames[i], new LazyMemberDescriptor(obj, raw, i));
        }

        return obj;
    }

    private static void FillEager(JsValue[] values, RawEnvelope raw)
    {
        var index = raw.Index;
        values[0] = JsString.Create("id-" + index);
        values[1] = JsString.Create(index % 2 == 0 ? "a" : "b");
        values[2] = JsString.Create("stream-" + (index % 8));
        values[3] = JsNumber.Create(index);
        values[4] = JsNumber.Create(1_700_000_000 + index);
        values[5] = JsString.Create("corr-" + index);
        values[6] = JsString.Create("cause-" + index);
        values[7] = JsString.Create("application/json");
        values[8] = JsBoolean.True;
        values[9] = JsNumber.Create(index * 64);
        values[10] = JsNumber.Create(index * 64 + 32);
        // The lazy members are supplied by the layout; the eager lane overwrites these after creation.
        for (var i = EagerNames.Length; i < values.Length; i++)
        {
            values[i] = null!;
        }
    }
}

/// <summary>The three ways a host can present a batch of records with a few expensive members.</summary>
public enum EnvelopeKind
{
    /// <summary>One shared hidden class, every member decoded at creation.</summary>
    LayoutEager,

    /// <summary>One shared hidden class, the expensive members decoded on the first read that observes them.</summary>
    LayoutLazy,

    /// <summary>The pre-existing workaround: raw <c>CustomJsValue</c> descriptors, dictionary representation.</summary>
    DictionaryCustomValue
}
