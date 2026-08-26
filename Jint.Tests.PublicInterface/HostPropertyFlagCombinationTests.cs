#nullable enable

using System.Linq;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the named <see cref="PropertyFlag"/> combinations against the attribute triples they claim, from
/// outside the Jint assembly — where a host actually constructs descriptors. The six shipped names covered six
/// of the eight full data-attribute combinations, so a host wanting either of the other two had to spell the
/// bits by hand and get the <c>*Set</c> half right without help.
/// </summary>
public class HostPropertyFlagCombinationTests
{
    /// <summary>One named combination and the attribute triple it must produce.</summary>
    private sealed record Combination(PropertyFlag Flags, bool Writable, bool Enumerable, bool Configurable);

    /// <summary>The eight full data-attribute combinations, named.</summary>
    private static readonly Combination[] AllCombinations =
    [
        new(PropertyFlag.ConfigurableEnumerableWritable, Writable: true, Enumerable: true, Configurable: true),
        new(PropertyFlag.NonConfigurable, Writable: true, Enumerable: true, Configurable: false),
        new(PropertyFlag.NonEnumerable, Writable: true, Enumerable: false, Configurable: true),
        new(PropertyFlag.NonWritable, Writable: false, Enumerable: true, Configurable: true),
        new(PropertyFlag.OnlyWritable, Writable: true, Enumerable: false, Configurable: false),
        new(PropertyFlag.OnlyEnumerable, Writable: false, Enumerable: true, Configurable: false),
        new(PropertyFlag.OnlyConfigurable, Writable: false, Enumerable: false, Configurable: true),
        new(PropertyFlag.AllForbidden, Writable: false, Enumerable: false, Configurable: false),
    ];

    public static TestCases<PropertyFlag, bool, bool, bool> Lattice
    {
        get
        {
            var data = new TestCases<PropertyFlag, bool, bool, bool>();
            foreach (var c in AllCombinations)
            {
                data.Add(c.Flags, c.Writable, c.Enumerable, c.Configurable);
            }

            return data;
        }
    }

    [TestCaseSource(nameof(Lattice))]
    public void ANamedCombinationRoundTripsToTheAttributeTripleItNames(
        PropertyFlag flags,
        bool writable,
        bool enumerable,
        bool configurable)
    {
        // The round trip that matters to a host: build the descriptor in CLR with the named combination,
        // define it, and read the attributes back the way script sees them.
        var engine = new Engine();
        var target = new JsObject(engine);
        target.DefineOwnProperty(new JsString("p"), new PropertyDescriptor(new JsString("v"), flags));
        engine.SetValue("target", target);

        engine.Evaluate("Object.getOwnPropertyDescriptor(target, 'p').writable").Should().Be(writable);
        engine.Evaluate("Object.getOwnPropertyDescriptor(target, 'p').enumerable").Should().Be(enumerable);
        engine.Evaluate("Object.getOwnPropertyDescriptor(target, 'p').configurable").Should().Be(configurable);
        engine.Evaluate("target.p").Should().Be("v");
    }

    [Test]
    public void TheNamedCombinationsCoverTheEightFullCombinationsExactlyOnceEach()
    {
        // What makes the two additions worth having rather than merely available: with them the named set is
        // the complete lattice, so a host constructing a full data descriptor never has to spell bits by hand
        // again. Distinctness is the other half — a name duplicating another would mean one triple had two
        // spellings and another had none.
        AllCombinations.Should().HaveCount(8);

        AllCombinations
            .Select(c => (c.Writable, c.Enumerable, c.Configurable))
            .Distinct()
            .Should().HaveCount(8);

        AllCombinations.Select(c => c.Flags).Distinct().Should().HaveCount(8);
    }

