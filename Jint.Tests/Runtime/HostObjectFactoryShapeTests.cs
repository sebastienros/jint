using System.Collections.Generic;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the hidden-class behavior of the host-facing object factories (<see cref="JsObject.Create"/> and
/// <see cref="JsObject.CreateFromEntries(Engine, System.ReadOnlySpan{KeyValuePair{string, JsValue}})"/>).
/// Interning — separately constructed objects of the same layout referencing the very same
/// <see cref="Jint.Native.Object.Shape"/> — is what keeps a script's <c>o.x</c> call site monomorphic
/// across a whole batch, and it is not observable through the public API, so it is asserted here where
/// internals are visible. The publicly observable behavior lives in
/// Jint.Tests.PublicInterface/FixedLayoutObjectTests.cs.
/// </summary>
public class HostObjectFactoryShapeTests
{
    private static readonly JsObjectLayout Layout = new("id", "name", "active");

    private static JsObject Sample(Engine engine, int id) => JsObject.Create(
        engine,
        Layout,
        [JsNumber.Create(id), JsString.Create("n" + id), JsBoolean.True]);

    [Fact]
    public void CreatedObjectIsInShapeMode()
    {
        var engine = new Engine();
        var obj = Sample(engine, 1);

        (obj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
        obj.ShapeOf.SlotCount.Should().Be(3);
    }

    [Fact]
    public void EveryObjectBuiltFromOneLayoutSharesOneShape()
    {
        var engine = new Engine();
        var first = Sample(engine, 1);
        var shape = first.ShapeOf;

        for (var i = 2; i <= 200; i++)
        {
            var next = Sample(engine, i);
            next.Should().NotBeSameAs(first);
            // Same instance, not merely an equal layout: this is what makes the member inline cache hit
            // across separately-constructed objects.
            next.ShapeOf.Should().BeSameAs(shape);
        }
    }

    [Fact]
    public void LayoutShapeIsTheSameShapeAnEquivalentObjectLiteralBuilds()
    {
        var engine = new Engine();
        var created = Sample(engine, 1);

        var literal = engine.Evaluate("({ id: 1, name: 'n1', active: true })").Should().BeOfType<JsObject>().Which;
        literal.ShapeOf.Should().BeSameAs(created.ShapeOf);

        // ...and the same one the entries factory reaches for the same key sequence.
        var fromEntries = JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("id", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("name", JsString.Create("n1")),
            new KeyValuePair<string, JsValue>("active", JsBoolean.True)
        ]);
        fromEntries.ShapeOf.Should().BeSameAs(created.ShapeOf);

        // ...and the one Object.fromEntries / spread reach, since all of them grow the same per-prototype
        // transition tree.
        var spread = engine.Evaluate("({ ...{ id: 1, name: 'n1', active: true } })").Should().BeOfType<JsObject>().Which;
        spread.ShapeOf.Should().BeSameAs(created.ShapeOf);
    }

    [Fact]
    public void RepeatedEntryKeySequencesShareOneShape()
    {
        var engine = new Engine();

        JsObject Build(int i) => JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("first", JsNumber.Create(i)),
            new KeyValuePair<string, JsValue>("second", JsString.Create("s")),
            new KeyValuePair<string, JsValue>("third", JsBoolean.False),
            new KeyValuePair<string, JsValue>("fourth", JsNumber.Create(i)),
            new KeyValuePair<string, JsValue>("fifth", JsNumber.Create(i))
        ]);

        var first = Build(0);
        (first._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);

        for (var i = 1; i < 50; i++)
        {
            Build(i).ShapeOf.Should().BeSameAs(first.ShapeOf);
        }

        // A different key sequence must land on a different shape (the tree branches, it does not merge).
        var other = JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("second", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("first", JsNumber.Create(1))
        ]);
        other.ShapeOf.Should().NotBeSameAs(first.ShapeOf);
    }

    [Fact]
    public void TheSameLayoutResolvesToADifferentShapePerEngine()
    {
        var first = new Engine();
        var second = new Engine();

        var a = Sample(first, 1);
        var b = Sample(second, 1);

        // Empty root shapes are interned per (engine, prototype) and the transition tree hangs off that
        // root, so one process-shared layout cannot carry a single Shape.
        a.ShapeOf.Should().NotBeSameAs(b.ShapeOf);
        a.ShapeOf.SlotCount.Should().Be(3);
        b.ShapeOf.SlotCount.Should().Be(3);

        // Within each engine the memo still holds.
        Sample(first, 2).ShapeOf.Should().BeSameAs(a.ShapeOf);
        Sample(second, 2).ShapeOf.Should().BeSameAs(b.ShapeOf);
    }

    [Fact]
    public void DifferentLayoutInstancesWithTheSameNamesShareOneShape()
    {
        var engine = new Engine();
        var equivalent = new JsObjectLayout("id", "name", "active");

        var a = Sample(engine, 1);
        var b = JsObject.Create(engine, equivalent, [JsNumber.Create(2), JsString.Create("n2"), JsBoolean.False]);

        // The memo is per layout instance, but the transitions it walks are interned per prototype, so a
        // host that (wastefully) rebuilds an equal layout still lands on the same hidden class.
        b.ShapeOf.Should().BeSameAs(a.ShapeOf);
    }

    [Theory]
    [InlineData("o.extra = 1;")]
    [InlineData("delete o.name;")]
    [InlineData("Object.freeze(o);")]
    [InlineData("Object.seal(o);")]
    [InlineData("Object.defineProperty(o, 'g', { get: function () { return 1; } });")]
    [InlineData("Object.defineProperty(o, 'name', { value: 2, writable: false });")]
    public void DeoptTriggersLeaveAnOrdinaryDictionaryObject(string script)
    {
        var engine = new Engine();
        var obj = Sample(engine, 1);
        engine.SetValue("o", obj);

        (obj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
        engine.Execute(script);

        (obj._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        obj._properties.Should().NotBeNull();
        engine.Evaluate("o.id").Should().Be(1);
    }

    [Fact]
    public void SettingThePrototypeKeepsTheLayoutValid()
    {
        // Unlike the other mutations, a prototype change does NOT deopt: a Shape maps names to slots and
        // never encodes the prototype, and the member inline cache only serves own-slot reads, so the
        // object stays shaped and correct. The shape it keeps is rooted at the previous prototype, which
        // costs nothing but a missed sharing opportunity with objects built under the new one.
        var engine = new Engine();
        var obj = Sample(engine, 1);
        engine.SetValue("o", obj);
        engine.Execute("var proto = { inherited: 'yes' }; Object.setPrototypeOf(o, proto);");

        engine.Evaluate("o.id").Should().Be(1);
        engine.Evaluate("o.inherited").Should().Be("yes");
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
    }

    [Fact]
    public void IntegerLikeEntryKeyDropsTheObjectToDictionaryMode()
    {
        var engine = new Engine();
        var obj = JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("0", JsNumber.Create(2))
        ]);

        (obj._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        engine.SetValue("o", obj);
        engine.Evaluate("Object.keys(o).join()").Should().Be("0,a");
    }

    [Fact]
    public void WildlyVaryingEntryKeySetsStopInterningAndFallBackToDictionaries()
    {
        // The fan-out guard bounds how many distinct continuations one shape may sprout: once the empty
        // root has MaxFanout children, an object whose very first key is new is refused immediately and
        // finishes as a dictionary. That is what keeps an adversarial key set from growing the
        // per-prototype transition tree without bound.
        var engine = new Engine();
        var shapedCount = 0;
        for (var i = 0; i < 500; i++)
        {
            var obj = JsObject.CreateFromEntries(engine,
            [
                new KeyValuePair<string, JsValue>("k" + i, JsNumber.Create(i)),
                new KeyValuePair<string, JsValue>("shared", JsNumber.Create(i))
            ]);

            if ((obj._type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                shapedCount++;
            }

            engine.SetValue("o", obj);
            engine.Evaluate("Object.keys(o).join()").Should().Be("k" + i + ",shared");
            engine.Evaluate("o.shared").Should().Be(i);
        }

        // Only the first MaxFanout distinct first-keys can be interned under the root.
        shapedCount.Should().Be(64);
    }

    [Fact]
    public void ExhaustingTheHostTransitionBudgetFallsBackToDictionaryObjects()
    {
        var engine = new Engine();

        // 64 properties is the widest layout a hidden class can describe, so this burns the engine's
        // whole host budget in the fewest possible iterations.
        const int Width = 64;
        var layouts = new List<JsObjectLayout>();
        var values = new JsValue[Width];
        for (var i = 0; i < Width; i++)
        {
            values[i] = JsNumber.Create(i);
        }

        var iterations = Engine.HostShapeTransitionBudget / Width;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var names = new string[Width];
            for (var i = 0; i < Width; i++)
            {
                names[i] = "L" + iteration + "_" + i;
            }

            var layout = new JsObjectLayout(names);
            layouts.Add(layout);
            var obj = JsObject.Create(engine, layout, values);
            (obj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
        }

        // Budget spent: a brand-new layout now produces a correct, ordinary dictionary-mode object.
        var overflowNames = new string[Width];
        for (var i = 0; i < Width; i++)
        {
            overflowNames[i] = "overflow_" + i;
        }

        var overflow = JsObject.Create(engine, new JsObjectLayout(overflowNames), values);
        (overflow._type & InternalTypes.ShapeMode).Should().Be(InternalTypes.Empty);
        engine.SetValue("o", overflow);
        engine.Evaluate("Object.keys(o).length").Should().Be(Width);
        engine.Evaluate("o.overflow_0 + ',' + o.overflow_63").Should().Be("0,63");

        // Layouts already resolved keep their interned shape — the memo hit never consults the budget.
        var reused = JsObject.Create(engine, layouts[0], values);
        (reused._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);

        // A different engine has its own budget.
        var fresh = new Engine();
        var freshObj = JsObject.Create(fresh, new JsObjectLayout(overflowNames), values);
        (freshObj._type & InternalTypes.ShapeMode).Should().NotBe(InternalTypes.Empty);
    }
}
