namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the dense-append fast path (ArrayInstance.TryAppendDense): computed-index writes at exactly
/// the current length append in place, and every restricted state defers to the full spec path.
/// </summary>
public class ArrayAppendLaneTests
{
    [Fact]
    public void AppendsBuildDenseArraysWithCorrectLength()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var m = [];
                for (var i = 0; i < 10; i++) { m[i] = i * 2; }
                var lazy = new Array(3);
                lazy[3] = 'x';
                var viaLength = [];
                viaLength[viaLength.length] = 'a';
                viaLength[viaLength.length] = 'b';
                return m.join(',') + '|' + m.length + '|' + lazy.length + '|' + lazy[0] + '|' + viaLength.join('');
            })()
            """).AsString();

        Assert.Equal("0,2,4,6,8,10,12,14,16,18|10|4|undefined|ab", result);
    }

    [Fact]
    public void NonExtensibleAndNonWritableLengthStillThrowInStrictMode()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                'use strict';
                var r = [];
                var sealedArr = [1];
                Object.preventExtensions(sealedArr);
                try { sealedArr[1] = 2; r.push('no-throw'); } catch (e) { r.push(e instanceof TypeError); }
                var fixedLen = [1];
                Object.defineProperty(fixedLen, 'length', { writable: false });
                try { fixedLen[1] = 2; r.push('no-throw'); } catch (e) { r.push(e instanceof TypeError); }
                var frozen = Object.freeze([]);
                try { frozen[0] = 1; r.push('no-throw'); } catch (e) { r.push(e instanceof TypeError); }
                return r.join(',') + '|' + sealedArr.length + '|' + fixedLen.length + '|' + frozen.length;
            })()
            """).AsString();

        Assert.Equal("true,true,true|1|1|0", result);
    }

    [Fact]
    public void HoleFillAndOutOfOrderWritesKeepSpecBehavior()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [];
                a[2] = 'c';        // out-of-order: not an append (length 0 -> 3 with holes)
                a[0] = 'a';        // hole fill below length
                var holes = (1 in a) ? 'no-hole' : 'hole';
                return a.length + '|' + a[0] + '|' + a[2] + '|' + holes;
            })()
            """).AsString();

        Assert.Equal("3|a|c|hole", result);
    }

    [Fact]
    public void AppendAfterCapacityGrowthKeepsElements()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var a = [];
                for (var i = 0; i < 100; i++) { a[i] = i; }
                var ok = true;
                for (var j = 0; j < 100; j++) { if (a[j] !== j) { ok = false; } }
                return ok + '|' + a.length;
            })()
            """).AsString();

        Assert.Equal("true|100", result);
    }
}
