#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="JsString.ConcatenatedString"/> is what <c>s += t</c> produces: an immutable value whose characters
/// live in chunks it shares with the value it was appended to, and whose flat text is memoized on the first read
/// that needs it. A host gets such a value back from <see cref="Engine.Evaluate(string, string, Jint.ScriptParsingOptions)"/>,
/// <see cref="Engine.GetValue(string)"/> or a property read, and may read it from several threads at once. Those
/// reads are safe without a fence only if the memo is the one field a read consults — so it must be either absent
/// or the whole text, at every point (sebastienros/jint#4171) — and only if nothing an append writes is something
/// a reader of an earlier value reads (sebastienros/jint#4259).
/// <para>
/// A race cannot be reproduced on demand, so these tests pin the representation that removes it, which any CPU
/// can check.
/// </para>
/// </summary>
public class ConcatenatedStringMemoTests
{
    [Test]
    public void AnAppendLeavesItsReceiverAsItWas()
    {
        using var engine = new Engine();

        var a = new JsString.ConcatenatedString("a");
        AssertMemoIsAbsentOrWhole(a, "a");

        // The first append starts the shared chunk, every later one writes into it.
        var ab = Append(engine, a, "b");
        var abc = Append(engine, ab, "c");
        AssertMemoIsAbsent(abc, "abc");
        AssertReadsAs(abc, "abc");

        var abcd = Append(engine, abc, "d");
        AssertMemoIsAbsent(abcd, "abcd");
        AssertReadsAs(abcd, "abcd");

        // Every value is still the text it was made with, memoized or not.
        AssertReadsAs(a, "a");
        AssertReadsAs(ab, "ab");
        AssertReadsAs(abc, "abc");
    }

    /// <summary>
    /// A value somebody already appended past, appended to again: the second append branches off rather than
    /// writing over the characters the first one wrote, and both results keep their own text.
    /// </summary>
    [Test]
    public void AppendingTwiceToOneValueBuildsTwoValues()
    {
        using var engine = new Engine();

        var ab = Append(engine, Append(engine, new JsString.ConcatenatedString("a"), "b"), "");
        var abc = Append(engine, ab, "c");
        var abx = Append(engine, ab, "x");
        var abxy = Append(engine, abx, "y");
        var abcd = Append(engine, abc, "d");

        AssertReadsAs(ab, "ab");
        AssertReadsAs(abc, "abc");
        AssertReadsAs(abx, "abx");
        AssertReadsAs(abxy, "abxy");
        AssertReadsAs(abcd, "abcd");
    }

