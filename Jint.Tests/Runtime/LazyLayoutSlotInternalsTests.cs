#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime;

/// <summary>
/// The parts of lazy layout slots (<c>JsObjectLayout.Builder.AddLazy</c>) that are invisible from the public
/// surface: that an unmaterialized slot really is a sentinel in the slot array rather than a descriptor, that
/// the <see cref="InternalTypes.HasLazySlots"/> flag arrives and leaves when it should, that the deopt emits
/// a field-backed lazy descriptor, and that spread declines to adopt a shape whose slots are not all real
/// values yet. The publicly observable behavior lives in
/// Jint.Tests.PublicInterface/HostLayoutLazySlotTests.cs.
/// </summary>
public class LazyLayoutSlotInternalsTests
{
    private sealed class Counter
    {
        public int A;
        public int B;
    }

    private static readonly JsObjectLayout MixedLayout = JsObjectLayout.CreateBuilder()
        .Add("eager")
        .AddLazy("a", static (_, state) => JsNumber.Create(++((Counter) state!).A))
        .AddLazy("b", static (_, state) => JsNumber.Create(100 + ++((Counter) state!).B))
        .Build();

    private static JsObject Create(Engine engine, Counter counter) =>
        JsObject.Create(engine, MixedLayout, [JsNumber.Create(0), null, null], counter);

    private static bool HasLazyFlag(JsObject obj) =>
        (obj._type & InternalTypes.HasLazySlots) != InternalTypes.Empty;

    // ---- storage ----

