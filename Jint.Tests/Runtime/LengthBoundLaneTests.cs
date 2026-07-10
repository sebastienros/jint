namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the member-bound arm of the comparison lane (`i &lt; arr.length` /
/// `i &lt; s.length` in JintBinaryExpression): the length is re-read live every iteration,
/// only JsArray and JsString bases engage, and every other base (plain objects with getters,
/// proxies, typed arrays, arguments, null/undefined) declines to the generic path with its
/// observable behavior intact.
/// </summary>
public class LengthBoundLaneTests
{
    [Fact]
    public void ArrayLengthBoundIsReadLiveWhenArrayGrows()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [0], n = 0;
                for (var i = 0; i < a.length; i++) {
                    n++;
                    if (a.length < 5) { a.push(i); }
                }
                return n + ':' + a.length;
            })()
            """).AsString();

        Assert.Equal("5:5", result);
    }

    [Fact]
    public void ArrayLengthBoundIsReadLiveWhenArrayShrinks()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [0, 1, 2, 3, 4, 5, 6, 7], n = 0;
                for (var i = 0; i < a.length; i++) {
                    n++;
                    if (i === 2) { a.length = 4; }
                }
                return n;
            })()
            """).AsNumber();

        Assert.Equal(4, result);
    }

    [Fact]
    public void StringLengthBoundCountsCharacters()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var s = 'hello', n = 0;
                for (var i = 0; i < s.length; i++) { n++; }
                return n;
            })()
            """).AsNumber();

        Assert.Equal(5, result);
    }

    [Fact]
    public void RopeAndSlicedStringLengthsWorkThroughTheLane()
    {
        var engine = new Engine();
        // += builds a ConcatenatedString (length from the builder) and substring produces a
        // SlicedString (stored length); both serve Length through the virtual without flattening
        var result = engine.Evaluate("""
            (function () {
                var s = '';
                for (var k = 0; k < 5; k++) { s += 'ab'; }
                var t = s.substring(2, 7);
                var n = 0;
                for (var i = 0; i < t.length; i++) { n++; }
                for (var j = 0; j < s.length; j++) { n++; }
                return n;
            })()
            """).AsNumber();

        Assert.Equal(15, result);
    }

    [Fact]
    public void BaseTypeChangeMidLoopIsFollowed()
    {
        var engine = new Engine();
        // the base slot is re-read every iteration: array (length 6) morphs into a string (length 3)
        var result = engine.Evaluate("""
            (function () {
                var a = [1, 2, 3, 4, 5, 6], n = 0;
                for (var i = 0; i < a.length; i++) {
                    n++;
                    if (i === 1) { a = 'xyz'; }
                }
                return n;
            })()
            """).AsNumber();

        Assert.Equal(3, result);
    }

    [Fact]
    public void PlainObjectLengthGetterStaysObservable()
    {
        var engine = new Engine();
        // a non-array base declines to the generic path, which must invoke the getter per test
        var result = engine.Evaluate("""
            (function () {
                var reads = 0;
                var o = { get length() { reads++; return 3; } };
                var n = 0;
                for (var i = 0; i < o.length; i++) { n++; }
                return n + ':' + reads;
            })()
            """).AsString();

        Assert.Equal("3:4", result);
    }

    [Fact]
    public void ProxyLengthTrapStaysObservable()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var traps = 0;
                var p = new Proxy([1, 2, 3], {
                    get: function (t, k) {
                        if (k === 'length') { traps++; }
                        return t[k];
                    }
                });
                var n = 0;
                for (var i = 0; i < p.length; i++) { n++; }
                return n + ':' + traps;
            })()
            """).AsString();

        Assert.Equal("3:4", result);
    }

    [Fact]
    public void UndefinedBaseStillThrowsTypeError()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var u;
                try {
                    for (var i = 0; i < u.length; i++) { }
                    return 'no-throw';
                } catch (e) {
                    return e instanceof TypeError ? 'TypeError' : 'other';
                }
            })()
            """).AsString();

        Assert.Equal("TypeError", result);
    }

    [Fact]
    public void TypedArrayAndArgumentsBasesDeclineButStayCorrect()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function f() {
                    var n = 0;
                    for (var i = 0; i < arguments.length; i++) { n++; }
                    return n;
                }
                var ta = new Int32Array(4), n = 0;
                for (var i = 0; i < ta.length; i++) { n++; }
                return n + ':' + f(1, 2, 3);
            })()
            """).AsString();

        Assert.Equal("4:3", result);
    }

    [Fact]
    public void GlobalBaseFallsBackToGenericPath()
    {
        var engine = new Engine();
        // top-level vars are global-object properties, never slots: the length arm declines
        // and the generic member read serves the bound
        var result = engine.Evaluate("""
            var ga = [1, 2, 3];
            var n = 0;
            for (var i = 0; i < ga.length; i++) { n++; }
            n;
            """).AsNumber();

        Assert.Equal(3, result);
    }

    [Fact]
    public void EqualityAgainstLengthTakesTheLaneShape()
    {
        var engine = new Engine();
        // the arm is shared by the relational and equality lanes
        var result = engine.Evaluate("""
            (function () {
                var a = [0, 1, 2], hits = 0;
                for (var i = 0; i <= a.length; i++) {
                    if (i === a.length) { hits++; }
                    if (i != a.length) { hits += 10; }
                }
                return hits;
            })()
            """).AsNumber();

        Assert.Equal(31, result);
    }

    [Fact]
    public void OptionalChainLengthIsNotClaimedByTheLane()
    {
        var engine = new Engine();
        // a?.length short-circuits to undefined for a null base; the probe rejects optional
        // member expressions so the short-circuit machinery stays in charge
        var result = engine.Evaluate("""
            (function () {
                var a = null, n = 0;
                for (var i = 0; i < (a?.length ?? 2); i++) { n++; }
                return n;
            })()
            """).AsNumber();

        Assert.Equal(2, result);
    }
}
