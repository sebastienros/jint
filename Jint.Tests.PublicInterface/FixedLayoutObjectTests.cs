using System.Collections.Generic;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The host-facing object factories: <see cref="JsObjectLayout"/> + <see cref="JsObject.Create"/> for a
/// fixed, known layout, and <see cref="JsObject.CreateFromEntries(Engine, System.ReadOnlySpan{KeyValuePair{string, JsValue}})"/>
/// for a runtime key set. Everything asserted here is observable through the public API only; the
/// hidden-class interning these build on is pinned separately (Jint.Tests, which can see internals).
/// </summary>
public class FixedLayoutObjectTests
{
    private static readonly JsObjectLayout Layout = new("id", "name", "active");

    private static JsObject CreateSample(Engine engine) => JsObject.Create(
        engine,
        Layout,
        [JsNumber.Create(1), new JsString("jint"), JsBoolean.True]);

    private static Engine EngineWithSample(out JsObject obj)
    {
        var engine = new Engine();
        obj = CreateSample(engine);
        engine.SetValue("o", obj);
        return engine;
    }

    // ---- layout validation ----

    [Fact]
    public void LayoutRejectsNullNameCollection()
    {
        Invoking(() => new JsObjectLayout((string[]) null)).Should().Throw<ArgumentNullException>();
        Invoking(() => new JsObjectLayout((IReadOnlyList<string>) null)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LayoutRejectsDuplicateNames()
    {
        Invoking(() => new JsObjectLayout("a", "b", "a"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Duplicate property name 'a' at index 2*");
    }

    [Fact]
    public void LayoutRejectsNullOrEmptyNames()
    {
        Invoking(() => new JsObjectLayout("a", null)).Should().Throw<ArgumentException>().WithMessage("*index 1*");
        Invoking(() => new JsObjectLayout("a", "")).Should().Throw<ArgumentException>().WithMessage("*index 1*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("1x")]
    [InlineData("0abc")]
    public void LayoutRejectsIntegerIndexLikeNames(string name)
    {
        // Such a key must enumerate in ascending numeric order ahead of the string keys, which a fixed
        // insertion-ordered layout cannot express.
        Invoking(() => new JsObjectLayout("a", name))
            .Should().Throw<ArgumentException>()
            .WithMessage("*starts with a digit*");
    }

    [Theory]
    [InlineData("３ｄ")]  // FULLWIDTH DIGIT THREE + FULLWIDTH LATIN SMALL LETTER D
    [InlineData("٣x")]       // ARABIC-INDIC DIGIT THREE
    [InlineData("৩")]        // BENGALI DIGIT THREE
    public void LayoutAcceptsNamesStartingWithANonAsciiDigit(string name)
    {
        // Only ASCII digits can begin a canonical array index, so a name like "３ｄ" enumerates
        // as an ordinary string key and a fixed layout can express it.
        var layout = new JsObjectLayout("a", name);
        layout.IndexOf(name).Should().Be(1);

        var engine = new Engine();
        engine.SetValue("o", JsObject.Create(engine, layout, [JsNumber.Create(1), JsNumber.Create(2)]));
        engine.Evaluate("Object.keys(o).join()").Should().Be("a," + name);
    }

    [Fact]
    public void LayoutAcceptsUpToSixtyFourNamesAndRejectsMore()
    {
        var sixtyFour = new string[64];
        for (var i = 0; i < sixtyFour.Length; i++)
        {
            sixtyFour[i] = "p" + i;
        }

        var layout = new JsObjectLayout(sixtyFour);
        layout.Count.Should().Be(64);

        var sixtyFive = new string[65];
        for (var i = 0; i < sixtyFive.Length; i++)
        {
            sixtyFive[i] = "p" + i;
        }

        Invoking(() => new JsObjectLayout(sixtyFive))
            .Should().Throw<ArgumentException>()
            .WithMessage("*at most 64 properties*");
    }

    [Fact]
    public void LayoutExposesCountAndIndexOf()
    {
        Layout.Count.Should().Be(3);
        Layout.IndexOf("id").Should().Be(0);
        Layout.IndexOf("name").Should().Be(1);
        Layout.IndexOf("active").Should().Be(2);
        Layout.IndexOf("missing").Should().Be(-1);
        Layout.IndexOf(null).Should().Be(-1);
    }

    [Fact]
    public void WideLayoutIndexOfUsesTheIndexedPath()
    {
        // Past the linear-scan cutover IndexOf switches to a built index; the answers must not change.
        var names = new string[40];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = "field" + i;
        }

        var layout = new JsObjectLayout(names);
        for (var i = 0; i < names.Length; i++)
        {
            layout.IndexOf(names[i]).Should().Be(i);
        }

        layout.IndexOf("field40").Should().Be(-1);
    }

    [Fact]
    public void CreateRejectsArityMismatchAndNullArguments()
    {
        var engine = new Engine();

        Invoking(() => JsObject.Create(engine, Layout, [JsNumber.Create(1)]))
            .Should().Throw<ArgumentException>()
            .WithMessage("*describes 3 properties but 1 values*");

        Invoking(() => JsObject.Create(engine, Layout, []))
            .Should().Throw<ArgumentException>();

        Invoking(() => JsObject.Create(null, Layout, [])).Should().Throw<ArgumentNullException>();
        Invoking(() => JsObject.Create(engine, null, [])).Should().Throw<ArgumentNullException>();
    }

    // ---- shape of the produced object ----

    [Fact]
    public void CreatedObjectReadsWritesAndReportsPresenceLikeALiteral()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("o.id").Should().Be(1);
        engine.Evaluate("o.name").Should().Be("jint");
        engine.Evaluate("o.active").Should().Be(true);
        engine.Evaluate("o.missing").Should().BeUndefined();

        engine.Evaluate("'name' in o").Should().Be(true);
        engine.Evaluate("'missing' in o").Should().Be(false);
        engine.Evaluate("Object.prototype.hasOwnProperty.call(o, 'active')").Should().Be(true);
        engine.Evaluate("Object.getPrototypeOf(o) === Object.prototype").Should().Be(true);

        // Values are writable in place, without changing the layout.
        engine.Evaluate("o.name = 'changed'; o.name").Should().Be("changed");
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");

        var descriptor = engine.Evaluate("""
            (function () {
                var d = Object.getOwnPropertyDescriptor(o, 'id');
                return [d.value, d.writable, d.enumerable, d.configurable].join();
            })()
            """);
        descriptor.Should().Be("1,true,true,true");
    }

    [Fact]
    public void CreatedObjectOwnKeyOrderMatchesAnEquivalentLiteral()
    {
        var engine = EngineWithSample(out _);
        engine.Execute("var literal = { id: 1, name: 'jint', active: true };");

        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
        engine.Evaluate("Object.keys(o).join() === Object.keys(literal).join()").Should().Be(true);

        engine.Evaluate("(function () { var r = []; for (var k in o) r.push(k); return r.join(); })()")
            .Should().Be("id,name,active");

        engine.Evaluate("JSON.stringify(o)").Should().Be("""{"id":1,"name":"jint","active":true}""");
        engine.Evaluate("JSON.stringify(o) === JSON.stringify(literal)").Should().Be(true);

        engine.Evaluate("Object.keys({ ...o }).join()").Should().Be("id,name,active");
        engine.Evaluate("JSON.stringify(Object.entries(o))").Should().Be("""[["id",1],["name","jint"],["active",true]]""");
    }

    [Fact]
    public void EmptyLayoutCreatesAnEmptyObject()
    {
        var engine = new Engine();
        var empty = new JsObjectLayout();
        empty.Count.Should().Be(0);

        engine.SetValue("o", JsObject.Create(engine, empty, []));
        engine.Evaluate("Object.keys(o).length").Should().Be(0);
        engine.Evaluate("JSON.stringify(o)").Should().Be("{}");
        engine.Evaluate("o.x = 1; o.x").Should().Be(1);
    }

    [Fact]
    public void LayoutWiderThanTheInlineSlotCapacityKeepsOrderAndValues()
    {
        var engine = new Engine();
        var layout = new JsObjectLayout("a", "b", "c", "d", "e", "f", "g");
        engine.SetValue("o", JsObject.Create(engine, layout,
        [
            JsNumber.Create(1), JsNumber.Create(2), JsNumber.Create(3), JsNumber.Create(4),
            JsNumber.Create(5), JsNumber.Create(6), JsNumber.Create(7)
        ]));

        engine.Evaluate("Object.keys(o).join()").Should().Be("a,b,c,d,e,f,g");
        engine.Evaluate("Object.values(o).join()").Should().Be("1,2,3,4,5,6,7");
        engine.Evaluate("o.g = 70; o.a = 10; o.a + ',' + o.g").Should().Be("10,70");
    }

    [Fact]
    public void NullValuesBecomeUndefined()
    {
        var engine = new Engine();
        var layout = new JsObjectLayout("a", "b");
        engine.SetValue("o", JsObject.Create(engine, layout, [null, JsValue.Null]));

        engine.Evaluate("o.a === undefined").Should().Be(true);
        engine.Evaluate("o.b === null").Should().Be(true);
        engine.Evaluate("Object.keys(o).join()").Should().Be("a,b");
    }

    // ---- degradation: every trigger must leave a correct ordinary object behind ----

    [Fact]
    public void AddingAPropertyKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("o.extra = 'x'; Object.keys(o).join()").Should().Be("id,name,active,extra");
        engine.Evaluate("JSON.stringify(o)").Should().Be("""{"id":1,"name":"jint","active":true,"extra":"x"}""");
        engine.Evaluate("o.id + '|' + o.extra").Should().Be("1|x");
    }

    [Fact]
    public void DeletingAPropertyKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("delete o.name").Should().Be(true);
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,active");
        engine.Evaluate("'name' in o").Should().Be(false);
        engine.Evaluate("o.name").Should().BeUndefined();
        engine.Evaluate("o.id + ',' + o.active").Should().Be("1,true");

        // still an ordinary object afterwards
        engine.Evaluate("o.name = 're-added'; Object.keys(o).join()").Should().Be("id,active,name");
    }

    [Fact]
    public void FreezingKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("Object.freeze(o); Object.isFrozen(o)").Should().Be(true);
        engine.Evaluate("o.name = 'nope'; o.name").Should().Be("jint");
        engine.Evaluate("o.brandNew = 1; 'brandNew' in o").Should().Be(false);
        engine.Evaluate("delete o.id; 'id' in o").Should().Be(true);
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");

        var descriptor = engine.Evaluate("""
            (function () {
                var d = Object.getOwnPropertyDescriptor(o, 'id');
                return [d.writable, d.configurable].join();
            })()
            """);
        descriptor.Should().Be("false,false");

        engine.Evaluate("(function () { 'use strict'; try { o.name = 'x'; return 'no-throw'; } catch (e) { return e instanceof TypeError ? 'TypeError' : 'other'; } })()")
            .Should().Be("TypeError");
    }

    [Fact]
    public void SealingKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("Object.seal(o); Object.isSealed(o)").Should().Be(true);
        engine.Evaluate("Object.isFrozen(o)").Should().Be(false);
        engine.Evaluate("o.name = 'still writable'; o.name").Should().Be("still writable");
        engine.Evaluate("o.brandNew = 1; 'brandNew' in o").Should().Be(false);
        engine.Evaluate("delete o.id; 'id' in o").Should().Be(true);
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
    }

