namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the build-time fused guard comparisons (`x === undefined`, `x === null`,
/// `typeof x === "literal"`, `x === true`, `x === "literal"` and the !== forms) against the
/// semantics of the generic strict-equality path they replace.
/// </summary>
public class GuardFusionTests
{
    [Test]
    public void UndefinedAndNullGuardsMatchGenericSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var u; var n = null; var s = 'x'; var zero = 0; var empty = '';
            [
                u === undefined, undefined === u, u !== undefined,
                n === null, null === n, n !== null,
                s === undefined, zero === null, empty === undefined,
                n === undefined, u === null
            ].join(',');
            """).AsString();

        result.Should().Be("true,true,false,true,true,false,false,false,false,false,false");
    }

    [Test]
    public void TypeofGuardsCoverEveryTypeofResult()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            [
                typeof undefined === 'undefined',
                typeof null === 'object',
                typeof true === 'boolean',
                typeof 1 === 'number',
                typeof 1n === 'bigint',
                typeof 'x' === 'string',
                typeof Symbol() === 'symbol',
                typeof (function () {}) === 'function',
                typeof {} === 'object',
                'string' === typeof 'x',
                typeof 'x' !== 'number'
            ].join(',');
            """).AsString();

        result.Should().Be("true,true,true,true,true,true,true,true,true,true,true");
    }

    [Test]
    public void TypeofUndeclaredIdentifierDoesNotThrow()
    {
        var engine = new Engine();
        engine.Evaluate("typeof notDeclaredAnywhere === 'undefined'").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof notDeclaredAnywhere !== 'undefined'").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void ImpossibleTypeofLiteralStillEvaluatesOperandOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var calls = 0;
            function probe() { calls++; return 'x'; }
            var matched = typeof probe() === 'bogus';
            [matched, calls].join(',');
            """).AsString();

        result.Should().Be("false,1");
    }

    [Test]
    public void TypeofClrFunctionIsFunction()
    {
        var engine = new Engine();
        engine.SetValue("clrCallback", new Func<int>(() => 42));
        engine.Evaluate("typeof clrCallback === 'function'").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof clrCallback === 'object'").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void FusedGuardsWorkAcrossAwaitSuspension()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function f() {
                var a = (await Promise.resolve(undefined)) === undefined;
                var b = (await Promise.resolve(null)) === null;
                var c = typeof (await Promise.resolve('s')) === 'string';
                return [a, b, c].join(',');
            }
            f();
            """).UnwrapIfPromise().AsString();

        result.Should().Be("true,true,true");
    }

    [Test]
    public void GuardsDriveIfStatementBooleanFastPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var log = [];
            var vals = [undefined, null, 'x', 5];
            for (var i = 0; i < vals.length; i++) {
                var v = vals[i];
                if (v === undefined) { log.push('u'); }
                else if (v === null) { log.push('n'); }
                else if (typeof v === 'string') { log.push('s'); }
                else { log.push('o'); }
            }
            log.join('');
            """).AsString();

        result.Should().Be("unso");
    }

    [Test]
    public void BooleanAndStringLiteralGuardsMatchGenericSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var t = true, f = false, one = 1, s = 'abc', empty = '', o = {};
            var boxedTrue = new Boolean(true), boxedString = new String('abc');
            var built = ['a', 'b', 'c'].join(''), concatenated = 'ab' + s.slice(2);
            [
                t === true, true === t, f === false, t !== true, f !== true,
                one === true, empty === false, o === true, boxedTrue === true, true !== boxedTrue,
                s === 'abc', 'abc' === s, s !== 'abc', s === 'ABC', empty === '', '' === empty,
                built === 'abc', concatenated === 'abc', 'abc' === concatenated,
                boxedString === 'abc', one === '1', t === 'true', o !== 'abc'
            ].join(',');
            """).AsString();

        result.Should().Be(
            "true,true,true,false,true," +
            "false,false,false,false,true," +
            "true,true,false,false,true,true," +
            "true,true,true," +
            "false,false,false,true");
    }

    [Test]
    public void LiteralGuardParityWithGenericPathAcrossOperandTypes()
    {
        // Cross-checks the fused `x === true` / `x === 'literal'` against the same comparison with the
        // literal held in a variable, which is not fused.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var trueVar = true, falseVar = false, stringVar = 's1';
            var samples = [undefined, null, 0, 1, '', 's1', 's10', true, false, NaN, {}, [], new Boolean(true), new String('s1')];
            var ok = true;
            for (var i = 0; i < samples.length; i++) {
                var v = samples[i];
                if ((v === true) !== (v === trueVar)) ok = false;
                if ((true === v) !== (trueVar === v)) ok = false;
                if ((v !== false) !== (v !== falseVar)) ok = false;
                if ((v === 's1') !== (v === stringVar)) ok = false;
                if (('s1' !== v) !== (stringVar !== v)) ok = false;
            }
            ok;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void LiteralGuardEvaluatesTheOperandOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var calls = 0;
            function probe(value) { calls++; return value; }
            var a = probe(true) === true;
            var b = 'x' !== probe('x');
            var c = probe(1) === false;
            [a, b, c, calls].join(',');
            """).AsString();

        result.Should().Be("true,false,false,3");
    }

    [Test]
    public void FusedLiteralGuardsWorkAcrossSuspension()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function f() {
                var a = (await Promise.resolve(true)) === true;
                var b = 'yes' === (await Promise.resolve('yes'));
                var c = (await Promise.resolve('no')) !== 'no';
                return [a, b, c].join(',');
            }
            f();
            """).UnwrapIfPromise().AsString();

        result.Should().Be("true,true,false");

        var generated = engine.Evaluate("""
            function* g() {
                var log = [];
                log.push((yield 1) === true);
                log.push('k' !== (yield 2));
                return log.join(',');
            }
            var it = g();
            it.next();
            it.next(true);
            it.next('k').value;
            """).AsString();

        generated.Should().Be("true,false");
    }

    [Test]
    public void LiteralGuardsDriveIfStatementBooleanFastPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var log = [];
            var vals = [true, false, 'on', 'off', 1, new Boolean(true), new String('on')];
            for (var i = 0; i < vals.length; i++) {
                var v = vals[i];
                if (v === true) { log.push('t'); }
                else if (false === v) { log.push('f'); }
                else if (v === 'on') { log.push('1'); }
                else if ('off' !== v) { log.push('x'); }
                else { log.push('0'); }
            }
            log.join('') + (vals[0] !== true ? '!' : '.') + ('on' === vals[2] && vals[1] === false ? '+' : '-');
            """).AsString();

        result.Should().Be("tf10xxx.+");
    }

    [Test]
    public void LiteralGuardsInBooleanContextsWorkAcrossSuspension()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function f() {
                var log = [];
                if ((await Promise.resolve(true)) === true) { log.push('a'); }
                if ('x' !== (await Promise.resolve('x'))) { log.push('wrong'); } else { log.push('b'); }
                var n = 0;
                while ((await Promise.resolve(n)) !== 2 && 'go' === 'go' + '') { n++; }
                return log.join('') + n;
            }
            f();
            """).UnwrapIfPromise().AsString();

        result.Should().Be("ab2");
    }

    [Test]
    public void LooseNullAndUndefinedGuardsMatchGenericSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var u; var n = null; var o = {}; var s = 'x'; var zero = 0; var empty = ''; var f = false; var nan = NaN;
            [
                // undefined / null are loosely equal to each other and to themselves (both orientations)
                u == undefined, undefined == u, n == null, null == n,
                u == null, null == u, n == undefined, undefined == n,
                null == undefined, undefined == null,
                // != negations
                u != undefined, n != null, null != undefined, o != null,
                // the classic false cases against null
                zero == null, empty == null, f == null, nan == null, o == null, s == null,
                // and against undefined
                zero == undefined, empty == undefined, f == undefined, o == undefined,
                // reversed operand order still declines for the non-nullish side
                null == zero, undefined == empty, null == o
            ].join(',');
            """).AsString();

        result.Should().Be("true,true,true,true," +
            "true,true,true,true," +
            "true,true," +
            "false,false,false,true," +
            "false,false,false,false,false,false," +
            "false,false,false,false," +
            "false,false,false");
    }

    [Test]
    public void LooseGuardParityWithGenericPathAcrossOperandTypes()
    {
        // Cross-checks the fused `x == null`/`x == undefined` against a runtime-computed generic
        // loose comparison (the ` == y` where y is a variable holding null/undefined is NOT fused,
        // because the variable is not the `undefined` identifier / `null` literal).
        var engine = new Engine();
        var result = engine.Evaluate("""
            var nullVar = null, undefVar = undefined;
            var samples = [undefined, null, 0, '', false, NaN, {}, [], 'x', 42];
            var ok = true;
            for (var i = 0; i < samples.length; i++) {
                var v = samples[i];
                if ((v == null) !== (v == nullVar)) ok = false;
                if ((v == undefined) !== (v == undefVar)) ok = false;
                if ((v != null) !== (v != nullVar)) ok = false;
                if ((v != undefined) !== (v != undefVar)) ok = false;
            }
            ok;
            """).AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void LooseGuardEvaluatesOnlyInterestingOperandOnce()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var calls = 0;
            function probe() { calls++; return undefined; }
            var a = probe() == null;   // true, calls once
            var b = null == probe();   // true, calls again
            var c = probe() != null;   // false, calls again
            [a, b, c, calls].join(',');
            """).AsString();

        result.Should().Be("true,true,false,3");
    }

    [Test]
    public void FusedLooseGuardsWorkAcrossAwaitSuspension()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function f() {
                var a = (await Promise.resolve(undefined)) == null;
                var b = (await Promise.resolve(null)) == undefined;
                var c = (await Promise.resolve('s')) != null;
                return [a, b, c].join(',');
            }
            f();
            """).UnwrapIfPromise().AsString();

        result.Should().Be("true,true,true");
    }

    [Test]
    public void LooseGuardsDriveIfStatementBooleanFastPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var log = [];
            var vals = [undefined, null, 'x', 5, {}];
            for (var i = 0; i < vals.length; i++) {
                var v = vals[i];
                if (v == null) { log.push('n'); }              // matches both undefined and null
                else if (typeof v === 'string') { log.push('s'); }
                else { log.push('o'); }
            }
            log.join('');
            """).AsString();

        result.Should().Be("nnsoo");
    }

    [Test]
    public void LooseNullEqualityHonorsHtmlDdaAnnexB()
    {
        var engine = new Engine();
        // an object with the [[IsHTMLDDA]] internal slot (the document.all shape)
        var htmlDDA = new Jint.Native.IsHTMLDDA(engine, engine.Realm);
        engine.SetValue("htmlDDA", (Jint.Native.JsValue) htmlDDA);

        // Annex B: loose equality with null/undefined is true (both orientations, both operators)
        engine.Evaluate("htmlDDA == null").AsBoolean().Should().BeTrue();
        engine.Evaluate("null == htmlDDA").AsBoolean().Should().BeTrue();
        engine.Evaluate("htmlDDA == undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("undefined == htmlDDA").AsBoolean().Should().BeTrue();
        engine.Evaluate("htmlDDA != null").AsBoolean().Should().BeFalse();
        engine.Evaluate("htmlDDA != undefined").AsBoolean().Should().BeFalse();

        // strict equality must remain false — the strict fusion path is untouched
        engine.Evaluate("htmlDDA === null").AsBoolean().Should().BeFalse();
        engine.Evaluate("htmlDDA === undefined").AsBoolean().Should().BeFalse();
        engine.Evaluate("htmlDDA !== null").AsBoolean().Should().BeTrue();

        // a plain object is never loosely equal to null/undefined
        engine.Evaluate("({}) == null").AsBoolean().Should().BeFalse();
        engine.Evaluate("({}) == undefined").AsBoolean().Should().BeFalse();
    }
}