    [Fact]
    public void AnUnmaterializedSlotHoldsOneSentinelSharedByEveryLazySlot()
    {
        var engine = new Engine();
        var obj = Create(engine, new Counter());

        (obj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
        HasLazyFlag(obj).Should().BeTrue();

        obj.GetSlot(0).Should().BeOfType<JsNumber>();
        var first = obj.GetSlot(1);
        var second = obj.GetSlot(2);
        first.Should().BeOfType<JsObject.UnmaterializedSlots>();
        // One allocation for the whole object, not one per lazy member.
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void ALazyLayoutInternsTheSameShapeAsAnEquivalentEagerOneAndAsALiteral()
    {
        var engine = new Engine();
        var lazy = Create(engine, new Counter());
        var eager = JsObject.Create(engine, new JsObjectLayout("eager", "a", "b"),
            [JsNumber.Create(0), JsNumber.Create(1), JsNumber.Create(2)]);
        var literal = engine.Evaluate("({ eager: 0, a: 1, b: 2 })").Should().BeOfType<JsObject>().Which;

        // The hidden class describes names and slots only, so laziness never segregates the transition tree —
        // which is what keeps a call site reading `o.a` monomorphic across all three.
        lazy.ShapeOf.Should().BeSameAs(eager.ShapeOf);
        lazy.ShapeOf.Should().BeSameAs(literal.ShapeOf);
    }

    [Fact]
    public void EveryObjectOfALazyLayoutSharesOneShape()
    {
        var engine = new Engine();
        var first = Create(engine, new Counter());
        for (var i = 0; i < 50; i++)
        {
            var next = Create(engine, new Counter());
            next.Should().NotBeSameAs(first);
            next.ShapeOf.Should().BeSameAs(first.ShapeOf);
        }
    }

    // ---- flag lifecycle ----

    [Fact]
    public void TheFlagClearsWhenTheLastSentinelIsMaterialized()
    {
        var engine = new Engine();
        var counter = new Counter();
        var obj = Create(engine, counter);
        engine.SetValue("o", obj);

        HasLazyFlag(obj).Should().BeTrue();

        engine.Evaluate("o.a").AsNumber().Should().Be(1);
        // One of two resolved: still flagged, so the checked read lane stays engaged.
        HasLazyFlag(obj).Should().BeTrue();
        obj.GetSlot(1).Should().BeOfType<JsNumber>();

        engine.Evaluate("o.b").AsNumber().Should().Be(101);
        // Fully materialized: the object rejoins the plain shape lanes, indistinguishable from a literal.
        HasLazyFlag(obj).Should().BeFalse();
        (obj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);

        engine.Evaluate("o.a + o.b").AsNumber().Should().Be(102);
        counter.A.Should().Be(1);
        counter.B.Should().Be(1);
    }

    [Fact]
    public void AWriteOverASentinelDiscardsTheFactoryAndTheFlagClearsOnTheNextMaterialization()
    {
        var engine = new Engine();
        var counter = new Counter();
        var obj = Create(engine, counter);
        engine.SetValue("o", obj);

        engine.Evaluate("o.a = 7");
        obj.GetSlot(1).Should().BeOfType<JsNumber>();
        counter.A.Should().Be(0);

        // The write path is deliberately raw, so it does not pay for a rescan: the flag is conservative and
        // stays set until something materializes and finds nothing left.
        HasLazyFlag(obj).Should().BeTrue();

        engine.Evaluate("o.b").AsNumber().Should().Be(101);
        HasLazyFlag(obj).Should().BeFalse();
    }

    [Fact]
    public void DeoptClearsTheFlagAndTheSlots()
    {
        var engine = new Engine();
        var obj = Create(engine, new Counter());
        engine.SetValue("o", obj);

        engine.Evaluate("delete o.eager");

        HasLazyFlag(obj).Should().BeFalse();
        (obj._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        engine.Diagnostics.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.Dictionary);
    }

    // ---- deopt ----

    [Fact]
    public void DeoptEmitsAFieldBackedLazyDescriptorForAnUnmaterializedSlotOnly()
    {
        var engine = new Engine();
        var counter = new Counter();
        var obj = Create(engine, counter);
        engine.SetValue("o", obj);

        // Materialize exactly one, then force the deopt.
        engine.Evaluate("o.a");
        engine.Evaluate("delete o.eager");

        counter.A.Should().Be(1);
        counter.B.Should().Be(0);

        var properties = obj._properties!;
        properties["a"].Should().BeOfType<PropertyDescriptor>("a materialized before the deopt");
        var lazy = properties["b"].Should().BeOfType<LazySlotPropertyDescriptor>().Which;
        // The marker is what lets a global-snapshot restore revert this descriptor by writing its fields —
        // including back to unmaterialized, which is exactly "the captured state".
        lazy.Should().BeAssignableTo<IFieldBackedLazyDescriptor>();
        lazy._value.Should().BeNull();
        (lazy.Flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);

        engine.Evaluate("o.b").AsNumber().Should().Be(101);
        counter.B.Should().Be(1);
        lazy._value.Should().NotBeNull();
        // Once resolved it is an ordinary data descriptor again, so the caches stop paying for the hook.
        (lazy.Flags & PropertyFlag.CustomJsValue).Should().Be(PropertyFlag.None);
    }

    [Fact]
    public void FreezingLeavesTheDescriptorLazyAndOnlyFlipsItsFlags()
    {
        var engine = new Engine();
        var counter = new Counter();
        var obj = Create(engine, counter);
        engine.SetValue("o", obj);

        engine.Evaluate("Object.freeze(o)");

        var lazy = obj._properties!["b"].Should().BeOfType<LazySlotPropertyDescriptor>().Which;
        lazy._value.Should().BeNull();
        lazy.Writable.Should().BeFalse();
        lazy.Configurable.Should().BeFalse();
        lazy.Enumerable.Should().BeTrue();
        counter.A.Should().Be(0);
        counter.B.Should().Be(0);
    }

    // ---- the dictionary fallback arm ----

    [Fact]
    public void TheBudgetExhaustedFallbackBuildsTheSameObjectWithTheSameLaziness()
    {
        var engine = new Engine();
        // Burn the engine's host transition budget so Create cannot intern this layout's shape.
        var field = typeof(Engine).GetField("_hostShapeTransitions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(engine, Engine.HostShapeTransitionBudget);

        var counter = new Counter();
        var obj = Create(engine, counter);
        engine.SetValue("o", obj);

        engine.Diagnostics.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.Dictionary);
        obj._properties!["b"].Should().BeOfType<LazySlotPropertyDescriptor>();

        // Behaviour-identical to the shaped arm, down to the factory not having run.
        counter.A.Should().Be(0);
        engine.Evaluate("Object.keys(o).join(',')").AsString().Should().Be("eager,a,b");
        engine.Evaluate("'a' in o").AsBoolean().Should().BeTrue();
        counter.A.Should().Be(0);

        engine.Evaluate("o.a").AsNumber().Should().Be(1);
        engine.Evaluate("o.a").AsNumber().Should().Be(1);
        counter.A.Should().Be(1);
        counter.B.Should().Be(0);
    }

    // ---- spread ----

    [Fact]
    public void SpreadDeclinesShapeAdoptionWhileASentinelIsLiveAndReadsTheSourceOnce()
    {
        var engine = new Engine();
        var counter = new Counter();
        var obj = Create(engine, counter);
        engine.SetValue("o", obj);

        var copy = engine.Evaluate("({ ...o })").Should().BeOfType<JsObject>().Which;

        // Streamed, not adopted — but the result is still shaped and holds real values, never the sentinel.
        counter.A.Should().Be(1);
        counter.B.Should().Be(1);
        (copy._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
        HasLazyFlag(copy).Should().BeFalse();
        copy.ShapeOf.Should().BeSameAs(obj.ShapeOf);
        copy.GetSlot(1).Should().BeOfType<JsNumber>();

        // The source materialized exactly once, so a second spread copies the memo.
        engine.Evaluate("({ ...o })");
        counter.A.Should().Be(1);
        counter.B.Should().Be(1);
    }

    [Fact]
    public void SpreadAdoptsTheShapeAgainOnceEveryLazySlotIsMaterialized()
    {
        var engine = new Engine();
        var obj = Create(engine, new Counter());
        engine.SetValue("o", obj);

        engine.Evaluate("o.a; o.b;");
        HasLazyFlag(obj).Should().BeFalse();

        var target = new JsObject(engine);
        target.StartShapeBuilding(engine.GetEmptyShape(engine.Realm.Intrinsics.Object.PrototypeObject));
        target.TryAdoptShapeFrom(obj).Should().BeTrue();
        target.ShapeOf.Should().BeSameAs(obj.ShapeOf);
    }

    [Fact]
    public void ShapeAdoptionIsRefusedWhileTheFlagIsSet()
    {
        var engine = new Engine();
        var obj = Create(engine, new Counter());

        var target = new JsObject(engine);
        target.StartShapeBuilding(engine.GetEmptyShape(engine.Realm.Intrinsics.Object.PrototypeObject));
        target.TryAdoptShapeFrom(obj).Should().BeFalse();
    }

    // ---- global snapshot corner ----

    [Fact]
    public void ASubstitutedLazyGlobalIsCapturedAndRestoredWithoutMaterializing()
    {
        // The only way a lazy layout slot reaches snapshot capture: a host substitutes a hidden-class JsObject
        // as the global. Capture normalizes it with ConvertToDictionaryMode, which emits the lazy descriptors;
        // because they are field-backed, capture records the (flags, null) pair and restore writes it back —
        // returning the property to unmaterialized, which is exactly the captured state.
        var engine = new Engine();
        var counter = new Counter();
        var host = Create(engine, counter);
        engine.SetValue("host", host);

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        counter.A.Should().Be(0);
        counter.B.Should().Be(0);

        engine.Evaluate("host.a").AsNumber().Should().Be(1);
        counter.A.Should().Be(1);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The object itself is a global's VALUE, captured by reference, so its own state is untouched by the
        // restore — the memo stands and the untouched sibling is still lazy.
        engine.Evaluate("host.a").AsNumber().Should().Be(1);
        counter.A.Should().Be(1);
        counter.B.Should().Be(0);
        engine.Evaluate("host.b").AsNumber().Should().Be(101);
        counter.B.Should().Be(1);
    }

}