    /// <summary>
    /// Characters past one chunk: the value reads across the chunks it spans, by index as well as whole, and an
    /// earlier value still reads only its own part of the chunk the later ones went on writing into.
    /// </summary>
    [Test]
    public void AValueSpanningChunksReadsAcrossThem()
    {
        using var engine = new Engine();

        var value = new JsString.ConcatenatedString("0");
        var expected = new System.Text.StringBuilder("0");
        JsString? early = null;
        for (var i = 1; i < 20_000; i++)
        {
            var piece = (i % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
            value = Append(engine, value, piece);
            expected.Append(piece);
            if (i == 100)
            {
                early = value;
            }
        }

        var text = expected.ToString();
        value.Length.Should().Be(text.Length);
        value[0].Should().Be('0');
        value[12_345].Should().Be(text[12_345]);
        value[text.Length - 1].Should().Be(text[text.Length - 1]);
        value.Equals(text).Should().BeTrue();
        value.Equals(new JsString(text)).Should().BeTrue();
        value.Equals(new JsString(text.Substring(0, text.Length - 1) + "!")).Should().BeFalse();
        AssertReadsAs(value, text);
        AssertReadsAs(early!, text.Substring(0, 101));
    }

    /// <summary>
    /// The one instance that changes: a value a binding owns, extended in place by its <c>+=</c> before anything
    /// has read it. The memo stays absent-or-whole through every step, from the flat first value through the
    /// chunks, and the first read that marks it shared ends the extensibility for good.
    /// </summary>
    [Test]
    public void AnOwnedValueExtendedInPlaceKeepsTheMemoAbsentOrWhole()
    {
        using var engine = new Engine();

        var value = (JsString.ConcatenatedString) new JsString("a").Append(engine.Realm, "b");
        value.IsExtensibleInPlace.Should().BeFalse("nothing owns a value until a binding claims it");

        value.TryClaimForBinding().Should().BeTrue();
        value.AppendInPlace(engine.Realm, "c");
        AssertMemoIsAbsent(value, "abc");

        for (var i = 0; i < 10_000; i++)
        {
            value.AppendInPlace(engine.Realm, "d");
        }

        var expected = "abc" + new string('d', 10_000);
        AssertMemoIsAbsent(value, expected);

        value.MarkShared();
        value.IsExtensibleInPlace.Should().BeFalse();
        value.TryClaimForBinding().Should().BeFalse("shared is final: a value once handed on is never claimed again");
        value.IsExtensibleInPlace.Should().BeFalse();
        AssertReadsAs(value, expected);

        // shared now, so an append makes a new value and leaves this one as it was
        var longer = Append(engine, value, "e");
        AssertReadsAs(longer, expected + "e");
        AssertReadsAs(value, expected);
    }

    /// <summary>
    /// The shape a host is handed: a script's <c>+=</c> result returned as the completion value, and read off
    /// an object property, before anything has read its text.
    /// </summary>
    [Test]
    public void AResultHandedToTheHostCarriesNoStaleMemo()
    {
        using var engine = new Engine();

        var fromEvaluate = engine.Evaluate("var s = 'a'; s += 'b'; s += 'c'; s");
        var fromProperty = engine.Evaluate("var o = { p: 'x' }; o.p += 'y'; o.p += 'z'; o").AsObject().Get("p");

        // The premise. Should a host ever be handed a flat copy instead, this test covers nothing; the two
        // above still pin the representation.
        fromEvaluate.Should().BeOfType<JsString.ConcatenatedString>("the host is handed the value += produced");
        fromProperty.Should().BeOfType<JsString.ConcatenatedString>("the host is handed the value += produced");

        AssertMemoIsAbsent((JsString) fromEvaluate, "abc");
        AssertMemoIsAbsent((JsString) fromProperty, "xyz");

        AssertReadsAs((JsString) fromEvaluate, "abc");
        AssertReadsAs((JsString) fromProperty, "xyz");
    }

    private static JsString.ConcatenatedString Append(Engine engine, JsString.ConcatenatedString value, string text)
    {
        var before = value.Length;
        var result = value.Append(engine.Realm, text);
        value.Length.Should().Be(before, "an append leaves its receiver as it was");
        if (text.Length > 0)
        {
            result.Should().NotBeSameAs(value, "an append returns a new value");
        }

        return result.Should().BeOfType<JsString.ConcatenatedString>().Subject;
    }

    /// <summary>
    /// The invariant, which the plain-load <c>ToString()</c> and the base class's field reads in equality and
    /// hashing all rely on: a memo that is present is returned as the text, so it must be the text. Reads only
    /// the field and the length, neither of which materializes.
    /// </summary>
    private static void AssertMemoIsAbsentOrWhole(JsString value, string expected)
    {
        var memo = value._value;
        if (memo is not null)
        {
            memo.Should().Be(expected, "a memo that is present is returned as the value without consulting the chunks");
        }

        value.Length.Should().Be(expected.Length);
    }

    /// <summary>
    /// A value an append produced has no memo until something reads its text.
    /// </summary>
    private static void AssertMemoIsAbsent(JsString value, string expected)
    {
        value._value.Should().BeNull("nothing has read the value's text yet");
        AssertMemoIsAbsentOrWhole(value, expected);
    }

    /// <summary>
    /// Reads through every member that materializes, then checks the read memoized the whole text. The
    /// comparisons go first because they answer from the chunks without materializing.
    /// </summary>
    private static void AssertReadsAs(JsString value, string expected)
    {
        var flat = new JsString(expected);

        value.Equals(expected).Should().BeTrue();
        value.Equals(flat).Should().BeTrue();
        flat.Equals(value).Should().BeTrue();
        value.ToString().Should().Be(expected);
        value.GetHashCode().Should().Be(flat.GetHashCode());

        value._value.Should().Be(expected, "a read memoizes the whole text");
    }
}
