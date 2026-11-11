#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Every lane that matches a regular expression updates the legacy <c>RegExp</c> statics with the last successful match
/// of the call, and a call that matches nothing leaves them alone. The RegExp Legacy Features proposal
/// (https://github.com/tc39/proposal-regexp-legacy-features) hooks the update into RegExpBuiltinExec
/// (https://tc39.es/ecma262/#sec-regexpbuiltinexec), which <c>test</c>, a global <c>match</c>, a <c>replace</c> and a
/// <c>split</c> all reach through RegExpExec, once per match, so the last one wins; for <c>split</c> that is the last
/// exec of the splitter (a sticky copy of the regexp) that found a match, including one it skipped for being empty at
/// the end of the last piece. The lanes that skip RegExpExec - because nothing can tell the difference - have to leave
/// the statics as that sequence of execs would. Each case starts from a match of <c>/(z)(y)/</c>, so "leaves the
/// statics" and "sets them" cannot be mistaken for each other. The expected strings are what Node prints for the same
/// script.
/// </summary>
public class RegExpLegacyStaticsLaneTests
{
    private const string Preamble =
        "var all = () => JSON.stringify([RegExp.$1, RegExp.$2, RegExp.$3, RegExp.$4, RegExp.$5, RegExp.$6, RegExp.$7, RegExp.$8, RegExp.$9, RegExp.lastMatch, RegExp.lastParen, RegExp.leftContext, RegExp.rightContext, RegExp.input]); ";

