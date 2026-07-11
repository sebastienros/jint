namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the bitwise identifier-operand lane (BitwiseIdentifierLane): unboxed
/// operand reads must be observably identical to the generic ToNumeric/ToInt32 path for every
/// operator and every operand class, and every non-int32 shape must decline to the generic path.
/// </summary>
public class BitwiseLaneTests
{
    [Fact]
    public void AllOperatorsMatchGenericSemanticsOverSlotNumbers()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var samples = [0, 1, 3, 5, 31, 32, 33, 1023, -1, -7, 2147483647, -2147483648];
                var parts = [];
                for (var i = 0; i < samples.length; i++) {
                    for (var j = 0; j < samples.length; j++) {
                        var x = samples[i];
                        var y = samples[j];
                        parts.push(x ^ y, x & y, x | y, x << y, x >> y, x >>> y);
                    }
                }
                return parts.join(',');
            })()
            """).AsString();

        // reference values computed with the generic path via Function-constructed code whose
        // operands are member reads (never identifier-shaped, so the lane cannot arm there)
        var engine2 = new Engine();
        var expected = engine2.Evaluate("""
            (function () {
                var samples = [0, 1, 3, 5, 31, 32, 33, 1023, -1, -7, 2147483647, -2147483648];
                var o = { x: 0, y: 0 };
                var parts = [];
                for (var i = 0; i < samples.length; i++) {
                    for (var j = 0; j < samples.length; j++) {
                        o.x = samples[i];
                        o.y = samples[j];
                        parts.push(o.x ^ o.y, o.x & o.y, o.x | o.y, o.x << o.y, o.x >> o.y, o.x >>> o.y);
                    }
                }
                return parts.join(',');
            })()
            """).AsString();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NonInt32OperandsDeclineToGenericConversion()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var frac = 3.7;
                var neg = -3.7;
                var nan = NaN;
                var inf = Infinity;
                var ninf = -Infinity;
                var big = 4294967298;   // 2^32 + 2 — beyond int32, ToInt32 wraps to 2
                var mask = 5;
                return [frac ^ mask, neg ^ mask, nan ^ mask, inf ^ mask, ninf ^ mask, big & -1,
                        frac << 1, big >>> 0].join(',');
            })()
            """).AsString();

        // ToInt32(3.7)=3, ToInt32(-3.7)=-3 (so -3^5 = 0xFFFFFFF8 = -8), ToInt32(NaN/±Inf)=0,
        // ToInt32(2^32+2)=2, ToUint32(2^32+2)=2
        Assert.Equal("6,-8,5,5,5,2,6,2", result);
    }

    [Fact]
    public void NegativeZeroBehavesLikePositiveZero()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var nz = -0;
                var x = 7;
                return (nz ^ x) + ':' + (1 / (nz | 0));
            })()
            """).AsString();

        // ToInt32(-0) = +0: xor gives 7, and (-0 | 0) is +0 so 1/(+0) = Infinity
        Assert.Equal("7:Infinity", result);
    }

    [Fact]
    public void BigIntOperandsKeepBigIntSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = 12345678901234567890n;
                var b = 987654321n;
                return (a ^ b).toString() + ':' + (a & b).toString();
            })()
            """).AsString();

        // reference values verified independently with System.Numerics.BigInteger
        Assert.Equal("12345678900808999523:706611344", result);

        var mixEx = Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            engine.Evaluate("(function () { var a = 1n; var b = 2; return a ^ b; })()"));
        Assert.Contains("BigInt", mixEx.Message);
    }

    [Fact]
    public void ValueOfCoercionRunsExactlyOncePerEvaluation()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var calls = 0;
                var box = { valueOf: function () { calls++; return 6; } };
                var mask = 3;
                var r = 0;
                for (var i = 0; i < 5; i++) {
                    r = box ^ mask;
                }
                return r + ':' + calls;
            })()
            """).AsString();

        // the lane declines (binding holds an object, not a number); generic path calls valueOf
        // once per evaluation — five iterations, five calls
        Assert.Equal("5:5", result);
    }

    [Fact]
    public void StringOperandsCoerceThroughGenericPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var s = '12';
                var x = 10;
                return (x ^ s) + ':' + (s << 1);
            })()
            """).AsString();

        Assert.Equal("6:24", result);
    }

    [Fact]
    public void LetConstAndGlobalShapesAllCompute()
    {
        var engine = new Engine();
        var viaLexical = engine.Evaluate("""
            (function () {
                let x = 12;
                const m = 10;
                let acc = 0;
                for (let i = 0; i < 3; i++) {
                    acc += x ^ m;
                }
                return acc;
            })()
            """).AsNumber();
        Assert.Equal(18, viaLexical);

        var viaGlobals = engine.Evaluate("""
            gx = 12; gm = 10; gacc = 0;
            for (var gi = 0; gi < 3; gi++) {
                gacc += gx ^ gm;
            }
            gacc;
            """).AsNumber();
        Assert.Equal(18, viaGlobals);
    }
}
