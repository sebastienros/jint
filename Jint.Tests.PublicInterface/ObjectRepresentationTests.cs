#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.AdvancedOperations.GetObjectRepresentation"/> is the only way a host can tell whether
/// the object factories actually shaped an object or quietly fell back to the dictionary representation.
/// These tests live outside the Jint assembly on purpose: the point of the API is that an integrator with
/// nothing but the public surface can write exactly these assertions.
/// </summary>
public class ObjectRepresentationTests
{
    private static readonly JsObjectLayout Layout = new("id", "name", "active");

    private static JsObject CreateSample(Engine engine) => JsObject.Create(
        engine,
        Layout,
        [JsNumber.Create(1), new JsString("sample"), JsBoolean.True]);

    private static KeyValuePair<string, JsValue>[] SampleEntries() =>
    [
        new("id", JsNumber.Create(1)),
        new("name", new JsString("sample")),
        new("active", JsBoolean.True)
    ];

    /// <summary>A host object that answers its own properties instead of using Jint's storage.</summary>
    private sealed class HostRecord : ObjectInstance
    {
        public HostRecord(Engine engine) : base(engine)
        {
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            return property is JsString name && string.Equals(name.ToString(), "answer", System.StringComparison.Ordinal)
                ? new PropertyDescriptor(JsNumber.Create(42), writable: false, enumerable: true, configurable: false)
                : PropertyDescriptor.Undefined;
        }
    }

    private sealed class HostPoint
    {
        public int X { get; set; }
    }

    // ---- reachability ----

    [Fact]
    public void TheDiagnosticIsReachableFromOutsideTheJintAssembly()
    {
        // This project has no InternalsVisibleTo, so the call below compiling at all is the guarantee. The
        // reflection assertions catch the accident of the surface being narrowed to internal + IVT later.
        var engine = new Engine();
        engine.Advanced.GetObjectRepresentation(CreateSample(engine)).Should().Be(ObjectRepresentation.HiddenClass);

        typeof(ObjectRepresentation).IsPublic.Should().BeTrue();
        typeof(Engine.AdvancedOperations).GetMethod(nameof(Engine.AdvancedOperations.GetObjectRepresentation))
            .Should().NotBeNull();
    }

