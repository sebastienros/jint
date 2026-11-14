#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// The <c>Array.prototype</c> generics read every element with <c>HasProperty</c> and <c>Get</c> on the array as it
/// is when that element's turn comes (https://tc39.es/ecma262/#sec-array.prototype.foreach steps 6.b-6.c), so a
/// callback that shifts the elements is seen by every later step. Jint's read lane over a dense array kept the store
/// it found when the call began, and <c>unshift</c> replaces that store, so <c>forEach</c>, <c>every</c> and
/// <c>flatMap</c> went on reading elements the array no longer had (sebastienros/jint#4579). Every expected string is
/// Node's answer to the same script.
/// </summary>
public class ArrayReadLaneStoreReplacementTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "var a = [1, 2, 3], r = []; a.forEach(function (v, i) { r.push(v); if (i === 0) a.unshift(0); }); r.join()", "1,1,2" },
        { "var a = [1, , 3], r = []; a.forEach(function (v, i) { r.push(i + ':' + v); if (i === 0) a.unshift(0); }); r.join()", "0:1,1:1" },
        { "var a = [1, 2, 3], r = []; a.every(function (v, i) { r.push(v); if (i === 0) a.unshift(0); return true; }); r.join()", "1,1,2" },
        { "var b = [1, , 3], s = []; b.flatMap(function (v, i) { s.push(i + ':' + v); if (i === 0) b.unshift(0); return 1; }); s.join()", "0:1,1:1" },
        { "var a = [1, 2, 3], r = []; a.some(function (v, i) { r.push(v); if (i === 0) a.unshift(0); return false; }); r.join()", "1,1,2" },
        { "var a = [1, 2, 3], r = []; a.forEach(function (v, i) { r.push(v); if (i === 0) a.length = 1; }); r.join()", "1" },
        { "var a = [1, 2, 3], r = []; a.forEach(function (v, i) { r.push(v); if (i === 0) a[100000000] = 9; }); r.join()", "1,2,3" },
        { "var a = [1, , 3], r = []; a.forEach(function (v, i) { r.push(v); if (i === 0) Array.prototype[1] = 'p'; }); delete Array.prototype[1]; r.join()", "1,p,3" },
        { "var a = [1, 2, 3]; String(a.indexOf(1, 1)) + ',' + a.join()", "-1,1,2,3" },
        { "var a = [1, { toString() { a.unshift(0); return 'x'; } }, 3]; a.join()", "1,x,x" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReadsTheArrayAsItIsNow(string script, string expected)
    {
        new Engine().Evaluate(script).ToString().Should().Be(expected);
    }
}
