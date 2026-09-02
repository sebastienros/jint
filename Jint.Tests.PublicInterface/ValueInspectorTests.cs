#nullable enable
#pragma warning disable JINT0002 // preview surface, acknowledged for the suite that exercises it

using Jint.Diagnostics;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="ValueInspector"/> seen from outside the assembly: what each kind of value describes as, and
/// the invariant the whole surface exists for — describing a value runs none of that value's code.
/// </summary>
/// <remarks>
/// The invariant is pinned positively rather than by inspection: every value that could run something is
/// built so that running it would be observable — a getter that counts and throws, a proxy whose every trap
/// throws, a <c>toString</c> that throws — and the assertion is that the description came back and the
/// counter is still zero.
/// </remarks>
public class ValueInspectorTests
{
    private static ValueDescription Describe(string script, ValueInspectorOptions? options = null)
        => ValueInspector.Describe(new Engine().Evaluate(script), options);

    // ---- the invariant -------------------------------------------------------------------------------

    [Test]
    public void AGetterIsReportedAndNeverInvoked()
    {
        var engine = new Engine();
        var calls = 0;
        engine.SetValue("record", new Action(() => calls++));
        var value = engine.Evaluate("({ plain: 1, get boom() { record(); throw new Error('ran'); }, set only(v) {} })");

        var description = ValueInspector.Describe(value);

        calls.Should().Be(0, "describing a value must not invoke its accessors");
        description.Kind.Should().Be(ValueKind.Object);

        var entries = description.Entries;
        entries.Should().HaveCount(3);
        entries[0].Key.Should().Be("plain");
        entries[0].IsAccessor.Should().BeFalse();
        entries[1].Key.Should().Be("boom");
        entries[1].IsAccessor.Should().BeTrue();
        entries[1].Value.Kind.Should().Be(ValueKind.Accessor);
        entries[1].Value.Description.Should().Be("[Getter]");
        entries[2].Value.Description.Should().Be("[Setter]");
    }

    [Test]
    public void AGetterAndSetterPairIsReportedAsBoth()
    {
        var description = Describe("({ get both() { return 1; }, set both(v) {} })");

        description.Entries.Should().ContainSingle().Which.Value.Description.Should().Be("[Getter/Setter]");
    }

    [Test]
    public void AProxyIsDescribedWithoutRunningATrap()
    {
        var engine = new Engine();
        var calls = 0;
        engine.SetValue("record", new Action(() => calls++));
        var value = engine.Evaluate("""
            new Proxy({ a: 1 }, {
                ownKeys() { record(); throw new Error('ownKeys ran'); },
                getOwnPropertyDescriptor() { record(); throw new Error('getOwnPropertyDescriptor ran'); },
                get() { record(); throw new Error('get ran'); },
                getPrototypeOf() { record(); throw new Error('getPrototypeOf ran'); },
                has() { record(); throw new Error('has ran'); },
            })
            """);

        var description = ValueInspector.Describe(value);

        calls.Should().Be(0, "a trap is script, and describing a value must run none");
        description.Kind.Should().Be(ValueKind.Proxy);
        description.Description.Should().Be("Proxy");
        description.Entries.Should().BeEmpty();
    }

    [Test]
    public void ARevokedProxyIsDescribedRatherThanThrownOn()
    {
        var description = Describe("(function () { var r = Proxy.revocable({}, {}); r.revoke(); return r.proxy; })()");

        description.Kind.Should().Be(ValueKind.Proxy);
        description.Description.Should().Be("Proxy (revoked)");
    }

    [Test]
    public void NothingIsReadThroughAConfigurablePrototypeMember()
    {
        var engine = new Engine();
        var calls = 0;
        engine.SetValue("record", new Action(() => calls++));

        // Every one of these is the property a naive renderer would read the value through.
        var value = engine.Evaluate("""
            (function () {
                RegExp.prototype.__defineGetter__('source', function () { record(); throw new Error('source ran'); });
                Date.prototype.toISOString = function () { record(); throw new Error('toISOString ran'); };
                Object.defineProperty(Object.prototype, Symbol.toStringTag, { get() { record(); throw new Error('tag ran'); }, configurable: true });
                return [/a/g, new Date(0), { a: 1 }];
            })()
            """);

        var description = ValueInspector.Describe(value);

        calls.Should().Be(0);
        description.Entries[0].Value.Description.Should().Be("/a/g");
        description.Entries[1].Value.Kind.Should().Be(ValueKind.Date);
    }

