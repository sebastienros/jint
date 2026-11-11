#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Whose legacy <c>RegExp</c> statics a match may touch, which the RegExp Legacy Features proposal
/// (https://github.com/tc39/proposal-regexp-legacy-features) decides at the end of RegExpBuiltinExec
/// (https://tc39.es/ecma262/#sec-regexpbuiltinexec): a regexp remembers the realm of the constructor that allocated it
/// (<c>[[Realm]]</c>) and whether that constructor was <c>%RegExp%</c> itself (<c>[[LegacyFeaturesEnabled]]</c>); when the
/// current realm - that of the <c>exec</c> function RegExpExec calls, not of the method that asked for the match - is the
/// regexp's own it updates that realm's statics for such a regexp and <em>invalidates</em> them for an instance a
/// subclass allocated, and when the realms differ it leaves them alone.
/// Invalidating empties every slot, and reading an empty slot throws a <c>TypeError</c> until the next successful match
/// sets them (assigning to <c>input</c> makes that one readable again by itself).
/// <para>
/// Node has no counterpart: V8 implements neither the subclass nor the cross-realm restriction (a subclass instance's
/// match updates the statics there), so the expected values are the proposal's. The proposal lists both restrictions
/// as features that need implementation experience, and test262 pins them for <c>compile</c> and for the accessors'
/// receivers only.
/// </para>
/// </summary>
public class RegExpLegacyFeaturesRuleTests
{
    private const string Probe = """
        class M extends RegExp {}
        function probe() {
          var out = [];
          for (var k of ['$1', 'lastMatch', 'lastParen', 'leftContext', 'rightContext', 'input']) {
            try { RegExp[k]; out.push('ok'); } catch (e) { out.push(e.constructor.name); }
          }
          return out.join();
        }
        /(z)/.exec('xz');
        """;