    [Fact]
    public void DefiningAnAccessorKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("""
            Object.defineProperty(o, 'label', {
                get: function () { return this.name + '#' + this.id; },
                enumerable: true,
                configurable: true
            });
            """);

        engine.Evaluate("o.label").Should().Be("jint#1");
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active,label");
        engine.Evaluate("typeof Object.getOwnPropertyDescriptor(o, 'label').get").Should().Be("function");
        engine.Evaluate("o.id = 7; o.label").Should().Be("jint#7");

        // redefining an existing layout property as an accessor works too
        engine.Evaluate("Object.defineProperty(o, 'name', { get: function () { return 'computed'; }, enumerable: true, configurable: true }); o.name")
            .Should().Be("computed");
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active,label");
    }

    [Fact]
    public void DefiningANonEnumerablePropertyKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("Object.defineProperty(o, 'hidden', { value: 42, enumerable: false, writable: false, configurable: false });");
        engine.Evaluate("o.hidden").Should().Be(42);
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
        engine.Evaluate("Object.getOwnPropertyNames(o).join()").Should().Be("id,name,active,hidden");
        engine.Evaluate("JSON.stringify(o)").Should().Be("""{"id":1,"name":"jint","active":true}""");
    }

    [Fact]
    public void SettingThePrototypeKeepsEverythingCorrect()
    {
        var engine = EngineWithSample(out _);
        engine.Execute("var proto = { inherited: 'yes', name: 'shadowed' };");

        engine.Evaluate("Object.setPrototypeOf(o, proto); Object.getPrototypeOf(o) === proto").Should().Be(true);
        engine.Evaluate("o.inherited").Should().Be("yes");
        engine.Evaluate("o.name").Should().Be("jint");
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
        engine.Evaluate("(function () { var r = []; for (var k in o) r.push(k); return r.join(); })()")
            .Should().Be("id,name,active,inherited");

        engine.Evaluate("Object.setPrototypeOf(o, null); o.inherited === undefined").Should().Be(true);
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
    }

    [Fact]
    public void SymbolKeysCoexistWithTheLayout()
    {
        var engine = EngineWithSample(out _);

        engine.Evaluate("var s = Symbol('s'); o[s] = 'sym'; o[s]").Should().Be("sym");
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
        engine.Evaluate("Object.getOwnPropertySymbols(o).length").Should().Be(1);
    }

    // ---- cross engine ----

    [Fact]
    public void TheSameLayoutWorksAcrossEnginesWithoutLeakingState()
    {
        var first = new Engine();
        var second = new Engine();

        first.SetValue("o", JsObject.Create(first, Layout, [JsNumber.Create(1), new JsString("first"), JsBoolean.True]));
        second.SetValue("o", JsObject.Create(second, Layout, [JsNumber.Create(2), new JsString("second"), JsBoolean.False]));

        first.Evaluate("JSON.stringify(o)").Should().Be("""{"id":1,"name":"first","active":true}""");
        second.Evaluate("JSON.stringify(o)").Should().Be("""{"id":2,"name":"second","active":false}""");

        // Mutating (and deopting) one engine's object must not disturb the other's, nor the shared layout.
        first.Evaluate("delete o.name; o.extra = true;");
        first.Evaluate("Object.keys(o).join()").Should().Be("id,active,extra");
        second.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");

        var third = new Engine();
        third.SetValue("o", JsObject.Create(third, Layout, [JsNumber.Create(3), new JsString("third"), JsBoolean.True]));
        third.Evaluate("Object.keys(o).join()").Should().Be("id,name,active");
        Layout.Count.Should().Be(3);
    }

    [Fact]
    public void ManyObjectsFromOneLayoutBehaveIdentically()
    {
        var engine = new Engine();
        var array = engine.Intrinsics.Array.Construct(0);
        for (var i = 0; i < 500; i++)
        {
            array.Push(JsObject.Create(engine, Layout, [JsNumber.Create(i), new JsString("n" + i), JsBoolean.True]));
        }

        engine.SetValue("items", array);
        engine.Evaluate("(function () { var s = 0; for (var i = 0; i < items.length; i++) s += items[i].id; return s; })()")
            .Should().Be(124750);
        engine.Evaluate("items[499].name").Should().Be("n499");
        engine.Evaluate("Object.keys(items[250]).join()").Should().Be("id,name,active");
    }

    // ---- CreateFromEntries ----

    [Fact]
    public void CreateFromEntriesKeepsInsertionOrder()
    {
        var engine = new Engine();
        engine.SetValue("o", JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("zebra", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("apple", new JsString("a")),
            new KeyValuePair<string, JsValue>("mango", JsBoolean.False)
        ]));

        engine.Evaluate("Object.keys(o).join()").Should().Be("zebra,apple,mango");
        engine.Evaluate("JSON.stringify(o)").Should().Be("""{"zebra":1,"apple":"a","mango":false}""");
        engine.Evaluate("(function () { var r = []; for (var k in o) r.push(k); return r.join(); })()")
            .Should().Be("zebra,apple,mango");
        engine.Evaluate("Object.keys({ ...o }).join()").Should().Be("zebra,apple,mango");
    }

    [Fact]
    public void CreateFromEntriesAcceptsAnArrayAndAnEnumerable()
    {
        var engine = new Engine();

        var array = new[]
        {
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("b", JsNumber.Create(2))
        };
        engine.SetValue("fromArray", JsObject.CreateFromEntries(engine, array));

        var list = new List<KeyValuePair<string, JsValue>>(array);
        engine.SetValue("fromList", JsObject.CreateFromEntries(engine, list));

        var dictionary = new Dictionary<string, JsValue>(StringComparer.Ordinal)
        {
            ["a"] = JsNumber.Create(1),
            ["b"] = JsNumber.Create(2)
        };
        engine.SetValue("fromDictionary", JsObject.CreateFromEntries(engine, dictionary));

        engine.Evaluate("JSON.stringify(fromArray)").Should().Be("""{"a":1,"b":2}""");
        engine.Evaluate("JSON.stringify(fromList)").Should().Be("""{"a":1,"b":2}""");
        engine.Evaluate("Object.keys(fromDictionary).sort().join()").Should().Be("a,b");
        engine.Evaluate("fromDictionary.a + fromDictionary.b").Should().Be(3);
    }

    [Fact]
    public void CreateFromEntriesRejectsNullArguments()
    {
        var engine = new Engine();

        Invoking(() => JsObject.CreateFromEntries(null, System.Array.Empty<KeyValuePair<string, JsValue>>()))
            .Should().Throw<ArgumentNullException>();
        Invoking(() => JsObject.CreateFromEntries(engine, (IEnumerable<KeyValuePair<string, JsValue>>) null))
            .Should().Throw<ArgumentNullException>();
        Invoking(() => JsObject.CreateFromEntries(engine, new[] { new KeyValuePair<string, JsValue>(null, JsValue.Null) }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromEntriesEmptyProducesAnOrdinaryEmptyObject()
    {
        var engine = new Engine();
        engine.SetValue("o", JsObject.CreateFromEntries(engine, System.Array.Empty<KeyValuePair<string, JsValue>>()));

        engine.Evaluate("Object.keys(o).length").Should().Be(0);
        engine.Evaluate("o.x = 1; JSON.stringify(o)").Should().Be("""{"x":1}""");
    }

    [Fact]
    public void CreateFromEntriesDuplicateKeyKeepsFirstPositionAndLastValue()
    {
        var engine = new Engine();
        engine.SetValue("o", JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("b", JsNumber.Create(2)),
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(3))
        ]));

        engine.Evaluate("JSON.stringify(o)").Should().Be("""{"a":3,"b":2}""");
    }

    [Fact]
    public void CreateFromEntriesWithIntegerLikeKeysUsesSpecOrder()
    {
        var engine = new Engine();
        engine.SetValue("o", JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("b", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("5", JsNumber.Create(2)),
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(3)),
            new KeyValuePair<string, JsValue>("0", JsNumber.Create(4))
        ]));

        // Integer indices ascending first, then the string keys in insertion order.
        engine.Evaluate("Object.keys(o).join()").Should().Be("0,5,b,a");
        engine.Evaluate("o[0] + ',' + o[5] + ',' + o.a + ',' + o.b").Should().Be("4,2,3,1");

        // Leading integer-like key: never shaped at all, same result.
        engine.SetValue("p", JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("0", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("z", JsNumber.Create(2))
        ]));
        engine.Evaluate("Object.keys(p).join()").Should().Be("0,z");
    }

    [Fact]
    public void CreateFromEntriesPastTheHiddenClassPropertyLimitKeepsEveryEntry()
    {
        var engine = new Engine();
        var entries = new KeyValuePair<string, JsValue>[80];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new KeyValuePair<string, JsValue>("k" + i, JsNumber.Create(i));
        }

        engine.SetValue("o", JsObject.CreateFromEntries(engine, entries));
        engine.Evaluate("""
            (function () {
                var keys = Object.keys(o);
                if (keys.length !== 80) return false;
                for (var i = 0; i < 80; i++) {
                    if (keys[i] !== ('k' + i) || o['k' + i] !== i) return false;
                }
                return true;
            })()
            """).Should().Be(true);
    }

    [Fact]
    public void CreateFromEntriesResultDegradesCorrectly()
    {
        var engine = new Engine();
        engine.SetValue("o", JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("a", JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("b", JsNumber.Create(2))
        ]));

        engine.Evaluate("o.c = 3; delete o.a; Object.defineProperty(o, 'd', { get: function () { return 4; }, enumerable: true });");
        engine.Evaluate("Object.keys(o).join()").Should().Be("b,c,d");
        engine.Evaluate("o.b + o.c + o.d").Should().Be(9);
        engine.Evaluate("Object.freeze(o); Object.isFrozen(o)").Should().Be(true);
    }

    [Fact]
    public void CreateFromEntriesWithVaryingKeySetsStaysCorrect()
    {
        // A host feeding wildly varying key sets must keep getting correct objects; the engine bounds how
        // much hidden-class state that can intern and silently degrades the representation, never the
        // behavior.
        var engine = new Engine();
        var array = engine.Intrinsics.Array.Construct(0);
        for (var i = 0; i < 2000; i++)
        {
            array.Push(JsObject.CreateFromEntries(engine,
            [
                new KeyValuePair<string, JsValue>("f" + i, JsNumber.Create(i)),
                new KeyValuePair<string, JsValue>("g" + (i * 7), JsNumber.Create(i)),
                new KeyValuePair<string, JsValue>("shared", JsNumber.Create(i))
            ]));
        }

        engine.SetValue("items", array);
        engine.Evaluate("""
            (function () {
                for (var i = 0; i < items.length; i++) {
                    var o = items[i];
                    if (Object.keys(o).join() !== ['f' + i, 'g' + (i * 7), 'shared'].join()) return false;
                    if (o['f' + i] !== i || o.shared !== i) return false;
                }
                return true;
            })()
            """).Should().Be(true);
    }
}