    [Test]
    public void AnErrorWhoseNameIsAGetterIsDescribedWithoutRunningIt()
    {
        var engine = new Engine();
        var calls = 0;
        engine.SetValue("record", new Action(() => calls++));
        var value = engine.Evaluate("""
            (function () {
                var e = new Error('boom');
                Object.defineProperty(e, 'name', { get() { record(); throw new Error('name ran'); } });
                return e;
            })()
            """);

        var description = ValueInspector.Describe(value);

        calls.Should().Be(0);
        description.Kind.Should().Be(ValueKind.Error);

        // The accessor is refused, so the label falls back to the constructor the error was built by.
        description.Description.Should().Be("Error: boom");
    }

    [Test]
    public void ACyclicObjectTerminates()
    {
        var description = Describe("(function () { var a = { name: 'a' }; a.self = a; return a; })()", new ValueInspectorOptions { MaxDepth = 32 });

        description.Entries.Should().HaveCount(2);
        var self = description.Entries[1];
        self.Key.Should().Be("self");
        self.Value.Description.Should().Be("[Circular]");
    }

    // ---- the kinds -----------------------------------------------------------------------------------

    [Test]
    public void ThePrimitivesDescribeAsThemselves()
    {
        ValueInspector.Describe(JsValue.Undefined).Kind.Should().Be(ValueKind.Undefined);
        ValueInspector.Describe(JsValue.Undefined).Description.Should().Be("undefined");
        ValueInspector.Describe(JsValue.Null).Kind.Should().Be(ValueKind.Null);
        ValueInspector.Describe(JsValue.Null).Description.Should().Be("null");

        Describe("true").Should().Match<ValueDescription>(d => d.Kind == ValueKind.Boolean && d.Description == "true");
        Describe("1.5").Should().Match<ValueDescription>(d => d.Kind == ValueKind.Number && d.Description == "1.5");
        Describe("NaN").Description.Should().Be("NaN");
        Describe("10n").Should().Match<ValueDescription>(d => d.Kind == ValueKind.BigInt && d.Description == "10n");
        Describe("'hi'").Should().Match<ValueDescription>(d => d.Kind == ValueKind.String && d.Description == "hi");
        Describe("Symbol('s')").Should().Match<ValueDescription>(d => d.Kind == ValueKind.Symbol && d.Description == "Symbol(s)");
    }

    [Test]
    public void AFunctionIsNamedNeverPrinted()
    {
        Describe("function f(a, b) { return 1; }; f").Should()
            .Match<ValueDescription>(d => d.Kind == ValueKind.Function && d.Description == "ƒ f()");

        Describe("(function () {})").Description.Should().Be("ƒ ()");
        Describe("(async function g() {})").Description.Should().Be("async ƒ g()");
        Describe("(function* h() {})").Description.Should().Be("ƒ* h()");
        Describe("(async function* i() {})").Description.Should().Be("async ƒ* i()");
        Describe("class C {}; C").Description.Should().Be("class C");
        Describe("(class {})").Description.Should().Be("class");
    }

    [Test]
    public void ThePlainCollectionsCarryTheirSizeAndTheirEntries()
    {
        var array = Describe("[1, 'x', null]");
        array.Kind.Should().Be(ValueKind.Array);
        array.Description.Should().Be("Array(3)");
        array.ClassName.Should().Be("Array");
        array.Entries.Should().HaveCount(3);
        array.Entries[1].Key.Should().Be("1");
        array.Entries[1].Value.Description.Should().Be("x");
        array.Overflow.Should().BeFalse();

        var map = Describe("new Map([['a', 1]])");
        map.Kind.Should().Be(ValueKind.Map);
        map.Description.Should().Be("Map(1)");
        var entry = map.Entries.Should().ContainSingle().Which;
        entry.EntryKey.Should().NotBeNull();
        entry.EntryKey!.Description.Should().Be("a");
        entry.Value.Description.Should().Be("1");

        var set = Describe("new Set([1, 2])");
        set.Kind.Should().Be(ValueKind.Set);
        set.Description.Should().Be("Set(2)");
        set.Entries.Should().HaveCount(2);
        set.Entries[0].EntryKey.Should().BeNull();
    }

