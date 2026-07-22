namespace Jint.Tests.Runtime;

/// <summary>
/// Relational comparisons against slot-stored numbers take an unboxed fast lane in both boolean
/// (loop/if test) and value positions; these pin the semantic corners against the materialized
/// path (NaN, non-number transitions, const bindings, shadowing).
/// </summary>
public class RelationalComparisonTests
{
    [Fact]
    public void NanComparisonsAreFalseInTestAndValuePositions()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("""
            function f() {
                var x = NaN;
                var r = '';
                for (var i = 0; i < 2; i++) {
                    if (x < 5) { r += 'a'; }
                    if (x > 5) { r += 'b'; }
                    if (x <= 5) { r += 'c'; }
                    if (x >= 5) { r += 'd'; }
                }
                return r + '|' + (x < 5) + (x > 5) + (x <= 5) + (x >= 5);
            }
            f();
            """).AsString();

        result.Should().Be("|falsefalsefalsefalse");
    }

    [Fact]
    public void ComparisonFallsBackWhenBindingTurnsNonNumeric()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("""
            function f() {
                var x = 10;
                var r = '';
                if (x < 20) { r += '1'; }
                x = 'zz';
                if (x < 20) { r += '2'; }
                x = '5';
                if (x < 20) { r += '3'; }
                return r;
            }
            f();
            """).AsString();

        result.Should().Be("13");
    }

    [Fact]
    public void ValuePositionsProduceBooleans()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("""
            function f() {
                var i = 5;
                var values = [];
                for (var n = 0; n < 2; n++) {
                    values.push(i < 5, i <= 5, i > 4, i >= 6);
                }
                return values.join(',');
            }
            f();
            """).AsString();

        result.Should().Be("false,true,true,false,false,true,true,false");
    }

    [Fact]
    public void ConstAndZeroEdgeCases()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("""
            function f() {
                const c = 1;
                var z = -0;
                var r = '';
                for (var i = 0; i < 2; i++) {
                    if (c < 2) { r += 'a'; }
                    if (z <= 0) { r += 'b'; }
                    if (z >= 0) { r += 'c'; }
                }
                return r;
            }
            f();
            """).AsString();

        result.Should().Be("abcabc");
    }

    [Fact]
    public void VariableBoundComparisons()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("""
            function f() {
                var n = 5, x = NaN, i = 3;
                var r = [];
                for (var k = 0; k < 2; k++) {
                    r.push(i < n, i > n, x < n, n >= i, n <= i);
                }
                n = 'zz';
                r.push(i < n);
                n = '10';
                r.push(i < n);
                return r.join(',');
            }
            f();
            """).AsString();

        result.Should().Be("true,false,false,true,false,true,false,false,true,false,false,true");
    }

    [Fact]
    public void CachedEvalComparisonRespectsBlockShadowing()
    {
        // the lane resolves through the shadow-aware slot-cache walk; a block-shadowed
        // binding must not compare against the outer variable's cached slot
        var engine = new Engine(static options => options.Strict());
        engine.Execute("""
            var log = [];
            function h() {
                var x = 1;
                eval("if (x < 5) { log.push('outer:' + x); }");
                eval("if (x < 5) { log.push('outer:' + x); }");
                { let x = 100; eval("if (x < 5) { log.push('inner-wrong'); } else { log.push('inner:' + x); }"); }
            }
            h();
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("outer:1,outer:1,inner:100");
    }
}
