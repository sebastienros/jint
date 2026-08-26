namespace Jint.Tests.Runtime;

/// <summary>
/// A built-in must attribute what it produces to its own realm, not to whichever realm happens to be
/// running when it is called. Two shapes are covered here: an error a cross-realm built-in raises has to
/// be an instance of <em>that</em> realm's error constructor, and a result array a cross-realm built-in
/// creates has to inherit from <em>that</em> realm's <c>Array.prototype</c>.
/// <para>
/// The second realm is built through <c>$262.createRealm()</c>. That object is internal and this project
/// has <c>InternalsVisibleTo</c>, which is deliberate: a <c>ShadowRealm</c> wraps every function and value
/// crossing its boundary, so the constructor identities these tests compare would never be observable
/// through one. This is the same realm plumbing the test262 staging suite exercises.
/// </para>
/// </summary>
public class CrossRealmAttributionTests
{
    /// <summary>
    /// <c>classify</c> answers "&lt;realm&gt; &lt;ErrorName&gt;" so a failure names the realm the error
    /// actually came from instead of merely reporting <c>false</c>. <c>realmOf</c> does the same for a value.
    /// </summary>
    private const string Preamble = """
        var otherGlobal = $262.createRealm().global;

        function realmOf(v) {
            if (v instanceof otherGlobal.Array) return 'other';
            if (v instanceof Array) return 'main';
            return 'neither';
        }

        function classify(fn) {
            try {
                fn();
                return 'no throw';
            } catch (e) {
                var realm = 'neither';
                if (e instanceof otherGlobal.Error) realm = 'other';
                else if (e instanceof Error) realm = 'main';
                return realm + ' ' + (e && e.constructor ? e.constructor.name : '?');
            }
        }

        // 2**32 elements is past MaxArrayLength, so every change-array-by-copy method rejects it.
        var tooLong = {
            get "0"() { throw new Error("must not be read"); },
            length: 2 ** 32
        };
        """;