    [Fact]
    public void NullIsRejected()
    {
        var engine = new Engine();
        Invoking(() => engine.Advanced.GetObjectRepresentation(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AnObjectFromAnotherEngineIsRejected()
    {
        // Hidden classes are interned per engine and the fallback thresholds are per-engine counters, so
        // asking one engine about another's object is a mistake worth failing on rather than answering.
        var engine = new Engine();
        var other = new Engine();

        Invoking(() => engine.Advanced.GetObjectRepresentation(CreateSample(other)))
            .Should().Throw<ArgumentException>().WithMessage("*different engine*");
    }

    // ---- what the factories produce ----

    [Fact]
    public void AnObjectBuiltFromALayoutIsShaped()
    {
        var engine = new Engine();
        engine.Advanced.GetObjectRepresentation(CreateSample(engine)).Should().Be(ObjectRepresentation.HiddenClass);

        // An empty layout still resolves to a (property-less) hidden class.
        var empty = JsObject.Create(engine, new JsObjectLayout(), []);
        engine.Advanced.GetObjectRepresentation(empty).Should().Be(ObjectRepresentation.HiddenClass);

        // Wider than the in-object slot capacity, so the values spill — still shaped.
        var wide = JsObject.Create(engine, new JsObjectLayout("a", "b", "c", "d", "e", "f", "g"),
        [
            JsNumber.Create(1), JsNumber.Create(2), JsNumber.Create(3), JsNumber.Create(4),
            JsNumber.Create(5), JsNumber.Create(6), JsNumber.Create(7)
        ]);
        engine.Advanced.GetObjectRepresentation(wide).Should().Be(ObjectRepresentation.HiddenClass);
    }

    [Fact]
    public void AnObjectBuiltFromEntriesIsShaped()
    {
        var engine = new Engine();
        var fromSpan = JsObject.CreateFromEntries(engine, SampleEntries());
        engine.Advanced.GetObjectRepresentation(fromSpan).Should().Be(ObjectRepresentation.HiddenClass);

        var fromEnumerable = JsObject.CreateFromEntries(engine, (IEnumerable<KeyValuePair<string, JsValue>>) SampleEntries());
        engine.Advanced.GetObjectRepresentation(fromEnumerable).Should().Be(ObjectRepresentation.HiddenClass);
    }

    [Fact]
    public void AnEmptyEntrySetStaysInTheDictionaryRepresentation()
    {
        // Shaping starts on the first entry, so an object that never got one was never shaped. This is a
        // real difference from the empty-layout case above, and exactly the kind of thing the diagnostic
        // exists to make visible.
        var engine = new Engine();
        var empty = JsObject.CreateFromEntries(engine, System.Array.Empty<KeyValuePair<string, JsValue>>());

        engine.Advanced.GetObjectRepresentation(empty).Should().Be(ObjectRepresentation.Dictionary);
    }

    [Fact]
    public void AnOrdinaryObjectLiteralIsShapedAndAnEmptyOneIsNot()
    {
        var engine = new Engine();

        var literal = engine.Evaluate("({ id: 1, name: 'sample', active: true })").AsObject();
        engine.Advanced.GetObjectRepresentation(literal).Should().Be(ObjectRepresentation.HiddenClass);

        var empty = engine.Evaluate("({})").AsObject();
        engine.Advanced.GetObjectRepresentation(empty).Should().Be(ObjectRepresentation.Dictionary);
    }

    // ---- fallback triggers ----

    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("2nd")]
    public void AnIntegerIndexLikeEntryKeyFallsBackToTheDictionaryRepresentation(string name)
    {
        var engine = new Engine();

        // as the first key: never shaped at all
        var leading = JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>(name, JsNumber.Create(1)),
            new KeyValuePair<string, JsValue>("other", JsNumber.Create(2))
        ]);
        engine.Advanced.GetObjectRepresentation(leading).Should().Be(ObjectRepresentation.Dictionary);

        // and part way through: shaped, then dropped mid-build
        var trailing = JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("other", JsNumber.Create(2)),
            new KeyValuePair<string, JsValue>(name, JsNumber.Create(1))
        ]);
        engine.Advanced.GetObjectRepresentation(trailing).Should().Be(ObjectRepresentation.Dictionary);
    }

    [Fact]
    public void TooManyEntriesFallBackToTheDictionaryRepresentation()
    {
        var engine = new Engine();
        var entries = new KeyValuePair<string, JsValue>[80];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new KeyValuePair<string, JsValue>("k" + i, JsNumber.Create(i));
        }

        engine.Advanced.GetObjectRepresentation(JsObject.CreateFromEntries(engine, entries))
            .Should().Be(ObjectRepresentation.Dictionary);
    }

    [Fact]
    public void VaryingKeySetsEventuallyStopBeingShaped()
    {
        // The first object of a brand-new key set is shaped; after enough *different* key sets have branched
        // off the same point, later ones are not. Nothing about the failing call differs from the succeeding
        // one — the trigger is the engine's accumulated state, which is precisely why a host cannot predict
        // this at the call site and needs to be able to observe it instead.
        var engine = new Engine();

        JsObject Build(int i) => JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("k" + i, JsNumber.Create(i)),
            new KeyValuePair<string, JsValue>("shared", JsNumber.Create(i))
        ]);

        engine.Advanced.GetObjectRepresentation(Build(0)).Should().Be(ObjectRepresentation.HiddenClass);

        var fellBackAt = -1;
        for (var i = 1; i < 500 && fellBackAt < 0; i++)
        {
            if (engine.Advanced.GetObjectRepresentation(Build(i)) == ObjectRepresentation.Dictionary)
            {
                fellBackAt = i;
            }
        }

        fellBackAt.Should().BeGreaterThan(0, "an unbounded variety of key sets must stop interning layouts");

        // The objects are still completely correct, whichever representation they landed in.
        engine.SetValue("o", Build(fellBackAt));
        engine.Evaluate("Object.keys(o).join()").Should().Be("k" + fellBackAt + ",shared");
        engine.Evaluate("o.shared").Should().Be(fellBackAt);
    }

    [Fact]
    public void TheEnginesLayoutBudgetIsCumulativeAndEventuallyRunsOut()
    {
        // Same layout, same call, different answer depending only on how many *other* layouts this engine
        // has interned before — the second reason a host cannot know the representation by construction.
        var engine = new Engine();
        var values = new JsValue[64];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = JsNumber.Create(i);
        }

        JsObjectLayout WideLayout(string prefix)
        {
            var names = new string[64];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = prefix + i;
            }

            return new JsObjectLayout(names);
        }

        var first = WideLayout("first_");
        engine.Advanced.GetObjectRepresentation(JsObject.Create(engine, first, values))
            .Should().Be(ObjectRepresentation.HiddenClass);

        var exhaustedAt = -1;
        for (var iteration = 0; iteration < 1000 && exhaustedAt < 0; iteration++)
        {
            var layout = WideLayout("burn" + iteration + "_");
            if (engine.Advanced.GetObjectRepresentation(JsObject.Create(engine, layout, values)) == ObjectRepresentation.Dictionary)
            {
                exhaustedAt = iteration;
            }
        }

        exhaustedAt.Should().BeGreaterThan(0, "the per-engine layout budget is finite");

        // An already-resolved layout keeps its hidden class: the budget only gates layouts new to the engine.
        engine.Advanced.GetObjectRepresentation(JsObject.Create(engine, first, values))
            .Should().Be(ObjectRepresentation.HiddenClass);

        // A different engine starts over with a full budget, from the very layout that just failed.
        var fresh = new Engine();
        fresh.Advanced.GetObjectRepresentation(JsObject.Create(fresh, WideLayout("burn" + exhaustedAt + "_"), values))
            .Should().Be(ObjectRepresentation.HiddenClass);
    }

    // ---- deopt after construction ----

    [Theory]
    [InlineData("o.extra = 1;")]
    [InlineData("delete o.name;")]
    [InlineData("Object.freeze(o);")]
    [InlineData("Object.seal(o);")]
    [InlineData("Object.defineProperty(o, 'g', { get: function () { return 1; } });")]
    [InlineData("Object.defineProperty(o, 'name', { value: 2, writable: false });")]
    public void MutationsAShapeCannotExpressDropTheObjectToTheDictionaryRepresentation(string script)
    {
        var engine = new Engine();
        var obj = CreateSample(engine);
        engine.SetValue("o", obj);

        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.HiddenClass);
        engine.Execute(script);
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.Dictionary);

        // ...and the object still behaves exactly as it did.
        engine.Evaluate("o.id").Should().Be(1);
    }

    [Fact]
    public void MutationsAShapeCanExpressKeepTheObjectShaped()
    {
        var engine = new Engine();
        var obj = CreateSample(engine);
        engine.SetValue("o", obj);

        // Overwriting a value in place, adding a symbol key (which lives outside the layout), preventing
        // extensions (which changes no property descriptor) and swapping the prototype all leave the layout
        // intact — so a host asserting "still shaped" is not asserting "never touched".
        engine.Execute("o.name = 'changed'; o[Symbol('s')] = 1;");
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.HiddenClass);

        engine.Execute("Object.setPrototypeOf(o, { inherited: 1 });");
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.HiddenClass);

        engine.Execute("Object.preventExtensions(o);");
        engine.Advanced.GetObjectRepresentation(obj).Should().Be(ObjectRepresentation.HiddenClass);

        engine.Evaluate("o.name + ',' + o.inherited").Should().Be("changed,1");
    }

    // ---- the other representations ----

    [Fact]
    public void ABuiltinUsesItsSharedLayout()
    {
        var engine = new Engine();

        // Built-ins populate lazily, so touch them first; the diagnostic deliberately does not do this
        // itself, because a diagnostic that initializes what it inspects changes what it is measuring.
        engine.Execute("Math.abs(1); JSON.stringify(1);");

        engine.Advanced.GetObjectRepresentation(engine.Evaluate("Math").AsObject())
            .Should().Be(ObjectRepresentation.SharedBuiltinLayout);
        engine.Advanced.GetObjectRepresentation(engine.Evaluate("JSON").AsObject())
            .Should().Be(ObjectRepresentation.SharedBuiltinLayout);
    }

    [Fact]
    public void SpecializedObjectTypesReportThemselvesAsSuch()
    {
        var engine = new Engine();
        engine.SetValue("host", new HostPoint { X = 1 });

        var array = engine.Evaluate("[1, 2, 3]").AsObject();
        engine.Advanced.GetObjectRepresentation(array).Should().Be(ObjectRepresentation.Specialized);

        var wrapper = engine.Evaluate("host").AsObject();
        engine.Advanced.GetObjectRepresentation(wrapper).Should().Be(ObjectRepresentation.Specialized);

        var proxy = engine.Evaluate("new Proxy({}, {})").AsObject();
        engine.Advanced.GetObjectRepresentation(proxy).Should().Be(ObjectRepresentation.Specialized);
    }

    [Fact]
    public void AHostDefinedObjectSubclassIsNotReportedAsADictionary()
    {
        // A subclass that answers its own properties stores nothing in Jint's dictionary, so calling it a
        // dictionary object would be wrong.
        var engine = new Engine();
        var host = new HostRecord(engine);
        engine.SetValue("host", host);

        engine.Evaluate("host.answer").Should().Be(42);
        engine.Advanced.GetObjectRepresentation(host).Should().Be(ObjectRepresentation.Specialized);
    }
}