    [Test]
    public void EveryNamedCombinationDecidesAllThreeAttributes()
    {
        // The property that separates these names from a partial spelling like `Configurable | Writable`: each
        // decides all three attributes, so defining with one never leaves an attribute to be filled in from
        // elsewhere. Reading the *Set half back is how a host tells the difference.
        foreach (var combination in AllCombinations)
        {
            var descriptor = new PropertyDescriptor(JsValue.Undefined, combination.Flags);

            descriptor.WritableSet.Should().BeTrue();
            descriptor.EnumerableSet.Should().BeTrue();
            descriptor.ConfigurableSet.Should().BeTrue();

            descriptor.Writable.Should().Be(combination.Writable);
            descriptor.Enumerable.Should().Be(combination.Enumerable);
            descriptor.Configurable.Should().Be(combination.Configurable);
        }

        // ...and the counter-example, so the assertion above is not vacuous.
        var partial = new PropertyDescriptor(JsValue.Undefined, PropertyFlag.Configurable | PropertyFlag.Writable);
        partial.EnumerableSet.Should().BeFalse();
    }

    [Test]
    public void TheTwoNewNamesEqualTheHandSpelledCompositesTheyReplace()
    {
        // The names replace spellings that already existed in the repository; equality here is what makes that
        // replacement mechanical rather than a behavioural change.
        PropertyFlag.NonWritable.Should()
            .Be(PropertyFlag.Configurable | PropertyFlag.Enumerable | PropertyFlag.WritableSet);
        PropertyFlag.OnlyConfigurable.Should()
            .Be(PropertyFlag.Configurable | PropertyFlag.EnumerableSet | PropertyFlag.WritableSet);
    }

    // ---- the two new names, behaviourally ----

    [Test]
    public void NonWritableIsReadOnlyDataThatStillEnumeratesAndCanBeRedefined()
    {
        var engine = new Engine();
        var target = new JsObject(engine);
        target.DefineOwnProperty(new JsString("p"), new PropertyDescriptor(new JsString("v"), PropertyFlag.NonWritable));
        engine.SetValue("target", target);

        // enumerable
        engine.Evaluate("Object.keys(target).join(',')").Should().Be("p");
        engine.Evaluate("JSON.stringify(target)").Should().Be("""{"p":"v"}""");

        // not writable: silently ignored in sloppy mode, a TypeError in strict
        engine.Execute("target.p = 'other';");
        engine.Evaluate("target.p").Should().Be("v");
        Invoking(() => engine.Execute("'use strict'; target.p = 'other';"))
            .Should().Throw<JavaScriptException>();

        // configurable: the owner can still redefine and delete it
        engine.Execute("Object.defineProperty(target, 'p', { value: 'redefined' });");
        engine.Evaluate("target.p").Should().Be("redefined");
        engine.Execute("delete target.p;");
        engine.Evaluate("'p' in target").Should().Be(false);
    }

    [Test]
    public void OnlyConfigurableIsInvisibleToEnumerationAndNeverAssignable()
    {
        var engine = new Engine();
        var target = new JsObject(engine);
        target.DefineOwnProperty(new JsString("p"), new PropertyDescriptor(new JsString("v"), PropertyFlag.OnlyConfigurable));
        engine.SetValue("target", target);

        // present, and readable
        engine.Evaluate("'p' in target").Should().Be(true);
        engine.Evaluate("target.p").Should().Be("v");

        // ...but not enumerable, so nothing that walks own enumerable keys sees it
        engine.Evaluate("Object.keys(target).length").Should().Be(0);
        engine.Evaluate("JSON.stringify(target)").Should().Be("{}");
        engine.Evaluate("var seen = []; for (var k in target) { seen.push(k); } seen.length;").Should().Be(0);
        engine.Evaluate("JSON.stringify(Object.assign({}, target))").Should().Be("{}");

        // ...and never assignable
        engine.Execute("target.p = 'other';");
        engine.Evaluate("target.p").Should().Be("v");
        Invoking(() => engine.Execute("'use strict'; target.p = 'other';"))
            .Should().Throw<JavaScriptException>();

        // configurable, so still redefinable and deletable
        engine.Execute("Object.defineProperty(target, 'p', { value: 'redefined', enumerable: true });");
        engine.Evaluate("Object.keys(target).join(',')").Should().Be("p");
        engine.Execute("delete target.p;");
        engine.Evaluate("'p' in target").Should().Be(false);
    }
}
