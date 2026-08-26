#if NET8_0_OR_GREATER
#nullable enable

using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The attribute matrix for the whole <c>Jint/WebApi/</c> surface: every member of every web-API object an
/// engine with every feature on can reach, classified by the WebIDL member kind it is, and held to the
/// property attributes that kind requires.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> WebIDL's attributes are not ECMAScript's. A regular operation is
/// <c>{ [[Writable]]: true, [[Enumerable]]: <b>true</b>, [[Configurable]]: true }</c>
/// (https://webidl.spec.whatwg.org/#es-operations), where an ECMAScript built-in function property is
/// <c>{ true, <b>false</b>, true }</c> — and the generator's default is the ECMAScript one, because every
/// host under <c>Jint/Native/</c> wants it. So each web-API member has to opt in, per declaration, and a
/// per-declaration opt-in is exactly the thing the next web-API PR forgets. This test is what makes
/// forgetting it fail: nothing is enumerated by hand, so a member added tomorrow is classified and checked
/// tomorrow.
/// </para>
/// <para>
/// <b>Where a new member goes.</b> Find its WebIDL member kind in the table, and declare the flags in the
/// right-hand column. There is nothing to add here.
/// </para>
/// <code>
/// | WebIDL member kind                          | attributes        | how it is declared                    |
/// | ------------------------------------------- | ----------------- | ------------------------------------- |
/// | regular operation, static operation,        | { w:t, e:t, c:t } | [JsFunction(Flags =                   |
/// |   stringifier, toJSON, iterable declaration,|                   |   PropertyFlag.ConfigurableEnumerable |
/// |   iterator prototype next/return            |                   |   Writable)]                          |
/// | attribute (getter, or getter and setter)    | {      e:t, c:t } | [JsAccessor(Flags =                   |
/// |                                             |                   |   PropertyFlag.Configurable |         |
/// |                                             |                   |   PropertyFlag.Enumerable)]           |
/// | constant                                    | { w:f, e:t, c:f } | [JsProperty(Flags =                   |
/// |                                             |                   |   PropertyFlag.OnlyEnumerable)]       |
/// | 'constructor' on an interface prototype     | { w:t, e:f, c:t } | [JsProperty(Flags =                   |
/// |                                             |                   |   PropertyFlag.NonEnumerable)]        |
/// | @@toStringTag, the class string             | { w:f, e:f, c:t } | [JsSymbol("ToStringTag", Flags =      |
/// |                                             |                   |   PropertyFlag.Configurable)]         |
/// | @@iterator / @@asyncIterator                | { w:t, e:f, c:t } | [JsSymbolAlias] (its default)         |
/// | interface object, on the global             | { w:t, e:f, c:t } | WebApiRegistration.Install(...,       |
/// |                                             |                   |   PropertyFlag.NonEnumerable)         |
/// | namespace object or singleton, on the global| { w:t, e:t, c:t } | WebApiRegistration.Install(...,       |
/// |                                             |                   |   PropertyFlag.Configurable           |
/// |                                             |                   |   EnumerableWritable)                 |
/// | global operation (setTimeout, fetch, ...)   | { w:t, e:t, c:t } | the same, and it is an operation      |
/// | an interface object's 'prototype'           | { w:f, e:f, c:f } | the Constructor base class            |
/// | a built-in function's 'name' / 'length'     | { w:f, e:f, c:t } | ECMA-262, not WebIDL                  |
/// </code>
/// <para>
/// The citations, all WHATWG WebIDL: operations https://webidl.spec.whatwg.org/#es-operations, attributes
/// https://webidl.spec.whatwg.org/#es-attributes, constants https://webidl.spec.whatwg.org/#es-constants,
/// the <c>constructor</c> property and <c>@@toStringTag</c>
/// https://webidl.spec.whatwg.org/#interface-prototype-object, <c>@@iterator</c>
/// https://webidl.spec.whatwg.org/#es-iterable, the iterator prototype objects
/// https://webidl.spec.whatwg.org/#es-iterator-prototype-object and
/// https://webidl.spec.whatwg.org/#es-asynchronous-iterator-prototype-object, interface objects
/// https://webidl.spec.whatwg.org/#es-interfaces, namespace objects
/// https://webidl.spec.whatwg.org/#es-namespaces.
/// </para>
/// <para>
/// <b>A green matrix is not a conformance certificate.</b> It says every member carries the attributes its
/// kind requires; it says nothing about whether the member is on the right object, whether the object has the
/// right prototype, or whether the interface exists at all. <see cref="Divergences"/> is where that
/// distinction bit — it held the three members whose object model was not the browser's, and for which the
/// right answer was never a flag but the interface object they had no place on. It is empty now, and the
/// machinery stays for the next member that finds itself in the same position.
/// </para>
/// </remarks>
public class WebIdlPropertyAttributeTests
{
    /// <summary>
    /// The web-API objects a script can only reach by calling something, so the sweep cannot find them by
    /// walking the global object. Adding an iterable declaration to an interface adds a line here.
    /// </summary>
    private static readonly (string Path, string Expression)[] ReachedByCalling =
    [
        // WebIDL pair iterators: the iterator prototype object is the [[Prototype]] of the default iterator
        // object, and nothing else refers to it. https://webidl.spec.whatwg.org/#es-iterator-prototype-object
        ("Headers Iterator.prototype", "Object.getPrototypeOf(new Headers().entries())"),
        ("FormData Iterator.prototype", "Object.getPrototypeOf(new FormData().entries())"),
        ("URLSearchParams Iterator.prototype", "Object.getPrototypeOf(new URLSearchParams().entries())"),

        // The asynchronous iterator prototype object for ReadableStream's asynchronously iterable
        // declaration. https://webidl.spec.whatwg.org/#es-asynchronous-iterator-prototype-object
        ("ReadableStream AsyncIterator.prototype", "Object.getPrototypeOf(new ReadableStream().values())"),
    ];

