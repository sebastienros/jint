namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the build-time fused guard comparisons (`x === undefined`, `x === null`,
/// `typeof x === "literal"` and the !== forms) against the semantics of the generic
/// strict-equality path they replace.
/// </summary>
public class GuardFusionTests
{
    [Fact]
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

    [Fact]
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

    [Fact]
    public void TypeofUndeclaredIdentifierDoesNotThrow()
    {
        var engine = new Engine();
        engine.Evaluate("typeof notDeclaredAnywhere === 'undefined'").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof notDeclaredAnywhere !== 'undefined'").AsBoolean().Should().BeFalse();
    }

    [Fact]
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

    [Fact]
    public void TypeofClrFunctionIsFunction()
    {
        var engine = new Engine();
        engine.SetValue("clrCallback", new Func<int>(() => 42));
        engine.Evaluate("typeof clrCallback === 'function'").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof clrCallback === 'object'").AsBoolean().Should().BeFalse();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void LooseNullEqualityHonorsHtmlDdaAnnexB()
    {
        var engine = new Engine();
        // an object with the [[IsHTMLDDA]] internal slot (the document.all shape)
        var htmlDDA = new Jint.Native.IsHTMLDDA(engine);
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
