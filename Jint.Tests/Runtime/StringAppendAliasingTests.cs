namespace Jint.Tests.Runtime;

/// <summary>
/// A string is a value. Once <c>s += t</c> has produced one, every other place that came to hold it — a
/// variable it was copied to, a property, an array element, a collection key, whatever a built-in copied it
/// into — keeps that text when script appends to <c>s</c> again. The compound assignment used to append into
/// the instance the binding held, which was correct only while nothing else held it, and the copies that were
/// supposed to keep it so covered declarations, identifier assignments, arguments and returns but not a member
/// assignment or any built-in that copies values (sebastienros/jint#4259).
/// </summary>
public class StringAppendAliasingTests
{
    /// <summary>
    /// Each script builds a value with two appends — the first produces a value from one concatenation, the
    /// second grows it — copies it somewhere, appends to the source again, and evaluates to the copy.
    /// </summary>
    [TestCase("var s = 'a'; s += 'b'; var t = s; s += 'c'; t", "ab", TestName = "var declaration")]
    [TestCase("let s = 'a'; s += 'b'; const t = s; s += 'c'; t", "ab", TestName = "const declaration")]
    [TestCase("var s = 'a'; s += 'b'; var t; t = s; s += 'c'; t", "ab", TestName = "identifier assignment")]
    [TestCase("var s = 'a'; s += 'b'; var o = {}; o.p = s; s += 'c'; o.p", "ab", TestName = "member assignment")]
    [TestCase("var s = 'a'; s += 'b'; var o = {}; o['p'] = s; s += 'c'; o.p", "ab", TestName = "computed member assignment")]
    [TestCase("var s = 'a'; s += 'b'; var a = []; a[0] = s; s += 'c'; a[0]", "ab", TestName = "array element assignment")]
    [TestCase("var s = 'a'; s += 'b'; var a = [s]; s += 'c'; a[0]", "ab", TestName = "array literal")]
    [TestCase("var s = 'a'; s += 'b'; var o = { p: s }; s += 'c'; o.p", "ab", TestName = "object literal")]
    [TestCase("var s = 'a'; s += 'b'; var a = []; a.push(s); s += 'c'; a[0]", "ab", TestName = "argument")]
    [TestCase("var s = 'a'; s += 'b'; function f(x) { return function () { return x; }; } var g = f(s); s += 'c'; g()", "ab", TestName = "closure over a parameter")]
    [TestCase("var s = 'a'; s += 'b'; function f(x = s) { s += 'c'; return x; } f()", "ab", TestName = "default parameter")]
    [TestCase("var s = 'a'; s += 'b'; var f = (...r) => r; var r = f(s); s += 'c'; r[0]", "ab", TestName = "rest parameter")]
    [TestCase("var s = 'a'; s += 'b'; try { throw s; } catch (e) { s += 'c'; e }", "ab", TestName = "catch binding")]
    [TestCase("var s = 'a'; s += 'b'; function* g() { yield s; } var r = [...g()]; s += 'c'; r[0]", "ab", TestName = "generator spread")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = a.slice(); a[0] += 'c'; b[0]", "xb", TestName = "Array.prototype.slice")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = a.concat(); a[0] += 'c'; b[0]", "xb", TestName = "Array.prototype.concat")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = [...a]; a[0] += 'c'; b[0]", "xb", TestName = "array spread")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = Array.from(a); a[0] += 'c'; b[0]", "xb", TestName = "Array.from")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = a.filter(v => true); a[0] += 'c'; b[0]", "xb", TestName = "Array.prototype.filter")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = a.toSorted(); a[0] += 'c'; b[0]", "xb", TestName = "Array.prototype.toSorted")]
    [TestCase("var a = ['x']; a[0] += 'b'; var b = a.toReversed(); a[0] += 'c'; b[0]", "xb", TestName = "Array.prototype.toReversed")]
    [TestCase("var a = ['x', 'y']; a[0] += 'b'; var b = a.with(1, 'z'); a[0] += 'c'; b[0]", "xb", TestName = "Array.prototype.with")]
    [TestCase("var a = ['x']; a[0] += 'b'; var g = Object.groupBy(a, v => 'k'); a[0] += 'c'; g.k[0]", "xb", TestName = "Object.groupBy")]
    [TestCase("var a = ['x']; a[0] += 'b'; var f = (...r) => r; var r = f(...a); a[0] += 'c'; r[0]", "xb", TestName = "spread into a rest parameter")]
    [TestCase("var a = ['x']; a[0] += 'b'; var m = new Map([[1, a[0]]]); a[0] += 'c'; m.get(1)", "xb", TestName = "Map value from entries")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var b = Object.assign({}, o); o.p += 'c'; b.p", "xb", TestName = "Object.assign")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var b = { ...o }; o.p += 'c'; b.p", "xb", TestName = "object spread")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var b = Object.values(o); o.p += 'c'; b[0]", "xb", TestName = "Object.values")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var b = Object.entries(o); o.p += 'c'; b[0][1]", "xb", TestName = "Object.entries")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var b = Object.fromEntries(Object.entries(o)); o.p += 'c'; b.p", "xb", TestName = "Object.fromEntries")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var d = Object.getOwnPropertyDescriptor(o, 'p'); o.p += 'c'; d.value", "xb", TestName = "Object.getOwnPropertyDescriptor")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var { p } = o; o.p += 'c'; p", "xb", TestName = "object destructuring")]
    [TestCase("var o = { p: 'x' }; o.p += 'b'; var h = {}; h.v = o.p; o.p += 'c'; h.v", "xb", TestName = "property to property")]
    [TestCase("var o = { p: 'x' }; with (o) { p += 'b'; } var h = {}; h.v = o.p; with (o) { p += 'c'; } h.v", "xb", TestName = "with statement")]
    [TestCase("globalThis.g = 'a'; g += 'b'; var h = {}; h.v = g; g += 'c'; h.v", "ab", TestName = "global object property")]
    [TestCase("var s = 'a'; s += 'b'; var o = {}; o.p = s; o.p += 'c'; s", "ab", TestName = "appending to the copy leaves the source")]
    [TestCase("var s = 'a'; s += 'b'; var o = {}; o.p = s; s += 'c'; o.p += 'd'; s + '|' + o.p", "abc|abd", TestName = "both copies grow apart")]
    [TestCase("var t = 'a'.concat('b', 'c'); var o = {}; o.p = t; t += 'd'; o.p", "abc", TestName = "String.prototype.concat result")]
    [TestCase("var s = 'a'; s += 'b'; var get = function () { return s; }; s += 'c'; get()", "abc", TestName = "a closure still reads the binding")]
    public void ACopyKeepsItsTextWhenTheSourceIsAppendedTo(string script, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(script).AsString().Should().Be(expected);
    }

    /// <summary>
    /// A string used as a collection key is the key it was when it went in, whichever representation it has
    /// and whatever happens to the binding it came from.
    /// </summary>
    [TestCase("var s = 'a'; s += 'b'; var m = new Map(); m.set(s, 1); s += 'c'; [m.has('ab'), m.has('abc'), m.has(s), m.get('ab')].join()", "true,false,false,1", TestName = "Map key")]
    [TestCase("var s = 'a'; s += 'b'; var st = new Set([s]); s += 'c'; [st.has('ab'), st.has('abc'), st.has(s), st.size].join()", "true,false,false,1", TestName = "Set key")]
    [TestCase("var a = ['x']; a[0] += 'b'; var st = new Set(a); a[0] += 'c'; [st.has('xb'), st.has(a[0]), [...st][0]].join()", "true,false,xb", TestName = "Set from an array")]
    [TestCase("var s = 'a'; s += 'b'; var o = {}; o[s] = 1; s += 'c'; Object.keys(o)[0]", "ab", TestName = "property key")]
    [TestCase("var s = 'a'; s += 'b'; var m = new Map(); m.set(s, 1); var k = s; s += 'c'; m.delete(k) + ',' + m.size", "true,0", TestName = "deleting through the inserting value")]
    public void AKeyKeepsItsTextWhenTheSourceIsAppendedTo(string script, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(script).AsString().Should().Be(expected);
    }

    /// <summary>
    /// A <c>+=</c> to a global, a property or a String object copies a right operand whose flat text would be a large
    /// object, and which has none yet, from its chunks rather than flattening it
    /// (<see cref="Jint.Native.JsString.ConcatenatedString.CopiesThroughBuffer"/>). Each script makes a string past
    /// that length and checks the result and every copy against text built another way; the local, whose binding can
    /// own its string and whose lane flattens such an operand, is the control. <c>'ab'</c> doubled 16 times is
    /// 131,072 characters, past the 42,500 at which an operand qualifies. The string somebody appended past is built
    /// by small appends instead, so the chunk it ends in has room and the <c>'x'</c> appended past it lands there: an
    /// append to it that wrote into that chunk rather than branching off would overwrite the <c>'x'</c>.
    /// </summary>
    [TestCase("var s = 'ab'; for (var i = 0; i < 17; i++) { s += s; } s === 'ab'.repeat(1 << 17)", TestName = "doubling a global")]
    [TestCase("function f() { var s = 'ab'; for (var i = 0; i < 17; i++) { s += s; } return s; } f() === 'ab'.repeat(1 << 17)", TestName = "doubling a local")]
    [TestCase("var o = { p: 'ab' }; for (var i = 0; i < 17; i++) { o.p += o.p; } o.p === 'ab'.repeat(1 << 17)", TestName = "doubling a property")]
    [TestCase("var s = 'ab'; for (var i = 0; i < 16; i++) { s += s; } var t = s; s += s; t === 'ab'.repeat(1 << 16) && s === t + t", TestName = "a copy keeps its text")]
    [TestCase("var s = ''; for (var i = 0; i < 50000; i++) { s += 'ab'; } var u = s; s += 'x'; u += u; u === 'ab'.repeat(100000) && s === 'ab'.repeat(50000) + 'x'", TestName = "an operand somebody appended past")]
    [TestCase("var s = 'ab'; for (var i = 0; i < 16; i++) { s += s; } var w = new String(s); w += w; typeof w === 'string' && w === s + s", TestName = "a String object")]
    [TestCase("var s = 'ab'; for (var i = 0; i < 16; i++) { s += s; } var f = 'x'; f += s; f === 'x' + 'ab'.repeat(1 << 16)", TestName = "a flat receiver")]
    [TestCase("var s = 'ab'; for (var i = 0; i < 16; i++) { s += s; } var g = 'x'; g += 'y'; g += s; g === 'xy' + 'ab'.repeat(1 << 16)", TestName = "a receiver with its text and no chunk")]
    public void ALongOperandAppendsAsItsText(string script)
    {
        var engine = new Engine();

        engine.Evaluate(script).AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The copies compare and hash by content against every other representation of the same text.
    /// </summary>
    [Test]
    public void CopiesThatGrewApartStillCompareByContent()
    {
        var engine = new Engine();

        engine.Evaluate("""
            var a = ['x']; a[0] += 'b'; var b = a.slice(); a[0] += 'c';
            [b[0] === 'xb', a[0] === 'xbc', b[0] !== a[0], b[0] + 'c' === a[0], a[0].length, b[0].length].join()
            """).AsString().Should().Be("true,true,true,true,3,2");
    }
}
