#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="JsObjectShape"/> — the host-facing way to declare a prototype-like object's members once per
/// process and create one such object per engine. These tests live in the public-interface suite on purpose:
/// the project has no <c>InternalsVisibleTo</c> grant, so every green assertion here is proof that a
/// third-party embedder can reach the same capability.
///
/// <para>
/// The three things the API promises, and which the tests below pin: a built shape is immutable and shared
/// (two engines instantiate from one <c>static readonly</c> field and cannot observe each other), members
/// materialize lazily and then keep a stable identity, and nothing about the representation is
/// script-visible — every mutation an ordinary object would accept behaves identically here, including the
/// ones that force the object back to the ordinary property dictionary.
/// </para>
/// </summary>
public class HostPrototypeShapeTests
{
    // The shared prototype description, declared once exactly as a host would: process-wide, engine-free,
    // capturing nothing but static delegates. Declaration order is own-key order, which several tests assert
    // verbatim, so members are grouped by kind rather than by enumerability.
    private static readonly JsObjectShape ElementShape = new JsObjectShape.Builder()
        .Method("greet", static (thisObject, args) => new JsString("hello " + (args.Length > 0 ? args[0].ToString() : "world")), length: 1)
        .Method("secretMethod", static (thisObject, args) => new JsString("secret"), enumerable: false)
        .Accessor("tagName", getter: GetTag, setter: SetTag)
        .Accessor("readOnlyTag", getter: GetTag)
        .Constant("ELEMENT_NODE", JsNumber.Create(1))
        .Constant("SECRET", new JsString("s"), enumerable: false)
        .PerRealmSlot("constructor", enumerable: false)
        .ToStringTag("Element")
        .Build();

    // Three shapes stacked into a prototype chain, the way a DOM binding stacks Node -> Element -> HTMLElement.
    // Each level declares a member of its own, so a lookup can be aimed at any depth — and an absent name has
    // to be refused by every level in turn before the chain as a whole can answer it.
    private static readonly JsObjectShape ChainRootShape = new JsObjectShape.Builder()
        .Method("rootMember", static (thisObject, args) => new JsString("root"))
        .Constant("ROOT_CONST", JsNumber.Create(10))
        .Build();

    private static readonly JsObjectShape ChainMiddleShape = new JsObjectShape.Builder()
        .Method("middleMember", static (thisObject, args) => new JsString("middle"))
        .Build();

    private static readonly JsObjectShape ChainLeafShape = new JsObjectShape.Builder()
        .Method("leafMember", static (thisObject, args) => new JsString("leaf"))
        .Build();

    // Engine-independent by construction: state comes from the receiver, never from a captured engine.
    private static JsValue GetTag(JsValue thisObject, JsValue[] arguments)
        => thisObject is ObjectInstance o ? o.Get("_tag") : JsValue.Undefined;

    private static JsValue SetTag(JsValue thisObject, JsValue[] arguments)
    {
        if (thisObject is ObjectInstance o)
        {
            o.Set("_tag", arguments.Length > 0 ? arguments[0] : JsValue.Undefined);
        }

        return JsValue.Undefined;
    }

    private static Engine EngineWithPrototype(out ObjectInstance prototype)
    {
        var engine = new Engine();
        prototype = ElementShape.Instantiate(engine);
        engine.SetValue("proto", prototype);
        engine.Execute("var el = Object.create(proto);");
        return engine;
    }

    // ---- reachability and representation witness ----

    [Fact]
    public void ShapeCountReportsDeclaredMemberCount()
    {
        // Eight string-keyed members; Symbol.toStringTag is not one of them (symbols live outside the
        // shared string-keyed layout).
        ElementShape.Count.Should().Be(7);
    }

    [Fact]
    public void AnInstantiatedObjectUsesTheSharedBuiltinLayout()
    {
        var engine = EngineWithPrototype(out var prototype);

        // GetObjectRepresentation observes without perturbing, so it reports the settled representation only
        // after something has touched the object.
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);

        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void InstantiateUsesObjectPrototypeByDefaultAndTheGivenPrototypeOtherwise()
    {
        var engine = new Engine();

        var defaulted = ElementShape.Instantiate(engine);
        engine.SetValue("defaulted", defaulted);
        engine.Evaluate("Object.getPrototypeOf(defaulted) === Object.prototype").Should().Be(true);

        var bare = ElementShape.Instantiate(engine, prototype: null);
        engine.SetValue("bare", bare);
        engine.Evaluate("Object.getPrototypeOf(bare) === null").Should().Be(true);
        // still fully functional without Object.prototype behind it
        engine.Evaluate("bare.ELEMENT_NODE").Should().Be(1);

        var chained = ElementShape.Instantiate(engine, defaulted);
        engine.SetValue("chained", chained);
        engine.Evaluate("Object.getPrototypeOf(chained) === defaulted").Should().Be(true);
    }

    [Fact]
    public void InstantiateRejectsANullEngineAndAForeignPrototype()
    {
        Invoking(() => ElementShape.Instantiate(null!)).Should().Throw<ArgumentNullException>();
        Invoking(() => ElementShape.Instantiate(null!, null)).Should().Throw<ArgumentNullException>();

        var engine = new Engine();
        var other = new Engine();
        Invoking(() => ElementShape.Instantiate(engine, ElementShape.Instantiate(other)))
            .Should().Throw<ArgumentException>()
            .WithMessage("*different engine*");
    }