    private const string AllOk = "ok,ok,ok,ok,ok,ok";
    private const string AllThrow = "TypeError,TypeError,TypeError,TypeError,TypeError,TypeError";

    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    private static string RunWithRealm(string script)
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        return engine.Evaluate(script).ToString();
    }

    // A match by an instance a subclass allocated invalidates the statics, whichever lane makes it.
    [TestCase("new M('(a)').exec('xa')")]
    [TestCase("new M('(a)').test('xa')")]
    [TestCase("new M('a').test('xa')")]
    [TestCase("var r = new M('(a)', 'g'); r.test('xa')")]
    [TestCase("var r = new M('(a)', 'y'); r.lastIndex = 1; r.test('xa')")]
    [TestCase("'xa'.match(new M('(a)'))")]
    [TestCase("'xa'.match(new M('(a)', 'g'))")]
    [TestCase("'xa'.search(new M('(a)'))")]
    [TestCase("'xa'.search(new M('a'))")]
    [TestCase("'xa'.replace(new M('a'), 'z')")]
    [TestCase("'xa'.replace(new M('(a)', 'g'), 'z')")]
    [TestCase("'xa'.replace(new M('(a)', 'g'), () => 'z')")]
    [TestCase("'xa'.replace(new M('(a)'), '$1$1')")]
    [TestCase("'x-a'.split(new M('-'))")]
    [TestCase("'x-a'.split(new M('(-)'))")]
    [TestCase("'xa'.split(new M(''))")]
    [TestCase("[...'xa'.matchAll(new M('(a)', 'g'))]")]
    [TestCase("new M('').exec('xa')")]
    [TestCase("Reflect.construct(RegExp, ['(a)'], M).exec('xa')")]
    [TestCase("var r = /-/; r.constructor = M; 'x-a'.split(r)")]
    public void AMatchByASubclassInstanceInvalidatesTheStatics(string match)
    {
        Run(Probe + $"var before = probe(); {match}; before + '|' + probe()")
            .Should().Be(AllOk + "|" + AllThrow);
    }

    // Invalidating drops a match that was only recorded, too: nothing is left to read.
    [Test]
    public void AMatchRecordedForALaterReadIsInvalidatedToo()
    {
        Run(Probe + "/(b)/.test('abc'); new M('(a)').exec('xa'); probe()").Should().Be(AllThrow);
    }

    // Only a successful match invalidates.
    [TestCase("new M('(q)').exec('xa')")]
    [TestCase("new M('(q)').test('xa')")]
    [TestCase("'xa'.match(new M('(q)', 'g'))")]
    [TestCase("'xa'.replace(new M('(q)', 'g'), 'z')")]
    [TestCase("'xa'.split(new M('(q)'))")]
    public void AFailedMatchBySubclassInstanceLeavesThemAlone(string match)
    {
        Run(Probe + $"{match}; probe()").Should().Be(AllOk);
    }

    // input is a slot of its own: assigning it makes it readable while the rest stays empty, and the next successful
    // match of a plain regexp sets every slot again.
    [Test]
    public void AssigningInputMakesOnlyInputReadableAndAPlainMatchRestoresTheRest()
    {
        Run(Probe + """
            new M('(a)').exec('xa');
            var invalid = probe();
            RegExp.input = 'seed';
            var afterInput = probe() + '|' + RegExp.input + '|' + RegExp.$_;
            /(b)/.exec('abc');
            [invalid, afterInput, probe(), RegExp.$1, RegExp.input].join('#')
            """).Should().Be(AllThrow + "#TypeError,TypeError,TypeError,TypeError,TypeError,ok|seed|seed#" + AllOk + "#b#abc");
    }

    // Every static reads the same way: an alias throws where its twin does.
    [Test]
    public void EveryStaticAndAliasThrowsWhileInvalidated()
    {
        Run(Probe + """
            new M('(a)').exec('xa');
            var out = [];
            for (var k of ['$1', '$2', '$3', '$4', '$5', '$6', '$7', '$8', '$9', 'lastMatch', '$&', 'lastParen', '$+', 'leftContext', '$`', 'rightContext', "$'", 'input', '$_']) {
              try { RegExp[k]; out.push('ok'); } catch (e) { out.push(e.constructor.name === 'TypeError' ? 'T' : e.constructor.name); }
            }
            out.join('')
            """).Should().Be(new string('T', 19));
    }

    // A regexp made from a subclass instance is an ordinary one: only the constructor that allocates decides.
    [TestCase("new RegExp(new M('(a)')).exec('xa')")]
    [TestCase("new RegExp(/(a)/).exec('xa')")]
    [TestCase("RegExp('(a)').exec('xa')")]
    [TestCase("new RegExp('(a)').test('xa')")]
    public void ARegExpAllocatedByRegExpUpdatesTheStatics(string match)
    {
        Run(Probe + $"{match}; JSON.stringify([RegExp.$1, RegExp.lastMatch, RegExp.input])")
            .Should().Be("""["a","a","xa"]""");
    }

    // Which realm's statics a match moves is decided by the realm of the exec function that runs RegExpBuiltinExec,
    // because calling a function makes its realm the current one (https://tc39.es/ecma262/#sec-built-in-function-objects-call-thisargument-argumentslist),
    // and RegExpExec (https://tc39.es/ecma262/#sec-regexpexec step 5) calls the exec it finds on the regexp - the one on
    // its own prototype chain, whichever realm's method asked. The statics move only when that is the realm the regexp
    // was allocated in.
    private const string TwoRealms = """
        var other = $262.createRealm().global;
        var re = new other.RegExp('(a)');
        var reGlobal = new other.RegExp('(a)', 'g');
        var mine = /(a)/;
        var mineGlobal = /(a)/g;
        /(z)/.exec('xz');
        """;

    private const string Statics = "JSON.stringify([RegExp.$1, other.RegExp.$1, RegExp.input, other.RegExp.input])";

    private const string Untouched = """["z","","xz",""]""";
    private const string OtherRealmMoved = """["z","a","xz","xa"]""";
    private const string ThisRealmMoved = """["a","","xa",""]""";

    private static string RunTwoRealms(string match) => RunWithRealm(TwoRealms + match + ";" + Statics);

    // The regexp is another realm's, and so is the exec it carries: that realm's statics move, this one's do not,
    // whichever realm's method asked for the match.
    [TestCase("RegExp.prototype.test.call(re, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.match].call(re, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.match].call(reGlobal, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.search].call(re, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.replace].call(re, 'xa', 'z')")]
    [TestCase("RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', 'z')")]
    [TestCase("RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', () => 'z')")]
    [TestCase("other.RegExp.prototype.exec.call(re, 'xa')")]
    [TestCase("other.RegExp.prototype.test.call(re, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.match].call(re, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.match].call(reGlobal, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.search].call(re, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(re, 'xa', 'z')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', 'z')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', () => 'z')")]
    [TestCase("other.eval('/(a)/').exec('xa')")]
    [TestCase("other.eval(\"/(a)/.exec('xa')\")")]
    [TestCase("other.eval(\"'xa'.replace(/(a)/g, 'z')\")")]
    public void AMatchWhoseExecBelongsToTheRegExpsRealmMovesThatRealmsStatics(string match)
    {
        RunTwoRealms(match).Should().Be(OtherRealmMoved);
    }

    // split builds its splitter with the regexp's species constructor, so the splitter is the other realm's too.
    [TestCase("RegExp.prototype[Symbol.split].call(re, 'x-a')")]
    [TestCase("other.RegExp.prototype[Symbol.split].call(re, 'x-a')")]
    public void ASplitOfAnotherRealmsRegExpMovesThatRealmsStatics(string match)
    {
        RunTwoRealms(match).Should().Be("""["z","a","xz","x-a"]""");
    }

    // The regexp is this realm's and its exec is this realm's, though another realm's method asked: this realm's move.
    [TestCase("other.RegExp.prototype.test.call(mine, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.match].call(mine, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.match].call(mineGlobal, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.search].call(mine, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(mine, 'xa', 'z')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(mineGlobal, 'xa', 'z')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(mineGlobal, 'xa', () => 'z')")]
    public void AnotherRealmsMethodOnARegExpOfThisRealmMovesThisRealmsStatics(string match)
    {
        RunTwoRealms(match).Should().Be(ThisRealmMoved);
    }

    [Test]
    public void ASplitByAnotherRealmsMethodOfARegExpOfThisRealmMovesThisRealmsStatics()
    {
        RunTwoRealms("other.RegExp.prototype[Symbol.split].call(mine, 'x-a')").Should().Be("""["a","","x-a",""]""");
    }

    // exec itself is the function that runs, so its realm is the current one: a regexp of the other realm is left
    // alone, and so is a regexp of this realm run by the other realm's exec. Nothing moves.
    [TestCase("RegExp.prototype.exec.call(re, 'xa')")]
    [TestCase("other.RegExp.prototype.exec.call(mine, 'xa')")]
    public void AnExecOfAnotherRealmThanTheRegExpsLeavesBothRealmsStaticsAlone(string match)
    {
        RunTwoRealms(match).Should().Be(Untouched);
    }

    // The exec RegExpExec calls is found through the regexp's prototype, not through where the regexp was allocated: a
    // regexp given another realm's RegExp.prototype runs that realm's exec, which is not its own realm's.
    [TestCase("Object.setPrototypeOf(mine, other.RegExp.prototype); mine.test('xa')")]
    [TestCase("Object.setPrototypeOf(mine, other.RegExp.prototype); RegExp.prototype.test.call(mine, 'xa')")]
    [TestCase("Object.setPrototypeOf(mineGlobal, other.RegExp.prototype); 'xa'.replace(mineGlobal, 'z')")]
    [TestCase("Object.setPrototypeOf(mine, other.RegExp.prototype); 'xa'.match(mine)")]
    [TestCase("Object.setPrototypeOf(re, RegExp.prototype); re.test('xa')")]
    [TestCase("Object.setPrototypeOf(reGlobal, RegExp.prototype); other.RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', 'z')")]
    public void ARegExpWhoseExecIsOfAnotherRealmThanItsOwnMovesNothing(string match)
    {
        RunTwoRealms(match).Should().Be(Untouched);
    }

    // ... while a prototype of the same realm that is not RegExp.prototype itself changes nothing.
    [TestCase("Object.setPrototypeOf(mine, Object.create(RegExp.prototype)); mine.test('xa')")]
    [TestCase("Object.setPrototypeOf(mine, Object.create(RegExp.prototype)); 'xa'.match(mine)")]
    [TestCase("Object.setPrototypeOf(mineGlobal, Object.create(RegExp.prototype)); 'xa'.replace(mineGlobal, 'z')")]
    public void ARegExpWhoseExecIsOfItsOwnRealmMovesThatRealmsStaticsWhateverItsPrototype(string match)
    {
        RunTwoRealms(match).Should().Be(ThisRealmMoved);
    }

    // RegExp.prototype.compile reads the same two slots (the proposal's changes to it): it throws a TypeError unless the
    // current realm - the realm of the compile function that was called - is the regexp's [[Realm]] and the regexp has
    // [[LegacyFeaturesEnabled]]. Neither is about the regexp's prototype, which a script can replace.
    [TestCase("new RegExp('a').compile('b').source", "b")]
    [TestCase("/a/.compile('b').source", "b")]
    [TestCase("new M('a').compile('b')", "TypeError")]
    [TestCase("RegExp.prototype.compile.call(new M('a'), 'b')", "TypeError")]
    [TestCase("Object.setPrototypeOf(new M('a'), RegExp.prototype).compile('b')", "TypeError")]
    [TestCase("Reflect.construct(RegExp, ['a'], M).compile('b')", "TypeError")]
    [TestCase("Object.setPrototypeOf(new RegExp('a'), M.prototype).compile('b').source", "b")]
    [TestCase("(function () { var r = new RegExp('a'); Object.setPrototypeOf(r, {}); RegExp.prototype.compile.call(r, 'b'); return Object.getOwnPropertyDescriptor(RegExp.prototype, 'source').get.call(r); })()", "b")]
    [TestCase("(function () { var r = new RegExp('a'); Object.setPrototypeOf(r, Object.create(RegExp.prototype)); return r.compile('b').source; })()", "b")]
    public void CompileFollowsTheRegExpsRealmAndFlagNotItsPrototype(string expression, string expected)
    {
        Run(Probe + $"(function () {{ try {{ return {expression}; }} catch (e) {{ return e.constructor.name; }} }})()").Should().Be(expected);
    }

    [TestCase("RegExp.prototype.compile.call(new other.RegExp('a'))", "TypeError")]
    [TestCase("other.RegExp.prototype.compile.call(/a/)", "other TypeError")]
    [TestCase("Object.setPrototypeOf(/a/, other.RegExp.prototype).compile('b')", "other TypeError")]
    [TestCase("Object.setPrototypeOf(new other.RegExp('a'), RegExp.prototype).compile('b')", "TypeError")]
    [TestCase("new other.RegExp('a').compile('b').source", "b")]
    [TestCase("other.RegExp.prototype.compile.call(new other.RegExp('a'), 'b').source", "b")]
    [TestCase("Object.setPrototypeOf(new other.RegExp('a'), Object.create(other.RegExp.prototype)).compile('b').source", "b")]
    public void CompileOfARegExpOfAnotherRealmIsRefusedByTheRealmOfTheCompileCalled(string expression, string expected)
    {
        RunWithRealm($$"""
            var other = $262.createRealm().global;
            (function () {
              try { return {{expression}}; }
              catch (e) { return (e instanceof other.TypeError ? 'other ' : e instanceof TypeError ? '' : 'neither ') + e.constructor.name; }
            })()
            """).Should().Be(expected);
    }

    // Each realm's statics are its own.
    [Test]
    public void TwoRealmsKeepTheirOwnStatics()
    {
        RunWithRealm("""
            var other = $262.createRealm().global;
            /(a)/.exec('xa');
            other.eval("/(b)/.exec('xb')");
            JSON.stringify([RegExp.$1, other.RegExp.$1, RegExp.input, other.RegExp.input])
            """).Should().Be("""["a","b","xa","xb"]""");
    }
}
