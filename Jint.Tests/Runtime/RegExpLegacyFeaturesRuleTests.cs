#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Whose legacy <c>RegExp</c> statics a match may touch, which the RegExp Legacy Features proposal
/// (https://github.com/tc39/proposal-regexp-legacy-features) decides at the end of RegExpBuiltinExec
/// (https://tc39.es/ecma262/#sec-regexpbuiltinexec): a regexp remembers the realm of the constructor that allocated it
/// (<c>[[Realm]]</c>) and whether that constructor was <c>%RegExp%</c> itself (<c>[[LegacyFeaturesEnabled]]</c>); when the
/// built-in running the match is of the regexp's realm it updates that realm's statics for such a regexp and
/// <em>invalidates</em> them for an instance a subclass allocated, and when the realms differ it leaves them alone.
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

    // A regexp of another realm touches nothing when a built-in of this one matches it: neither realm's statics move.
    [TestCase("RegExp.prototype.exec.call(re, 'xa')")]
    [TestCase("RegExp.prototype.test.call(re, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.match].call(re, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.match].call(reGlobal, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.search].call(re, 'xa')")]
    [TestCase("RegExp.prototype[Symbol.replace].call(re, 'xa', 'z')")]
    [TestCase("RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', 'z')")]
    [TestCase("RegExp.prototype[Symbol.replace].call(reGlobal, 'xa', () => 'z')")]
    [TestCase("RegExp.prototype[Symbol.split].call(re, 'x-a')")]
    public void ARegExpOfAnotherRealmIsLeftAloneByThisRealmsBuiltIns(string match)
    {
        RunWithRealm("""
            var other = $262.createRealm().global;
            var re = new other.RegExp('(a)');
            var reGlobal = new other.RegExp('(a)', 'g');
            /(z)/.exec('xz');
            """ + match + """
            ;
            JSON.stringify([RegExp.$1, other.RegExp.$1, RegExp.input, other.RegExp.input])
            """).Should().Be("""["z","","xz",""]""");
    }

    // ... and a built-in of the regexp's own realm matches it as any other: that realm's statics move, this one's do not.
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
    public void ABuiltInOfTheRegExpsOwnRealmUpdatesThatRealmsStatics(string match)
    {
        RunWithRealm("""
            var other = $262.createRealm().global;
            var re = new other.RegExp('(a)');
            var reGlobal = new other.RegExp('(a)', 'g');
            /(z)/.exec('xz');
            """ + match + """
            ;
            JSON.stringify([RegExp.$1, other.RegExp.$1, RegExp.input, other.RegExp.input])
            """).Should().Be("""["z","a","xz","xa"]""");
    }

    // A regexp of this realm matched by another realm's built-in: the realms differ, so nothing moves in either.
    [TestCase("other.RegExp.prototype.exec.call(mine, 'xa')")]
    [TestCase("other.RegExp.prototype.test.call(mine, 'xa')")]
    [TestCase("other.RegExp.prototype[Symbol.replace].call(mine, 'xa', 'z')")]
    public void AnotherRealmsBuiltInLeavesARegExpOfThisRealmAlone(string match)
    {
        RunWithRealm("""
            var other = $262.createRealm().global;
            var mine = /(a)/;
            /(z)/.exec('xz');
            """ + match + """
            ;
            JSON.stringify([RegExp.$1, other.RegExp.$1, RegExp.input, other.RegExp.input])
            """).Should().Be("""["z","","xz",""]""");
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