    /// <summary>
    /// The members that deliberately do <b>not</b> carry their WebIDL kind's attributes, each with the reason
    /// — and, like the WPT exclusion table, an entry has to be real: <see cref="EveryRecordedDivergenceIsReal"/>
    /// fails on one that has started agreeing with the rule, so a fix cannot leave a stale exemption behind.
    /// </summary>
    /// <remarks>
    /// <b>Empty, and that is the news.</b> It used to hold <c>navigator.userAgent</c>, <c>scheduler.postTask</c>
    /// and <c>scheduler.yield</c>, which were own properties of their singleton because Jint exposed no
    /// <c>Navigator</c> and no <c>Scheduler</c> interface object for them to sit on. WebIDL's enumerability
    /// rule assumes the member is where the specification puts it — on the interface prototype object, where
    /// the enumerability is invisible from the instance — so declaring them enumerable on the singleton would
    /// have made <c>Object.keys(navigator)</c> answer <c>["userAgent"]</c> and <c>Object.keys(scheduler)</c>
    /// answer <c>["postTask", "yield"]</c>, which no implementation does. The fix was never a flag: it was the
    /// interface object, and once the members moved onto <c>Navigator.prototype</c> and
    /// <c>Scheduler.prototype</c> they became ordinary and the exemptions went with them.
    /// <see cref="TheSingletonsAreEmptyOfOwnKeys"/> pins the property the whole argument rested on.
    /// <para>
    /// <c>console</c> is the counter-example that proves the rule rather than a precedent against it: it is a
    /// WebIDL <i>namespace</i> (https://webidl.spec.whatwg.org/#es-namespaces), whose members genuinely are
    /// own properties of the namespace object, so its eighteen operations are enumerable here exactly as they
    /// are in Node.
    /// </para>
    /// </remarks>
    private static readonly string[] Divergences = [];

    /// <summary>
    /// Hosts the sweep cannot reach from the principal realm's global object, with the reason. Anything else
    /// missing is a hole in the sweep, not a fact about the engine.
    /// </summary>
    private static readonly string[] UnreachableFromThePrincipalRealm =
    [
        // A worker's global scope only exists inside a worker: neither interface object is installed on the
        // principal realm's global, and 'self' there is the ordinary global object.
        // https://html.spec.whatwg.org/multipage/workers.html#the-workerglobalscope-common-interface
        "Jint.WebApi.Workers.WorkerGlobalScopePrototype",
        "Jint.WebApi.Workers.DedicatedWorkerGlobalScopePrototype",
    ];

