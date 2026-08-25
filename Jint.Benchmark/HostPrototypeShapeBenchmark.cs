#nullable enable

// Reads ObjectRepresentation, which Jint declares as a non-contract diagnostic. Acknowledged the way an
// embedder acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// The embedding shape a DOM-style binding has: a large, fixed family of prototype objects rebuilt for every
/// engine, where each prototype carries dozens of Web IDL members (enumerable accessors, operations,
/// constants, a <c>constructor</c> back-reference and a <c>Symbol.toStringTag</c>) and a page touches only a
/// fraction of them. Modelled on a real report of ~170 prototypes costing ~33 MB and ~110 ms per document
/// before any script runs.
///
/// <para>
/// Scaled down to stay runnable: <see cref="PrototypeCount"/> prototypes × <see cref="MembersPerPrototype"/>
/// members, roughly a quarter of the reported width, plus one full-width prototype
/// (<see cref="WidePrototypeMembers"/> members) for the widest real interface. Read the deltas as ratios, not
/// as absolute per-document figures.
/// </para>
///
/// <para><b>The lanes</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="BuildPrototypes"/> — per-engine setup, the headline. <see cref="HostPrototypeKind.Dictionary"/>
/// is today's pattern: a plain <see cref="ObjectInstance"/> populated through
/// <see cref="ObjectInstance.FastSetProperty(string, PropertyDescriptor)"/> with one eagerly-created
/// <see cref="ClrFunction"/> per operation and a getter/setter pair per attribute.
/// <see cref="HostPrototypeKind.Shape"/> is the same members declared once per process as a
/// <see cref="JsObjectShape"/> and instantiated per engine. The <c>Allocated</c> column is the point: the
/// shaped lane allocates one small object per prototype and defers every descriptor and function.
/// </description></item>
/// <item><description>
/// <see cref="BuildAndTouchPrototypes"/> — the honest one. Setup, then three member reads on
/// <see cref="TouchedPrototypes"/> of the prototypes, modelling a page that touches ten to twenty DOM types.
/// Laziness only pays where nothing is touched, so this lane bounds the win rather than flattering it.
/// </description></item>
/// <item><description>
/// <see cref="SteadyStateReads"/> — a warmed read/call loop over instances whose <c>[[Prototype]]</c> is the
/// prototype under test. The receiver is a <em>correct</em> host object: it answers only its own properties
/// and overrides nothing but <see cref="ObjectInstance.GetOwnProperty"/>, so inherited members genuinely
/// resolve on the prototype and the prototype-method inline cache is reachable. That matters — a host whose
/// instances claim inherited members as their own can never reach this lane whatever its prototypes look
/// like, so measuring against such a receiver would credit this feature for a fix that is not its own.
/// </description></item>
/// <item><description>
/// <see cref="AbsentNameMissOverChain"/> — the absent-name walk, over a depth-<see cref="ChainDepth"/>
/// prototype chain built the way a DOM binding stacks its interfaces. A Web IDL named-property check
/// (<c>el.attributes['nope']</c>) asks for a name nothing declares, and the question is refused once per
/// level before the chain as a whole can answer it, so this is where a per-level cost multiplies. The
/// <c>in</c> operator drives exactly that walk.
/// </description></item>
/// <item><description>
/// <see cref="DeepHitOverChain"/> — the same loop aimed at a member declared only on the ROOT of the chain,
/// so it pays every level's refusal and then one hit. The hit-path control: whatever the miss lane costs,
/// this row must not move.
/// </description></item>
/// </list>
///
/// <para>
/// <b>Public surface only.</b> Jint grants this project <c>InternalsVisibleTo</c>, but every host type below
/// restricts itself to members an embedder in an unrelated assembly could also reach — otherwise the lanes
/// would measure a host nobody can actually write. The one place the restriction bites is
/// <see cref="ReflectiveHostInstance"/>: it cannot answer a read without allocating a
/// <see cref="PropertyDescriptor"/>, because the public surface offers no reusable data-descriptor form. That
/// cost sits in both prototype lanes equally, so it does not distort the comparison.
/// </para>
///
/// <para>
/// <b>Engine isolation.</b> <see cref="BuildPrototypes"/> and <see cref="BuildAndTouchPrototypes"/> build
/// their engine inside the benchmark method, which is what they measure. The other three rows used to
/// share one <c>_readEngine</c> warmed with all three of their scripts, so each was measured on an engine
/// carrying the others' globals (<c>items</c>, <c>chainLeaf</c>, <c>chainObj</c>) and their handler-tree
/// and prototype-method-cache state — and a class whose whole subject is what a prototype lookup costs
/// must not let one row warm another's caches. Each now gets its own engine, built by
/// <c>CreateReadEngine</c> or <c>CreateChainEngine</c> and warmed with its own script and nothing else
/// (see <see cref="IsolatedScript"/>); both factories keep the representation assertions, so engagement is
/// still asserted per engine rather than inferred. The rows still measure warm reads, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from the
/// three warm rows are not comparable to any published before the harness changed.</b>
/// </para>
/// </summary>
[MemoryDiagnoser]
public class HostPrototypeShapeBenchmark
{
    private const int PrototypeCount = 32;
    private const int MembersPerPrototype = 48;
    private const int WidePrototypeMembers = 292;
    private const int TouchedPrototypes = 5;
    private const int SteadyStateInstances = 50;
    private const int ChainDepth = 4;
    private const int ChainMembersPerLevel = 24;
    private const int ChainLoopIterations = 20000;

