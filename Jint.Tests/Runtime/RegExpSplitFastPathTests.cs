#nullable enable

using Jint.Native;
using Jint.Native.RegExp;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>RegExp.prototype[@@split]</c> (https://tc39.es/ecma262/#sec-regexp.prototype-@@split) looks up the species
/// constructor, reads <c>flags</c> and constructs a sticky splitter before it splits anything. For an unmodified
/// RegExp none of that can be observed, and the engine goes straight to the split on the receiver's own matcher.
/// <para>
/// The first group pins what that path answers; every other group makes one of the skipped steps observable and pins
/// that the specified path still runs, with the number of times script is called. All expected values were produced
/// by Node 26 (the second realm by a <c>vm</c> context in place of <c>$262.createRealm()</c>).
/// </para>
/// </summary>
public class RegExpSplitFastPathTests
{
    private static string Evaluate(string source)
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        return engine.Evaluate(source).AsString();
    }

    // ---- what the path answers ----

    [TestCase("""JSON.stringify('a,b,c,d,e'.split(/,/))""", """["a","b","c","d","e"]""")]
    // captures are spliced in after each segment, an unmatched one as undefined
    [TestCase("""JSON.stringify('a1b22c'.split(/(\d+)/))""", """["a","1","b","22","c"]""")]
    [TestCase("""JSON.stringify('abc'.split(/(x)?b/))""", """["a",null,"c"]""")]
    [TestCase("""JSON.stringify('a1b2c3'.split(/(\d)(x)?/))""", """["a","1",null,"b","2",null,"c","3",null,""]""")]
    [TestCase("""JSON.stringify('A<B>bold</B>and<CODE>coded</CODE>'.split(/<(\/)?([^<>]+)>/))""",
        """["A",null,"B","bold","/","B","and",null,"CODE","coded","/","CODE",""]""")]
    // limit, including the ToUint32 wrap-around and the values that coerce to zero
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, 2))""", """["a","b"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, 0))""", "[]")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, 1))""", """["a"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, -1))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, '2'))""", """["a","b"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, NaN))""", "[]")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, 2 ** 32 + 1))""", """["a"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/, 4294967295))""", """["a","b","c"]""")]
    // the limit counts the spliced captures too
    [TestCase("""JSON.stringify('a1b2c'.split(/(\d)/, 2))""", """["a","1"]""")]
    [TestCase("""JSON.stringify('a1b2c'.split(/(\d)/, 3))""", """["a","1","b"]""")]
    // empty matches never split at the start, at the end or where the last split ended
    [TestCase("""JSON.stringify('abc'.split(/(?:)/))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('abc'.split(/x*/))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('abc'.split(/$/))""", """["abc"]""")]
    [TestCase("""JSON.stringify('abc'.split(/^/))""", """["abc"]""")]
    [TestCase("""JSON.stringify('abc'.split(/(?:)/, 2))""", """["a","b"]""")]
    [TestCase("""JSON.stringify('a,b'.split(/,?/))""", """["a","b"]""")]
    // u and v advance by code point, no flag by code unit
    [TestCase("""JSON.stringify('\u{1F600}a\u{1F601}'.split(/(?:)/u).map(s => s.length))""", "[2,1,2]")]
    [TestCase("""JSON.stringify('\u{1F600}a\u{1F601}'.split(/(?:)/).map(s => s.length))""", "[1,1,1,1,1]")]
    [TestCase("""JSON.stringify('a\u{1F600}b'.split(/(?:)/v).map(s => s.length))""", "[1,2,1]")]
    [TestCase("""JSON.stringify('a\u{1F600}b'.split(/\u{1F600}/u))""", """["a","b"]""")]
    [TestCase("""JSON.stringify('\u{1F600}\u{1F601}'.split(/./u))""", """["","",""]""")]
    // the receiver's own i, m, s survive
    [TestCase("""JSON.stringify('aXbxc'.split(/x/i))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('a\nb\nc'.split(/^/m))""", """["a\n","b\n","c"]""")]
    [TestCase("""JSON.stringify('a\nb'.split(/./s))""", """["","","",""]""")]
    [TestCase("""JSON.stringify('a\nb'.split(/./))""", """["","\n",""]""")]
    [TestCase("""JSON.stringify('abc'.split(new RegExp('b', 'gimsuy')))""", """["a","c"]""")]
    // g, d and an already sticky receiver change nothing
    [TestCase("""JSON.stringify('a,b,c'.split(/,/y))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/gy))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/g))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('a,b,c'.split(/,/d))""", """["a","b","c"]""")]
    [TestCase("""JSON.stringify('abc'.split(/(?:)/y))""", """["a","b","c"]""")]
    // an empty subject is answered by matching the empty string once
    [TestCase("""JSON.stringify(''.split(/x/))""", """[""]""")]
    [TestCase("""JSON.stringify(''.split(/(?:)/))""", "[]")]
    [TestCase("""JSON.stringify(''.split(/x*/))""", "[]")]
    [TestCase("""JSON.stringify(''.split(/x/, 0))""", "[]")]
    [TestCase("""JSON.stringify(',a,'.split(/,/))""", """["","a",""]""")]
    [TestCase("""JSON.stringify('a,,b'.split(/,/))""", """["a","","b"]""")]
    public void TheUnmodifiedRegExpSplitsAsSpecified(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    [Test]
    public void TheReceiversLastIndexIsNeitherReadNorWritten()
    {
        // @@split works on a copy; it never touches the receiver's lastIndex, whatever the flags say.
        Evaluate("""
            const g = /,/g; g.lastIndex = 7; 'a,b'.split(g);
            const y = /,/y; y.lastIndex = 1; 'a,b'.split(y);
            let coerced = 0;
            const o = /,/; o.lastIndex = { valueOf() { coerced++; return 0; } }; 'a,b'.split(o);
            JSON.stringify([g.lastIndex, y.lastIndex, coerced]);
            """).Should().Be("[7,1,0]");
    }

    [Test]
    public void TheCapturedFlagsAreThoseOfTheReceiverNotOfTheSplitter()
    {
        // The splitter is sticky, the receiver is not; nothing about the receiver may change.
        Evaluate("""
            const re = /(,)/i;
            const out = 'a,b'.split(re);
            JSON.stringify([out, re.flags, re.sticky, re.lastIndex]);
            """).Should().Be("""[["a",",","b"],"i",false,0]""");
    }

    // ---- each skipped step, made observable ----

    // 5. Get(rx, "flags"), on the instance and on the prototype, directly and through a flag accessor it reads.
    [TestCase("""
        (() => { const re = /x/; let reads = 0;
          Object.defineProperty(re, 'flags', { get() { reads++; return 'i'; } });
          return JSON.stringify(['aXb'.split(re), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(RegExp.prototype, 'flags', { get() { reads++; return 'i'; }, configurable: true });
          return JSON.stringify(['aXb'.split(/x/), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(RegExp.prototype, 'global', { get() { reads++; return true; }, configurable: true });
          return JSON.stringify(['a,b'.split(/,/), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(RegExp.prototype, 'sticky', { get() { reads++; return false; }, configurable: true });
          return JSON.stringify(['a,b'.split(/,/y), reads]); })()
        """, """[["a","b"],1]""")]
    // ... and the flags read decide how the string is advanced, so a getter answering u makes a pair one character
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(RegExp.prototype, 'unicode', { get() { reads++; return true; }, configurable: true });
          return JSON.stringify(['\u{1F600}'.split(/(?:)/).map(s => s.length), reads]); })()
        """, "[[2],1]")]
    [TestCase("""
        (() => { delete RegExp.prototype.ignoreCase; return JSON.stringify(['aXb'.split(/x/i)]); })()
        """, """[["aXb"]]""")]
    public void TheFlagsAreReadThroughTheirAccessors(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    // 4. SpeciesConstructor: Get(rx, "constructor"), then Get(C, @@species).
    [TestCase("""
        (() => { const log = []; const re = /,/;
          re.constructor = { [Symbol.species]: function (p, f) { log.push(String(f)); return new RegExp(p, f); } };
          return JSON.stringify(['a,b'.split(re), log]); })()
        """, """[["a","b"],["y"]]""")]
    [TestCase("""
        (() => { const log = [];
          RegExp.prototype.constructor = { [Symbol.species]: function (p, f) { log.push(String(f)); return new RegExp(p, f); } };
          return JSON.stringify(['a,b'.split(/,/), log]); })()
        """, """[["a","b"],["y"]]""")]
    // a deleted constructor is found on Object.prototype, and its species is read from Object
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(Object, Symbol.species, { get() { reads++; return undefined; }, configurable: true });
          delete RegExp.prototype.constructor;
          return JSON.stringify(['a,b'.split(/,/), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(RegExp, Symbol.species, { get() { reads++; return RegExp; }, configurable: true });
          return JSON.stringify(['a,b'.split(/,/), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { const log = [];
          Object.defineProperty(RegExp, Symbol.species, { get() { return function (p, f) { log.push(String(f)); return new RegExp(p, f); }; }, configurable: true });
          return JSON.stringify(['a,b'.split(/,/i), log]); })()
        """, """[["a","b"],["iy"]]""")]
    [TestCase("""
        (() => { delete RegExp[Symbol.species]; return JSON.stringify(['a,b'.split(/,/)]); })()
        """, """[["a","b"]]""")]
    public void TheSpeciesConstructorIsLookedUp(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    // 10. Construct(C, «rx, newFlags») reads rx[@@match] through IsRegExp.
    [TestCase("""
        (() => { let reads = 0; const re = /,/;
          Object.defineProperty(re, Symbol.match, { get() { reads++; return true; }, configurable: true });
          return JSON.stringify(['a,b'.split(re), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(RegExp.prototype, Symbol.match, { get() { reads++; return undefined; }, configurable: true });
          return JSON.stringify(['a,b'.split(/,/), reads]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { let reads = 0;
          Object.defineProperty(Object.prototype, Symbol.match, { get() { reads++; return undefined; }, configurable: true });
          delete RegExp.prototype[Symbol.match];
          return JSON.stringify(['a,b'.split(/,/), reads]); })()
        """, """[["a","b"],1]""")]
    public void IsRegExpReadsSymbolMatch(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    // 19b. RegExpExec(splitter, S) looks exec up on the splitter, which inherits it from RegExp.prototype; the
    // receiver's own exec is never consulted.
    [TestCase("""
        (() => { let calls = 0; const orig = RegExp.prototype.exec;
          RegExp.prototype.exec = function (s) { calls++; return orig.call(this, s); };
          return JSON.stringify(['a,b,c'.split(/,/), calls]); })()
        """, """[["a","b","c"],5]""")]
    [TestCase("""
        (() => { let reads = 0; const orig = RegExp.prototype.exec;
          Object.defineProperty(RegExp.prototype, 'exec', { get() { reads++; return orig; }, configurable: true });
          return JSON.stringify(['a,b'.split(/,/), reads]); })()
        """, """[["a","b"],3]""")]
    [TestCase("""
        (() => { let calls = 0; const re = /,/; const orig = re.exec;
          re.exec = function (s) { calls++; return orig.call(this, s); };
          return JSON.stringify(['a,b'.split(re), calls]); })()
        """, """[["a","b"],0]""")]
    public void ExecIsLookedUpOnTheSplitter(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    // A receiver that is not the plain RegExp the engine can read straight off.
    [TestCase("""
        (() => { let made = 0; class R extends RegExp { constructor(p, f) { super(p, f); made++; } }
          const re = new R(','); const out = 'a,b'.split(re); return JSON.stringify([out, made]); })()
        """, """[["a","b"],2]""")]
    [TestCase("""
        (() => { let n = 0; const re = /,/; const p = Object.create(RegExp.prototype);
          p.constructor = { [Symbol.species]: function (a, b) { n++; return new RegExp(a, b); } };
          Object.setPrototypeOf(re, p);
          return JSON.stringify(['a,b'.split(re), n]); })()
        """, """[["a","b"],1]""")]
    [TestCase("""
        (() => { const re = /,/; re.tag = 1; return JSON.stringify(['a,b'.split(re)]); })()
        """, """[["a","b"]]""")]
    // the species and the result array belong to the realms they are looked up in, whichever method runs
    [TestCase("""
        (() => { const other = $262.createRealm().global; let reads = 0;
          Object.defineProperty(other.RegExp, Symbol.species, { get() { reads++; return other.RegExp; }, configurable: true });
          const out = RegExp.prototype[Symbol.split].call(other.eval('/,/'), 'a,b');
          return JSON.stringify([out, reads, Object.getPrototypeOf(out) === Array.prototype]); })()
        """, """[["a","b"],1,true]""")]
    [TestCase("""
        (() => { const other = $262.createRealm().global; let reads = 0;
          Object.defineProperty(RegExp, Symbol.species, { get() { reads++; return RegExp; }, configurable: true });
          const out = other.RegExp.prototype[Symbol.split].call(/,/, 'a,b');
          return JSON.stringify([out, reads, Object.getPrototypeOf(out) === other.Array.prototype]); })()
        """, """[["a","b"],1,true]""")]
    public void OnlyThePlainRegExpOfTheRunningRealmTakesTheDirectPath(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    // Script that runs between the steps: the string and the limit are coerced around the species lookup, the flags
    // read and the splitter, so what they change is seen exactly where the specification sees it.
    [TestCase("""
        (() => { let calls = 0; const orig = RegExp.prototype.exec;
          const subject = { toString() { RegExp.prototype.exec = function (s) { calls++; return orig.call(this, s); }; return 'a,b'; } };
          const out = /,/[Symbol.split](subject);
          RegExp.prototype.exec = orig;
          return JSON.stringify([out, calls]); })()
        """, """[["a","b"],3]""")]
    [TestCase("""
        (() => { let reads = 0;
          const subject = { toString() { Object.defineProperty(RegExp, Symbol.species, { get() { reads++; return RegExp; }, configurable: true }); return 'a,b'; } };
          const out = /,/[Symbol.split](subject);
          return JSON.stringify([out, reads]); })()
        """, """[["a","b"],1]""")]
    // the limit is coerced after the splitter exists: a recompile of the receiver is not seen,
    [TestCase("""
        (() => { const re = /,/;
          const out = 'a,b;c'.split(re, { valueOf() { re.compile(';'); return 9; } });
          return JSON.stringify([out, re.source]); })()
        """, """[["a","b;c"],";"]""")]
    // a species lookup is not repeated,
    [TestCase("""
        (() => { let reads = 0;
          const out = 'a,b'.split(/,/, { valueOf() { Object.defineProperty(RegExp, Symbol.species, { get() { reads++; return RegExp; }, configurable: true }); return 9; } });
          return JSON.stringify([out, reads]); })()
        """, """[["a","b"],0]""")]
    // but the splitter's exec is looked up afterwards, so a replacement made while coercing is called.
    [TestCase("""
        (() => { let calls = 0; const orig = RegExp.prototype.exec;
          const out = 'a,b'.split(/,/, { valueOf() { RegExp.prototype.exec = function (s) { calls++; return orig.call(this, s); }; return 9; } });
          RegExp.prototype.exec = orig;
          return JSON.stringify([out, calls]); })()
        """, """[["a","b"],3]""")]
    [TestCase("""(() => { try { /,/[Symbol.split]('a,b', Symbol()); return 'no throw'; } catch (e) { return e.constructor.name; } })()""", "TypeError")]
    [TestCase("""(() => { try { /,/[Symbol.split]('a,b', 1n); return 'no throw'; } catch (e) { return e.constructor.name; } })()""", "TypeError")]
    public void ScriptThatRunsBetweenTheStepsIsSeenWhereTheSpecificationSeesIt(string script, string expected)
    {
        Evaluate(script).Should().Be(expected);
    }

    [Test]
    public void AnEmptySubjectStillRunsTheSplittersExecAndSoUpdatesTheLegacyStatics()
    {
        // The empty string is matched through RegExpExec on the splitter (step 15), which is a RegExpBuiltinExec and
        // so records the match in RegExp.lastMatch and RegExp.$1 like any other.
        Evaluate("""
            /(a)/.exec('xa');
            /(x)?/[Symbol.split]('');
            JSON.stringify([RegExp.lastMatch, RegExp.$1]);
            """).Should().Be("""["",""]""");
    }

    // ---- which receivers take the direct path ----
    //
    // Everything above holds whichever path ran. These pin that the path is taken at all, which no script can tell:
    // the predicate is the engine's own, and a change that made it answer "no" for an unmodified RegExp would leave
    // every test above green.

    private static (Engine Engine, RegExpPrototype Prototype) EngineWithRealmOf(string setup = "")
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        if (setup.Length > 0)
        {
            engine.Execute(setup);
        }

        return (engine, engine.Realm.Intrinsics.RegExp.PrototypeObject);
    }

    private static bool CanSplitOnOwnMatcher((Engine Engine, RegExpPrototype Prototype) realm, string receiver)
    {
        return realm.Prototype.CanSplitOnOwnMatcher((JsRegExp) realm.Engine.Evaluate(receiver));
    }

    [TestCase("/,/")]
    [TestCase("/,/gimsyd")]
    [TestCase("/,/y")]
    [TestCase("/(a)(b)?/u")]
    [TestCase("new RegExp('a', 'v')")]
    [TestCase("new RegExp('')")]
    public void AnUnmodifiedRegExpTakesTheDirectPath(string receiver)
    {
        CanSplitOnOwnMatcher(EngineWithRealmOf(), receiver).Should().BeTrue();
    }

    [TestCase("""const re = /,/; re.tag = 1; globalThis.re = re;""")]
    [TestCase("""const re = /,/; Object.defineProperty(re, 'flags', { value: 'i' }); globalThis.re = re;""")]
    [TestCase("""const re = /,/; re.exec = RegExp.prototype.exec; globalThis.re = re;""")]
    [TestCase("""const re = /,/; Object.defineProperty(re, Symbol.match, { value: true }); globalThis.re = re;""")]
    [TestCase("""globalThis.re = new (class R extends RegExp {})(',');""")]
    [TestCase("""const re = /,/; Object.setPrototypeOf(re, Object.create(RegExp.prototype)); globalThis.re = re;""")]
    [TestCase("""globalThis.re = $262.createRealm().global.eval('/,/');""")]
    public void AReceiverThatIsNotPlainTakesTheSpecifiedPath(string setup)
    {
        var realm = EngineWithRealmOf(setup);
        CanSplitOnOwnMatcher(realm, "re").Should().BeFalse();
    }

    [TestCase("""Object.defineProperty(RegExp.prototype, 'flags', { get() { return ''; }, configurable: true });""")]
    [TestCase("""Object.defineProperty(RegExp.prototype, 'global', { get() { return false; }, configurable: true });""")]
    [TestCase("""Object.defineProperty(RegExp.prototype, 'sticky', { value: false, configurable: true });""")]
    [TestCase("""delete RegExp.prototype.unicodeSets;""")]
    [TestCase("""RegExp.prototype.constructor = function () {};""")]
    [TestCase("""delete RegExp.prototype.constructor;""")]
    [TestCase("""Object.defineProperty(RegExp.prototype, 'constructor', { get() { return RegExp; }, configurable: true });""")]
    [TestCase("""Object.defineProperty(RegExp, Symbol.species, { get() { return RegExp; }, configurable: true });""")]
    [TestCase("""Object.defineProperty(RegExp, Symbol.species, { value: RegExp, configurable: true });""")]
    [TestCase("""delete RegExp[Symbol.species];""")]
    [TestCase("""Object.defineProperty(RegExp.prototype, Symbol.match, { get() { return undefined; }, configurable: true });""")]
    [TestCase("""delete RegExp.prototype[Symbol.match];""")]
    [TestCase("""RegExp.prototype.exec = function (s) { return null; };""")]
    [TestCase("""Object.defineProperty(RegExp.prototype, 'exec', { get() { return null; }, configurable: true });""")]
    [TestCase("""delete RegExp.prototype.exec;""")]
    public void ChangingWhatTheSkippedStepsReadTakesTheSpecifiedPath(string setup)
    {
        var realm = EngineWithRealmOf(setup);
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeFalse();
    }

    [Test]
    public void AValueWrittenInPlaceIsSeenAndSoIsItsRestoration()
    {
        // constructor and exec are data properties: writing them moves no version, so what is asked is the value.
        var realm = EngineWithRealmOf("""
            globalThis.originalConstructor = RegExp.prototype.constructor;
            globalThis.originalExec = RegExp.prototype.exec;
            """);
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeTrue();

        realm.Engine.Execute("RegExp.prototype.constructor = function () {};");
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeFalse();
        realm.Engine.Execute("RegExp.prototype.constructor = originalConstructor;");
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeTrue();

        realm.Engine.Execute("RegExp.prototype.exec = function (s) { return null; };");
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeFalse();
        realm.Engine.Execute("RegExp.prototype.exec = originalExec;");
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeTrue();
    }

    [TestCase("""RegExp.prototype[Symbol.match] = false;""")]
    [TestCase("""RegExp.prototype[Symbol.match] = undefined;""")]
    [TestCase("""RegExp.prototype[Symbol.split] = function () {};""")]
    public void WhatIsRegExpAndTheSplitterDoNotReadLeavesTheDirectPathAlone(string setup)
    {
        // A data property of @@match is read without running script whatever it holds, and @@split is not read by
        // the method itself.
        var realm = EngineWithRealmOf(setup);
        CanSplitOnOwnMatcher(realm, "/,/").Should().BeTrue();
    }

    [Test]
    public void AHostSuppliedRegexTakesTheSpecifiedPath()
    {
        var engine = new Engine();
        engine.SetValue("host", new System.Text.RegularExpressions.Regex("[a-z]+"));

        engine.Realm.Intrinsics.RegExp.PrototypeObject.CanSplitOnOwnMatcher((JsRegExp) engine.Evaluate("host")).Should().BeFalse();
    }

    [Test]
    public void ASplitterMatchesUnderTheTimeoutOfTheScriptThatCallsSplit()
    {
        // The splitter is built while split runs, so it takes the match timeout of the script running at that point; a
        // receiver made by another script, under another timeout, must not carry its own into the split.
        var engine = new Engine();
        engine.Execute("globalThis.re = /^(?:a|a)*b/;", parsingOptions: new ScriptParsingOptions { RegexTimeout = TimeSpan.FromSeconds(4) });

        var act = () => engine.Execute("('a'.repeat(30) + '!b,').repeat(20).split(re)", parsingOptions: new ScriptParsingOptions { RegexTimeout = TimeSpan.FromMilliseconds(50) });

        act.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>()
            .Which.MatchTimeout.Should().Be(TimeSpan.FromMilliseconds(50));
    }
}