    [Test]
    public void EveryWebApiMemberCarriesTheAttributesItsWebIdlMemberKindRequires()
    {
        var sweep = Sweep();

        sweep.Failures.Should().BeEmpty(
            "every member of every Jint/WebApi/ object must carry the property attributes WebIDL gives its member kind");
    }

    [Test]
    public void TheSweepReachesEveryGeneratedWebApiHost()
    {
        var sweep = Sweep();

        var reached = new HashSet<string>(sweep.VisitedTypes, StringComparer.Ordinal);
        var documented = new HashSet<string>(UnreachableFromThePrincipalRealm, StringComparer.Ordinal);

        var missed = GeneratedWebApiHosts()
            .Where(name => !reached.Contains(name) && !documented.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        missed.Should().BeEmpty(
            "a [JsObject] host under Jint/WebApi/ the sweep never visits is a hole in the matrix — seed it in ReachedByCalling, or record why it is out of reach");

        // A floor, not a pin: it only ever moves up, and it is here so that a sweep which silently stopped
        // checking anything could not pass the two assertions above by checking nothing.
        sweep.Checked.Should().BeGreaterThan(500, "the sweep must actually be checking the surface");

        // And the other way round: a host that has become reachable must lose its exemption.
        var stillOutOfReach = documented.Where(name => reached.Contains(name)).ToList();
        stillOutOfReach.Should().BeEmpty("a host recorded as unreachable is reachable after all");
    }

    [Test]
    public void EveryRecordedDivergenceIsReal()
    {
        var sweep = Sweep();

        var observed = new HashSet<string>(sweep.Divergent, StringComparer.Ordinal);
        var stale = Divergences.Where(path => !observed.Contains(path)).ToList();

        stale.Should().BeEmpty(
            "a member recorded as diverging from its WebIDL kind now agrees with it — delete the entry rather than leaving a stale exemption");
    }

    /// <summary>
    /// The property the deleted divergences rested on, kept as the pin it always was: a platform-object
    /// singleton enumerates as empty, because its members belong to its interface prototype object. Node 24
    /// reports <c>Object.keys(navigator)</c> as <c>[]</c> — and <c>Reflect.ownKeys(navigator)</c> as <c>[]</c>
    /// too, since the instance has no own properties whatever — with <c>userAgent</c> an enumerable accessor
    /// on <c>Navigator.prototype</c>; a browser reports the same of <c>scheduler</c>.
    /// </summary>
    [Test]
    public void TheSingletonsAreEmptyOfOwnKeys()
    {
        var engine = BuildEngine();

        foreach (var singleton in new[] { "navigator", "scheduler", "crypto", "performance" })
        {
            engine.Evaluate($"JSON.stringify(Object.keys({singleton}))").AsString().Should().Be("[]", singleton);
            engine.Evaluate($"Reflect.ownKeys({singleton}).length").AsNumber().Should().Be(0, singleton);
        }

        // And the counter-example: console is a WebIDL namespace, so its operations really are own
        // enumerable properties of the namespace object, exactly as they are in Node.
        engine.Evaluate("Object.keys(console).length").AsNumber().Should().BeGreaterThan(0);
        engine.Evaluate("Object.getOwnPropertyDescriptor(console, 'log').enumerable").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The sweep proper — the walk, and what it found.
    /// </summary>
    private sealed record SweepResult(List<string> Failures, List<string> Divergent, HashSet<string> VisitedTypes, int Checked);

    private static SweepResult Sweep()
    {
        var engine = BuildEngine();
        var global = engine.Realm.GlobalObject;

        var failures = new List<string>();
        var divergent = new List<string>();
        var checkedMembers = 0;
        var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
        var divergences = new HashSet<string>(Divergences, StringComparer.Ordinal);

        var seen = new HashSet<ObjectInstance>(ReferenceComparer.Instance);
        var queue = new Queue<(string Path, ObjectInstance Object)>();

        void Enqueue(string path, JsValue value)
        {
            if (value is not ObjectInstance instance || !IsWebApiObject(instance))
            {
                return;
            }

            // Constructors and plain objects only: an operation's function object carries nothing but
            // ECMAScript's own 'name' and 'length', which are not part of this matrix.
            if (instance is ICallable && instance is not IConstructor)
            {
                return;
            }

            if (seen.Add(instance))
            {
                queue.Enqueue((path, instance));
            }
        }

        // The roots: every own name on the principal realm's global object that a bare engine does not have.
        var baseline = new HashSet<string>(
            new Engine().Realm.GlobalObject.GetOwnPropertyKeys(Types.String).Select(k => k.ToString()),
            StringComparer.Ordinal);

        foreach (var key in global.GetOwnPropertyKeys(Types.String))
        {
            var name = key.ToString();
            if (baseline.Contains(name))
            {
                continue;
            }

            var descriptor = global.GetOwnProperty(key);
            var value = descriptor.Value;

            var kind = value switch
            {
                IConstructor => MemberKind.InterfaceObject,
                ICallable => MemberKind.Operation,
                _ => MemberKind.GlobalSingleton,
            };

            Record("globalThis." + name, kind, descriptor, failures, divergent, divergences);
            checkedMembers++;
            Enqueue(name, value);
        }

        foreach (var (path, expression) in ReachedByCalling)
        {
            Enqueue(path, engine.Evaluate(expression));
        }

        while (queue.Count > 0)
        {
            var (path, obj) = queue.Dequeue();
            visitedTypes.Add(obj.GetType().FullName!);

            var isInterfaceObject = obj is IConstructor;

            foreach (var key in obj.GetOwnPropertyKeys())
            {
                var descriptor = obj.GetOwnProperty(key);
                var kind = Classify(key, descriptor, isInterfaceObject);
                var name = key is JsSymbol symbol ? "[" + symbol.ToString() + "]" : "." + key;

                Record(path + name, kind, descriptor, failures, divergent, divergences);
                checkedMembers++;

                if (!descriptor.IsAccessorDescriptor())
                {
                    Enqueue(path + name, descriptor.Value);
                }
            }
        }

        return new SweepResult(failures, divergent, visitedTypes, checkedMembers);
    }

    private static MemberKind Classify(JsValue key, PropertyDescriptor descriptor, bool isInterfaceObject)
    {
        if (key is JsSymbol symbol)
        {
            if (symbol == GlobalSymbolRegistry.ToStringTag)
            {
                return MemberKind.ClassString;
            }

            if (symbol == GlobalSymbolRegistry.Iterator || symbol == GlobalSymbolRegistry.AsyncIterator)
            {
                return MemberKind.IterationSymbol;
            }

            return MemberKind.UnknownSymbol;
        }

        if (isInterfaceObject)
        {
            if (key == CommonProperties.Prototype)
            {
                return MemberKind.InterfaceObjectPrototype;
            }

            if (key == CommonProperties.Name || key == CommonProperties.Length)
            {
                return MemberKind.BuiltinFunctionMetadata;
            }
        }
        else if (key == CommonProperties.Constructor)
        {
            return MemberKind.Constructor;
        }

        if (descriptor.IsAccessorDescriptor())
        {
            return MemberKind.Attribute;
        }

        return descriptor.Value is ICallable ? MemberKind.Operation : MemberKind.Constant;
    }

    private static void Record(
        string path,
        MemberKind kind,
        PropertyDescriptor descriptor,
        List<string> failures,
        List<string> divergent,
        HashSet<string> divergences)
    {
        if (kind == MemberKind.UnknownSymbol)
        {
            failures.Add($"{path} is a symbol-keyed member the matrix has no rule for — classify it in Classify() and give it a rule in Required()");
            return;
        }

        var (writable, enumerable, configurable) = Required(kind);

        var matches = descriptor.Enumerable == enumerable
                      && descriptor.Configurable == configurable
                      && (writable is null || descriptor.Writable == writable);

        if (matches)
        {
            return;
        }

        if (divergences.Contains(path))
        {
            divergent.Add(path);
            return;
        }

        failures.Add(
            $"{path} is a WebIDL {kind} and should be {Show(writable, enumerable, configurable)}, " +
            $"but is {Show(descriptor.IsAccessorDescriptor() ? null : descriptor.Writable, descriptor.Enumerable, descriptor.Configurable)}");
    }

    /// <summary>The rule table: what each WebIDL member kind's property attributes are.</summary>
    private static (bool? Writable, bool Enumerable, bool Configurable) Required(MemberKind kind) => kind switch
    {
        // https://webidl.spec.whatwg.org/#es-operations — "[[Writable]]: modifiable, [[Enumerable]]: true,
        // [[Configurable]]: modifiable", modifiable being true for everything that is not [Unforgeable].
        MemberKind.Operation => (true, true, true),

        // https://webidl.spec.whatwg.org/#es-attributes — an accessor; writability does not apply.
        MemberKind.Attribute => (null, true, true),

        // https://webidl.spec.whatwg.org/#es-constants
        MemberKind.Constant => (false, true, false),

        // https://webidl.spec.whatwg.org/#interface-prototype-object, the 'constructor' property.
        MemberKind.Constructor => (true, false, true),

        // https://webidl.spec.whatwg.org/#dfn-class-string — @@toStringTag.
        MemberKind.ClassString => (false, false, true),

        // https://webidl.spec.whatwg.org/#es-iterable — @@iterator and @@asyncIterator.
        MemberKind.IterationSymbol => (true, false, true),

        // https://webidl.spec.whatwg.org/#es-interfaces — the interface object on the global object.
        MemberKind.InterfaceObject => (true, false, true),

        // https://webidl.spec.whatwg.org/#es-interfaces, "create an interface object": 'prototype' is
        // { [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: false }.
        MemberKind.InterfaceObjectPrototype => (false, false, false),

        // ECMA-262, not WebIDL: https://tc39.es/ecma262/#sec-ecmascript-standard-built-in-objects.
        MemberKind.BuiltinFunctionMetadata => (false, false, true),

        // A namespace object or a platform-object singleton named by the global. WebIDL makes it a
        // [Replaceable] accessor pair; Jint installs an enumerable data property instead, a simplification
        // WebApiRegistration documents, and the enumerability is the part the two agree on.
        MemberKind.GlobalSingleton => (true, true, true),

        // MemberKind.UnknownSymbol never reaches here: Record fails it outright, because a symbol nobody has
        // classified has no rule to be held to.
        _ => throw new InvalidOperationException("No WebIDL rule for " + kind),
    };

    private static string Show(bool? writable, bool enumerable, bool configurable)
        => $"{{ writable: {(writable is null ? "n/a" : writable.Value ? "true" : "false")}, enumerable: {(enumerable ? "true" : "false")}, configurable: {(configurable ? "true" : "false")} }}";

    private enum MemberKind
    {
        Operation,
        Attribute,
        Constant,
        Constructor,
        ClassString,
        IterationSymbol,
        InterfaceObject,
        InterfaceObjectPrototype,
        BuiltinFunctionMetadata,
        GlobalSingleton,
        UnknownSymbol,
    }

    private static bool IsWebApiObject(ObjectInstance instance)
        => instance.GetType().Namespace?.StartsWith("Jint.WebApi", StringComparison.Ordinal) == true;

    /// <summary>
    /// Every <c>[JsObject]</c> host under <c>Jint/WebApi/</c>, found by the method the generator emits on one
    /// — so a host added tomorrow is in this list tomorrow, without anybody writing it down.
    /// </summary>
    private static IEnumerable<string> GeneratedWebApiHosts()
        => typeof(Engine).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("Jint.WebApi", StringComparison.Ordinal) == true)
            .Where(t => t.GetMethod("CreateProperties_Generated", BindingFlags.Instance | BindingFlags.NonPublic) is not null)
            .Select(t => t.FullName!);

    /// <summary>Every feature there is, so the sweep reaches every host an engine can carry.</summary>
    private static Engine BuildEngine()
    {
        var everything = WebApiFeatures.None;
        foreach (var feature in Enum.GetValues<WebApiFeatures>())
        {
            everything |= feature;
        }

        return new Engine(options =>
        {
            options.UseWebApis(everything);

            // The one global conditioned on something other than a flag: without a provider there is no
            // execution resource for a worker, so WebApiRegistration leaves `Worker` absent rather than
            // installing a constructor that could only throw. A provider that refuses every request is
            // enough to make the interface object exist, which is all the sweep needs.
            options.WebApi.Workers.Provider = new RefusesEveryWorker();
        });
    }

    private sealed class RefusesEveryWorker : WorkerProvider
    {
        public override Engine? CreateWorkerEngine(WorkerRequest request) => null;
    }

    private sealed class ReferenceComparer : IEqualityComparer<ObjectInstance>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(ObjectInstance? x, ObjectInstance? y) => ReferenceEquals(x, y);

        public int GetHashCode(ObjectInstance obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
#endif