    [Test]
    public void TheBinaryKindsAnswerFromTheirSlots()
    {
        var typed = Describe("new Uint8Array([1, 2, 3])");
        typed.Kind.Should().Be(ValueKind.TypedArray);
        typed.Description.Should().Be("Uint8Array(3)");
        typed.Entries.Should().HaveCount(3);

        Describe("new ArrayBuffer(8)").Should()
            .Match<ValueDescription>(d => d.Kind == ValueKind.ArrayBuffer && d.Description == "ArrayBuffer(8)");
        Describe("new DataView(new ArrayBuffer(8))").Should()
            .Match<ValueDescription>(d => d.Kind == ValueKind.DataView && d.Description == "DataView(8)");

        var detached = Describe("(function () { var b = new ArrayBuffer(8); b.transfer(); return b; })()");
        detached.Description.Should().Be("ArrayBuffer(detached)");
    }

    [Test]
    public void TheWeakKindsAreNamedAndNeverOpened()
    {
        Describe("new WeakMap()").Should().Match<ValueDescription>(d => d.Kind == ValueKind.WeakMap && d.Entries.Count == 0);
        Describe("new WeakSet()").Should().Match<ValueDescription>(d => d.Kind == ValueKind.WeakSet && d.Entries.Count == 0);
        Describe("new WeakRef({})").Should().Match<ValueDescription>(d => d.Kind == ValueKind.WeakRef && d.Entries.Count == 0);
    }

    [Test]
    public void APromiseCarriesItsStateAndItsResult()
    {
        var pending = Describe("new Promise(function () {})");
        pending.Kind.Should().Be(ValueKind.Promise);
        pending.Description.Should().Be("Promise { <pending> }");
        pending.PromiseState.Should().Be(ValuePromiseState.Pending);
        pending.PromiseResult.Should().BeNull();

        var fulfilled = Describe("Promise.resolve(1)");
        fulfilled.Description.Should().Be("Promise { <fulfilled>: 1 }");
        fulfilled.PromiseState.Should().Be(ValuePromiseState.Fulfilled);
        fulfilled.PromiseResult.Should().NotBeNull();
        fulfilled.PromiseResult!.Description.Should().Be("1");

        var rejected = Describe("(function () { var p = Promise.reject(new TypeError('no')); p.catch(function () {}); return p; })()");
        rejected.PromiseState.Should().Be(ValuePromiseState.Rejected);
        rejected.Description.Should().Be("Promise { <rejected>: TypeError: no }");
    }

    [Test]
    public void TheRemainingExoticsAreNamedByTheirSlots()
    {
        Describe("new Date(0)").Should()
            .Match<ValueDescription>(d => d.Kind == ValueKind.Date && d.Description == "1970-01-01T00:00:00.000Z");
        Describe("new Date(NaN)").Description.Should().Be("Invalid Date");
        Describe("/ab+c/gi").Should().Match<ValueDescription>(d => d.Kind == ValueKind.RegExp && d.Description == "/ab+c/gi");
        Describe("new TypeError('x')").Should()
            .Match<ValueDescription>(d => d.Kind == ValueKind.Error && d.Description == "TypeError: x");
        Describe("(function () { return arguments; })(1, 2)").Kind.Should().Be(ValueKind.Arguments);
        Describe("(function* g() { yield 1; })()").Kind.Should().Be(ValueKind.Generator);
        Describe("[1].values()").Kind.Should().Be(ValueKind.Iterator);
        Describe("new String('x')").Description.Should().Be("[String: x]");
    }

    [Test]
    public void AClassInstanceIsNamedByItsConstructor()
    {
        var described = Describe("class Point { constructor() { this.x = 1; } }; new Point()");

        described.Kind.Should().Be(ValueKind.Object);
        described.ClassName.Should().Be("Point");
        described.Description.Should().Be("Point");
        described.Entries.Should().ContainSingle().Which.Key.Should().Be("x");
    }

