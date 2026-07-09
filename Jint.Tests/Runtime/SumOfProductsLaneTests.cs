namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the sum-of-products arithmetic lane (JintBinaryExpression.SumOfProductsLane): assignments
/// whose right-hand side is a ± tree of products over numeric leaves compute on raw doubles and
/// box once, and must match the generic tree evaluation.
/// </summary>
public class SumOfProductsLaneTests
{
    [Fact]
    public void MatrixKernelMatchesManualComputation()
    {
        var engine = new Engine();
        // the MMulti shape: 4 products over chained dense reads, computed-member store
        var result = engine.Evaluate("""
            (function () {
                var m1 = [[1.5, 2, 3, 4], [5, 6, 7, 8]];
                var m2 = [[0.5, 1], [2, 3], [4, 5], [6, 7]];
                var m = [[], []];
                for (var i = 0; i < 2; i++) {
                    for (var j = 0; j < 2; j++) {
                        m[i][j] = m1[i][0] * m2[0][j] + m1[i][1] * m2[1][j] + m1[i][2] * m2[2][j] + m1[i][3] * m2[3][j];
                    }
                }
                return m.map(r => r.join(',')).join(';');
            })()
            """).AsString();

        // row0: [1.5*0.5+2*2+3*4+4*6, 1.5*1+2*3+3*5+4*7] = [40.75, 50.5]
        // row1: [5*0.5+6*2+7*4+8*6, 5*1+6*3+7*5+8*7] = [90.5, 114]
        Assert.Equal("40.75,50.5;90.5,114", result);
    }

    [Fact]
    public void MinusTermsAndConstantsAndScalarsWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var v = [2, 3, 4];
                var cos = 0.5, sin = 0.25;
                var out1 = [];
                out1[0] = v[1] * cos - v[2] * sin;
                out1[1] = v[1] * sin + v[2] * cos;
                out1[2] = v[0] * 2 - v[1] * 3 + v[2] * 0.5;
                return out1.join(',');
            })()
            """).AsString();

        Assert.Equal("0.5,2.75,-3", result); // 1.5-1=0.5; 0.75+2=2.75; 4-9+2=-3
    }

    [Fact]
    public void SpecialValuesFlowThroughIeeeSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [NaN, Infinity, -Infinity, 0, 5];
                var r = [];
                r[0] = a[0] * 2 + a[4] * 1;        // NaN
                r[1] = a[1] * 2 + a[4] * 1;        // Infinity
                r[2] = a[1] * 1 + a[2] * 1;        // Infinity + -Infinity = NaN
                r[3] = a[3] * 1 + a[3] * 1;        // 0
                return r.map(x => '' + x).join(',');
            })()
            """).AsString();

        Assert.Equal("NaN,Infinity,NaN,0", result);
    }

    [Fact]
    public void IntegerLeavesStayExact()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [3, 7, 11, 13];
                var r = [];
                r[0] = a[0] * a[1] + a[2] * a[3];             // 21 + 143 = 164
                r[1] = a[3] * 1000000 + a[2] * 1000;          // 13011000
                return r.join(',') + ':' + (r[0] === 164) + ':' + Number.isInteger(r[1]);
            })()
            """).AsString();

        Assert.Equal("164,13011000:true:true", result);
    }

    [Fact]
    public void NonNumericLeafDeclinesWithSingleEvaluation()
    {
        var engine = new Engine();
        // the array element is a string: the lane's pure probe declines before any effect and the
        // generic path performs the spec concatenation exactly once
        var result = engine.Evaluate("""
            (function () {
                var a = [2, 'x'];
                var r = [];
                r[0] = a[0] * 3 + a[1] * 2;   // 6 + NaN ('x'*2) = NaN
                r[1] = a[0] * 3 + a[0] * 2;   // 10
                return r.map(x => '' + x).join(',');
            })()
            """).AsString();

        Assert.Equal("NaN,10", result);
    }

    [Fact]
    public void GetterBaseDeclinesAndFiresExactlyOnce()
    {
        var engine = new Engine();
        // a Proxy-backed "array" can't take the dense fast read: the lane must decline BEFORE
        // touching it, so the generic evaluation's trap count stays exactly one per element read
        var result = engine.Evaluate("""
            (function () {
                var reads = 0;
                var target = [4, 5];
                var p = new Proxy(target, { get(t, k, r) { if (k === '0' || k === '1') { reads++; } return Reflect.get(t, k, r); } });
                var sink = [];
                sink[0] = p[0] * 2 + p[1] * 3;   // 8 + 15 = 23
                return sink[0] + ':' + reads;
            })()
            """).AsString();

        Assert.Equal("23:2", result);
    }

    [Fact]
    public void HoleyAndOutOfRangeReadsDecline()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [1, , 3];       // hole at 1 → undefined → NaN through multiplication
                var r = [];
                r[0] = a[0] * 2 + a[1] * 3;
                r[1] = a[0] * 2 + a[5] * 3;   // out of range → undefined
                return r.map(x => '' + x).join(',');
            })()
            """).AsString();

        Assert.Equal("NaN,NaN", result);
    }

    [Fact]
    public void NegativeZeroProductsFollowSpec()
    {
        var engine = new Engine();
        // 0 * -1 is -0 per spec; summing two negative zeros keeps -0. The raw-double lane follows
        // the spec exactly (Number::multiply is double multiplication).
        var result = engine.Evaluate("""
            (function () {
                var a = [0, -1];
                var r = [];
                r[0] = a[0] * a[1] + a[0] * a[1];
                return Object.is(r[0], -0) + ':' + Object.is(a[0] * a[1] + a[0] * 0, 0);
            })()
            """).AsString();

        Assert.Equal("true:true", result);
    }
}
