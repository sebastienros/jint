#nullable enable

using System.Text.RegularExpressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// The legacy <c>RegExp</c> static properties - <c>$1</c>..<c>$9</c>, <c>lastMatch</c>, <c>lastParen</c>,
/// <c>leftContext</c>, <c>rightContext</c> and <c>input</c> with their aliases - from the RegExp Legacy Features
/// proposal (https://github.com/tc39/proposal-regexp-legacy-features), not ECMA-262. Every successful match updates
/// them (https://tc39.es/ecma262/#sec-regexpbuiltinexec calls the proposal's UpdateLegacyRegExpStaticProperties);
/// a failed one leaves them alone. Each value is cut out of the subject when it is read, from the capture spans the
/// last match recorded, so these pin that a read sees the last match and nothing older: a later exec with fewer
/// groups, a group that did not take part, a value read before the next exec, and an <c>input</c> assigned in between.
/// The expected strings are what Node prints for the same script, apart from the receiver and descriptor checks,
/// which are the proposal's (V8 has no receiver check and gives every accessor a setter).
/// <para>
/// Not under test: <c>test()</c>, a global <c>match</c>, a plain-string <c>replace</c> and <c>split</c>, which do not
/// update the statics yet (J3 in docs/design/regexp-gregsex-migration.md), and <c>lastParen</c> after a match whose
/// last group took no part or that has more than nine groups, where V8 reports that last group (the empty string if
/// it took no part) and Jint the last of the first nine that did take part.
/// </para>
/// </summary>
public class RegExpLegacyStaticsTests
{
    private const string Preamble =
        "var all = () => JSON.stringify([RegExp.$1, RegExp.$2, RegExp.$3, RegExp.$4, RegExp.$5, RegExp.$6, RegExp.$7, RegExp.$8, RegExp.$9, RegExp.lastMatch, RegExp.lastParen, RegExp.leftContext, RegExp.rightContext, RegExp.input]); "
        + "var core = () => JSON.stringify([RegExp.$1, RegExp.$2, RegExp.$3, RegExp.$4, RegExp.$5, RegExp.$6, RegExp.$7, RegExp.$8, RegExp.$9, RegExp.lastMatch, RegExp.leftContext, RegExp.rightContext, RegExp.input]); ";

    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    // one exec
    [TestCase(Preamble + "/(a)(b)?(c)/.exec('xxabcyy'); all()", "[\"a\",\"b\",\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"abc\",\"c\",\"xx\",\"yy\",\"xxabcyy\"]")]
    // a sequence of execs on different strings
    [TestCase(Preamble + "/(a)(b)?(c)/.exec('xxabcyy'); var s1 = all(); /(\\d+)-(\\d+)/.exec('tel 12-345 end'); var s2 = all(); /(?:)/.exec('abc'); /(q)/.exec('zq!'); JSON.stringify([s1, s2, all()])", "[\"[\\\"a\\\",\\\"b\\\",\\\"c\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"abc\\\",\\\"c\\\",\\\"xx\\\",\\\"yy\\\",\\\"xxabcyy\\\"]\",\"[\\\"12\\\",\\\"345\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"12-345\\\",\\\"345\\\",\\\"tel \\\",\\\" end\\\",\\\"tel 12-345 end\\\"]\",\"[\\\"q\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"q\\\",\\\"q\\\",\\\"z\\\",\\\"!\\\",\\\"zq!\\\"]\"]")]
    // fewer groups after more
    [TestCase(Preamble + "/(a)(b)(c)/.exec('abc'); var s1 = all(); /(x)/.exec('x'); JSON.stringify([s1, all()])", "[\"[\\\"a\\\",\\\"b\\\",\\\"c\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"abc\\\",\\\"c\\\",\\\"\\\",\\\"\\\",\\\"abc\\\"]\",\"[\\\"x\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"x\\\",\\\"x\\\",\\\"\\\",\\\"\\\",\\\"x\\\"]\"]")]
    // no groups after groups
    [TestCase(Preamble + "/(a)(b)(c)/.exec('abc'); /b/.exec('abc'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"\",\"a\",\"c\",\"abc\"]")]
    // a read between two execs
    [TestCase(Preamble + "/(a)(b)/.exec('ab'); var r1 = [RegExp.$1, RegExp.$2, RegExp.lastParen, RegExp.lastMatch]; /(c)(d)/.exec('xcd'); JSON.stringify([r1, RegExp.$1, RegExp.$2, RegExp.lastParen, RegExp.lastMatch, RegExp.leftContext])", "[[\"a\",\"b\",\"b\",\"ab\"],\"c\",\"d\",\"d\",\"cd\",\"x\"]")]
    // a read of one group only
    [TestCase(Preamble + "/(a)(b)/.exec('ab'); var r1 = RegExp.$2; /(c)(d)/.exec('cd'); JSON.stringify([r1, RegExp.$2, RegExp.$1])", "[\"b\",\"d\",\"c\"]")]
    // groups that did not participate
    [TestCase(Preamble + "/(a)|(b)/.exec('a'); var s1 = core(); /(a)|(b)/.exec('b'); JSON.stringify([s1, core()])", "[\"[\\\"a\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"a\\\",\\\"\\\",\\\"\\\",\\\"a\\\"]\",\"[\\\"\\\",\\\"b\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"b\\\",\\\"\\\",\\\"\\\",\\\"b\\\"]\"]")]
    // a group that participated empty
    [TestCase(Preamble + "/(a*)(b*)/.exec('c'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"c\",\"c\"]")]
    // a failed exec leaves the previous values
    [TestCase(Preamble + "/(z)(y)/.exec('xzy!'); var s1 = all(); var r = /(q)/.exec('abc'); JSON.stringify([s1, all() === s1, r])", "[\"[\\\"z\\\",\\\"y\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"zy\\\",\\\"y\\\",\\\"x\\\",\\\"!\\\",\\\"xzy!\\\"]\",true,null]")]
    // a failed test leaves the previous values
    [TestCase(Preamble + "/(z)/.exec('zz'); var s1 = all(); var r = /(q)/.test('abc'); JSON.stringify([all() === s1, r])", "[true,false]")]
    // a failed match leaves the previous values
    [TestCase(Preamble + "/(z)/.exec('zz'); var s1 = all(); var r = 'abc'.match(/(q)/); var g = 'abc'.match(/(q)/g); JSON.stringify([all() === s1, r, g])", "[true,null,null]")]
    // a failed search leaves the previous values
    [TestCase(Preamble + "/(z)/.exec('zz'); var s1 = all(); var r = 'abc'.search(/(q)/); JSON.stringify([all() === s1, r])", "[true,-1]")]
    // a failed sticky exec leaves the previous values
    [TestCase(Preamble + "/(z)/.exec('zz'); var s1 = all(); var re = /(q)/y; re.lastIndex = 0; var r = re.exec('aqc'); JSON.stringify([all() === s1, r, re.lastIndex])", "[true,null,0]")]
    // input assignment
    [TestCase(Preamble + "/(z)/.exec('zz'); RegExp.input = 'x'; core()", "[\"z\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"z\",\"\",\"z\",\"x\"]")]
    // input assignment through $_
    [TestCase(Preamble + "/(z)(y)/.exec('zy'); RegExp.$_ = 5; JSON.stringify([RegExp.input, RegExp.$_, typeof RegExp.input, RegExp.$1, RegExp.$2, RegExp.lastMatch])", "[\"5\",\"5\",\"string\",\"z\",\"y\",\"zy\"]")]
    // input is replaced by the next exec
    [TestCase(Preamble + "/(z)/.exec('zz'); RegExp.input = 'x'; /(y)/.exec('yyy'); core()", "[\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"y\",\"\",\"yy\",\"yyy\"]")]
    // input assignment before any exec reads back
    [TestCase(Preamble + "RegExp.input = 'seed'; RegExp.input", "seed")]
    // more than nine groups
    [TestCase(Preamble + "/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)/.exec('zabcdefghijkw'); core()", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"abcdefghijk\",\"z\",\"w\",\"zabcdefghijkw\"]")]
    // exactly nine groups
    [TestCase(Preamble + "/(a)(b)(c)(d)(e)(f)(g)(h)(i)/.exec('zabcdefghiw'); all()", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"abcdefghi\",\"i\",\"z\",\"w\",\"zabcdefghiw\"]")]
    // ten groups with a non-participating tenth
    [TestCase(Preamble + "/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)?/.exec('zabcdefghiw'); core()", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"abcdefghi\",\"z\",\"w\",\"zabcdefghiw\"]")]
    // named groups and indices
    [TestCase(Preamble + "/(?<n>a)(?<m>b)?/d.exec('xa'); core()", "[\"a\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"x\",\"\",\"xa\"]")]
    // the aliases
    [TestCase(Preamble + "/(a)(b)/.exec('xaby'); JSON.stringify([RegExp['$&'] === RegExp.lastMatch, RegExp['$+'] === RegExp.lastParen, RegExp['$`'] === RegExp.leftContext, RegExp[\"$'\"] === RegExp.rightContext, RegExp.$_ === RegExp.input, RegExp['$&'], RegExp['$+'], RegExp['$`'], RegExp[\"$'\"], RegExp.$_])", "[true,true,true,true,true,\"ab\",\"b\",\"x\",\"y\",\"xaby\"]")]
    // empty subject
    [TestCase(Preamble + "/(a*)/.exec(''); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"]")]
    // a capture spanning most of the subject
    [TestCase(Preamble + "var s = 'x' + 'a'.repeat(3000) + 'y'; /x(a+)y/.exec(s); JSON.stringify([RegExp.$1.length, RegExp.lastMatch.length, RegExp.leftContext, RegExp.rightContext, RegExp.input.length])", "[3000,3002,\"\",\"\",3002]")]
    // a long capture read after a shorter subject
    [TestCase(Preamble + "/(a{600})/.exec('a'.repeat(700)); var n = RegExp.$1.length; /(b)/.exec('b'); JSON.stringify([n, RegExp.$1])", "[600,\"b\"]")]
    // astral captures
    [TestCase(Preamble + "/(\\u{1F600})(.)/u.exec('x\\u{1F600}yz'); JSON.stringify([RegExp.$1, RegExp.$2, RegExp.leftContext, RegExp.rightContext, RegExp.$1.length])", "[\"😀\",\"y\",\"x\",\"z\",2]")]
    // global exec
    [TestCase(Preamble + "var re = /(\\d)/g; re.exec('a1b2'); var s1 = all(); re.exec('a1b2'); JSON.stringify([s1, all()])", "[\"[\\\"1\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"1\\\",\\\"1\\\",\\\"a\\\",\\\"b2\\\",\\\"a1b2\\\"]\",\"[\\\"2\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"2\\\",\\\"2\\\",\\\"a1b\\\",\\\"\\\",\\\"a1b2\\\"]\"]")]
    // sticky exec
    [TestCase(Preamble + "var re = /(b)/y; re.lastIndex = 1; re.exec('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // match
    [TestCase(Preamble + "'xab'.match(/(a)(b)/); all()", "[\"a\",\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"ab\",\"b\",\"x\",\"\",\"xab\"]")]
    // matchAll
    [TestCase(Preamble + "var r = [...'a1b2'.matchAll(/([a-z])(\\d)/g)]; all()", "[\"b\",\"2\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b2\",\"2\",\"a1\",\"\",\"a1b2\"]")]
    // search
    [TestCase(Preamble + "'xaby'.search(/(a)(b)/); all()", "[\"a\",\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"ab\",\"b\",\"x\",\"y\",\"xaby\"]")]
    // global replace with a function
    [TestCase(Preamble + "var seen = []; 'a1b2'.replace(/([a-z])(\\d)/g, function (m, a, b) { seen.push(RegExp.$1 + RegExp.$2); return m; }); JSON.stringify([seen, all()])", "[[\"b2\",\"b2\"],\"[\\\"b\\\",\\\"2\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"b2\\\",\\\"2\\\",\\\"a1\\\",\\\"\\\",\\\"a1b2\\\"]\"]")]
    // a statics getter inside a replace callback
    [TestCase(Preamble + "'a1'.replace(/([a-z])(\\d)/, function () { return RegExp.$2 + RegExp.$1; })", "1a")]
    // a read does not change what the next read returns
    [TestCase(Preamble + "/(a)(b)/.exec('xab'); var a = RegExp.$1 + RegExp.$1 + RegExp.$2; var b = RegExp.lastMatch + RegExp.lastMatch; JSON.stringify([a, b, RegExp.$1, RegExp.lastMatch])", "[\"aab\",\"abab\",\"a\",\"ab\"]")]
    // a failed exec before the first read
    [TestCase(Preamble + "/(z)(y)/.exec('xzy!'); /(q)/.exec('abc'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // a failed exec between two reads of different groups
    [TestCase(Preamble + "/(a)(b)(c)/.exec('abc'); var r1 = RegExp.$3; /(q)(r)/.exec('abc'); JSON.stringify([r1, RegExp.$1, RegExp.$2, RegExp.$3, RegExp.lastMatch])", "[\"c\",\"a\",\"b\",\"c\",\"abc\"]")]
    // a match with a lone zero length capture
    [TestCase(Preamble + "/(a)()(c)/.exec('xac'); all()", "[\"a\",\"\",\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"ac\",\"c\",\"x\",\"\",\"xac\"]")]
    // read nothing then exec then read
    [TestCase(Preamble + "/(a)/.exec('a'); /(b)/.exec('b'); /(c)/.exec('xc'); all()", "[\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"c\",\"c\",\"x\",\"\",\"xc\"]")]
    public void TheStaticsReadWhatTheLastMatchLeft(string script, string expected)
    {
        Run(script).Should().Be(expected);
    }

    // The accessor receiver check: GetLegacyRegExpStaticProperty throws unless the receiver is %RegExp% itself, so a
    // subclass or a plain object reads nothing. Another realm's constructor is covered by the test262
    // annexB/built-ins/RegExp/legacy-accessors/*/this-cross-realm-constructor.js cases.
    [Test]
    public void AGetterThrowsForAnyReceiverButTheConstructor()
    {
        Run(Preamble + "/(z)/.exec('z'); var d = Object.getOwnPropertyDescriptor(RegExp, '$1'); var out = []; for (var t of [{}, undefined, RegExp.prototype, function () {}]) { try { d.get.call(t); out.push('ok'); } catch (e) { out.push(e.constructor.name); } } out.push(d.get.call(RegExp)); out.join()")
            .Should().Be("TypeError,TypeError,TypeError,TypeError,z");
    }

    [Test]
    public void ASubclassReadsNothing()
    {
        Run("class M extends RegExp {} /(z)/.exec('z'); var out = []; for (var k of ['$1', '$9', 'lastMatch', '$&', 'lastParen', '$+', 'leftContext', '$`', 'rightContext', \"$'\", 'input', '$_']) { try { M[k]; out.push('ok'); } catch (e) { out.push(e.constructor.name); } } out.join()")
            .Should().Be("TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,TypeError");
    }

    [Test]
    public void TheSetterRejectsAnotherReceiverAndAcceptsTheConstructor()
    {
        Run("var d = Object.getOwnPropertyDescriptor(RegExp, 'input'); var out = []; try { d.set.call({}, 'x'); out.push('ok'); } catch (e) { out.push(e.constructor.name); } d.set.call(RegExp, 'y'); out.push(RegExp.input); out.join()")
            .Should().Be("TypeError,y");
    }

    [Test]
    public void OnlyInputAndItsAliasAreWritable()
    {
        Run("var out = []; for (var k of ['$1', '$9', 'lastMatch', '$&', 'lastParen', '$+', 'leftContext', '$`', 'rightContext', \"$'\", 'input', '$_']) { var d = Object.getOwnPropertyDescriptor(RegExp, k); out.push(k + ':' + typeof d.get + ':' + typeof d.set + ':' + d.enumerable + ':' + d.configurable); } out.join()")
            .Should().Be("$1:function:undefined:false:true,$9:function:undefined:false:true,lastMatch:function:undefined:false:true,$&:function:undefined:false:true,lastParen:function:undefined:false:true,$+:function:undefined:false:true,leftContext:function:undefined:false:true,$`:function:undefined:false:true,rightContext:function:undefined:false:true,$':function:undefined:false:true,input:function:function:false:true,$_:function:function:false:true");
    }

    // A statics write to a read-only alias is ignored in sloppy mode and throws in strict mode (the setter is undefined).
    [Test]
    public void AssigningAReadOnlyStaticDoesNothingOrThrowsInStrictMode()
    {
        Run("/(z)/.exec('z'); RegExp.$1 = 'q'; RegExp.lastMatch = 'q'; RegExp.$1 + RegExp.lastMatch")
            .Should().Be("zz");
        Run("'use strict'; /(z)/.exec('z'); try { RegExp.$1 = 'q'; 'no throw' } catch (e) { e.constructor.name }")
            .Should().Be("TypeError");
    }

    // A fresh engine starts empty: no previous evaluation's text, and nothing for a group that never took part.
    [Test]
    public void AFreshEngineReadsEmptyStrings()
    {
        Run(Preamble + "all()").Should().Be("[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"]");
    }

    // Two engines never share the statics.
    [Test]
    public void EnginesKeepTheirOwnStatics()
    {
        var a = new Engine();
        var b = new Engine();
        a.Evaluate("/(a)/.exec('xa')");
        b.Evaluate("/(b)/.exec('xb')");
        a.Evaluate("RegExp.$1 + RegExp.leftContext").ToString().Should().Be("ax");
        b.Evaluate("RegExp.$1 + RegExp.leftContext").ToString().Should().Be("bx");
    }

    // The .NET lane records the same spans for a Regex the host handed over.
    [Test]
    public void AHostSuppliedRegexUpdatesTheStaticsToo()
    {
        var engine = new Engine();
        engine.SetValue("host", new Regex("([a-z])(b)?(c)"));

        engine.Evaluate("host.exec('xac'); JSON.stringify([RegExp.$1, RegExp.$2, RegExp.$3, RegExp.lastMatch, RegExp.lastParen, RegExp.leftContext, RegExp.rightContext, RegExp.input])").ToString()
            .Should().Be("""["a","","c","ac","c","x","","xac"]""");
        engine.Evaluate("host.exec('xabc'); JSON.stringify([RegExp.$1, RegExp.$2, RegExp.$3, RegExp.lastMatch, RegExp.leftContext])").ToString()
            .Should().Be("""["a","b","c","abc","x"]""");
    }
}
