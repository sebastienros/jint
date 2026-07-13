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

        Assert.Equal("true,true,false,true,true,false,false,false,false,false,false", result);
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

        Assert.Equal("true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void TypeofUndeclaredIdentifierDoesNotThrow()
    {
        var engine = new Engine();
        Assert.True(engine.Evaluate("typeof notDeclaredAnywhere === 'undefined'").AsBoolean());
        Assert.False(engine.Evaluate("typeof notDeclaredAnywhere !== 'undefined'").AsBoolean());
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

        Assert.Equal("false,1", result);
    }

    [Fact]
    public void TypeofClrFunctionIsFunction()
    {
        var engine = new Engine();
        engine.SetValue("clrCallback", new Func<int>(() => 42));
        Assert.True(engine.Evaluate("typeof clrCallback === 'function'").AsBoolean());
        Assert.False(engine.Evaluate("typeof clrCallback === 'object'").AsBoolean());
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

        Assert.Equal("true,true,true", result);
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

        Assert.Equal("unso", result);
    }
}