    // ---- members work through the prototype ----

    [Fact]
    public void MethodsAccessorsAndConstantsResolveThroughAnInstance()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("el.greet('world')").Should().Be("hello world");
        engine.Evaluate("el.greet.length").Should().Be(1);
        engine.Evaluate("el.greet.name").Should().Be("greet");
        engine.Evaluate("el.secretMethod()").Should().Be("secret");
        engine.Evaluate("el.ELEMENT_NODE").Should().Be(1);
        engine.Evaluate("el.SECRET").Should().Be("s");

        // The accessor's `this` is the receiver, not the prototype that owns the member.
        engine.Execute("el._tag = 'DIV';");
        engine.Evaluate("el.tagName").Should().Be("DIV");
        engine.Evaluate("proto.tagName").Should().Be(JsValue.Undefined);

        engine.Execute("var other = Object.create(proto); other._tag = 'SPAN';");
        engine.Evaluate("other.tagName").Should().Be("SPAN");
        engine.Evaluate("el.tagName").Should().Be("DIV");

        // ...and the setter reaches the receiver too.
        engine.Execute("el.tagName = 'P';");
        engine.Evaluate("el._tag").Should().Be("P");
        engine.Evaluate("other._tag").Should().Be("SPAN");
    }

    [Fact]
    public void AnAccessorWithoutASetterIsReadOnly()
    {
        var engine = EngineWithPrototype(out _);
        engine.Execute("el._tag = 'DIV';");

        engine.Evaluate("el.readOnlyTag").Should().Be("DIV");
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'readOnlyTag').set").Should().Be(JsValue.Undefined);
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(proto, 'readOnlyTag').get").Should().Be("function");

        Invoking(() => engine.Execute("'use strict'; el.readOnlyTag = 'X';"))
            .Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void AccessorHalvesAreNamedPerSpecConvention()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'tagName').get.name").Should().Be("get tagName");
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'tagName').set.name").Should().Be("set tagName");
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'tagName').get.length").Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'tagName').set.length").Should().Be(1);
    }

    [Fact]
    public void MaterializedMembersKeepAStableIdentityWithinAnEngine()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("proto.greet === proto.greet").Should().Be(true);
        engine.Evaluate("el.greet === proto.greet").Should().Be(true);

        var descriptorIdentity = engine.Evaluate(
            "Object.getOwnPropertyDescriptor(proto, 'tagName').get === Object.getOwnPropertyDescriptor(proto, 'tagName').get");
        descriptorIdentity.Should().Be(true);
    }

    [Fact]
    public void MaterializedFunctionsBelongToTheInstantiatingRealm()
    {
        // Materialization is lazy, so it can be triggered while a different realm is the active one — a
        // ShadowRealm callback, say. The function must still be anchored to the realm of the object it came
        // from, which is what its [[Prototype]] shows.
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("Object.getPrototypeOf(proto.greet) === Function.prototype").Should().Be(true);
        engine.Evaluate("Object.getPrototypeOf(Object.getOwnPropertyDescriptor(proto, 'tagName').get) === Function.prototype")
            .Should().Be(true);

        // Running a ShadowRealm in between changes nothing about members materialized afterwards.
        engine.Execute("var sr = new ShadowRealm(); sr.evaluate('1 + 1');");
        engine.Evaluate("Object.getPrototypeOf(proto.secretMethod) === Function.prototype").Should().Be(true);
        engine.Evaluate("proto.secretMethod()").Should().Be("secret");
    }

    [Fact]
    public void ToStringTagIsApplied()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("Object.prototype.toString.call(proto)").Should().Be("[object Element]");
        engine.Evaluate("Object.prototype.toString.call(el)").Should().Be("[object Element]");
        engine.Evaluate("proto[Symbol.toStringTag]").Should().Be("Element");

        // the intrinsic convention: non-enumerable, non-writable, configurable
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag).enumerable").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag).writable").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag).configurable").Should().Be(true);
    }

    // ---- constants ----

    [Fact]
    public void ConstantsAreNonWritableAndNonConfigurable()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'ELEMENT_NODE').writable").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'ELEMENT_NODE').configurable").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'ELEMENT_NODE').enumerable").Should().Be(true);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'SECRET').enumerable").Should().Be(false);

        // sloppy write is silently ignored, strict write throws, the value never changes
        engine.Execute("proto.ELEMENT_NODE = 9;");
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
        Invoking(() => engine.Execute("'use strict'; proto.ELEMENT_NODE = 9;")).Should().Throw<JavaScriptException>();

        Invoking(() => engine.Execute("Object.defineProperty(proto, 'ELEMENT_NODE', { value: 9 });"))
            .Should().Throw<JavaScriptException>();
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);

        // a same-value redefine is allowed by the spec and must leave the shared descriptor intact
        engine.Execute("Object.defineProperty(proto, 'ELEMENT_NODE', { value: 1, writable: false, configurable: false });");
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
    }

    [Fact]
    public void DeletingANonConfigurableConstantFailsPerSpec()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Evaluate("delete proto.ELEMENT_NODE").Should().Be(false);
        Invoking(() => engine.Execute("'use strict'; delete proto.ELEMENT_NODE;")).Should().Throw<JavaScriptException>();

        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    // ---- per-realm slots ----

    [Fact]
    public void APerRealmValueSlotStartsUndefinedAndIsFilledPerEngine()
    {
        var engineA = new Engine();
        var protoA = ElementShape.Instantiate(engineA);
        engineA.SetValue("proto", protoA);

        var engineB = new Engine();
        var protoB = ElementShape.Instantiate(engineB);
        engineB.SetValue("proto", protoB);

        engineA.Evaluate("proto.constructor").Should().Be(JsValue.Undefined);

        // The setup-time fill: a raw descriptor store into a declared slot, which replaces it in place.
        protoA.FastSetProperty("constructor", new PropertyDescriptor(new JsString("ctor-A"), PropertyFlag.NonEnumerable));
        protoB.FastSetProperty("constructor", new PropertyDescriptor(new JsString("ctor-B"), PropertyFlag.NonEnumerable));

        engineA.Evaluate("proto.constructor").Should().Be("ctor-A");
        engineB.Evaluate("proto.constructor").Should().Be("ctor-B");

        // ...and the object is still shaped afterwards (a declared name never forces the dictionary)
        engineA.Advanced.GetObjectRepresentation(protoA).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engineB.Advanced.GetObjectRepresentation(protoB).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

        // the declared attributes permit the ordinary spec-validated route too
        engineA.Execute("Object.defineProperty(proto, 'constructor', { value: 'redefined' });");
        engineA.Evaluate("proto.constructor").Should().Be("redefined");
        engineB.Evaluate("proto.constructor").Should().Be("ctor-B");
    }

    [Fact]
    public void APerRealmFactorySlotRunsOnceOnFirstAccessAndNotBefore()
    {
        var calls = 0;
        var shape = new JsObjectShape.Builder()
            .Method("m", static (t, a) => JsValue.Undefined)
            .PerRealmSlot("lazy", o => { calls++; return new JsString("value-" + calls.ToString(System.Globalization.CultureInfo.InvariantCulture)); })
            .Build();

        var engine = new Engine();
        var proto = shape.Instantiate(engine);
        engine.SetValue("proto", proto);
        calls.Should().Be(0, "instantiation must not run any member factory");

        // Touching a different member initializes the object but must not run this factory.
        engine.Evaluate("typeof proto.m").Should().Be("function");
        calls.Should().Be(0);

        engine.Evaluate("proto.lazy").Should().Be("value-1");
        engine.Evaluate("proto.lazy").Should().Be("value-1");
        calls.Should().Be(1, "the factory result is cached per instantiated object");

        // A second object from the same shape gets its own value.
        var second = shape.Instantiate(engine);
        engine.SetValue("second", second);
        engine.Evaluate("second.lazy").Should().Be("value-2");
        calls.Should().Be(2);
    }

    [Fact]
    public void APerRealmFactorySlotReceivesTheInstantiatedObject()
    {
        var shape = new JsObjectShape.Builder()
            .PerRealmSlot("self", static o => o)
            .Build();

        var engine = new Engine();
        var proto = shape.Instantiate(engine);
        engine.SetValue("proto", proto);

        engine.Evaluate("proto.self === proto").Should().Be(true);
    }

    // ---- sharing across engines ----

    [Fact]
    public void TwoEnginesShareOneShapeWithoutSharingAnythingElse()
    {
        var engineA = new Engine();
        var protoA = ElementShape.Instantiate(engineA);
        engineA.SetValue("proto", protoA);
        engineA.Execute("var el = Object.create(proto); el._tag = 'A';");

        var engineB = new Engine();
        var protoB = ElementShape.Instantiate(engineB);
        engineB.SetValue("proto", protoB);
        engineB.Execute("var el = Object.create(proto); el._tag = 'B';");

        engineA.Evaluate("el.greet('a')").Should().Be("hello a");
        engineB.Evaluate("el.greet('b')").Should().Be("hello b");
        engineA.Evaluate("el.tagName").Should().Be("A");
        engineB.Evaluate("el.tagName").Should().Be("B");

        // A polyfill overwrite in A is invisible in B...
        engineA.Execute("proto.greet = function () { return 'patched'; };");
        engineA.Evaluate("el.greet()").Should().Be("patched");
        engineB.Evaluate("el.greet('b')").Should().Be("hello b");

        // ...as is a redefine, a delete and a freeze. (Delete before freeze: freezing makes every member
        // non-configurable, which is exactly what a later delete is supposed to be refused by.)
        engineA.Execute("Object.defineProperty(proto, 'ELEMENT_NODE_2', { value: 2, enumerable: true });");
        engineB.Evaluate("'ELEMENT_NODE_2' in proto").Should().Be(false);

        engineA.Execute("delete proto.secretMethod;");
        engineA.Evaluate("proto.secretMethod").Should().Be(JsValue.Undefined);
        engineB.Evaluate("proto.secretMethod()").Should().Be("secret");

        engineA.Execute("Object.freeze(proto);");
        engineA.Evaluate("Object.isFrozen(proto)").Should().Be(true);
        engineB.Evaluate("Object.isFrozen(proto)").Should().Be(false);

        // B is untouched down to its representation and its constant attributes.
        engineB.Advanced.GetObjectRepresentation(protoB).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engineB.Evaluate("Object.getOwnPropertyDescriptor(proto, 'ELEMENT_NODE').writable").Should().Be(false);
        engineB.Evaluate("Object.getOwnPropertyDescriptor(proto, 'ELEMENT_NODE').configurable").Should().Be(false);
        engineB.Evaluate("Object.getOwnPropertyDescriptor(proto, 'ELEMENT_NODE').enumerable").Should().Be(true);
        engineB.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
    }

    [Fact]
    public void FunctionIdentityIsPerEngine()
    {
        var engineA = new Engine();
        var protoA = ElementShape.Instantiate(engineA);
        var engineB = new Engine();
        var protoB = ElementShape.Instantiate(engineB);

        var greetA = protoA.Get("greet");
        var greetB = protoB.Get("greet");

        greetA.Should().NotBeNull();
        ReferenceEquals(greetA, greetB).Should().BeFalse("each engine materializes its own function objects");
        ReferenceEquals(greetA, protoA.Get("greet")).Should().BeTrue("identity is stable within one engine");
    }

    // ---- mutation and deopt semantics ----

    [Fact]
    public void AWritableMethodCanBeOverwrittenInPlace()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Evaluate("el.greet('x')").Should().Be("hello x");
        engine.Execute("proto.greet = function () { return 'patched'; };");
        engine.Evaluate("el.greet('x')").Should().Be("patched");
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void RedefiningADeclaredMemberIncludingDataToAccessorKeepsTheSharedLayout()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("Object.defineProperty(proto, 'greet', { value: 42, writable: true, enumerable: true, configurable: true });");
        engine.Evaluate("proto.greet").Should().Be(42);

        engine.Execute("Object.defineProperty(proto, 'greet', { get: function () { return 'from getter'; }, configurable: true });");
        engine.Evaluate("proto.greet").Should().Be("from getter");
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(proto, 'greet').get").Should().Be("function");

        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void AddingANewStringKeyKeepsTheSharedLayoutAndAppendsToOwnKeyOrder()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("proto.added = 1; Object.defineProperty(proto, 'defined', { value: 2, enumerable: true, configurable: true });");

        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')")
            .Should().Be("greet,secretMethod,tagName,readOnlyTag,ELEMENT_NODE,SECRET,constructor,added,defined");
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engine.Evaluate("proto.added").Should().Be(1);
        engine.Evaluate("proto.defined").Should().Be(2);
    }

    [Fact]
    public void AddingAnIntegerLikeKeyFallsBackToTheDictionary()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("proto[0] = 'zero';");

        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.Specialized);
        engine.Evaluate("proto[0]").Should().Be("zero");
        engine.Evaluate("proto.greet('x')").Should().Be("hello x");
        engine.Evaluate("Object.getOwnPropertyNames(proto)[0]").Should().Be("0");
    }

    [Fact]
    public void DeletingAConfigurableMemberFallsBackToTheDictionaryAndKeepsEveryOtherMemberCorrect()
    {
        var engine = EngineWithPrototype(out var prototype);

        // Touch exactly one member first, so the delete has to carry both a materialized and a whole set of
        // never-touched members across the fallback.
        engine.Evaluate("proto.greet('x')").Should().Be("hello x");

        engine.Evaluate("delete proto.greet").Should().Be(true);

        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.Specialized);
        engine.Evaluate("proto.greet").Should().Be(JsValue.Undefined);

        // Every member that was never touched before the fallback still works, including the ones that need
        // a function or a factory created after it.
        engine.Evaluate("proto.secretMethod()").Should().Be("secret");
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
        engine.Evaluate("proto.SECRET").Should().Be("s");
        engine.Execute("el._tag = 'DIV';");
        engine.Evaluate("el.tagName").Should().Be("DIV");
        engine.Evaluate("Object.prototype.toString.call(proto)").Should().Be("[object Element]");
        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')")
            .Should().Be("secretMethod,tagName,readOnlyTag,ELEMENT_NODE,SECRET,constructor");
    }

    [Fact]
    public void PreventExtensionsAndFreezeBehaveAsOnAnyOtherObject()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("Object.preventExtensions(proto);");
        engine.Evaluate("Object.isExtensible(proto)").Should().Be(false);
        engine.Execute("proto.brandNew = 1;");
        engine.Evaluate("'brandNew' in proto").Should().Be(false);
        engine.Evaluate("proto.greet('x')").Should().Be("hello x");
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

        engine.Execute("Object.freeze(proto);");
        engine.Evaluate("Object.isFrozen(proto)").Should().Be(true);
        engine.Execute("proto.greet = 1;");
        engine.Evaluate("typeof proto.greet").Should().Be("function");
        // reads keep working, and the object stays in the shared layout after the freeze materialized it
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void SettingThePrototypeOfAShapedObjectDoesNotDisturbItsMembers()
    {
        var engine = EngineWithPrototype(out var prototype);
        engine.Execute("var base = { inherited: 'yes' }; Object.setPrototypeOf(proto, base);");

        engine.Evaluate("proto.inherited").Should().Be("yes");
        engine.Evaluate("proto.greet('x')").Should().Be("hello x");
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void SymbolKeyedMembersNeverDisturbTheSharedLayout()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("var s = Symbol('extra'); Object.defineProperty(proto, s, { value: 7, configurable: true }); proto.symbolValue = s;");
        engine.Evaluate("proto[proto.symbolValue]").Should().Be(7);
        engine.Evaluate("Object.prototype.toString.call(proto)").Should().Be("[object Element]");

        engine.Execute("delete proto[proto.symbolValue];");
        engine.Evaluate("proto[proto.symbolValue]").Should().Be(JsValue.Undefined);
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    // ---- enumeration ----

    [Fact]
    public void EnumerationSeesExactlyTheEnumerableMembers()
    {
        var engine = EngineWithPrototype(out _);

        // enumerable: greet, tagName, readOnlyTag, ELEMENT_NODE — non-enumerable: secretMethod, SECRET,
        // constructor. Own-key order is declaration order in both cases.
        engine.Evaluate("Object.keys(proto).join(',')").Should().Be("greet,tagName,readOnlyTag,ELEMENT_NODE");
        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')")
            .Should().Be("greet,secretMethod,tagName,readOnlyTag,ELEMENT_NODE,SECRET,constructor");

        engine.Evaluate("Object.values(proto).length").Should().Be(4);
        engine.Evaluate("Object.entries(proto).map(function (e) { return e[0]; }).join(',')")
            .Should().Be("greet,tagName,readOnlyTag,ELEMENT_NODE");

        engine.Evaluate("proto.hasOwnProperty('SECRET')").Should().Be(true);
        engine.Evaluate("proto.hasOwnProperty('nope')").Should().Be(false);
        engine.Evaluate("proto.propertyIsEnumerable('ELEMENT_NODE')").Should().Be(true);
        engine.Evaluate("proto.propertyIsEnumerable('SECRET')").Should().Be(false);
        engine.Evaluate("'SECRET' in el").Should().Be(true);
    }

    [Fact]
    public void EnumerabilityAnswersFollowTheLiveDescriptorAfterARedefine()
    {
        // Existence and enumerability are answered from the shared layout without creating the member's
        // function, but a member that has been redefined carries a live descriptor whose flags win — the
        // two answers must never diverge.
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("Object.keys(proto).join(',')").Should().Be("greet,tagName,readOnlyTag,ELEMENT_NODE");

        // non-enumerable member made enumerable, and an enumerable one hidden
        engine.Execute("Object.defineProperty(proto, 'secretMethod', { enumerable: true });");
        engine.Execute("Object.defineProperty(proto, 'greet', { enumerable: false });");

        engine.Evaluate("Object.keys(proto).join(',')").Should().Be("secretMethod,tagName,readOnlyTag,ELEMENT_NODE");
        engine.Evaluate("proto.propertyIsEnumerable('secretMethod')").Should().Be(true);
        engine.Evaluate("proto.propertyIsEnumerable('greet')").Should().Be(false);
        engine.Evaluate("(function () { var k = []; for (var n in proto) { k.push(n); } return k.join(','); })()")
            .Should().Be("secretMethod,tagName,readOnlyTag,ELEMENT_NODE");

        // ...and both are still own properties either way
        engine.Evaluate("proto.hasOwnProperty('greet')").Should().Be(true);
        engine.Evaluate("'greet' in proto").Should().Be(true);
    }

    [Fact]
    public void ExistenceAndEnumerabilityStayCorrectAfterTheFallbackToTheDictionary()
    {
        var engine = EngineWithPrototype(out var prototype);

        engine.Evaluate("delete proto.greet").Should().Be(true);
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.Specialized);

        engine.Evaluate("'greet' in proto").Should().Be(false);
        engine.Evaluate("proto.hasOwnProperty('greet')").Should().Be(false);
        engine.Evaluate("proto.hasOwnProperty('secretMethod')").Should().Be(true);
        engine.Evaluate("proto.propertyIsEnumerable('secretMethod')").Should().Be(false);
        engine.Evaluate("proto.propertyIsEnumerable('ELEMENT_NODE')").Should().Be(true);
        engine.Evaluate("Object.keys(proto).join(',')").Should().Be("tagName,readOnlyTag,ELEMENT_NODE");
    }

    [Fact]
    public void ExistenceAnswersCoverHybridAdditionsAndAbsentNames()
    {
        var engine = EngineWithPrototype(out _);

        engine.Execute("proto.added = 1; Object.defineProperty(proto, 'hiddenAdd', { value: 2, configurable: true });");

        engine.Evaluate("proto.hasOwnProperty('added')").Should().Be(true);
        engine.Evaluate("proto.propertyIsEnumerable('added')").Should().Be(true);
        engine.Evaluate("proto.hasOwnProperty('hiddenAdd')").Should().Be(true);
        engine.Evaluate("proto.propertyIsEnumerable('hiddenAdd')").Should().Be(false);
        engine.Evaluate("proto.hasOwnProperty('neverDeclared')").Should().Be(false);
        engine.Evaluate("'neverDeclared' in proto").Should().Be(false);
        engine.Evaluate("proto.hasOwnProperty('toString')").Should().Be(false, "toString is inherited, not own");
        engine.Evaluate("'toString' in proto").Should().Be(true);
    }

    [Fact]
    public void AbsentNamesAnswerAcrossAChainOfShapedPrototypes()
    {
        // A name nothing declares is the expensive question: it is refused once per prototype level before the
        // chain as a whole can answer it, so a chain of shaped prototypes is where an absent name has to stay
        // as cheap and as correct as one dictionary prototype.
        var engine = new Engine();
        var root = ChainRootShape.Instantiate(engine);
        var middle = ChainMiddleShape.Instantiate(engine, root);
        var leaf = ChainLeafShape.Instantiate(engine, middle);

        engine.SetValue("root", root);
        engine.SetValue("middle", middle);
        engine.SetValue("leaf", leaf);
        engine.Execute("var instance = Object.create(leaf);");

        // Touch every level so the representation has settled, then witness it.
        engine.Evaluate("[root.ROOT_CONST, typeof middle.middleMember, typeof leaf.leafMember].length").Should().Be(3);
        engine.Advanced.GetObjectRepresentation(root).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engine.Advanced.GetObjectRepresentation(middle).Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engine.Advanced.GetObjectRepresentation(leaf).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

        engine.Evaluate("Object.getPrototypeOf(leaf) === middle").Should().Be(true);
        engine.Evaluate("Object.getPrototypeOf(middle) === root").Should().Be(true);
        engine.Evaluate("Object.getPrototypeOf(root) === Object.prototype").Should().Be(true);

        // The absent name: refused by the instance, then by all three shaped levels, then by Object.prototype.
        engine.Evaluate("'nope' in instance").Should().Be(false);
        engine.Evaluate("instance.nope").Should().Be(JsValue.Undefined);
        engine.Evaluate("instance.hasOwnProperty('nope')").Should().Be(false);
        engine.Evaluate("leaf.hasOwnProperty('nope')").Should().Be(false);
        engine.Evaluate("middle.hasOwnProperty('nope')").Should().Be(false);
        engine.Evaluate("root.hasOwnProperty('nope')").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(middle, 'nope')").Should().Be(JsValue.Undefined);

        // ...while a member declared only on the deepest level still resolves the whole way down.
        engine.Evaluate("'rootMember' in instance").Should().Be(true);
        engine.Evaluate("instance.rootMember()").Should().Be("root");
        engine.Evaluate("instance.ROOT_CONST").Should().Be(10);
        engine.Evaluate("instance.leafMember()").Should().Be("leaf");
        engine.Evaluate("instance.middleMember()").Should().Be("middle");
        engine.Evaluate("instance.hasOwnProperty('rootMember')").Should().Be(false, "it is inherited, not own");
        engine.Evaluate("root.hasOwnProperty('rootMember')").Should().Be(true);
        engine.Evaluate("middle.hasOwnProperty('rootMember')").Should().Be(false);
    }

    [Fact]
    public void AnUntouchedFactorySlotAnswersExistenceBeforeItsFactoryRuns()
    {
        // A per-realm factory slot has no declared attributes until its factory has run, so the shared layout
        // cannot answer its enumerability on its own. That is a "cannot answer yet", never a "not there" — an
        // existence question about a declared-but-unmaterialized member must still answer true.
        var calls = 0;
        var shape = new JsObjectShape.Builder()
            .Method("m", static (t, a) => JsValue.Undefined)
            .PerRealmSlot("lazyCtor", o => { calls++; return new JsString("made"); })
            .Build();

        var engine = new Engine();
        var proto = shape.Instantiate(engine);
        engine.SetValue("proto", proto);

        // Initialize the object through a different member, leaving the factory slot untouched.
        engine.Evaluate("typeof proto.m").Should().Be("function");
        calls.Should().Be(0, "nothing has asked for the slot's value yet");

        engine.Evaluate("proto.hasOwnProperty('lazyCtor')").Should().Be(true);
        engine.Evaluate("'lazyCtor' in proto").Should().Be(true);
        engine.Evaluate("Object.getOwnPropertyNames(proto).join(',')").Should().Be("m,lazyCtor");

        engine.Evaluate("proto.lazyCtor").Should().Be("made");
        engine.Evaluate("proto.hasOwnProperty('lazyCtor')").Should().Be(true);
        engine.Evaluate("proto.hasOwnProperty('neverDeclared')").Should().Be(false);
    }

    [Fact]
    public void AbsentNamesStayCorrectAfterAHybridAddition()
    {
        // A brand-new string key joins a side dictionary beside the shared layout, so the layout index alone
        // is no longer the whole own-property set — an absent-name answer must account for the addition.
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("proto.extra = 1;");
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

        engine.Evaluate("proto.hasOwnProperty('extra')").Should().Be(true);
        engine.Evaluate("'extra' in proto").Should().Be(true);
        engine.Evaluate("proto.extra").Should().Be(1);

        engine.Evaluate("proto.hasOwnProperty('stillAbsent')").Should().Be(false);
        engine.Evaluate("'stillAbsent' in proto").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'stillAbsent')").Should().Be(JsValue.Undefined);

        // and the declared members are untouched by either answer
        engine.Evaluate("proto.greet('x')").Should().Be("hello x");
        engine.Evaluate("proto.hasOwnProperty('ELEMENT_NODE')").Should().Be(true);
    }

    [Fact]
    public void AbsentNamesStayCorrectAfterADigitLeadingDefineDeopts()
    {
        // An integer-like key cannot be expressed in the shape-then-additions own-key order, so defining one
        // drops the object back to the ordinary dictionary. Every answer has to survive the transition.
        var engine = EngineWithPrototype(out var prototype);

        engine.Execute("Object.defineProperty(proto, '0', { value: 1, configurable: true });");
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.Specialized);

        engine.Evaluate("proto.hasOwnProperty('0')").Should().Be(true);
        engine.Evaluate("'0' in proto").Should().Be(true);
        engine.Evaluate("proto[0]").Should().Be(1);

        engine.Evaluate("proto.hasOwnProperty('nope')").Should().Be(false);
        engine.Evaluate("'nope' in proto").Should().Be(false);
        engine.Evaluate("Object.getOwnPropertyDescriptor(proto, 'nope')").Should().Be(JsValue.Undefined);

        engine.Evaluate("proto.greet('x')").Should().Be("hello x");
        engine.Evaluate("proto.ELEMENT_NODE").Should().Be(1);
    }

    [Fact]
    public void ForInWalksTheEnumerablePrototypeMembers()
    {
        var engine = EngineWithPrototype(out _);
        engine.Execute("el.own = 1;");

        engine.Evaluate("(function () { var k = []; for (var n in el) { k.push(n); } return k.join(','); })()")
            .Should().Be("own,greet,tagName,readOnlyTag,ELEMENT_NODE");
    }

    [Fact]
    public void JsonStringifyAndSpreadSeeTheEnumerableMembers()
    {
        var engine = EngineWithPrototype(out _);

        // greet is a function (omitted) and tagName's getter yields undefined for the prototype (omitted).
        engine.Evaluate("JSON.stringify(proto)").Should().Be("{\"ELEMENT_NODE\":1}");

        engine.Execute("proto._tag = 'ROOT';");
        engine.Evaluate("JSON.stringify({ tag: proto.tagName, node: proto.ELEMENT_NODE })")
            .Should().Be("{\"tag\":\"ROOT\",\"node\":1}");

        engine.Evaluate("Object.keys(Object.assign({}, proto)).join(',')").Should().Be("greet,tagName,readOnlyTag,ELEMENT_NODE,_tag");
        engine.Evaluate("Object.keys({ ...proto }).join(',')").Should().Be("greet,tagName,readOnlyTag,ELEMENT_NODE,_tag");
    }

    // ---- inline-cache interaction ----

    [Fact]
    public void WarmedReadsStayCorrectAcrossAMidLoopOverwriteWithTheShapedObjectAsReceiver()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("""
            (function () {
              var seen = '';
              for (var i = 0; i < 40; i++) {
                if (i === 20) { proto.greet = function () { return 'patched'; }; }
                if (i === 30) { Object.defineProperty(proto, 'greet', { value: function () { return 'redefined'; } }); }
                seen = proto.greet('x');
              }
              return seen;
            })()
            """).Should().Be("redefined");
    }

    [Fact]
    public void WarmedReadsThroughAScriptReceiverStayCorrectAcrossAMidLoopOverwrite()
    {
        var engine = EngineWithPrototype(out _);

        engine.Evaluate("""
            (function () {
              var results = [];
              for (var i = 0; i < 40; i++) {
                if (i === 20) { proto.greet = function () { return 'patched'; }; }
                results.push(el.greet('x'));
              }
              return results[0] + '|' + results[39];
            })()
            """).Should().Be("hello x|patched");
    }

    [Fact]
    public void WarmedReadsThroughAHostInstanceStayCorrectAcrossAMidLoopOverwrite()
    {
        var engine = new Engine();
        var prototype = ElementShape.Instantiate(engine);
        var host = new MinimalHostObject(engine, prototype).Project("_tag", new JsString("HOST"));

        engine.SetValue("proto", prototype);
        engine.SetValue("host", host);

        // Cold, then warm, then invalidated mid-loop: the prototype-method cache must not keep serving the
        // pre-overwrite descriptor.
        engine.Evaluate("""
            (function () {
              var results = [];
              for (var i = 0; i < 40; i++) {
                if (i === 20) { proto.greet = function () { return 'patched'; }; }
                results.push(host.greet('x') + ':' + host.tagName);
              }
              return results[0] + '|' + results[39];
            })()
            """).Should().Be("hello x:HOST|patched:HOST");

        // and an own property projected by the host after the fact still shadows the prototype
        host.Project("greet", new JsString("shadowed"));
        engine.Evaluate("host.greet").Should().Be("shadowed");
    }

    // ---- builder validation ----

    [Fact]
    public void BuilderRejectsInvalidNames()
    {
        Invoking(() => new JsObjectShape.Builder().Method(null!, static (t, a) => JsValue.Undefined))
            .Should().Throw<ArgumentException>().WithMessage("*non-empty*");
        Invoking(() => new JsObjectShape.Builder().Method("", static (t, a) => JsValue.Undefined))
            .Should().Throw<ArgumentException>().WithMessage("*non-empty*");
        Invoking(() => new JsObjectShape.Builder().Constant("0", JsNumber.Create(1)))
            .Should().Throw<ArgumentException>().WithMessage("*starts with a digit*");
        Invoking(() => new JsObjectShape.Builder().Accessor("42x", getter: static (t, a) => JsValue.Undefined))
            .Should().Throw<ArgumentException>().WithMessage("*starts with a digit*");
    }

    [Fact]
    public void BuilderRejectsDuplicateNamesAcrossKinds()
    {
        Invoking(() => new JsObjectShape.Builder()
                .Method("x", static (t, a) => JsValue.Undefined)
                .Constant("x", JsNumber.Create(1)))
            .Should().Throw<ArgumentException>().WithMessage("*already been declared*");

        Invoking(() => new JsObjectShape.Builder()
                .Accessor("x", getter: static (t, a) => JsValue.Undefined)
                .PerRealmSlot("x"))
            .Should().Throw<ArgumentException>().WithMessage("*already been declared*");
    }

    [Fact]
    public void BuilderRejectsMissingDelegatesAndObjectConstants()
    {
        Invoking(() => new JsObjectShape.Builder().Method("x", null!))
            .Should().Throw<ArgumentNullException>();
        Invoking(() => new JsObjectShape.Builder().Method("x", static (t, a) => JsValue.Undefined, length: -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        Invoking(() => new JsObjectShape.Builder().Accessor("x", getter: null, setter: null))
            .Should().Throw<ArgumentException>().WithMessage("*neither a getter nor a setter*");
        Invoking(() => new JsObjectShape.Builder().PerRealmSlot("x", (Func<ObjectInstance, JsValue>) null!))
            .Should().Throw<ArgumentNullException>();
        Invoking(() => new JsObjectShape.Builder().Constant("x", null!))
            .Should().Throw<ArgumentNullException>();

        var engine = new Engine();
        var objectValue = engine.Evaluate("({})").AsObject();
        Invoking(() => new JsObjectShape.Builder().Constant("x", objectValue))
            .Should().Throw<ArgumentException>().WithMessage("*object value*");
    }

    [Fact]
    public void BuilderRejectsAnUnfillablePerRealmSlot()
    {
        Invoking(() => new JsObjectShape.Builder().PerRealmSlot("x", writable: false, configurable: false))
            .Should().Throw<ArgumentException>().WithMessage("*neither writable nor configurable*");
    }

    [Fact]
    public void BuilderRejectsASecondToStringTag()
    {
        Invoking(() => new JsObjectShape.Builder().ToStringTag("A").ToStringTag("B"))
            .Should().Throw<InvalidOperationException>().WithMessage("*already been declared*");
        Invoking(() => new JsObjectShape.Builder().ToStringTag(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuilderRejectsUseAfterBuild()
    {
        var builder = new JsObjectShape.Builder().Method("x", static (t, a) => JsValue.Undefined);
        builder.Build();

        Invoking(() => builder.Method("y", static (t, a) => JsValue.Undefined))
            .Should().Throw<InvalidOperationException>().WithMessage("*already been built*");
        Invoking(() => builder.Constant("y", JsNumber.Create(1)))
            .Should().Throw<InvalidOperationException>();
        Invoking(() => builder.Accessor("y", getter: static (t, a) => JsValue.Undefined))
            .Should().Throw<InvalidOperationException>();
        Invoking(() => builder.PerRealmSlot("y"))
            .Should().Throw<InvalidOperationException>();
        Invoking(() => builder.ToStringTag("y"))
            .Should().Throw<InvalidOperationException>();
        Invoking(() => builder.Build())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuilderRejectsMoreMembersThanAShapeCanDescribe()
    {
        var builder = new JsObjectShape.Builder();
        for (var i = 0; i <= 4096; i++)
        {
            builder.Constant("c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), JsNumber.Create(i));
        }

        Invoking(() => builder.Build()).Should().Throw<ArgumentException>().WithMessage("*at most 4096 members*");
    }

    [Fact]
    public void AnEmptyShapeIsUsable()
    {
        var shape = new JsObjectShape.Builder().Build();
        shape.Count.Should().Be(0);

        var engine = new Engine();
        var obj = shape.Instantiate(engine);
        engine.SetValue("o", obj);
        engine.Evaluate("Object.keys(o).length").Should().Be(0);
        engine.Execute("o.x = 1;");
        engine.Evaluate("o.x").Should().Be(1);
    }

    /// <summary>
    /// The smallest host receiver that reaches the prototype-method inline cache: an
    /// <see cref="ObjectInstance"/> subclass overriding <see cref="ObjectInstance.GetOwnProperty"/> and
    /// nothing else, so the engine derives ordinary access semantics for it. Deliberately built only from
    /// members an embedder in an unrelated assembly can reach.
    /// </summary>
    private sealed class MinimalHostObject : ObjectInstance
    {
        private readonly Dictionary<string, JsValue> _own = new(StringComparer.Ordinal);

        public MinimalHostObject(Engine engine, ObjectInstance prototype) : base(engine)
        {
            Prototype = prototype;
        }

        public MinimalHostObject Project(string name, JsValue value)
        {
            _own[name] = value;
            return this;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property.IsString() && _own.TryGetValue(property.ToString(), out var value))
            {
                return new PropertyDescriptor(value, writable: true, enumerable: true, configurable: true);
            }

            return PropertyDescriptor.Undefined;
        }
    }
}
