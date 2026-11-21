#nullable enable

using Jint.Native;
using Jint.Native.RegExp;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins when <c>RegExpExec</c> may skip <c>Get(R, "exec")</c> and run the built-in directly: only while the
/// receiver is an ordinary RegExp with no own properties whose prototype is its realm's <c>RegExp.prototype</c>
/// and that prototype's <c>exec</c> is still the built-in. Every scenario warms the default path first, then
/// leaves it by one of the ways the specification allows and checks the replacement is observed, so a cached
/// answer that outlived its reason would show here. Expected values are from Node.
/// https://tc39.es/ecma262/#sec-regexpexec
/// </summary>
public class RegExpDefaultExecTests
{
    private static string Run(string body)
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        return engine.Evaluate("(function() {" + body + "})()").ToString();
    }

    private static RegExpPrototype PrototypeOf(Engine engine) => engine.Realm.Intrinsics.RegExp.PrototypeObject;

    [Test]
    public void TheBuiltinExecIsTheDefaultUntilItIsReplacedAndAgainWhenItIsRestored()
    {
        var engine = new Engine();
        var prototype = PrototypeOf(engine);

        prototype.HasDefaultExec.Should().BeTrue();
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("var orig = RegExp.prototype.exec; RegExp.prototype.exec = function(s) { return orig.call(this, s); };");
        prototype.HasDefaultExec.Should().BeFalse();
        prototype.HasDefaultExec.Should().BeFalse();

        engine.Execute("RegExp.prototype.exec = orig;");
        prototype.HasDefaultExec.Should().BeTrue();
    }

    [Test]
    public void ARedefinitionByValueIsSeen()
    {
        var engine = new Engine();
        var prototype = PrototypeOf(engine);
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("var orig = RegExp.prototype.exec; Object.defineProperty(RegExp.prototype, 'exec', { value: function(s) { return orig.call(this, s); } });");
        prototype.HasDefaultExec.Should().BeFalse();

        engine.Execute("Object.defineProperty(RegExp.prototype, 'exec', { value: orig });");
        prototype.HasDefaultExec.Should().BeTrue();
    }

    [Test]
    public void AnAccessorIsNeverTheDefaultAndIsNotCalledByTheCheck()
    {
        var engine = new Engine();
        var prototype = PrototypeOf(engine);
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("""
            var orig = RegExp.prototype.exec, getterCalls = 0;
            Object.defineProperty(RegExp.prototype, 'exec', { configurable: true, get: function() { getterCalls++; return orig; } });
            """);
        prototype.HasDefaultExec.Should().BeFalse();
        engine.Evaluate("getterCalls").AsNumber().Should().Be(0, "asking must not run the getter with RegExp.prototype as its receiver");
    }

    [Test]
    public void ADeletedExecIsNotTheDefault()
    {
        var engine = new Engine();
        var prototype = PrototypeOf(engine);
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("var orig = RegExp.prototype.exec; delete RegExp.prototype.exec;");
        prototype.HasDefaultExec.Should().BeFalse();

        engine.Execute("RegExp.prototype.exec = orig;");
        prototype.HasDefaultExec.Should().BeTrue("the same built-in function put back is the default again");
    }

    [Test]
    public void UnrelatedChangesToThePrototypeDoNotHideAReplacement()
    {
        var engine = new Engine();
        var prototype = PrototypeOf(engine);
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("RegExp.prototype.extra = 1; Object.defineProperty(RegExp.prototype, 'other', { value: 2, configurable: true });");
        prototype.HasDefaultExec.Should().BeTrue();
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("var orig = RegExp.prototype.exec; RegExp.prototype.exec = function(s) { return orig.call(this, s); };");
        prototype.HasDefaultExec.Should().BeFalse();
    }

    [Test]
    public void ExecFromAnotherRealmIsNotTheDefault()
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        var prototype = PrototypeOf(engine);
        prototype.HasDefaultExec.Should().BeTrue();

        engine.Execute("var other = $262.createRealm().global; var orig = RegExp.prototype.exec; RegExp.prototype.exec = other.RegExp.prototype.exec;");
        prototype.HasDefaultExec.Should().BeFalse();
        engine.Execute("RegExp.prototype.exec = orig;");
        prototype.HasDefaultExec.Should().BeTrue();
    }

    [Test]
    public void EachRealmAnswersForItsOwnPrototype()
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        engine.Execute("""
            var other = $262.createRealm().global;
            var rm = /a/, ro = new other.RegExp('a');
            """);

        var rm = (JsRegExp) engine.Evaluate("rm");
        var ro = (JsRegExp) engine.Evaluate("ro");
        rm.HasDefaultRegExpExec.Should().BeTrue();
        ro.HasDefaultRegExpExec.Should().BeTrue();

        engine.Execute("RegExp.prototype.exec = function(s) { return null; };");
        rm.HasDefaultRegExpExec.Should().BeFalse();
        ro.HasDefaultRegExpExec.Should().BeTrue("the other realm's prototype was not touched");

        engine.Execute("var orig = other.RegExp.prototype.exec; other.RegExp.prototype.exec = function(s) { return null; };");
        ro.HasDefaultRegExpExec.Should().BeFalse();
    }

    [Test]
    public void AnInstanceIsOnlyTheDefaultWhileItHasNoOwnPropertyAndItsPrototypeIsTheRealmsOwn()
    {
        var engine = new Engine();
        engine.Execute("""
            class Sub extends RegExp {}
            var plain = /a/, own = /a/, moved = /a/, sub = new Sub('a');
            own.exec = function(s) { return null; };
            Object.setPrototypeOf(moved, Object.create(RegExp.prototype));
            """);

        ((JsRegExp) engine.Evaluate("plain")).HasDefaultRegExpExec.Should().BeTrue();
        ((JsRegExp) engine.Evaluate("own")).HasDefaultRegExpExec.Should().BeFalse();
        ((JsRegExp) engine.Evaluate("moved")).HasDefaultRegExpExec.Should().BeFalse();
        ((JsRegExp) engine.Evaluate("sub")).HasDefaultRegExpExec.Should().BeFalse();

        engine.Execute("Object.setPrototypeOf(moved, RegExp.prototype);");
        ((JsRegExp) engine.Evaluate("moved")).HasDefaultRegExpExec.Should().BeTrue();
    }

    [Test]
    public void OwnExecOnTheInstance()
    {
        Run("""
            var r = /a/g, log = [];
            var warm = 'xa'.replace(r, 'b') + r.test('a') + 'a'.match(r) + 'a'.search(r);
            r.exec = function(s) { log.push('own:' + s); return null; };
            var results = ['xa'.replace(r, '[$&]'), r.test('a'), 'a'.match(r), 'a'.search(r), 'a-a'.split(r).length];
            delete r.exec;
            var after = 'xa'.replace(r, '[$&]');
            return warm + '|' + log.join(',') + '|' + JSON.stringify(results) + '|' + after;
            """).Should().Be("xbtruea0|own:xa,own:a,own:a,own:a|[\"xa\",false,null,-1,3]|x[a]");
    }

    [Test]
    public void ReplacedPrototypeExec()
    {
        Run("""
            var orig = RegExp.prototype.exec, log = [];
            for (var i = 0; i < 3; i++) { /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]'); }
            RegExp.prototype.exec = function(s) { log.push(this.source); return orig.call(this, s); };
            var results = [/a/.test('a'), 'xa'.replace(/(a)/g, '[$1]'), String('a'.match(/a/g)), 'a'.search(/a/), 'xay'.split(/a/).length];
            var during = log.length;
            RegExp.prototype.exec = orig;
            var results2 = [/a/.test('a'), 'xa'.replace(/(a)/g, '[$1]')];
            return JSON.stringify(results) + '|' + during + '|' + log.length + '|' + JSON.stringify(results2);
            """).Should().Be("[true,\"x[a]\",\"a\",0,2]|9|9|[true,\"x[a]\"]");
    }

    [Test]
    public void PrototypeExecRestoredToTheBuiltin()
    {
        Run("""
            var orig = RegExp.prototype.exec, calls = 0;
            /a/.test('a');
            RegExp.prototype.exec = function(s) { calls++; return orig.call(this, s); };
            /a/.test('a');
            RegExp.prototype.exec = orig;
            /a/.test('a'); 'a'.replace(/(a)/g, '$1');
            RegExp.prototype.exec = function(s) { calls += 10; return orig.call(this, s); };
            /a/.test('a');
            return String(calls);
            """).Should().Be("11");
    }

    [Test]
    public void PrototypeExecAsAnAccessor()
    {
        Run("""
            var orig = RegExp.prototype.exec, seen = [], r = /a/g;
            r.test('a'); 'xa'.replace(r, '[$&]');
            Object.defineProperty(RegExp.prototype, 'exec', { configurable: true, get: function() { seen.push(this === r); return orig; } });
            var results = [r.test('a'), 'xa'.replace(r, '[$&]'), String('a'.match(r))];
            var n = seen.length;
            Object.defineProperty(RegExp.prototype, 'exec', { configurable: true, writable: true, value: orig });
            var m = seen.length;
            r.test('a'); 'xa'.replace(r, '[$&]');
            return JSON.stringify(results) + '|' + n + '|' + seen.every(function(x) { return x; }) + '|' + (seen.length === m);
            """).Should().Be("[true,\"x[a]\",\"a\"]|5|true|true");
    }

    [Test]
    public void PrototypeExecRedefinedByValue()
    {
        Run("""
            var orig = RegExp.prototype.exec, calls = 0;
            'xa'.replace(/(a)/g, '[$1]'); /a/.test('a');
            Object.defineProperty(RegExp.prototype, 'exec', { value: function(s) { calls++; return orig.call(this, s); } });
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            var n = calls;
            Object.defineProperty(RegExp.prototype, 'exec', { value: orig });
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            return n + '|' + calls;
            """).Should().Be("3|3");
    }

    [Test]
    public void PrototypeExecDeleted()
    {
        Run("""
            var orig = RegExp.prototype.exec;
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            delete RegExp.prototype.exec;
            var results = [/a/.test('a'), 'xa'.replace(/(a)/g, '[$1]'), typeof /a/.exec];
            RegExp.prototype.exec = function(s) { results.push('custom'); return orig.call(this, s); };
            /a/.test('a');
            return JSON.stringify(results);
            """).Should().Be("[true,\"x[a]\",\"undefined\",\"custom\"]");
    }

    [Test]
    public void UnrelatedPrototypeChangesKeepTheDefault()
    {
        Run("""
            var orig = RegExp.prototype.exec, calls = 0;
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            RegExp.prototype.extra = 1;
            Object.defineProperty(RegExp.prototype, 'other', { value: 2, configurable: true });
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            RegExp.prototype.exec = function(s) { calls++; return orig.call(this, s); };
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            var n = calls;
            RegExp.prototype.exec = orig;
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            delete RegExp.prototype.extra; delete RegExp.prototype.other;
            RegExp.prototype.exec = function(s) { calls++; return orig.call(this, s); };
            /a/.test('a');
            return n + '|' + calls;
            """).Should().Be("3|4");
    }

    [Test]
    public void PrototypeExecWrittenWhileNonWritable()
    {
        Run("""
            'use strict';
            var orig = RegExp.prototype.exec, calls = 0, threw = false;
            /a/.test('a');
            Object.defineProperty(RegExp.prototype, 'exec', { writable: false });
            try { RegExp.prototype.exec = function(s) { calls++; return orig.call(this, s); }; } catch (e) { threw = e instanceof TypeError; }
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            return threw + '|' + calls + '|' + (RegExp.prototype.exec === orig);
            """).Should().Be("true|0|true");
    }

    [Test]
    public void FrozenPrototypeKeepsTheDefault()
    {
        Run("""
            var results = [];
            /a/.test('a'); 'xa'.replace(/(a)/g, '[$1]');
            Object.freeze(RegExp.prototype);
            results.push(/a/.test('a'), 'xa'.replace(/(a)/g, '[$1]'), Object.isFrozen(RegExp.prototype));
            return JSON.stringify(results);
            """).Should().Be("[true,\"x[a]\",true]");
    }

    [Test]
    public void SetPrototypeOfTheInstance()
    {
        Run("""
            var calls = [], r = /a/g;
            r.test('a'); 'xa'.replace(r, '[$&]');
            var proto = Object.create(RegExp.prototype, { exec: { value: function(s) { calls.push('proto'); return RegExp.prototype.exec.call(this, s); } } });
            Object.setPrototypeOf(r, proto);
            r.lastIndex = 0;
            var results = [r.test('a'), 'xa'.replace(r, '[$&]'), String('a'.match(r))];
            Object.setPrototypeOf(r, RegExp.prototype);
            var n = calls.length;
            r.lastIndex = 0;
            results.push('xa'.replace(r, '[$&]'));
            return JSON.stringify(results) + '|' + n + '|' + calls.length;
            """).Should().Be("[true,\"x[a]\",\"a\",\"x[a]\"]|5|5");
    }

    [Test]
    public void NullPrototypeInstanceStillMatches()
    {
        Run("""
            var r = /a/g;
            r.test('a'); r.lastIndex = 0;
            Object.setPrototypeOf(r, null);
            var results = [RegExp.prototype.test.call(r, 'a'), RegExp.prototype[Symbol.replace].call(r, 'xa', '[$&]'), typeof r.exec];
            return JSON.stringify(results);
            """).Should().Be("[true,\"x[a]\",\"undefined\"]");
    }

    [Test]
    public void SubclassOverridingExec()
    {
        Run("""
            var calls = 0;
            class S extends RegExp { exec(s) { calls++; return super.exec(s); } }
            var r = new S('a', 'g');
            var results = [r.test('a'), 'xa'.replace(r, '[$&]'), String('a'.match(r)), 'a'.search(r), 'xay'.split(r).length];
            return JSON.stringify(results) + '|' + calls;
            """).Should().Be("[true,\"x[a]\",\"a\",0,2]|9");
    }

    [Test]
    public void SubclassNotOverridingExec()
    {
        Run("""
            class S extends RegExp {}
            var r = new S('(a)', 'g');
            RegExp.prototype.exec = (function(orig) { return function(s) { return orig.call(this, s); }; })(RegExp.prototype.exec);
            var results = [r.test('a'), 'xa'.replace(r, '[$1]'), String('a'.match(r)), 'a'.search(r), 'xay'.split(r).join('|')];
            return JSON.stringify(results);
            """).Should().Be("[true,\"x[a]\",\"a\",0,\"x|a|y\"]");
    }

    [Test]
    public void ReplacedPrototypeExecSeenBySubclass()
    {
        Run("""
            var calls = 0, orig = RegExp.prototype.exec;
            class S extends RegExp {}
            var r = new S('(a)', 'g');
            r.test('a'); r.lastIndex = 0; 'xa'.replace(r, '[$1]');
            RegExp.prototype.exec = function(s) { calls++; return orig.call(this, s); };
            r.lastIndex = 0;
            var results = [r.test('a'), 'xa'.replace(r, '[$1]')];
            return JSON.stringify(results) + '|' + calls;
            """).Should().Be("[true,\"x[a]\"]|3");
    }

    [Test]
    public void OtherRealmsExecIsItsOwn()
    {
        Run("""
            var other = $262.createRealm().global;
            var mainOrig = RegExp.prototype.exec, otherOrig = other.RegExp.prototype.exec;
            var calls = { main: 0, other: 0 };
            var rm = /(a)/g, ro = new other.RegExp('(a)', 'g');
            function run() { rm.lastIndex = 0; ro.lastIndex = 0; return [rm.test('a'), 'xa'.replace(rm, '[$1]'), String.prototype.replace.call('xa', ro, '[$1]'), other.String.prototype.replace.call('xa', ro, '[$1]')].join(','); }
            var r0 = run();
            other.RegExp.prototype.exec = function(s) { calls.other++; return otherOrig.call(this, s); };
            var r1 = run();
            other.RegExp.prototype.exec = otherOrig;
            RegExp.prototype.exec = function(s) { calls.main++; return mainOrig.call(this, s); };
            var r2 = run();
            RegExp.prototype.exec = mainOrig;
            var r3 = run();
            return [r0 === r1, r1 === r2, r2 === r3].join(',') + '|' + JSON.stringify(calls);
            """).Should().Be("true,true,true|{\"main\":3,\"other\":4}");
    }

    [Test]
    public void OtherRealmsExecInstalledOnThisRealm()
    {
        Run("""
            var other = $262.createRealm().global;
            var calls = 0, orig = RegExp.prototype.exec, otherExec = other.RegExp.prototype.exec;
            /(a)/g.test('a');
            RegExp.prototype.exec = otherExec;
            var r = /(a)/g;
            var results = [r.test('a'), 'xa'.replace(/(a)/g, '[$1]')];
            RegExp.prototype.exec = orig;
            return JSON.stringify(results);
            """).Should().Be("[true,\"x[a]\"]");
    }
}
