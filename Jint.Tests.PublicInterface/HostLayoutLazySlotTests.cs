// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Lazily-produced layout properties: <c>JsObjectLayout.CreateBuilder().AddLazy(...)</c> plus the
/// <c>JsObject.Create</c> overload that carries the per-object state those factories read.
/// <para>
/// The shape this exists for is a batch of host records — many items, a fixed set of members, a few of which
/// are expensive to produce (each parses or decodes a part of the item's raw payload) and most of which are
/// never touched by the script. What must hold: every item is still a hidden-class object sharing one shape
/// with the rest of the batch, no factory runs until something observes that member's value, and asking
/// merely whether a member exists never runs one.
/// </para>
/// <para>
/// Everything here goes through the public API only, so it also proves the surface is reachable by a third
/// party.
/// </para>
/// </summary>
public class HostLayoutLazySlotTests
{
    /// <summary>
    /// One item's raw payload: the "not yet parsed" state a lazy factory decodes, plus a counter per member
    /// so a test can assert exactly which factories ran. Deliberately a plain CLR object with no Jint
    /// dependency — this is what a host would already have.
    /// </summary>
    private sealed class Payload
    {
        public Payload(int id, string body, string metadata)
        {
            Id = id;
            RawBody = body;
            RawMetadata = metadata;
        }

        public int Id { get; }
        public string RawBody { get; }
        public string RawMetadata { get; }

        public int BodyParses { get; private set; }
        public int MetadataParses { get; private set; }

        public JsValue ParseBody(JsObject owner)
        {
            BodyParses++;
            return owner.Engine.Evaluate("JSON.parse").Call(new JsString(RawBody));
        }

        public JsValue ParseMetadata(JsObject owner)
        {
            MetadataParses++;
            return owner.Engine.Evaluate("JSON.parse").Call(new JsString(RawMetadata));
        }
    }

    // Declared once for the process, as a host would: the factories are static lambdas, so nothing
    // engine-affine is captured and the same layout serves every engine.
    private static readonly JsObjectLayout EnvelopeLayout = JsObjectLayout.CreateBuilder()
        .Add("id")
        .Add("type")
        .Add("stream")
        .Add("created")
        .AddLazy("body", static (o, state) => ((Payload) state!).ParseBody(o))
        .AddLazy("metadata", static (o, state) => ((Payload) state!).ParseMetadata(o))
        .Build();

    private static JsObject CreateEnvelope(Engine engine, Payload payload) => JsObject.Create(
        engine,
        EnvelopeLayout,
        [
            JsNumber.Create(payload.Id),
            new JsString("ItemCreated"),
            new JsString("stream-1"),
            JsNumber.Create(1234),
            null,
            null
        ],
        payload);

    private static Payload SamplePayload(int id = 1) =>
        new(id, $$"""{"amount":{{id}},"note":"n{{id}}"}""", """{"origin":"test"}""");

    private static Engine EngineWithEnvelope(out Payload payload, out JsObject envelope)
    {
        var engine = new Engine();
        payload = SamplePayload();
        envelope = CreateEnvelope(engine, payload);
        engine.SetValue("e", envelope);
        return engine;
    }

    // ---- creation ----

