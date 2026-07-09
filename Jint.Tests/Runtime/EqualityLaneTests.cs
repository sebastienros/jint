namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the unboxed slot-number lane on ==/===/!=/!== (JintBinaryExpression):
/// the lane engages for slot-stored numbers compared against numeric constants or other slots,
/// and must reproduce the generic path exactly, declining for any non-number operand.
/// </summary>
public class EqualityLaneTests
{
    [Fact]
    public void SlotNumberEqualityMatchesSpecForSpecialValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var nan = NaN, pz = 0, nz = -0, one = 1;
                return [
                    nan === nan, nan !== nan, nan == nan, nan != nan,
                    pz === nz, pz == nz, pz !== nz,
                    one === 1, one == 1, one !== 1, one != 1
                ].join(',');
            })()
            """).AsString();

        Assert.Equal("false,true,false,true,true,true,false,true,true,false,false", result);
    }

    [Fact]
    public void LaneDeclinesWhenSlotTypeChangesMidLoop()
    {
        var engine = new Engine();
        // x starts as a number (lane engages), then becomes a string mid-loop; the
        // per-evaluation slot probe must fall back to the coercing path
        var result = engine.Evaluate("""
            (function () {
                var x = 1, hits = 0;
                for (var i = 0; i < 4; i++) {
                    if (x == 1) { hits++; }
                    if (i === 1) { x = '1'; }
                }
                return hits;
            })()
            """).AsNumber();

        Assert.Equal(4, result); // '1' == 1 keeps matching through loose coercion
    }

    [Fact]
    public void BigIntAndMixedOperandsBailToGenericEquality()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var big = 1n, other = 1n, num = 1, str = '1', undef;
                return [
                    big === other, big !== other, big == num, big === num,
                    str == num, str === num,
                    undef == null, undef === null
                ].join(',');
            })()
            """).AsString();

        Assert.Equal("true,false,true,false,true,false,true,false", result);
    }

    [Fact]
    public void EqualityLaneWorksInValuePositions()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = 2, b = 2, c = 3;
                var r1 = (a === b), r2 = (a !== c), r3 = (a == b), r4 = (a != c);
                return r1 && r2 && r3 && r4;
            })()
            """).AsBoolean();

        Assert.True(result);
    }
}