    private static readonly JsObjectShape[] _shapes = BuildShapes(PrototypeCount, MembersPerPrototype);
    private static readonly JsObjectShape _wideShape = BuildShape("Wide", WidePrototypeMembers);

    private static readonly MemberTable[] _tables = BuildTables(PrototypeCount, MembersPerPrototype);
    private static readonly MemberTable _wideTable = new("Wide", WidePrototypeMembers);

    private static readonly MemberTable[] _chainTables = BuildChainTables();
    private static readonly JsObjectShape[] _chainShapes = BuildChainShapes();

    private IsolatedScript _readLoop;
    private Prepared<Script> _touchScript;
    private IsolatedScript _absentMissLoop;
    private IsolatedScript _deepHitLoop;

    /// <summary>How the per-engine prototype objects are built.</summary>
    [Params(HostPrototypeKind.Dictionary, HostPrototypeKind.Shape)]
    public HostPrototypeKind PrototypeKind { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _touchScript = Engine.PrepareScript($$"""
            (function (protos) {
              var sum = 0;
              for (var i = 0; i < {{TouchedPrototypes}}; i++) {
                var p = protos[i];
                sum += p.CONST_0;
                sum += p.op_0 !== undefined ? 1 : 0;
                sum += p.attr_0 === undefined ? 0 : 1;
              }
              return sum;
            })(protos)
            """);

        _readLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            (function (items) {
              var total = 0;
              for (var i = 0; i < items.length; i++) {
                var it = items[i];
                total += it.attr_0.length + it.op_0().length + it.CONST_0;
              }
              return total;
            })(items)
            """), CreateReadEngine);

        // The chain lanes: a depth-ChainDepth prototype chain, with an ordinary script object in front of
        // it as the receiver. BuildChain asserts every level's representation.
        var deepestMember = _chainTables[0].Members[0].Name;
        _absentMissLoop = IsolatedScript.Warm(Engine.PrepareScript($$"""
            (function (obj) {
              var hits = 0;
              for (var i = 0; i < {{ChainLoopIterations}}; i++) {
                hits += ('__absent__' in obj) ? 1 : 0;
              }
              return hits;
            })(chainObj)
            """), CreateChainEngine);

        _deepHitLoop = IsolatedScript.Warm(Engine.PrepareScript($$"""
            (function (obj) {
              var hits = 0;
              for (var i = 0; i < {{ChainLoopIterations}}; i++) {
                hits += ('{{deepestMember}}' in obj) ? 1 : 0;
              }
              return hits;
            })(chainObj)
            """), CreateChainEngine);
    }

    /// <summary>
    /// The read row's engine: the prototype under test plus the instances in front of it, and nothing else.
    /// It is built in <c>[GlobalSetup]</c> because the row measures steady-state reads, not setup.
    /// </summary>
    private Engine CreateReadEngine()
    {
        var engine = new Engine();
        var prototype = BuildPrototype(engine, PrototypeKind, 0);

        // Engagement is asserted, never inferred from timing: if the shaped lane silently stopped being
        // shaped, the row would still run and quietly measure the dictionary path. The representation
        // settles on first touch, so force one before asking.
        prototype.Get("CONST_0");

        var expected = PrototypeKind == HostPrototypeKind.Shape
            ? ObjectRepresentation.SharedBuiltinLayout
            : ObjectRepresentation.Dictionary;
        var representation = engine.Diagnostics.GetObjectRepresentation(prototype);
        if (representation != expected)
        {
            throw new InvalidOperationException(
                $"{PrototypeKind} prototypes landed in the {representation} representation, expected {expected}.");
        }

        var items = new JsValue[SteadyStateInstances];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new ReflectiveHostInstance(engine, prototype, i);
        }

        engine.SetValue("items", new JsArray(engine, items));
        return engine;
    }

    /// <summary>
    /// A chain row's engine: the depth-<see cref="ChainDepth"/> prototype chain, an ordinary script object
    /// in front of it as the receiver, and nothing else. Each chain row gets one of its own, so neither
    /// walks a chain the other has already warmed.
    /// </summary>
    private Engine CreateChainEngine()
    {
        var engine = new Engine();
        engine.SetValue("chainLeaf", BuildChain(engine, PrototypeKind));
        engine.Execute("var chainObj = Object.create(chainLeaf);");
        return engine;
    }

    /// <summary>Lane A: everything a fresh document pays before a single line of script runs.</summary>
    [Benchmark]
    public Engine BuildPrototypes()
    {
        var engine = new Engine();
        for (var i = 0; i < PrototypeCount; i++)
        {
            BuildPrototype(engine, PrototypeKind, i);
        }

        BuildWidePrototype(engine, PrototypeKind);
        return engine;
    }

    /// <summary>Lane B: the same setup, plus the members a page that touches a handful of types actually reads.</summary>
    [Benchmark]
    public JsValue BuildAndTouchPrototypes()
    {
        var engine = new Engine();
        var protos = new JsValue[PrototypeCount];
        for (var i = 0; i < PrototypeCount; i++)
        {
            protos[i] = BuildPrototype(engine, PrototypeKind, i);
        }

        BuildWidePrototype(engine, PrototypeKind);
        engine.SetValue("protos", new JsArray(engine, protos));
        return engine.Evaluate(_touchScript);
    }

    /// <summary>Lane C: warmed inherited reads and calls through a correct host instance.</summary>
    [Benchmark]
    public JsValue SteadyStateReads() => _readLoop.Run();

    /// <summary>Lane D: a name nothing on the chain declares, refused once per prototype level, in a loop.</summary>
    [Benchmark]
    public JsValue AbsentNameMissOverChain() => _absentMissLoop.Run();

    /// <summary>Lane E: the same loop resolving on the chain's deepest level — the hit-path control.</summary>
    [Benchmark]
    public JsValue DeepHitOverChain() => _deepHitLoop.Run();

    /// <summary>
    /// Builds the depth-<see cref="ChainDepth"/> prototype chain both chain lanes walk and returns its leaf.
    /// The root chains to <c>Object.prototype</c>, exactly as the flat prototypes do, so the walk ends where a
    /// real one does. Each level's representation is asserted after a forcing touch — engagement is asserted,
    /// never inferred from timing: a shaped level that silently stopped being shaped would still run the row
    /// and quietly measure the dictionary path.
    /// </summary>
    private static ObjectInstance BuildChain(Engine engine, HostPrototypeKind kind)
    {
        var expected = kind == HostPrototypeKind.Shape
            ? ObjectRepresentation.SharedBuiltinLayout
            : ObjectRepresentation.Dictionary;

        ObjectInstance? previous = null;
        for (var i = 0; i < ChainDepth; i++)
        {
            ObjectInstance level;
            if (kind == HostPrototypeKind.Shape)
            {
                level = previous is null
                    ? _chainShapes[i].Instantiate(engine)
                    : _chainShapes[i].Instantiate(engine, previous);
            }
            else
            {
                level = BuildDictionaryPrototype(engine, _chainTables[i]);
                if (previous is not null)
                {
                    level.Prototype = previous;
                }
            }

            // The representation settles on first touch, so force one before asking.
            level.Get(_chainTables[i].Members[0].Name);
            var representation = engine.Diagnostics.GetObjectRepresentation(level);
            if (representation != expected)
            {
                throw new InvalidOperationException(
                    $"{kind} chain level {i} landed in the {representation} representation, expected {expected}.");
            }

            previous = level;
        }

        return previous!;
    }

    private static MemberTable[] BuildChainTables()
    {
        var tables = new MemberTable[ChainDepth];
        for (var i = 0; i < ChainDepth; i++)
        {
            var level = i.ToString(CultureInfo.InvariantCulture);
            tables[i] = new MemberTable("ChainLevel" + level, ChainMembersPerLevel, "l" + level + "_");
        }

        return tables;
    }

    private static JsObjectShape[] BuildChainShapes()
    {
        var shapes = new JsObjectShape[ChainDepth];
        for (var i = 0; i < ChainDepth; i++)
        {
            shapes[i] = BuildShapeFrom(_chainTables[i]);
        }

        return shapes;
    }

    private static ObjectInstance BuildPrototype(Engine engine, HostPrototypeKind kind, int index)
        => kind == HostPrototypeKind.Shape
            ? _shapes[index].Instantiate(engine)
            : BuildDictionaryPrototype(engine, _tables[index]);

    private static ObjectInstance BuildWidePrototype(Engine engine, HostPrototypeKind kind)
        => kind == HostPrototypeKind.Shape
            ? _wideShape.Instantiate(engine)
            : BuildDictionaryPrototype(engine, _wideTable);

    /// <summary>
    /// Today's pattern, verbatim: a plain object filled with raw descriptors, one eagerly-created function per
    /// operation and a getter/setter pair per attribute. Nothing here is lazy and nothing is shared between
    /// engines, which is the cost the shaped lane is measured against.
    /// </summary>
    private static ObjectInstance BuildDictionaryPrototype(Engine engine, MemberTable table)
    {
        var prototype = new JsObject(engine);

        foreach (var member in table.Members)
        {
            switch (member.Kind)
            {
                case MemberKind.Operation:
                    prototype.FastSetProperty(
                        member.Name,
                        new PropertyDescriptor(
                            new ClrFunction(engine, member.Name, member.Implementation!, 0),
                            PropertyFlag.ConfigurableEnumerableWritable));
                    break;

                case MemberKind.Attribute:
                    prototype.FastSetProperty(
                        member.Name,
                        new GetSetPropertyDescriptor(
                            new ClrFunction(engine, "get " + member.Name, member.Implementation!, 0),
                            new ClrFunction(engine, "set " + member.Name, member.Setter!, 1),
                            enumerable: true,
                            configurable: true));
                    break;

                default:
                    prototype.FastSetProperty(
                        member.Name,
                        new PropertyDescriptor(member.Constant!, PropertyFlag.OnlyEnumerable));
                    break;
            }
        }

        prototype.FastSetProperty("constructor", new PropertyDescriptor(JsValue.Undefined, PropertyFlag.NonEnumerable));
        prototype.FastSetProperty(
            ToStringTagKey,
            new PropertyDescriptor(new JsString(table.Name), PropertyFlag.Configurable));

        return prototype;
    }

    private static JsObjectShape[] BuildShapes(int count, int members)
    {
        var shapes = new JsObjectShape[count];
        for (var i = 0; i < count; i++)
        {
            shapes[i] = BuildShape("Type" + i.ToString(CultureInfo.InvariantCulture), members);
        }

        return shapes;
    }

    /// <summary>
    /// The shape a Web IDL binding generator would emit once per interface: declared from the same member
    /// table the dictionary lane walks, so the two lanes present identical members in identical order.
    /// </summary>
    private static JsObjectShape BuildShape(string name, int memberCount) => BuildShapeFrom(new MemberTable(name, memberCount));

    private static JsObjectShape BuildShapeFrom(MemberTable table)
    {
        var name = table.Name;
        var builder = new JsObjectShape.Builder();
        foreach (var member in table.Members)
        {
            switch (member.Kind)
            {
                case MemberKind.Operation:
                    builder.Method(member.Name, member.Implementation!);
                    break;
                case MemberKind.Attribute:
                    builder.Accessor(member.Name, member.Implementation, member.Setter);
                    break;
                default:
                    builder.Constant(member.Name, member.Constant!);
                    break;
            }
        }

        return builder
            .PerRealmSlot("constructor", enumerable: false)
            .ToStringTag(name)
            .Build();
    }

    private static MemberTable[] BuildTables(int count, int members)
    {
        var tables = new MemberTable[count];
        for (var i = 0; i < count; i++)
        {
            tables[i] = new MemberTable("Type" + i.ToString(CultureInfo.InvariantCulture), members);
        }

        return tables;
    }

    /// <summary>
    /// Reached the way an embedder would — through script, since the well-known symbol table is internal.
    /// A well-known symbol is process-shared, so one lookup serves every engine the benchmark builds.
    /// </summary>
    private static JsValue ToStringTagKey { get; } = new Engine().Evaluate("Symbol.toStringTag");
}

/// <summary>How the per-engine prototype objects under test are built.</summary>
public enum HostPrototypeKind
{
    /// <summary>
    /// A plain object filled with raw descriptors and eagerly-created functions — the shape a binding written
    /// against today's public API ends up with, and the one the report measured.
    /// </summary>
    Dictionary,

    /// <summary>
    /// One process-shared <see cref="JsObjectShape"/> per interface, instantiated per engine.
    /// </summary>
    Shape,
}

internal enum MemberKind
{
    Operation,
    Attribute,
    Constant,
}

/// <summary>
/// One interface's member list, generated deterministically so the two prototype lanes are built from exactly
/// the same declarations. The mix — one constant in six, two attributes in six, the rest operations — is
/// modelled on a Web IDL interface with its inherited members flattened onto it.
/// </summary>
internal sealed class MemberTable
{
    /// <param name="memberPrefix">
    /// Prepended to every member name. Empty for the flat lanes; the chain lanes give each level its own
    /// prefix so no level shadows another and a lookup can be aimed at a chosen depth.
    /// </param>
    internal MemberTable(string name, int memberCount, string memberPrefix = "")
    {
        Name = name;
        var members = new Member[memberCount];
        for (var i = 0; i < memberCount; i++)
        {
            var suffix = i.ToString(CultureInfo.InvariantCulture);
            members[i] = (i % 6) switch
            {
                0 => new Member(memberPrefix + "CONST_" + suffix, MemberKind.Constant, null, null, JsNumber.Create(i)),
                1 or 2 => new Member(memberPrefix + "attr_" + suffix, MemberKind.Attribute, ReadAttribute, WriteAttribute, null),
                _ => new Member(memberPrefix + "op_" + suffix, MemberKind.Operation, RunOperation, null, null),
            };
        }

        // Name the first member of each kind predictably so the benchmark scripts can address them.
        members[0] = new Member(memberPrefix + "CONST_0", MemberKind.Constant, null, null, JsNumber.Create(1));
        members[1] = new Member(memberPrefix + "attr_0", MemberKind.Attribute, ReadAttribute, WriteAttribute, null);
        members[3] = new Member(memberPrefix + "op_0", MemberKind.Operation, RunOperation, null, null);

        Members = members;
    }

    internal string Name { get; }

    internal Member[] Members { get; }

    // Engine-independent by construction: the receiver carries both the host state and the engine, which is
    // exactly the contract a shape's delegates must satisfy.
    private static JsValue ReadAttribute(JsValue thisObject, JsValue[] arguments)
        => thisObject is ObjectInstance o ? o.Get("_state") : JsValue.Undefined;

    private static JsValue WriteAttribute(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is ObjectInstance o && arguments.Length > 0)
        {
            o.Set("_state", arguments[0]);
        }

        return JsValue.Undefined;
    }

    private static JsValue RunOperation(JsValue thisObject, JsValue[] arguments)
        => thisObject is ObjectInstance o ? o.Get("_state") : JsValue.Undefined;
}

internal sealed record Member(
    string Name,
    MemberKind Kind,
    Func<JsValue, JsValue[], JsValue>? Implementation,
    Func<JsValue, JsValue[], JsValue>? Setter,
    JsValue? Constant);

/// <summary>
/// The receiver Lane C reads through: a host instance that answers <b>only</b> its own state and lets every
/// declared member resolve on its prototype. That honesty is what makes the prototype-method inline cache
/// reachable, and it is the precondition a host must meet before any prototype representation can help its
/// steady-state reads.
/// <para>
/// It overrides <see cref="ObjectInstance.GetOwnProperty"/> and nothing else, so the engine derives ordinary
/// access semantics for it without the host declaring anything.
/// </para>
/// </summary>
internal sealed class ReflectiveHostInstance : ObjectInstance
{
    private readonly JsValue _state;

    public ReflectiveHostInstance(Engine engine, ObjectInstance prototype, int index) : base(engine)
    {
        Prototype = prototype;
        _state = new JsString("state-" + index.ToString(CultureInfo.InvariantCulture));
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property.IsString() && string.Equals(property.ToString(), "_state", StringComparison.Ordinal))
        {
            // A fresh descriptor per call: the public surface has no reusable data-descriptor form. Identical
            // in both prototype lanes, so it cancels out of the comparison.
            return new PropertyDescriptor(_state, writable: true, enumerable: false, configurable: true);
        }

        return PropertyDescriptor.Undefined;
    }
}