    [Fact]
    public void CreateRunsNoFactoryAndStillProducesAHiddenClassObject()
    {
        var engine = EngineWithEnvelope(out var payload, out var envelope);

        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.HiddenClass);
    }

    [Fact]
    public void EagerMembersReadNormallyAndRunNoFactory()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("e.id").AsNumber().Should().Be(1);
        engine.Evaluate("e.type").AsString().Should().Be("ItemCreated");
        engine.Evaluate("e.stream + e.created").AsString().Should().Be("stream-11234");

        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);
    }

    [Fact]
    public void ALazyMemberMaterializesOnceOnFirstReadAndLeavesItsSiblingsAlone()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("e.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(0);

        // Repeated reads are served from the memo, and identity is stable — the factory produced one object.
        engine.Evaluate("e.body === e.body").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.body.note").AsString().Should().Be("n1");
        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(0);
    }

    [Fact]
    public void ObjectStaysAHiddenClassObjectAcrossMaterialization()
    {
        var engine = EngineWithEnvelope(out _, out var envelope);

        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.HiddenClass);
        engine.Evaluate("e.body");
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.HiddenClass);
        engine.Evaluate("e.metadata");
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.HiddenClass);
    }

    // ---- the batch: one hidden class, one factory run per item ----

    [Fact]
    public void ABatchSharesOneHiddenClassAndRunsOneFactoryPerItem()
    {
        const int count = 64;
        var engine = new Engine();
        var payloads = Enumerable.Range(1, count).Select(SamplePayload).ToArray();

        var push = engine.Evaluate("var items = []; (function (o) { items.push(o); })");
        for (var i = 0; i < count; i++)
        {
            var envelope = CreateEnvelope(engine, payloads[i]);
            engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.HiddenClass);
            push.Call(envelope);
        }

        // One eager and one lazy member per item, the loop a script over such a batch actually writes.
        var total = engine.Evaluate("""
            let total = 0;
            for (let i = 0; i < items.length; i++) { total += items[i].id + items[i].body.amount; }
            total;
            """).AsNumber();

        total.Should().Be(2 * Enumerable.Range(1, count).Sum());
        payloads.Should().AllSatisfy(static p => p.BodyParses.Should().Be(1));
        // The untouched lazy member of every item never ran.
        payloads.Should().AllSatisfy(static p => p.MetadataParses.Should().Be(0));
    }

    // ---- existence questions never materialize ----

    [Theory]
    [InlineData("'body' in e")]
    [InlineData("e.hasOwnProperty('body')")]
    [InlineData("Object.prototype.propertyIsEnumerable.call(e, 'body')")]
    [InlineData("Object.keys(e).indexOf('body') >= 0")]
    [InlineData("Object.getOwnPropertyNames(e).indexOf('body') >= 0")]
    [InlineData("Reflect.ownKeys(e).indexOf('body') >= 0")]
    [InlineData("(function () { for (const k in e) { if (k === 'body') return true; } return false; })()")]
    public void ExistenceQuestionsAnswerTrueWithoutRunningAnyFactory(string expression)
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate(expression).AsBoolean().Should().BeTrue();

        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);
    }

    [Fact]
    public void KeyOrderIsLayoutOrderWithoutRunningAnyFactory()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("Object.keys(e).join(',')").AsString()
            .Should().Be("id,type,stream,created,body,metadata");
        payload.BodyParses.Should().Be(0);
    }

    [Fact]
    public void StringifyingAnObjectThatDropsTheEnvelopeRunsNoFactory()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("JSON.stringify({ e: e, n: 1 }, function (k, v) { return k === 'e' ? undefined : v; })")
            .AsString().Should().Be("""{"n":1}""");

        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);
    }

    // ---- value observations do materialize ----

    [Theory]
    [InlineData("Object.values(e).length")]
    [InlineData("Object.entries(e).length")]
    [InlineData("JSON.stringify(e).length")]
    [InlineData("Object.keys({ ...e }).length")]
    [InlineData("Object.keys(Object.assign({}, e)).length")]
    [InlineData("Object.getOwnPropertyDescriptor(e, 'body').value.amount")]
    [InlineData("e['bo' + 'dy'].amount")]
    [InlineData("Reflect.get(e, 'body').amount")]
    [InlineData("e.body.hasOwnProperty('amount') ? 1 : 0")]
    public void ValueObservationsRunTheFactory(string expression)
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate(expression);

        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void StringifyIncludesTheLazyMembersAndMaterializesEachOnce()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("JSON.stringify(e)").AsString().Should().Be(
            """{"id":1,"type":"ItemCreated","stream":"stream-1","created":1234,"body":{"amount":1,"note":"n1"},"metadata":{"origin":"test"}}""");

        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(1);
    }

    [Fact]
    public void SpreadCopiesMaterializedValuesAndReadsTheSourceOnce()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("const c = { ...e }; c.body === e.body").AsBoolean().Should().BeTrue();
        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(1);

        // The copy is a plain object: reading it again cannot re-run anything.
        engine.Evaluate("c.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void ASpreadCopyIsItselfAHiddenClassObject()
    {
        var engine = EngineWithEnvelope(out _, out _);

        var copy = engine.Evaluate("({ ...e })").Should().BeOfType<JsObject>().Which;
        engine.Advanced.GetObjectRepresentation(copy).Should().Be(ObjectRepresentation.HiddenClass);
        engine.Evaluate("Object.keys({ ...e }).join(',')").AsString()
            .Should().Be("id,type,stream,created,body,metadata");
    }

    // ---- write / delete before read discards the factory ----

    [Fact]
    public void WritingALazyMemberBeforeAnyReadDiscardsItsFactory()
    {
        var engine = EngineWithEnvelope(out var payload, out var envelope);

        engine.Evaluate("e.body = 'replaced'");
        engine.Evaluate("e.body").AsString().Should().Be("replaced");
        engine.Evaluate("JSON.stringify(e.body)").AsString().Should().Be("\"replaced\"");

        payload.BodyParses.Should().Be(0);
        // A write keeps the object shaped; the untouched sibling is still lazy.
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.HiddenClass);
        payload.MetadataParses.Should().Be(0);
        engine.Evaluate("e.metadata.origin").AsString().Should().Be("test");
        payload.MetadataParses.Should().Be(1);
    }

    [Fact]
    public void DeletingALazyMemberBeforeAnyReadDiscardsItsFactory()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("delete e.body").AsBoolean().Should().BeTrue();
        engine.Evaluate("'body' in e").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.body").Should().Be(JsValue.Undefined);

        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);
    }

    // ---- deopt keeps laziness ----

    [Fact]
    public void DeletingAnUnrelatedKeyLeavesTheOtherLazyMembersLazy()
    {
        var engine = EngineWithEnvelope(out var payload, out var envelope);

        engine.Evaluate("delete e.stream");
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.Dictionary);
        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);

        // Still lazy, and still answers existence questions without running.
        engine.Evaluate("'body' in e").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.keys(e).join(',')").AsString().Should().Be("id,type,created,body,metadata");
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("e.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(0);
    }

    [Fact]
    public void DefiningAnUnrelatedKeyLeavesTheLazyMembersLazy()
    {
        var engine = EngineWithEnvelope(out var payload, out var envelope);

        engine.Evaluate("Object.defineProperty(e, 'extra', { value: 7, enumerable: true })");
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.Dictionary);
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("e.extra").AsNumber().Should().Be(7);
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("e.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void RedefiningTheLazyKeyWithAValueDiscardsItsFactory()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        // Supplying a value is a write, so — exactly like `e.body = ...` — the factory is discarded rather
        // than run and then overwritten.
        engine.Evaluate("Object.defineProperty(e, 'body', { value: 'x' })");
        engine.Evaluate("e.body").AsString().Should().Be("x");
        engine.Evaluate("JSON.stringify(e.body)").AsString().Should().Be("\"x\"");

        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);
    }

    [Fact]
    public void RedefiningTheLazyKeyWithAttributesOnlyKeepsItLazy()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("Object.defineProperty(e, 'body', { enumerable: false })");
        engine.Evaluate("Object.keys(e).indexOf('body')").AsNumber().Should().Be(-1);
        engine.Evaluate("'body' in e").AsBoolean().Should().BeTrue();
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("e.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void RedefiningANonConfigurableLazyKeyValidatesAgainstItsRealValue()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        // Freezing leaves the member lazy and non-writable; a redefinition that supplies a DIFFERENT value
        // must be rejected, which is the one redefinition that genuinely has to know the current value.
        engine.Evaluate("Object.freeze(e)");
        payload.BodyParses.Should().Be(0);

        Invoking(() => engine.Evaluate("Object.defineProperty(e, 'body', { value: 'other' })"))
            .Should().Throw<JavaScriptException>();
        payload.BodyParses.Should().Be(1);

        // Redefining it to the value it already has is allowed, and does not run the factory a second time.
        engine.Evaluate("Object.defineProperty(e, 'body', { value: e.body })");
        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void FreezingKeepsTheMembersLazyRejectsWritesAndStillServesReads()
    {
        var engine = EngineWithEnvelope(out var payload, out var envelope);

        engine.Evaluate("Object.freeze(e)");
        engine.Advanced.GetObjectRepresentation(envelope).Should().Be(ObjectRepresentation.Dictionary);
        engine.Evaluate("Object.isFrozen(e)").AsBoolean().Should().BeTrue();
        payload.BodyParses.Should().Be(0);
        payload.MetadataParses.Should().Be(0);

        engine.Evaluate("e.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(0);

        // Writes are rejected, in both modes.
        engine.Evaluate("e.metadata = 1; e.metadata.origin").AsString().Should().Be("test");
        Invoking(() => engine.Evaluate("'use strict'; e.body = 1;")).Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void SealingKeepsTheMembersLazyAndWritable()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("Object.seal(e)");
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("e.body = 5; e.body").AsNumber().Should().Be(5);
        // A write into a sealed lazy member still discards the factory rather than running it.
        payload.BodyParses.Should().Be(0);
    }

    // ---- factory contract ----

    [Fact]
    public void AFactoryReturningNullStoresUndefined()
    {
        var layout = JsObjectLayout.CreateBuilder()
            .Add("a")
            .AddLazy("b", static (_, _) => null!)
            .Build();

        var engine = new Engine();
        engine.SetValue("o", JsObject.Create(engine, layout, [JsNumber.Create(1), null], null));

        engine.Evaluate("o.b").Should().Be(JsValue.Undefined);
        engine.Evaluate("'b' in o").AsBoolean().Should().BeTrue();
        engine.Evaluate("JSON.stringify(o)").AsString().Should().Be("""{"a":1}""");
    }

    [Fact]
    public void AThrowingFactorySurfacesAndLeavesTheSlotUnmaterialized()
    {
        var runs = new int[1];
        var layout = JsObjectLayout.CreateBuilder()
            .Add("a")
            .AddLazy("b", static (o, state) =>
            {
                var counter = (int[]) state!;
                counter[0]++;
                if (counter[0] < 3)
                {
                    throw new InvalidOperationException("payload is corrupt");
                }

                return JsNumber.Create(counter[0]);
            })
            .Build();

        var engine = new Engine();
        engine.SetValue("o", JsObject.Create(engine, layout, [JsNumber.Create(1), null], runs));

        Invoking(() => engine.Evaluate("o.b")).Should().Throw<InvalidOperationException>();
        runs[0].Should().Be(1);

        // No half-memo: the next read retries.
        Invoking(() => engine.Evaluate("o.b")).Should().Throw<InvalidOperationException>();
        runs[0].Should().Be(2);

        engine.Evaluate("o.b").AsNumber().Should().Be(3);
        engine.Evaluate("o.b").AsNumber().Should().Be(3);
        runs[0].Should().Be(3);
    }

    [Fact]
    public void EachObjectGetsItsOwnStateAndItsOwnMaterialization()
    {
        var engine = new Engine();
        var first = SamplePayload(1);
        var second = SamplePayload(2);
        engine.SetValue("a", CreateEnvelope(engine, first));
        engine.SetValue("b", CreateEnvelope(engine, second));

        engine.Evaluate("a.body.amount").AsNumber().Should().Be(1);
        first.BodyParses.Should().Be(1);
        second.BodyParses.Should().Be(0);

        engine.Evaluate("b.body.amount").AsNumber().Should().Be(2);
        first.BodyParses.Should().Be(1);
        second.BodyParses.Should().Be(1);

        engine.Evaluate("a.body === b.body").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void OneLayoutServesSeveralEnginesIndependently()
    {
        var firstEngine = new Engine();
        var secondEngine = new Engine();
        var first = SamplePayload(1);
        var second = SamplePayload(2);

        firstEngine.SetValue("e", CreateEnvelope(firstEngine, first));
        secondEngine.SetValue("e", CreateEnvelope(secondEngine, second));

        firstEngine.Evaluate("e.body.amount").AsNumber().Should().Be(1);
        first.BodyParses.Should().Be(1);
        second.BodyParses.Should().Be(0);

        secondEngine.Evaluate("e.body.amount").AsNumber().Should().Be(2);
        second.BodyParses.Should().Be(1);

        // Each engine interns its own hidden class from the shared, engine-agnostic layout.
        var firstObject = firstEngine.Evaluate("e").Should().BeOfType<JsObject>().Which;
        var secondObject = secondEngine.Evaluate("e").Should().BeOfType<JsObject>().Which;
        firstEngine.Advanced.GetObjectRepresentation(firstObject).Should().Be(ObjectRepresentation.HiddenClass);
        secondEngine.Advanced.GetObjectRepresentation(secondObject).Should().Be(ObjectRepresentation.HiddenClass);
    }

    [Fact]
    public void AMemberCallOnALazyMemberMaterializesIt()
    {
        var layout = JsObjectLayout.CreateBuilder()
            .Add("id")
            .AddLazy("decode", static (o, _) => o.Engine.Evaluate("(function () { return 'decoded'; })"))
            .Build();

        var engine = new Engine();
        engine.SetValue("o", JsObject.Create(engine, layout, [JsNumber.Create(1), null], null));

        // The callee lane, not the read lane: `o.decode` is resolved as a call target.
        engine.Evaluate("o.decode()").AsString().Should().Be("decoded");
    }

    // ---- API validation ----

    [Fact]
    public void AValueMustNotBeGivenForALazyProperty()
    {
        var engine = new Engine();

        Invoking(() => JsObject.Create(engine, EnvelopeLayout,
            [
                JsNumber.Create(1), new JsString("t"), new JsString("s"), JsNumber.Create(0),
                new JsString("supplied"), null
            ], SamplePayload()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*index 4 ('body') must be null*");
    }

    [Fact]
    public void TheThreeArgumentCreateWorksWithALazyLayoutAndPassesNullState()
    {
        var layout = JsObjectLayout.CreateBuilder()
            .Add("a")
            .AddLazy("b", static (_, state) => state is null ? new JsString("no state") : new JsString("state"))
            .Build();

        var engine = new Engine();
        engine.SetValue("o", JsObject.Create(engine, layout, [JsNumber.Create(1), null]));

        engine.Evaluate("o.b").AsString().Should().Be("no state");
    }

    [Fact]
    public void BuilderRejectsANullFactory()
    {
        Invoking(() => JsObjectLayout.CreateBuilder().AddLazy("a", null))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuilderAppliesTheSameNameRulesAsTheConstructors()
    {
        Invoking(() => JsObjectLayout.CreateBuilder().Add("a").Add("a"))
            .Should().Throw<ArgumentException>().WithMessage("*Duplicate property name 'a'*");
        Invoking(() => JsObjectLayout.CreateBuilder().Add("a").AddLazy("a", static (_, _) => JsValue.Undefined))
            .Should().Throw<ArgumentException>().WithMessage("*Duplicate property name 'a'*");
        Invoking(() => JsObjectLayout.CreateBuilder().Add(null))
            .Should().Throw<ArgumentException>().WithMessage("*null or empty*");
        Invoking(() => JsObjectLayout.CreateBuilder().Add(""))
            .Should().Throw<ArgumentException>().WithMessage("*null or empty*");
        Invoking(() => JsObjectLayout.CreateBuilder().AddLazy("0x", static (_, _) => JsValue.Undefined))
            .Should().Throw<ArgumentException>().WithMessage("*starts with a digit*");
    }

    [Fact]
    public void BuilderRejectsMorePropertiesThanAHiddenClassCanDescribe()
    {
        var builder = JsObjectLayout.CreateBuilder();
        for (var i = 0; i < 64; i++)
        {
            builder.Add("p" + i);
        }

        builder.Build().Count.Should().Be(64);

        var another = JsObjectLayout.CreateBuilder();
        for (var i = 0; i < 64; i++)
        {
            another.Add("p" + i);
        }

        Invoking(() => another.Add("overflow")).Should().Throw<ArgumentException>().WithMessage("*at most 64*");
    }

    [Fact]
    public void ABuilderIsSingleUse()
    {
        var builder = JsObjectLayout.CreateBuilder().Add("a");
        builder.Build().Count.Should().Be(1);

        Invoking(() => builder.Build()).Should().Throw<InvalidOperationException>();
        Invoking(() => builder.Add("b")).Should().Throw<InvalidOperationException>();
        Invoking(() => builder.AddLazy("b", static (_, _) => JsValue.Undefined)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AnAllEagerBuilderProducesAnOrdinaryLayout()
    {
        var layout = JsObjectLayout.CreateBuilder().Add("x").Add("y").Build();
        layout.Count.Should().Be(2);
        layout.IndexOf("y").Should().Be(1);
        layout.IndexOf("nope").Should().Be(-1);

        var engine = new Engine();
        var obj = JsObject.Create(engine, layout, [JsNumber.Create(1), JsNumber.Create(2)]);
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.HiddenClass);

        // The same hidden class an equivalent literal reaches — a builder-built layout is nothing special.
        var literal = engine.Evaluate("({ x: 1, y: 2 })").Should().BeOfType<JsObject>().Which;
        engine.Advanced.GetObjectRepresentation(literal).Should().Be(ObjectRepresentation.HiddenClass);
        engine.SetValue("o", obj);
        engine.Evaluate("Object.keys(o).join(',')").AsString().Should().Be("x,y");
    }

    [Fact]
    public void ALayoutMayBeEntirelyLazy()
    {
        var layout = JsObjectLayout.CreateBuilder()
            .AddLazy("a", static (_, state) => JsNumber.Create(((int[]) state!)[0]++))
            .AddLazy("b", static (_, state) => JsNumber.Create(((int[]) state!)[0]++))
            .Build();

        var counter = new[] { 10 };
        var engine = new Engine();
        engine.SetValue("o", JsObject.Create(engine, layout, [null, null], counter));

        engine.Evaluate("Object.keys(o).join(',')").AsString().Should().Be("a,b");
        counter[0].Should().Be(10);

        engine.Evaluate("o.b").AsNumber().Should().Be(10);
        engine.Evaluate("o.a").AsNumber().Should().Be(11);
        engine.Evaluate("o.a + o.b").AsNumber().Should().Be(21);
        counter[0].Should().Be(12);
    }

    [Fact]
    public void ALazyMemberIsAnOrdinaryDataProperty()
    {
        var engine = EngineWithEnvelope(out _, out _);

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(e, 'body')");
        engine.SetValue("d", descriptor);
        engine.Evaluate("d.writable && d.enumerable && d.configurable").AsBoolean().Should().BeTrue();
        engine.Evaluate("'get' in d").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void ProxyingAnEnvelopeSeesOrdinaryProperties()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("const p = new Proxy(e, {});");
        engine.Evaluate("'body' in p").AsBoolean().Should().BeTrue();
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("p.body.amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
        engine.Evaluate("p.body === e.body").AsBoolean().Should().BeTrue();
        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void AWithScopeResolvesLazyMembers()
    {
        var engine = EngineWithEnvelope(out var payload, out _);

        engine.Evaluate("var found; with (e) { found = 'body' in e; } found").AsBoolean().Should().BeTrue();
        payload.BodyParses.Should().Be(0);

        engine.Evaluate("var amount; with (e) { amount = body.amount; } amount").AsNumber().Should().Be(1);
        payload.BodyParses.Should().Be(1);
    }

    [Fact]
    public void ToObjectMaterializesEveryMember()
    {
        var engine = EngineWithEnvelope(out var payload, out var envelope);

        var dictionary = envelope.ToObject().Should().BeAssignableTo<IDictionary<string, object>>().Which;
        dictionary.Keys.Should().Contain("body");
        payload.BodyParses.Should().Be(1);
        payload.MetadataParses.Should().Be(1);
    }
}