    private static string Evaluate(string source)
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        engine.Execute(Preamble);
        return engine.Evaluate(source).AsString();
    }

    // Array.prototype.{toSorted,toReversed,with,toSpliced} all reach ValidateArrayLength.
    [TestCase("otherGlobal.Array.prototype.toSorted.call(tooLong)", "other RangeError")]
    [TestCase("otherGlobal.Array.prototype.toReversed.call(tooLong)", "other RangeError")]
    [TestCase("otherGlobal.Array.prototype.with.call(tooLong, 0, 0)", "other RangeError")]
    [TestCase("otherGlobal.Array.prototype.toSpliced.call(tooLong, 0, 0)", "other RangeError")]
    // Controls: these two already used the callee realm before the fix.
    [TestCase("otherGlobal.Array.prototype.with.call([0, 1, 2], 3, 7)", "other RangeError")]
    [TestCase("otherGlobal.Array.prototype.toSorted.call([], 5)", "other TypeError")]
    public void ArrayChangeByCopyErrorsComeFromTheCalleeRealm(string call, string expected)
    {
        Evaluate("classify(() => " + call + ")").Should().Be(expected);
    }

    [TestCase("otherGlobal.Array.prototype.with.call([1, 2, 3], 1, 3)")]
    [TestCase("otherGlobal.Array.prototype.toSpliced.call([1, 2, 3], 0, 1, 4, 5)")]
    [TestCase("otherGlobal.Array.prototype.toReversed.call([1, 2, 3])")]
    [TestCase("otherGlobal.Array.prototype.toSorted.call([1, 2, 3], (x, y) => y > x)")]
    // The empty-source early returns build a result array too.
    [TestCase("otherGlobal.Array.prototype.toReversed.call([])")]
    [TestCase("otherGlobal.Array.prototype.toSorted.call([])")]
    public void ArrayChangeByCopyResultsUseTheCalleeRealmPrototype(string call)
    {
        Evaluate("realmOf(" + call + ")").Should().Be("other");
    }

    // `this` is the other realm's Array constructor, so the result is constructed through it.
    [TestCase("otherGlobal.Array.from([1, 2, 3])", "other")]
    // `this` is undefined, so Array.from falls back to ArrayCreate in the *callee's* realm.
    [TestCase("(0, otherGlobal.Array.from)([1, 2, 3])", "other")]
    // ...and the reverse still holds: an explicit constructor wins over the callee's realm.
    [TestCase("otherGlobal.Array.from.call(Array, [1, 2, 3])", "main")]
    [TestCase("Array.from.call(otherGlobal.Array, [1, 2, 3])", "other")]
    public void ArrayFromUsesTheCalleeRealm(string call, string expected)
    {
        Evaluate("realmOf(" + call + ")").Should().Be(expected);
    }

    [TestCase("map")]
    [TestCase("filter")]
    [TestCase("flatMap")]
    [TestCase("reduce")]
    [TestCase("forEach")]
    [TestCase("some")]
    [TestCase("every")]
    [TestCase("find")]
    public void IteratorHelperArgumentTypeErrorsComeFromTheCalleeRealm(string method)
    {
        // Every one of these throws because the callback argument is missing, i.e. not callable.
        Evaluate("classify(otherGlobal.Iterator.prototype." + method + ".bind([].values()))")
            .Should().Be("other TypeError");
    }

    [Test]
    public void IteratorHelperArgumentTypeErrorsStillComeFromTheMainRealm()
    {
        Evaluate("classify(() => [].values().every())").Should().Be("main TypeError");
    }

    [Test]
    public void IteratorFromComparesAgainstTheCalleeRealmIterator()
    {
        // The main realm's array iterator is an instance of the main %Iterator%, so the main
        // Iterator.from hands it straight back; the other realm's must wrap it instead.
        var engine = new Engine();
        Test262Object.Install(engine);
        engine.Execute(Preamble);
        engine.Execute("var iter = [1, 2, 3].values();");

        engine.Evaluate("Iterator.from(iter) === iter").AsBoolean()
            .Should().BeTrue("the main realm's Iterator.from recognises a main-realm iterator");
        engine.Evaluate("otherGlobal.Iterator.from(iter) === iter").AsBoolean()
            .Should().BeFalse("the other realm's %Iterator% is a different intrinsic");
    }

    [Test]
    public void IteratorFromWrapperUsesTheCalleeRealmPrototype()
    {
        // %WrapForValidIteratorPrototype%'s own [[Prototype]] is that realm's %Iterator.prototype%,
        // which is the only handle a script has on the wrapper prototype's realm.
        Evaluate("""
            (function () {
                var wrapper = otherGlobal.Iterator.from([1, 2, 3].values());
                var wrapProto = Object.getPrototypeOf(wrapper);
                if (Object.getPrototypeOf(wrapProto) === otherGlobal.Iterator.prototype) return 'other';
                if (Object.getPrototypeOf(wrapProto) === Iterator.prototype) return 'main';
                return 'neither';
            })()
            """).Should().Be("other");
    }

    [Test]
    public void IteratorToArrayCreatesInTheCalleeRealm()
    {
        Evaluate("realmOf([1, 2, 3].values().toArray())").Should().Be("main");
        Evaluate("realmOf(otherGlobal.Iterator.prototype.toArray.call([1, 2, 3].values()))").Should().Be("other");
    }

    [TestCase("otherGlobal.Function('\\'use strict\\'; var yield = 3;')", "other SyntaxError")]
    [TestCase("otherGlobal.Function('return }')", "other SyntaxError")]
    [TestCase("Function('return }')", "main SyntaxError")]
    public void DynamicFunctionSyntaxErrorsComeFromTheCalleeRealm(string call, string expected)
    {
        Evaluate("classify(() => " + call + ")").Should().Be(expected);
    }

    [Test]
    public void DynamicFunctionAcceptsYieldAsIdentifierInSloppyMode()
    {
        Evaluate("classify(() => otherGlobal.Function('var yield = 3;'))").Should().Be("no throw");
    }

    // ToString(Symbol) throws a realm-less TypeError that the callee has to attribute to itself.
    [TestCase("otherGlobal.JSON.rawJSON(Symbol('123'))", "other TypeError")]
    [TestCase("JSON.rawJSON(Symbol('123'))", "main TypeError")]
    // Controls: rawJSON's own SyntaxErrors already used the callee realm.
    [TestCase("otherGlobal.JSON.rawJSON('')", "other SyntaxError")]
    [TestCase("otherGlobal.JSON.rawJSON('\\t123')", "other SyntaxError")]
    public void JsonRawJsonErrorsComeFromTheCalleeRealm(string call, string expected)
    {
        Evaluate("classify(() => " + call + ")").Should().Be(expected);
    }
}
