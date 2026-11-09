namespace Jint.Tests.Runtime;

/// <summary>
/// Relational comparisons against slot-stored numbers take an unboxed fast lane in both boolean
/// (loop/if test) and value positions; these pin the semantic corners against the materialized
/// path (NaN, non-number transitions, const bindings, shadowing).
/// </summary>
public class RelationalComparisonTests
{
    [Test]
    public void NanComparisonsAreFalseInTestAndValuePositions()
    {
        var engine = new Engine(static options => options.Strict = true);
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

    [Test]
    public void ComparisonFallsBackWhenBindingTurnsNonNumeric()
    {
        var engine = new Engine(static options => options.Strict = true);
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

    [Test]
    public void ValuePositionsProduceBooleans()
    {
        var engine = new Engine(static options => options.Strict = true);
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

    [Test]
    public void ConstAndZeroEdgeCases()
    {
        var engine = new Engine(static options => options.Strict = true);
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

    [Test]
    public void VariableBoundComparisons()
    {
        var engine = new Engine(static options => options.Strict = true);
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

    [Test]
    public void NumbersReadFromPropertiesAndElementsCompareLikeTheGenericPath()
    {
        // Property and element reads are not slot-stored, so they reach the evaluated-operand number
        // lane rather than the slot lane: integers, doubles, mixed, -0, infinities and NaN.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { i: 3, j: 5, d: 2.5, nz: -0, inf: Infinity, ninf: -Infinity, nan: NaN };
            var a = [3, 2.5, -0, NaN];
            [
                o.i < o.j, o.i > o.j, o.i <= o.i, o.i >= o.j,
                o.d < o.i, a[1] > a[0], a[0] <= o.d, o.d >= a[1],
                o.nz < 0, o.nz <= 0, a[2] >= 0, 0 > a[2],
                o.ninf < o.i, o.inf > o.j, o.inf <= o.inf, o.ninf >= o.inf,
                o.nan < o.i, a[3] > o.i, o.nan <= o.nan, a[3] >= a[3]
            ].join(',');
            """).AsString();

        result.Should().Be(
            "true,false,true,false," +
            "true,false,false,true," +
            "false,true,true,false," +
            "true,true,true,false," +
            "false,false,false,false");
    }

    [Test]
    public void NonNumberOperandsStillTakeTheGenericPath()
    {
        // Only two number operands take the lane: strings compare by code units, objects convert
        // through valueOf, and a number against a string converts the string.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var calls = 0;
            var o = { valueOf: function () { calls++; return 4; } };
            var s = { a: '10', b: '9' };
            [s.a < s.b, s.a > 9, o < 5, o >= 4, 3 <= o, calls].join(',');
            """).AsString();

        result.Should().Be("true,true,true,true,true,3");
    }

    [Test]
    public void NumbersReadFromPropertiesDriveBooleanContexts()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var o = { i: 3, d: 2.5, nz: -0, nan: NaN };
            var log = [];
            var pairs = [[o.i, o.d], [o.d, o.i], [o.nz, 0], [o.nan, o.i]];
            for (var k = 0; k < pairs.length; k++) {
                var p = pairs[k];
                log.push((p[0] < p[1] ? 'l' : '-') + (p[0] > p[1] ? 'g' : '-') + (p[0] <= p[1] ? 'L' : '-') + (p[0] >= p[1] ? 'G' : '-'));
            }
            var n = 0;
            while (o.d < o.i) { o.d++; n++; }
            if (!(o.nan >= o.nan) && o.nz <= o.nz) { log.push('nan-and-zero'); }
            log.join(',') + '|' + n;
            """).AsString();

        result.Should().Be("-g-G,l-L-,--LG,----,nan-and-zero|1");
    }

    [Test]
    public void NumberComparisonsAcrossSuspensionInBooleanContexts()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function f() {
                var o = { i: 1 };
                var n = 0;
                while (o.i < (await Promise.resolve(4))) { o.i++; n++; }
                var r = (await Promise.resolve(2.5)) >= o.i ? 'ge' : 'lt';
                return n + ',' + r;
            }
            f();
            """).UnwrapIfPromise().AsString();

        result.Should().Be("3,lt");
    }

    [Test]
    public void NumberComparisonsAreUnchangedWithOperatorOverloadingAllowed()
    {
        var engine = new Engine(static options => options.Interop.AllowOperatorOverloading = true);
        var result = engine.Evaluate("""
            var o = { i: 3, d: 2.5, nan: NaN };
            [
                o.i < o.d, o.i > o.d, o.i <= o.i, o.d >= o.i,
                o.nan < o.i, o.nan >= o.nan,
                o.i < o.d ? 'y' : 'n', o.d <= o.i ? 'y' : 'n'
            ].join(',');
            """).AsString();

        result.Should().Be("false,true,true,false,false,false,n,y");
    }

    [Test]
    public void CachedEvalComparisonRespectsBlockShadowing()
    {
        // the lane resolves through the shadow-aware slot-cache walk; a block-shadowed
        // binding must not compare against the outer variable's cached slot
        var engine = new Engine(static options => options.Strict = true);
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
