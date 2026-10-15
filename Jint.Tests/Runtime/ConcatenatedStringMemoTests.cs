#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="JsString.ConcatenatedString"/> is the builder behind <c>s += t</c>: appends land in a
/// <see cref="System.Text.StringBuilder"/> and the flat text is memoized on the first read after them. A host
/// gets that instance back from <see cref="Engine.Evaluate(string, string, Jint.ScriptParsingOptions)"/>,
/// <see cref="Engine.GetValue(string)"/> or a property read with its appends still pending, and may read it
/// from several threads while the engine is idle. Those reads are safe without a fence only if the memo is
/// the one field a read consults — so it must be either absent or the whole text, at every point. A memo
/// left standing, stale, beside a "dirty" flag is what let a reader on a weakly ordered CPU see the flag
/// cleared before the new text and return the value from before the appends (sebastienros/jint#4171).
/// <para>
/// The race itself cannot be reproduced on demand — x64 keeps the two stores in order anyway — so these
/// tests pin the representation that removes it, which any CPU can check.
/// </para>
/// </summary>
public class ConcatenatedStringMemoTests
{
    [Test]
    public void AnAppendDropsTheMemoAndAReadRestoresTheWholeText()
    {
        using var engine = new Engine();

        var value = new JsString.ConcatenatedString("a");
        AssertMemoIsAbsentOrWhole(value, "a");

        // The first append creates the builder, every later one grows it.
        Append(engine, value, "b");
        AssertMemoIsAbsent(value, "ab");

        Append(engine, value, "c");
        AssertMemoIsAbsent(value, "abc");

        AssertReadsAs(value, "abc");

        Append(engine, value, "d");
        AssertMemoIsAbsent(value, "abcd");

        AssertReadsAs(value, "abcd");
    }

    /// <summary>
    /// <see cref="JsString.EnsureCapacity"/> builds the other shape: a builder from the start, so the first
    /// append grows an existing builder instead of creating one.
    /// </summary>
    [Test]
    public void AnAppendDropsTheMemoWhenTheBuilderIsPresized()
    {
        using var engine = new Engine();

        var value = (JsString.ConcatenatedString) new JsString("a").EnsureCapacity(16);
        AssertMemoIsAbsentOrWhole(value, "a");

        Append(engine, value, "b");
        AssertMemoIsAbsent(value, "ab");

        AssertReadsAs(value, "ab");
    }

    /// <summary>
    /// The shape a host is handed: a script's <c>+=</c> result with its appends still pending, returned as
    /// the completion value, and read off an object property.
    /// </summary>
    [Test]
    public void AResultHandedToTheHostWithAppendsPendingCarriesNoStaleMemo()
    {
        using var engine = new Engine();

        var fromEvaluate = engine.Evaluate("var s = 'a'; s += 'b'; s += 'c'; s");
        var fromProperty = engine.Evaluate("var o = { p: 'x' }; o.p += 'y'; o.p += 'z'; o").AsObject().Get("p");

        // The premise. Should a host ever be handed a flat copy instead, this test covers nothing; the two
        // above still pin the representation.
        fromEvaluate.Should().BeOfType<JsString.ConcatenatedString>("the host is handed the builder itself, appends pending");
        fromProperty.Should().BeOfType<JsString.ConcatenatedString>("the host is handed the builder itself, appends pending");

        AssertMemoIsAbsent((JsString) fromEvaluate, "abc");
        AssertMemoIsAbsent((JsString) fromProperty, "xyz");

        AssertReadsAs((JsString) fromEvaluate, "abc");
        AssertReadsAs((JsString) fromProperty, "xyz");
    }

    private static void Append(Engine engine, JsString.ConcatenatedString value, string text)
    {
        value.Append(engine.Realm, text).Should().BeSameAs(value, "a ConcatenatedString appends in place");
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
            memo.Should().Be(expected, "a memo that is present is returned as the value without consulting the builder");
        }

        value.Length.Should().Be(expected.Length);
    }

    /// <summary>
    /// Pending appends leave no memo at all: the previous text must not be left standing in front of them.
    /// </summary>
    private static void AssertMemoIsAbsent(JsString value, string expected)
    {
        value._value.Should().BeNull("an append must drop the memo rather than leave the previous text standing in front of it");
        AssertMemoIsAbsentOrWhole(value, expected);
    }

    /// <summary>
    /// Reads through every member that materializes, then checks the read memoized the whole text.
    /// </summary>
    private static void AssertReadsAs(JsString value, string expected)
    {
        var flat = new JsString(expected);

        value.ToString().Should().Be(expected);
        value.Equals(expected).Should().BeTrue();
        value.Equals(flat).Should().BeTrue();
        value.GetHashCode().Should().Be(flat.GetHashCode());

        value._value.Should().Be(expected, "a read memoizes the whole text");
    }
}
