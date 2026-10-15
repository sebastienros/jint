#nullable enable

using Jint.Native;
using Jint.Tests.Runtime.Debugger;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>s += t</c> extends the string in place, without allocating a new value, while the declarative binding that
/// produced it is the only place that holds it (see <see cref="JsString.ConcatenatedString"/>). That is sound only
/// if every route a value takes out of a binding ends the ownership first. Each case here builds an owned string,
/// takes it out of its binding by one route, appends to the binding again, and checks the copy kept its text. Each
/// one fails if the marking on its route is removed.
/// </summary>
public class StringAppendInPlaceTests
{
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; var t = s; s += 'd'; return t; } f()", "abc", TestName = "identifier read of a slot")]
    [TestCase("function f() { var s = 'a'; var held = []; for (var i = 0; i < 3; i++) { s += 'b'; held.push(s); } s += 'c'; return held.join('|'); } f()", "ab|abb|abbb", TestName = "identifier read of a slot through its cached lane")]
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; var get = function () { return s; }; var t = get(); s += 'd'; return t; } f()", "abc", TestName = "closure read")]
    [TestCase("function f() { var s = 'a'; var get = function () { return s; }; var held = []; for (var i = 0; i < 3; i++) { s += 'b'; held.push(get()); } s += 'c'; return held.join('|'); } f()", "ab|abb|abbb", TestName = "closure read through its cached lane")]
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; var t = s; (function () { s += 'd'; })(); return t + '|' + s; } f()", "abc|abcd", TestName = "closure append")]
    [TestCase("'use strict'; String.prototype.self = function () { return this; }; function f() { var s = 'a'; s += 'b'; s += 'c'; var t = s.self(); s += 'd'; return t; } f()", "abc", TestName = "method receiver")]
    [TestCase("function f(s) { s += 'b'; s += 'c'; var t = arguments[0]; s += 'd'; return t; } f('a')", "abc", TestName = "mapped arguments object")]
    [TestCase("function f(s) { eval(''); s += 'b'; s += 'c'; var t = arguments[0]; s += 'd'; return t; } f('a')", "abc", TestName = "mapped arguments object, dictionary binding")]
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; var t = eval('s'); s += 'd'; return t; } f()", "abc", TestName = "direct eval reads a local")]
    [TestCase("function f() { eval(\"var s = 'a'; s += 'b'; s += 'c'; 0;\"); var t = s; s += 'd'; return t; } f()", "abc", TestName = "eval-declared variable")]
    [TestCase("function f() { var fns = []; for (let s = 'x', i = 0; i < 2; i++) { if (i === 0) { s += 'a'; } fns.push({ append: () => { s += 'z'; }, read: () => s }); } fns[0].append(); return fns[1].read() + '|' + fns[0].read(); } f()", "xa|xaz", TestName = "per-iteration let copy")]
    [TestCase("function f() { var s = 'a'; s += 'b'; var x = (s += 'c'); s += 'd'; return x; } f()", "abc", TestName = "value of the += expression assigned")]
    [TestCase("function f() { eval(''); var s = 'a'; s += 'b'; var x = (s += 'c'); s += 'd'; return x; } f()", "abc", TestName = "value of the += expression assigned, dictionary binding")]
    [TestCase("function f() { var s = 'a'; var x = (s += 'b'); s += 'c'; return x; } f()", "ab", TestName = "value of a += that created the string, assigned")]
    [TestCase("function f() { eval(''); var s = 'a'; var x = (s += 'b'); s += 'c'; return x; } f()", "ab", TestName = "value of a += that created the string, assigned, dictionary binding")]
    [TestCase("function id(v) { return v; } function f() { var s = 'a'; s += 'b'; var held = id(s += 'c'); s += 'd'; return held; } f()", "abc", TestName = "value of the += expression as an argument")]
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; var t = (s ??= 'q'); s += 'd'; return t; } f()", "abc", TestName = "value of a ??= that keeps the binding's string")]
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; var t = (s ||= 'q'); s += 'd'; return t; } f()", "abc", TestName = "value of a ||= that keeps the binding's string")]
    [TestCase("function f() { var s = 'a'; s += 'b'; var t; s += (t = s, 'c'); s += 'd'; return t; } f()", "ab", TestName = "right-hand side reads the slot")]
    [TestCase("function f() { eval(''); var s = 'a'; s += 'b'; var t; s += (t = s, 'c'); s += 'd'; return t; } f()", "ab", TestName = "right-hand side reads the dictionary binding")]
    [TestCase("function f() { eval(''); var s = 'a'; s += 'b'; s += (s = 'z', 'c'); return s; } f()", "abc", TestName = "right-hand side reassigns the dictionary binding")]
    [TestCase("function f() { var s = 'a'; s += 'b'; var t = s; s += ''; s += 'c'; return t; } f()", "ab", TestName = "an empty append claims nothing")]
    [TestCase("function f() { eval(''); var s = 'a'; s += 'b'; var t = s; s += ''; s += 'c'; return t; } f()", "ab", TestName = "an empty append claims nothing in a dictionary binding")]
    [TestCase("function f() { var held = 'a'.concat('b'); var s = held; s += ''; s += 'c'; return held; } f()", "ab", TestName = "an empty append to a string nothing owns")]
    [TestCase("function f() { eval(''); var held = 'a'.concat('b'); var s = held; s += ''; s += 'c'; return held; } f()", "ab", TestName = "an empty append to a string nothing owns, dictionary binding")]
    [TestCase("function f() { var t = ''; t += 'a'; t += 'b'; var held = t; var s = { valueOf() { return held; } }; s += ''; s += 'c'; return held; } f()", "ab", TestName = "an object whose valueOf returns a shared string")]
    [TestCase("function f() { eval(''); var t = ''; t += 'a'; t += 'b'; var held = t; var s = { valueOf() { return held; } }; s += ''; s += 'c'; return held; } f()", "ab", TestName = "an object whose valueOf returns a shared string, dictionary binding")]
    [TestCase("function f() { var t = ''; t += 'a'; t += 'b'; var held = t; var s = Object.create(null); s.toString = function () { return held; }; s += ''; s += 'c'; return held; } f()", "ab", TestName = "a null-prototype object whose toString returns a shared string")]
    [TestCase("function f() { var held = 'a'.concat('b'); var s = { valueOf() { return held; } }; s += ''; s += 'c'; return held; } f()", "ab", TestName = "an object whose valueOf returns a string nothing owns")]
    [TestCase("function f() { eval(''); var held = 'a'.concat('b'); var s = { valueOf() { return held; } }; s += ''; s += 'c'; return held; } f()", "ab", TestName = "an object whose valueOf returns a string nothing owns, dictionary binding")]
    public void ACopyTakenOutOfTheBindingKeepsItsText(string script, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(script).AsString().Should().Be(expected);
    }

    /// <summary>
    /// A generator's appends never extend in place (its frame can suspend, so its <c>+=</c> reads its operand as
    /// usual), and what it yields is a value like any other: a later append inside the generator, or by a closure
    /// over its variable, leaves every string already handed out as it was.
    /// </summary>
    [Test]
    public void StringsAGeneratorYieldedKeepTheirText()
    {
        var engine = new Engine();

        engine.Evaluate("""
            function* g() { var s = 'a'; s += 'b'; s += 'c'; yield [s, () => { s += 'X'; }]; s += 'd'; s += 'e'; yield s; }
            var it = g();
            var [first, append] = it.next().value;
            append();
            var second = it.next().value;
            append();
            first + '|' + second
            """).AsString().Should().Be("abc|abcXde");
    }

    /// <summary>
    /// A debugger reading a local through its scope hands the value to the host, so it ends the ownership too.
    /// </summary>
    [TestCase("function f() { var s = 'a'; s += 'b'; s += 'c'; debugger; s += 'd'; return s; } var r = f();", TestName = "{m}(slot binding)")]
    [TestCase("function f() { eval(''); var s = 'a'; s += 'b'; s += 'c'; debugger; s += 'd'; return s; } var r = f();", TestName = "{m}(dictionary binding)")]
    public void ADebuggerScopeReadKeepsItsText(string script)
    {
        JsValue? held = null;
        TestHelpers.TestAtBreak(
            engine => engine.Execute(script),
            (engine, info) => held = info.CurrentScopeChain[0].GetBindingValue("s"));

        held!.AsString().Should().Be("abc");
    }

    /// <summary>
    /// A binding that owned a string comes to hold a number: through a numeric compound assignment and an increment,
    /// and through a <c>+=</c> whose right-hand side makes the binding own a string after the lane read its number and
    /// before it stores the unboxed result, which leaves the owned flag on the number. Every read and append after
    /// that sees the number, whichever reader comes first (the identifier read's cached write-back, or the reference
    /// path's <c>ReadSlotSlow</c> for <c>typeof</c>, a closure or eval code), and the strings built from it later keep
    /// their text.
    /// </summary>
    [TestCase("function f() { var s = ''; s += '1'; s += '2'; s -= 0; s += 1; s++; return s; } f()", "14", TestName = "{m}(numeric compound assignment and increment)")]
    [TestCase("function f() { var s = 0; s += (function () { s = ''; s += 'a'; s += 'b'; return 1; })(); var a = s; var b = typeof s; var c = (function () { return s; })(); s += 'x'; s += 'y'; var u = s; s += 'z'; return a + '|' + b + '|' + c + '|' + u + '|' + s; } f()", "1|number|1|1xy|1xyz", TestName = "{m}(read back through the identifier's write-back, then typeof and a closure)")]
    [TestCase("function f() { var s = 0; s += (function () { s = ''; s += 'a'; s += 'b'; return 1; })(); var b = typeof s; var a = s; s += 'x'; return b + '|' + a + '|' + s; } f()", "number|1|1x", TestName = "{m}(read first through typeof)")]
    [TestCase("function f() { var s = 0; s += (function () { s = ''; s += 'a'; s += 'b'; return 1; })(); var d = (function () { return eval('s'); })(); s += 'x'; return d + '|' + s; } f()", "1|1x", TestName = "{m}(read first by eval code)")]
    [TestCase("function f() { var s = 0; s += (function () { s = ''; s += 'a'; s += 'b'; return 1; })(); var t = s; s += 'x'; s += 'y'; var u = s; s += 'z'; return typeof t + ':' + t + '|' + u + '|' + s; } f()", "number:1|1xy|1xyz", TestName = "{m}(right-hand side makes the binding own a string)")]
    [TestCase("function f() { var s = 0; s += (function () { s = ''; s += 'a'; s += 'b'; return 1; })(); var v = (s += 'x'); s += 'y'; var u = s; s += 'z'; return v + '|' + u + '|' + s; } f()", "1x|1xy|1xyz", TestName = "{m}(then a += whose value is used)")]
    [TestCase("function f() { var s = 0; s += (function () { s = ''; s += 'a'; s += 'b'; return 1; })(); s++; s += 2; return s; } f()", "4", TestName = "{m}(then numeric updates)")]
    public void ABindingThatOwnedAStringCanHoldANumber(string script, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(script).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The premise of every case above, and the guard against a later change silently falling back to a new value
    /// per append: a one-character append to a function local, or to a variable a direct eval declared, allocates
    /// only its share of the characters' storage.
    /// </summary>
    [TestCase("function f(n) { var s = ''; for (var i = 0; i < n; i++) { s += 'x'; } return s.length; } f(n)", TestName = "{m}(slot binding)")]
    [TestCase("function outer() { eval(\"var s = ''\"); return function (n) { for (var i = 0; i < n; i++) { s += 'x'; } return s.length; }; } var f = outer(); f(n)", TestName = "{m}(eval-declared dictionary binding)")]
    [TestCase("function f(n) { var s = 'x'; for (var i = 0; i < s.length && i < n; i++) { s += 'x'; } return s.length; } f(n)", TestName = "{m}(loop bounded by the string's length)")]
    public void AnAppendToABindingThatOwnsItsStringAllocatesNoValue(string script)
    {
        var perAppend = AllocatedBytesPerAppend(script);
        if (perAppend is null)
        {
            return;
        }

        // A value object is 48 bytes on 64-bit; the characters cost two bytes each plus their chunks' headers.
        perAppend.Value.Should().BeLessThan(12, "the append extends the string the binding owns rather than allocating a value");
    }

    /// <summary>
    /// The other side: a property is not a binding, so nothing can prove it is the only holder of its string, and each
    /// append allocates a new value. If this ever measures near zero, something extended a string anyone could hold.
    /// </summary>
    [Test]
    public void AnAppendToAPropertyAllocatesANewValue()
    {
        var perAppend = AllocatedBytesPerAppend("function f(n) { var o = { p: '' }; for (var i = 0; i < n; i++) { o.p += 'x'; } return o.p.length; } f(n)");
        if (perAppend is null)
        {
            return;
        }

        perAppend.Value.Should().BeInRange(40, 64, "each append to a property allocates one immutable value");
    }

    private static double? AllocatedBytesPerAppend(string script)
    {
        if (!GCPolyfills.AllocatedBytesForCurrentThreadIsSupported)
        {
            // true on every runtime this suite executes on; see StringConcatenationTests
            return null;
        }

        var engine = new Engine();
        var prepared = Engine.PrepareScript(script);

        // warm the handler tree and the call-site caches, so what is measured is the loop and not reaching it
        engine.SetValue("n", 64);
        engine.Evaluate(prepared);

        long Measure(int iterations)
        {
            engine.SetValue("n", iterations);
            GC.Collect();
            GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var before);
            engine.Evaluate(prepared);
            GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var after);
            return after - before;
        }

        var small = Measure(20_000);
        var large = Measure(40_000);
        return (double) (large - small) / 20_000;
    }
}