    [Test]
    public void ANullPrototypeObjectHasNoConstructorToName()
    {
        var described = Describe("Object.assign(Object.create(null), { a: 1 })");

        described.ClassName.Should().BeNull();
        described.Description.Should().Be("Object");
        described.Entries.Should().ContainSingle();
    }

    [Test]
    public void SymbolKeysComeAfterStringKeys()
    {
        var described = Describe("({ [Symbol.iterator]: 1, a: 2 })");

        described.Entries.Should().HaveCount(2);
        described.Entries[0].Key.Should().Be("a");
        described.Entries[1].Key.Should().Be("Symbol(Symbol.iterator)");
    }

    // ---- host objects --------------------------------------------------------------------------------

    private sealed class Widget
    {
        public string Name => throw new InvalidOperationException("a host getter must not run");
    }

    [Test]
    public void AWrappedClrObjectIsNamedAndNeverRead()
    {
        var engine = new Engine();
        engine.SetValue("widget", new Widget());

        var description = ValueInspector.Describe(engine.Evaluate("widget"));

        description.Kind.Should().Be(ValueKind.HostObject);
        description.ClassName.Should().Be(nameof(Widget));
        description.Description.Should().Be(typeof(Widget).FullName);
        description.Entries.Should().BeEmpty("reading a member of a host object is running host code");
    }

    [Test]
    public void AClrTypeReferenceIsNamedByTheTypeItStandsFor()
    {
        var engine = new Engine();
        engine.SetValue("Widget", TypeReference.CreateTypeReference<Widget>(engine));

        var description = ValueInspector.Describe(engine.Evaluate("Widget"));

        description.Kind.Should().Be(ValueKind.HostObject);
        description.ClassName.Should().Be(nameof(Widget));
    }

    // ---- the bounds ----------------------------------------------------------------------------------

    [Test]
    public void MaxEntriesBoundsTheWalkAndSetsOverflow()
    {
        var options = new ValueInspectorOptions { MaxEntries = 2 };

        var array = ValueInspector.Describe(new Engine().Evaluate("[1, 2, 3, 4]"), options);
        array.Entries.Should().HaveCount(2);
        array.Overflow.Should().BeTrue();

        var obj = ValueInspector.Describe(new Engine().Evaluate("({ a: 1, b: 2, c: 3 })"), options);
        obj.Entries.Should().HaveCount(2);
        obj.Overflow.Should().BeTrue();

        var set = ValueInspector.Describe(new Engine().Evaluate("new Set([1, 2, 3])"), options);
        set.Entries.Should().HaveCount(2);
        set.Overflow.Should().BeTrue();
    }

    [Test]
    public void MaxDepthStopsTheWalkWithTheHeaderIntact()
    {
        var described = ValueInspector.Describe(
            new Engine().Evaluate("({ a: { b: { c: 1 } } })"),
            new ValueInspectorOptions { MaxDepth = 1 });

        var a = described.Entries.Should().ContainSingle().Which.Value;
        a.Description.Should().Be("Object");
        a.Entries.Should().BeEmpty("beyond the depth bound a value keeps its header and loses its entries");
    }

    [Test]
    public void ALongStringIsTruncated()
    {
        var engine = new Engine();
        engine.SetValue("long", new string('x', 10_000));

        var described = ValueInspector.Describe(engine.Evaluate("long"));

        described.Kind.Should().Be(ValueKind.String);
        described.Description.Should().HaveLength(101);
        described.Description.Should().EndWith("…");

        var wider = ValueInspector.Describe(engine.Evaluate("long"), new ValueInspectorOptions { MaxStringLength = 10 });
        wider.Description.Should().Be(new string('x', 10) + "…");
    }

    [Test]
    public void ADescriptionOutlivesTheCallAndHoldsNoValue()
    {
        var described = Describe("({ a: [1, 2] })");

        // Nothing here is engine-affine, which is the whole point: it is strings and further descriptions.
        described.ToString().Should().Be("Object");
        described.Entries[0].Value.Entries[1].Value.Description.Should().Be("2");
    }

    [Test]
    public void DescribingNothingIsRefused()
    {
        var describe = () => ValueInspector.Describe(null!);

        describe.Should().Throw<ArgumentNullException>();
    }
}
