#nullable enable

using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins what every way of building a <see cref="JsRegExp"/> hands script: the matcher compiled for its own pattern
/// and flags, the source it reports, and a <c>lastIndex</c> of zero. The object is allocated first and initialized
/// after, as <c>RegExpAlloc</c> and <c>RegExpInitialize</c> specify, and nothing may be able to run script (or
/// read the matcher) in between.
/// https://tc39.es/ecma262/#sec-regexpalloc
/// https://tc39.es/ecma262/#sec-regexpinitialize
/// </summary>
public class RegExpConstructionPathTests
{
    // Every path a script can build a RegExp through. The expectation is "source|flags|exec result" against
    // 'xxABBBCaa', checked against Node.
    [TestCase("/a(b+)c/gi", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("new RegExp('a(b+)c', 'gi')", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("RegExp('a(b+)c', 'gi')", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("new RegExp(/a(b+)c/gi)", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("new RegExp(/a(b+)c/g, 'i')", "a(b+)c|i|[\"ABBBC\",\"BBB\"]")]
    [TestCase("new (class extends RegExp {})('a(b+)c', 'gi')", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("(function() { function F() {} F.prototype = RegExp.prototype; return Reflect.construct(RegExp, ['a(b+)c', 'gi'], F); })()", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("(function() { var r = /x/; r.compile('a(b+)c', 'gi'); return r; })()", "a(b+)c|gi|[\"ABBBC\",\"BBB\"]")]
    [TestCase("new RegExp('a/b')", "a\\/b||null")]
    [TestCase("new RegExp('a\\nb')", "a\\nb||null")]
    // The empty pattern, which the instance reports as (?:) whichever way it is spelled.
    [TestCase("new RegExp()", "(?:)||[\"\"]")]
    [TestCase("new RegExp('')", "(?:)||[\"\"]")]
    [TestCase("new RegExp(undefined, 'g')", "(?:)|g|[\"\"]")]
    [TestCase("new RegExp('', 'g')", "(?:)|g|[\"\"]")]
    [TestCase("RegExp('(?:)')", "(?:)||[\"\"]")]
    [TestCase("(function() { var r = /x/g; r.compile(); return r; })()", "(?:)||[\"\"]")]
    public void EveryScriptConstructionPathCarriesItsOwnPattern(string expression, string expected)
    {
        var engine = new Engine();
        var script = "var r = " + expression + "; r.source + '|' + r.flags + '|' + JSON.stringify(r.exec('xxABBBCaa'))";

        engine.Evaluate(script).AsString().Should().Be(expected);

        var regExp = engine.Evaluate("r").Should().BeOfType<JsRegExp>().Which;
        regExp.Matcher.Should().NotBeNull();
        regExp.HostRegex.Should().BeNull();
    }

    [Test]
    public void AnEmptyPatternMatchesTheEmptyStringAtZero()
    {
        var engine = new Engine();
        engine.Evaluate("new RegExp().exec('abc').index").AsNumber().Should().Be(0);
        engine.Evaluate("new RegExp('').test('')").AsBoolean().Should().BeTrue();
        engine.Evaluate("new RegExp(undefined, 'g').exec('abc')[0] === ''").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc'.split(new RegExp()).join('|')").AsString().Should().Be("a|b|c");
    }

    [Test]
    public void ALiteralEvaluatedRepeatedlyYieldsDistinctObjectsOfTheSamePattern()
    {
        var engine = new Engine();
        engine.Execute("function f() { return /ab+/g; }");

        engine.Evaluate("f() !== f()").AsBoolean().Should().BeTrue();
        engine.Evaluate("f().exec('xabbb')[0]").AsString().Should().Be("abbb");

        // compile re-initializes one instance; the matcher the literal's node memoizes belongs to the node.
        engine.Execute("var a = f(); a.compile('c+', 'i');");
        engine.Evaluate("a.source").AsString().Should().Be("c+");
        engine.Evaluate("f().source").AsString().Should().Be("ab+");
        engine.Evaluate("f().test('xabbb')").AsBoolean().Should().BeTrue();
    }

    [TestCase("(")]
    [TestCase("a**")]
    [TestCase("[b-a]")]
    [TestCase("(?<n>a)(?<n>b)")]
    public void AnInvalidPatternRaisesASyntaxErrorAndLeavesNothingBehind(string pattern)
    {
        var engine = new Engine();
        engine.SetValue("pattern", pattern);

        engine.Evaluate("try { new RegExp(pattern); 'no error' } catch (e) { e.constructor === SyntaxError }").AsBoolean().Should().BeTrue();

        // A later, valid construction is unaffected.
        engine.Evaluate("new RegExp('ok').test('ok')").AsBoolean().Should().BeTrue();
    }

    [TestCase("gg")]
    [TestCase("uv")]
    [TestCase("x")]
    public void InvalidFlagsRaiseASyntaxErrorAndLeaveNothingBehind(string flags)
    {
        var engine = new Engine();
        engine.SetValue("flags", flags);

        engine.Evaluate("try { new RegExp('a', flags); 'no error' } catch (e) { e.constructor === SyntaxError }").AsBoolean().Should().BeTrue();
        engine.Evaluate("new RegExp('ok', 'g').test('ok')").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ConvertingThePatternRunsScriptBeforeTheMatcherExists()
    {
        var engine = new Engine();

        // The pattern's toString runs after the object is allocated and before it is initialized. It builds and uses
        // a RegExp of its own, and the outer one must still come out as the pattern it returned.
        engine.Execute(@"
            var inner;
            var outer = new RegExp({ toString() { inner = new RegExp('in+', 'g'); inner.test('xinn'); return 'out+'; } }, 'g');
        ");

        engine.Evaluate("outer.source").AsString().Should().Be("out+");
        engine.Evaluate("outer.exec('xout')[0]").AsString().Should().Be("out");
        engine.Evaluate("inner.source").AsString().Should().Be("in+");
        engine.Evaluate("inner.lastIndex").AsNumber().Should().Be(4);
    }

    [Test]
    public void AThrowingPatternConversionAbandonsTheAllocation()
    {
        var engine = new Engine();
        engine.Evaluate("try { new RegExp({ toString() { throw new RangeError('boom'); } }); 'no error' } catch (e) { e.constructor === RangeError }")
            .AsBoolean().Should().BeTrue();
        engine.Evaluate("new RegExp('a+').exec('baa')[0]").AsString().Should().Be("aa");
    }

    [Test]
    public void ANewTargetsPrototypeLookupRunsScriptBeforeTheObjectIsAllocated()
    {
        var engine = new Engine();

        // GetPrototypeFromConstructor reads newTarget.prototype, which a Proxy can answer with script. The RegExp
        // that script builds must be whole, and so must the one being constructed around it.
        engine.Execute(@"
            var nested;
            var newTarget = new Proxy(function() {}, {
                get(target, key, receiver) {
                    if (key === 'prototype') {
                        nested = new RegExp('nes+t', 'y');
                        return RegExp.prototype;
                    }
                    return Reflect.get(target, key, receiver);
                }
            });
            var built = Reflect.construct(RegExp, ['bui+lt', 'g'], newTarget);
        ");

        engine.Evaluate("built.source").AsString().Should().Be("bui+lt");
        engine.Evaluate("built.exec('xbuiilt')[0]").AsString().Should().Be("buiilt");
        engine.Evaluate("nested.exec('nesst')[0]").AsString().Should().Be("nesst");
    }

    [Test]
    public void ASubclassInstanceIsInitializedThroughTheSuperCall()
    {
        var engine = new Engine();
        engine.Execute(@"
            class Tagged extends RegExp {
                constructor(pattern, flags) { super(pattern, flags); this.tag = 't'; }
            }
            var t = new Tagged('x(y)', 'g');
        ");

        engine.Evaluate("t.tag").AsString().Should().Be("t");
        engine.Evaluate("t.source + '|' + t.flags").AsString().Should().Be("x(y)|g");
        engine.Evaluate("t.exec('axy')[1]").AsString().Should().Be("y");
        engine.Evaluate("t instanceof Tagged && t instanceof RegExp").AsBoolean().Should().BeTrue();
    }

    // The String methods that build a RegExp from a string argument (RegExpCreate in the specification), and the
    // species construction @@split performs with the sticky flag added.
    [TestCase("'a.b'.match('.')[0]", "a")]
    [TestCase("'a1b2'.match('\\\\d')[0]", "1")]
    [TestCase("'ab12'.search('\\\\d')", "2")]
    [TestCase("'a1b2'.matchAll('\\\\d').next().value[0]", "1")]
    [TestCase("'a,b,c'.split(/,/).join('|')", "a|b|c")]
    [TestCase("'a,b,c'.split(new (class extends RegExp {})(',')).join('|')", "a|b|c")]
    [TestCase("'a1b2'.replace(/\\d/g, '#')", "a#b#")]
    [TestCase("'a1b2'.replaceAll(/\\d/g, '#')", "a#b#")]
    public void StringMethodsThatBuildARegExpGetAWorkingOne(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    [Test]
    public void ASpeciesConstructorMayHandSplitAnyInitializedRegExp()
    {
        var engine = new Engine();

        // @@split asks the species constructor for the splitter, and one that hands back an existing, initialized
        // RegExp is legal: the split must run on that instance's own matcher, not on one built for the receiver.
        engine.Execute(@"
            var shared = /;/y;
            class Odd extends RegExp {
                static get [Symbol.species]() { return function() { return shared; }; }
            }
        ");

        engine.Evaluate("'a;b;c'.split(new Odd('ignored')).join('|')").AsString().Should().Be("a|b|c");
    }

    // The host-supplied Regex path. It keeps .NET semantics and never owned an ECMAScript matcher: the property's
    // own contract is "null only for a host-supplied Regex".
    [Test]
    public void AHostSuppliedRegexCarriesNoEcmaScriptMatcher()
    {
        var engine = new Engine();
        var regExp = engine.Realm.Intrinsics.RegExp.Construct(new Regex("a+"));

        regExp.HostRegex.Should().NotBeNull();
        regExp.UsesDotNetEngine.Should().BeTrue();
        regExp.Matcher.Should().BeNull();
        regExp.Source.Should().Be("?[native regex]");
    }

    [Test]
    public void AHostSuppliedRegexStillMatchesThroughEveryLane()
    {
        var engine = new Engine();
        engine.SetValue("r", new Regex("a+"));
        engine.SetValue("g", new Regex("a+"));

        engine.Evaluate("r.test('baab')").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.exec('baab')[0]").AsString().Should().Be("aa");
        engine.Evaluate("'baab'.replace(r, 'x')").AsString().Should().Be("bxb");
        engine.Evaluate("'baab'.match(r)[0]").AsString().Should().Be("aa");
        engine.Evaluate("'baab'.search(r)").AsNumber().Should().Be(1);
        engine.Evaluate("'baab'.replace(g, m => m.length)").AsString().Should().Be("b2b");
    }

    [Test]
    public void AHostSuppliedRegexRecompiledByScriptBecomesAnEcmaScriptOne()
    {
        var engine = new Engine();
        engine.SetValue("r", new Regex("a+"));

        engine.Execute("r.compile('b(c)', 'g');");

        var regExp = (JsRegExp) engine.Evaluate("r");
        regExp.HostRegex.Should().BeNull();
        regExp.UsesDotNetEngine.Should().BeFalse();
        regExp.Matcher.Should().NotBeNull();
        engine.Evaluate("r.source + '|' + r.flags").AsString().Should().Be("b(c)|g");
        engine.Evaluate("r.exec('abcd')[1]").AsString().Should().Be("c");
    }

    // RegExpInitialize can throw, or run script, before it assigns anything to the object, so a compile that fails
    // has to leave a host Regex exactly as it was: still a .NET pattern, and still matching. Checked against Node
    // with a script RegExp, which keeps its pattern through the same failures.
    // https://tc39.es/ecma262/#sec-regexpinitialize
    [TestCase("r.compile('(')")]
    [TestCase("r.compile('b', 'gg')")]
    [TestCase("r.compile('b', 'x')")]
    [TestCase("r.compile({ toString() { throw 1; } })")]
    [TestCase("r.compile('b', { toString() { throw 1; } })")]
    public void AFailedCompileLeavesAHostSuppliedRegexMatching(string compile)
    {
        var engine = new Engine();
        engine.SetValue("r", new Regex("a+"));

        engine.Execute("try { " + compile + " } catch (e) { }");

        var regExp = (JsRegExp) engine.Evaluate("r");
        regExp.HostRegex.Should().NotBeNull();
        regExp.UsesDotNetEngine.Should().BeTrue();
        engine.Evaluate("r.source").AsString().Should().Be("?[native regex]");
        engine.Evaluate("r.test('baab')").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.exec('baab')[0]").AsString().Should().Be("aa");
        engine.Evaluate("'baab'.replace(r, 'x')").AsString().Should().Be("bxb");
        engine.Evaluate("'baab'.search(r)").AsNumber().Should().Be(1);
    }

    [TestCase("r.compile('(')")]
    [TestCase("r.compile('b', 'gg')")]
    [TestCase("r.compile({ toString() { throw 1; } })")]
    public void AFailedCompileLeavesAScriptRegExpAsItWas(string compile)
    {
        var engine = new Engine();
        engine.Execute("var r = /a+/; try { " + compile + " } catch (e) { }");

        engine.Evaluate("r.source + '|' + r.test('baab') + '|' + r.exec('baab')[0] + '|' + 'baab'.replace(r, 'x')")
            .AsString().Should().Be("a+|true|aa|bxb");
    }

    [Test]
    public void ACompileWhosePatternConversionUsesTheHostSuppliedRegexSeesItIntact()
    {
        var engine = new Engine();
        engine.SetValue("r", new Regex("a+"));

        // The conversion runs before RegExpInitialize assigns anything, so the object is still the host's Regex.
        engine.Execute("var seen; r.compile({ toString() { seen = r.test('baab') + '|' + r.source; return 'b'; } });");

        engine.Evaluate("seen").AsString().Should().Be("true|?[native regex]");
        var regExp = (JsRegExp) engine.Evaluate("r");
        regExp.HostRegex.Should().BeNull();
        regExp.Matcher.Should().NotBeNull();
        engine.Evaluate("r.source").AsString().Should().Be("b");
        engine.Evaluate("r.test('baab')").AsBoolean().Should().BeTrue();
        engine.Evaluate("r.exec('xaa')").IsNull().Should().BeTrue();
    }

    [Test]
    public void AHostRegexReadAsEcmaScriptCarriesAMatcherForItsOwnPattern()
    {
        var engine = new Engine(o => o.Interop.RegexConversion = RegexConversionMode.EcmaScript);
        var regExp = engine.Realm.Intrinsics.RegExp.Construct(new Regex("a(b+)", RegexOptions.IgnoreCase));

        regExp.HostRegex.Should().BeNull();
        regExp.Matcher.Should().NotBeNull();
        regExp.Source.Should().Be("a(b+)");
        regExp.Flags.Should().Be("i");

        engine.SetValue("r", regExp);
        engine.Evaluate("r.exec('xABB')[1]").AsString().Should().Be("BB");
    }

    [Test]
    public void AHostRegexReadAsEcmaScriptWithAnEmptyPatternReportsTheEmptyPattern()
    {
        var engine = new Engine(o => o.Interop.RegexConversion = RegexConversionMode.EcmaScript);
        var regExp = engine.Realm.Intrinsics.RegExp.Construct(new Regex(""));

        regExp.Matcher.Should().NotBeNull();
        regExp.Source.Should().Be("(?:)");
        regExp.Flags.Should().Be("");
    }

    // The public constructor is documented (migrating-to-v5) as the empty pattern; it must go on being usable as one.
    [Test]
    public void ThePublicConstructorIsTheEmptyPattern()
    {
        var engine = new Engine();
        var regExp = new JsRegExp(engine);

        regExp.Source.Should().Be("(?:)");
        regExp.Matcher.Should().NotBeNull();
        regExp.HostRegex.Should().BeNull();
        regExp.UsesDotNetEngine.Should().BeFalse();
        regExp.Matcher!.CaptureCount.Should().Be(1);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void StructuredCloneCarriesAScriptMatcher()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));
        engine.Execute("var clone = structuredClone(/a(b+)c/gi);");

        var clone = (JsRegExp) engine.Evaluate("clone");
        clone.Matcher.Should().NotBeNull();
        clone.HostRegex.Should().BeNull();
        engine.Evaluate("clone.exec('xxABBBC')[1]").AsString().Should().Be("BBB");
    }

    [Test]
    public void StructuredCloneKeepsAHostRegexOnDotNet()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.StructuredClone));
        engine.SetValue("r", new Regex("a+"));
        engine.Execute("var clone = structuredClone(r);");

        var clone = (JsRegExp) engine.Evaluate("clone");
        clone.HostRegex.Should().NotBeNull();
        clone.UsesDotNetEngine.Should().BeTrue();
        engine.Evaluate("clone.test('baab')").AsBoolean().Should().BeTrue();
        engine.Evaluate("clone.source").AsString().Should().Be("?[native regex]");
    }
#endif
}