    private const string Seed = "/(z)(y)/.exec('xzy!'); ";

    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    // split
    [TestCase(Preamble + Seed + "'a-b-c'.split(/-/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"a-b\",\"c\",\"a-b-c\"]")]
    // split with a limit that stops at the first piece
    [TestCase(Preamble + Seed + "'a-b-c'.split(/-/, 1); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"a\",\"b-c\",\"a-b-c\"]")]
    // split with a limit that stops at the second piece
    [TestCase(Preamble + Seed + "'a-b-c'.split(/-/, 2); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"a-b\",\"c\",\"a-b-c\"]")]
    // split with a limit larger than the pieces
    [TestCase(Preamble + Seed + "'a-b-c'.split(/-/, 10); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"a-b\",\"c\",\"a-b-c\"]")]
    // split with a limit of zero leaves the statics
    [TestCase(Preamble + Seed + "'a-b-c'.split(/-/, 0); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // split with a capture
    [TestCase(Preamble + Seed + "'a-b-c'.split(/(-)/); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"a-b\",\"c\",\"a-b-c\"]")]
    // split with a capture that took no part
    [TestCase(Preamble + Seed + "'a-b-c'.split(/(-)(x)?/); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"a-b\",\"c\",\"a-b-c\"]")]
    // split with a capture and a limit
    [TestCase(Preamble + Seed + "'a-b-c'.split(/(-)/, 2); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"a\",\"b-c\",\"a-b-c\"]")]
    // split with no separator match leaves the statics
    [TestCase(Preamble + Seed + "'abc'.split(/x/); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // split with no separator match and a capture leaves the statics
    [TestCase(Preamble + Seed + "'abc'.split(/(x)/); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // split of an empty subject that does not match leaves the statics
    [TestCase(Preamble + Seed + "''.split(/x/); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // split of an empty subject that matches
    [TestCase(Preamble + Seed + "''.split(/x*/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"]")]
    // split of an empty subject that matches with a capture
    [TestCase(Preamble + Seed + "''.split(/(x)*/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"]")]
    // split on the empty pattern
    [TestCase(Preamble + Seed + "'abc'.split(/(?:)/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"ab\",\"c\",\"abc\"]")]
    // split on the empty pattern with a limit of two
    [TestCase(Preamble + Seed + "'abc'.split(/(?:)/, 2); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"ab\",\"c\",\"abc\"]")]
    // split on the empty pattern with a limit of one
    [TestCase(Preamble + Seed + "'abc'.split(/(?:)/, 1); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"bc\",\"abc\"]")]
    // split of one character on the empty pattern
    [TestCase(Preamble + Seed + "'a'.split(/(?:)/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"a\"]")]
    // split on the empty pattern under u
    [TestCase(Preamble + Seed + "'a\\u{1F600}b'.split(/(?:)/u); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a😀\",\"b\",\"a😀b\"]")]
    // split on a pattern that matches empty strings
    [TestCase(Preamble + Seed + "'abc'.split(/b*/); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"ab\",\"c\",\"abc\"]")]
    // split on a sticky pattern
    [TestCase(Preamble + Seed + "'abc'.split(/(b)/y); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // split on a case-insensitive pattern
    [TestCase(Preamble + Seed + "'aXbxc'.split(/x/i); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"x\",\"\",\"aXb\",\"c\",\"aXbxc\"]")]
    // split on a string leaves the statics
    [TestCase(Preamble + Seed + "'a-b-c'.split('-'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // split with more than nine captures
    [TestCase(Preamble + Seed + "'a-b'.split(/(-)(1)?(2)?(3)?(4)?(5)?(6)?(7)?(8)?(9)?(0)?/); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"a\",\"b\",\"a-b\"]")]
    // global match
    [TestCase(Preamble + Seed + "'a1b2'.match(/\\d/g); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"2\",\"\",\"a1b\",\"\",\"a1b2\"]")]
    // global match with captures
    [TestCase(Preamble + Seed + "'a1b22'.match(/(\\d)(\\d)?/g); all()", "[\"2\",\"2\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"22\",\"2\",\"a1b\",\"\",\"a1b22\"]")]
    // global match with no match leaves the statics
    [TestCase(Preamble + Seed + "'abc'.match(/x/g); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // global match with no match and a capture leaves the statics
    [TestCase(Preamble + Seed + "'abc'.match(/(x)/g); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // global match of the empty pattern
    [TestCase(Preamble + Seed + "'abc'.match(/(?:)/g); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"abc\",\"\",\"abc\"]")]
    // global match on a sticky pattern
    [TestCase(Preamble + Seed + "'aab'.match(/(a)/gy); all()", "[\"a\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"a\",\"a\",\"b\",\"aab\"]")]
    // global match through matchAll
    [TestCase(Preamble + Seed + "[...'a1b2'.matchAll(/(\\d)/g)]; all()", "[\"2\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"2\",\"2\",\"a1b\",\"\",\"a1b2\"]")]
    // global match with the u flag
    [TestCase(Preamble + Seed + "'a\\u{1F600}b\\u{1F600}'.match(/(\\u{1F600})/gu); all()", "[\"😀\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"😀\",\"😀\",\"a😀b\",\"\",\"a😀b😀\"]")]
    // global replace with a string
    [TestCase(Preamble + Seed + "'x-y'.replace(/-/g, '+'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"x\",\"y\",\"x-y\"]")]
    // replace with a string
    [TestCase(Preamble + Seed + "'x-y'.replace(/-/, '+'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"x\",\"y\",\"x-y\"]")]
    // global replace with a string and a capture
    [TestCase(Preamble + Seed + "'x-y-z'.replace(/(-)/g, '+'); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"x-y\",\"z\",\"x-y-z\"]")]
    // replace with a string and a capture
    [TestCase(Preamble + Seed + "'x-y-z'.replace(/(-)/, '+'); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"x\",\"y-z\",\"x-y-z\"]")]
    // replace with no match leaves the statics
    [TestCase(Preamble + Seed + "'x-y'.replace(/(q)/g, '+'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // replace with no match and no capture leaves the statics
    [TestCase(Preamble + Seed + "'x-y'.replace(/q/, '+'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // replace with a template
    [TestCase(Preamble + Seed + "'x-y'.replace(/-/g, '$&$&'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"x\",\"y\",\"x-y\"]")]
    // replace of the empty pattern
    [TestCase(Preamble + Seed + "'xyz'.replace(/(?:)/g, '+'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"xyz\",\"\",\"xyz\"]")]
    // replaceAll with a string
    [TestCase(Preamble + Seed + "'x-y-z'.replaceAll(/-/g, '+'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"\",\"x-y\",\"z\",\"x-y-z\"]")]
    // replace with a string search value leaves the statics
    [TestCase(Preamble + Seed + "'x-y'.replace('-', '+'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // replace with a function
    [TestCase(Preamble + Seed + "'x-y-z'.replace(/(-)/g, () => '+'); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"x-y\",\"z\",\"x-y-z\"]")]
    // test
    [TestCase(Preamble + Seed + "/b/.test('abc'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"\",\"a\",\"c\",\"abc\"]")]
    // test with captures
    [TestCase(Preamble + Seed + "/(b)(c)?/.test('abc'); all()", "[\"b\",\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"bc\",\"c\",\"a\",\"\",\"abc\"]")]
    // test with a capture that took no part
    [TestCase(Preamble + Seed + "/(b)(x)?/.test('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"\",\"a\",\"c\",\"abc\"]")]
    // a failed test leaves the statics
    [TestCase(Preamble + Seed + "/b/.test('xyz'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // a failed test with a capture leaves the statics
    [TestCase(Preamble + Seed + "/(b)/.test('xyz'); all()", "[\"z\",\"y\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"zy\",\"y\",\"x\",\"!\",\"xzy!\"]")]
    // a global test and the next
    [TestCase(Preamble + Seed + "var re = /(b)/g; re.test('abcb'); var a = all(); re.test('abcb'); JSON.stringify([a, all()])", "[\"[\\\"b\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"b\\\",\\\"b\\\",\\\"a\\\",\\\"cb\\\",\\\"abcb\\\"]\",\"[\\\"b\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"b\\\",\\\"b\\\",\\\"abc\\\",\\\"\\\",\\\"abcb\\\"]\"]")]
    // a global test that fails after matching
    [TestCase(Preamble + Seed + "var re = /(b)/g; re.test('abc'); var a = all(); var r = re.test('abc'); JSON.stringify([a, r, all() === a])", "[\"[\\\"b\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"b\\\",\\\"b\\\",\\\"a\\\",\\\"c\\\",\\\"abc\\\"]\",false,true]")]
    // a sticky test
    [TestCase(Preamble + Seed + "var re = /(b)/y; re.lastIndex = 1; re.test('abcb'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"cb\",\"abcb\"]")]
    // a sticky test that fails
    [TestCase(Preamble + Seed + "var re = /(b)/y; re.lastIndex = 0; var r = re.test('abcb'); JSON.stringify([r, all()])", "[false,\"[\\\"z\\\",\\\"y\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"zy\\\",\\\"y\\\",\\\"x\\\",\\\"!\\\",\\\"xzy!\\\"]\"]")]
    // test of the empty pattern
    [TestCase(Preamble + Seed + "/(?:)/.test('abc'); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"abc\",\"abc\"]")]
    // test of a pattern with a lookbehind
    [TestCase(Preamble + Seed + "/(?<=a)(b)/.test('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // test of an anchored pattern
    [TestCase(Preamble + Seed + "/^(a)/.test('abc'); all()", "[\"a\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"a\",\"\",\"bc\",\"abc\"]")]
    // test with the i flag
    [TestCase(Preamble + Seed + "/(B)/i.test('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // test with the m flag
    [TestCase(Preamble + Seed + "/^(c)/m.test('ab\\nc'); all()", "[\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"c\",\"c\",\"ab\\n\",\"\",\"ab\\nc\"]")]
    // input assignment after a test
    [TestCase(Preamble + Seed + "/(b)/.test('abc'); RegExp.input = 'seed'; JSON.stringify([RegExp.input, RegExp.$1, RegExp.lastMatch, RegExp.leftContext])", "[\"seed\",\"b\",\"b\",\"a\"]")]
    // a failed test after a successful one
    [TestCase(Preamble + Seed + "/(b)/.test('abc'); /(q)/.test('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // a test followed by an exec
    [TestCase(Preamble + Seed + "/(b)/.test('abc'); /(c)/.exec('abc'); all()", "[\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"c\",\"c\",\"ab\",\"\",\"abc\"]")]
    // an exec followed by a test
    [TestCase(Preamble + Seed + "/(c)/.exec('abc'); /(b)/.test('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // two tests and one read
    [TestCase(Preamble + Seed + "/(a)/.test('abc'); /(b)/.test('abc'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"a\",\"c\",\"abc\"]")]
    // a read between two tests
    [TestCase(Preamble + Seed + "/(a)/.test('abc'); var r1 = RegExp.$1; /(b)/.test('abc'); JSON.stringify([r1, RegExp.$1])", "[\"a\",\"b\"]")]
    // a read of lastMatch only
    [TestCase(Preamble + Seed + "/(a)(b)/.test('xab'); RegExp.lastMatch", "ab")]
    // a read of leftContext only
    [TestCase(Preamble + Seed + "/(a)(b)/.test('xab'); RegExp.leftContext", "x")]
    // a read of input only
    [TestCase(Preamble + Seed + "/(a)(b)/.test('xab'); RegExp.input", "xab")]
    // the same read twice
    [TestCase(Preamble + Seed + "/(a)(b)/.test('xab'); [RegExp.$1, RegExp.$1, RegExp.$2, RegExp.lastParen, RegExp.lastParen].join()", "a,a,b,b,b")]
    // a read after a global match then a test
    [TestCase(Preamble + Seed + "'a1b2'.match(/(\\d)/g); /(a)/.test('a'); all()", "[\"a\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"a\",\"\",\"\",\"a\"]")]
    // statics read inside a replacer
    [TestCase(Preamble + Seed + "'a1b2'.replace(/(\\d)/g, function () { return RegExp.$1; })", "a2b2")]
    // many matches with one read
    [TestCase(Preamble + Seed + "var re = /(\\d)/g; var s = 'a1b2c3d4'; for (var i = 0; i < 10; i++) { re.lastIndex = 0; re.test(s); } all()", "[\"1\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"1\",\"1\",\"a\",\"b2c3d4\",\"a1b2c3d4\"]")]
    // a long subject
    [TestCase(Preamble + Seed + "var s = 'x'.repeat(5000) + 'abc' + 'y'.repeat(5000); /(b)(c)/.test(s); JSON.stringify([RegExp.$1, RegExp.$2, RegExp.lastMatch, RegExp.leftContext.length, RegExp.rightContext.length, RegExp.input.length])", "[\"b\",\"c\",\"bc\",5001,5000,10003]")]
    // many groups
    [TestCase(Preamble + Seed + "/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)/.test('zabcdefghijkw'); all()", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"abcdefghijk\",\"k\",\"z\",\"w\",\"zabcdefghijkw\"]")]
    // many groups through a global match
    [TestCase(Preamble + Seed + "'zabcdefghijkw'.match(/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)/g); all()", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"abcdefghijk\",\"k\",\"z\",\"w\",\"zabcdefghijkw\"]")]
    // many groups through a replace
    [TestCase(Preamble + Seed + "'zabcdefghijkw'.replace(/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)/g, 'X'); all()", "[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\",\"abcdefghijk\",\"k\",\"z\",\"w\",\"zabcdefghijkw\"]")]
    // named groups through a test
    [TestCase(Preamble + Seed + "/(?<n>a)(?<m>b)?/.test('xa'); all()", "[\"a\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"\",\"x\",\"\",\"xa\"]")]
    // The record stores a reference into the constructor only when it differs from the one held, so these record the
    // same regexp or subject again, and again after something else has been recorded or assigned in between.
    // the same test again
    [TestCase(Preamble + Seed + "var re = /(b)(c)?/; var s = 'abc'; re.test(s); re.test(s); all()", "[\"b\",\"c\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"bc\",\"c\",\"a\",\"\",\"abc\"]")]
    // the same test again after a read
    [TestCase(Preamble + Seed + "var re = /(b)(c)?/; var s = 'abc'; re.test(s); var a = all(); re.test(s); JSON.stringify([a, all()])", "[\"[\\\"b\\\",\\\"c\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"bc\\\",\\\"c\\\",\\\"a\\\",\\\"\\\",\\\"abc\\\"]\",\"[\\\"b\\\",\\\"c\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"\\\",\\\"bc\\\",\\\"c\\\",\\\"a\\\",\\\"\\\",\\\"abc\\\"]\"]")]
    // the same test again after input was assigned
    [TestCase(Preamble + Seed + "var re = /(b)(c)?/; var s = 'abc'; re.test(s); RegExp.input = 'q'; var a = RegExp.input; re.test(s); JSON.stringify([a, RegExp.input, RegExp.$1])", "[\"q\",\"abc\",\"b\"]")]
    // the same test again after another match in between
    [TestCase(Preamble + Seed + "var re = /(b)(c)?/; var s = 'abc'; re.test(s); var a = RegExp.$1; /(z)/.exec('z'); var b = RegExp.$1; re.test(s); JSON.stringify([a, b, RegExp.$1, RegExp.$2, RegExp.input])", "[\"b\",\"z\",\"b\",\"c\",\"abc\"]")]
    // the same subject with another regexp
    [TestCase(Preamble + Seed + "var s = 'abc'; /(a)/.test(s); /(b)/.test(s); /(a)/.test(s); all()", "[\"a\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"a\",\"a\",\"\",\"bc\",\"abc\"]")]
    // the same regexp on another subject
    [TestCase(Preamble + Seed + "var re = /(b)/; re.test('abc'); re.test('xbx'); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"x\",\"x\",\"xbx\"]")]
    // the same global match again
    [TestCase(Preamble + Seed + "'abcb'.match(/(b)/g); 'abcb'.match(/(b)/g); all()", "[\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"b\",\"abc\",\"\",\"abcb\"]")]
    // the same plain replace again
    [TestCase(Preamble + Seed + "var s = 'x-y-z'; s.replace(/(-)/g, '+'); s.replace(/(-)/g, '+'); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"x-y\",\"z\",\"x-y-z\"]")]
    // the same split again
    [TestCase(Preamble + Seed + "var s = 'a-b-c'; s.split(/(-)/); s.split(/(-)/); all()", "[\"-\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"-\",\"-\",\"a-b\",\"c\",\"a-b-c\"]")]
    // the same exec again
    [TestCase(Preamble + Seed + "var re = /(a)(b)/; re.exec('xab'); re.exec('xab'); all()", "[\"a\",\"b\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"ab\",\"b\",\"x\",\"\",\"xab\"]")]
    // input assigned between two reads of one test
    [TestCase(Preamble + Seed + "/(b)/.test('abc'); RegExp.input = 'q'; var a = RegExp.$1; /(b)/.test('abc'); JSON.stringify([a, RegExp.input, RegExp.$1])", "[\"b\",\"abc\",\"b\"]")]
    // the same range record again
    [TestCase(Preamble + Seed + "var re = /b/g; var s = 'abcb'; re.lastIndex = 0; re.test(s); re.lastIndex = 0; re.test(s); all()", "[\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"b\",\"\",\"a\",\"cb\",\"abcb\"]")]
    public void TheStaticsAreTheLastMatchOfTheCall(string script, string expected)
    {
        Run(script).Should().Be(expected);
    }
}
