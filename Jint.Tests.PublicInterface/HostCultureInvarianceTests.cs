using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Jint runs inside a host process whose <see cref="CultureInfo.CurrentCulture"/> is none of Jint's business:
/// an ASP.NET request can set it per user, a desktop app inherits it from the OS. None of it may reach what a
/// script sees. Everything asserted here is machine-readable output — JSON, ISO 8601 dates, stack traces,
/// <c>Number.prototype</c> conversions — which ECMAScript specifies down to the character, so the only correct
/// answer under every culture is the same ASCII one.
///
/// <para>
/// The failure this pins is not hypothetical. Several ICU locales — sv-SE, fi-FI, nb-NO, lt-LT, et-EE — spell
/// <c>NumberFormatInfo.NegativeSign</c> as U+2212 MINUS SIGN rather than U+002D, and ar-SA prefixes it with
/// U+061C ARABIC LETTER MARK. A formatting call that took its provider from the ambient culture therefore made
/// <c>JSON.stringify(-1)</c> emit a string no JSON parser accepts, and did so inconsistently: integers went
/// through the cultured path while doubles did not, so <c>{"a":-1,"b":-2.5}</c> came back with one of its two
/// minus signs replaced.
/// </para>
///
/// <para>
/// These tests live in the public-interface suite because that is the vantage point that matters — an embedder
/// sets a culture and calls <see cref="Engine.Evaluate(string)"/>, and nothing here needs internals. They run on
/// net472 as well as net10.0, which is what covers both halves of <c>ValueStringBuilder</c>'s target-framework
/// split.
/// </para>
/// </summary>
// CultureInfo.CurrentCulture is ambient state, so this class must not run beside anything else: it is
// restored in a finally, but a test running concurrently would observe it in between. Same reasoning, and
// the same shape, as GarbageCollectionTests over in Jint.Tests.
[NonParallelizable]
public class HostCultureInvarianceTests
{
    /// <summary>
    /// A culture built in the test rather than looked up, whose negative sign is U+2212 and whose decimal
    /// separator is a comma whatever the machine says. The named cultures below only carry a non-ASCII sign
    /// under ICU: net472 resolves them through NLS, and a host can put the whole process into invariant
    /// globalization mode. Without this row the theory would pass vacuously on exactly the configurations it
    /// exists to cover, and it would also be hostage to a CLDR release changing sv-SE's mind.
    /// </summary>
    private const string Hostile = "<hostile>";

    public static TestCases<string> Cultures() =>
    [
        "en-US",
        "sv-SE",
        "fi-FI",
        "ar-SA",
        "de-DE",
        Hostile,
    ];

    [TestCaseSource(nameof(Cultures))]
    public void JsonStringifyOfNegativeNumbersIsAscii(string cultureName)
    {
        RunUnderCulture(cultureName, engine =>
        {
            // Bare, nested in an object, and inside an array. The three used to disagree with each other.
            AssertEvaluates(engine, "JSON.stringify(-1)", "-1");
            AssertEvaluates(engine, "JSON.stringify([-1,-2,-3])", "[-1,-2,-3]");
            AssertEvaluates(engine, "JSON.stringify({a:-1,b:[-2,-3],c:{d:-42}})", """{"a":-1,"b":[-2,-3],"c":{"d":-42}}""");

            // The inconsistency that made the bug obvious: -1 took the integer path and -2.5 did not.
            AssertEvaluates(engine, "JSON.stringify({a:-1,b:-2.5,c:[-3]})", """{"a":-1,"b":-2.5,"c":[-3]}""");

            // Widths either side of Int32, since int and long format through separate BCL entry points.
            AssertEvaluates(engine, "JSON.stringify([-2147483648,-2147483649,-9007199254740991])", "[-2147483648,-2147483649,-9007199254740991]");
        });
    }

    [TestCaseSource(nameof(Cultures))]
    public void ErrorStackLineAndColumnAreAscii(string cultureName)
    {
        RunUnderCulture(cultureName, engine =>
        {
            var stack = engine.Evaluate("function a(){b()} function b(){throw new Error('x')} try{a()}catch(e){e.stack}").AsString();

            AssertAscii(stack, "e.stack");
            // Both frames must carry a machine-parseable file:line:column, which is what a source-map
            // consumer reads back out of a stack trace.
            Regex.Matches(stack, @":\d+:\d+").Count.Should().Be(3, "each of the three frames reports line:column");
        });
    }

    [TestCaseSource(nameof(Cultures))]
    public void ToIsoStringIsAscii(string cultureName)
    {
        RunUnderCulture(cultureName, engine =>
        {
            AssertEvaluates(engine, "new Date(0).toISOString()", "1970-01-01T00:00:00.000Z");
            AssertEvaluates(engine, "new Date(Date.UTC(2020,0,1,2,3,4,5)).toISOString()", "2020-01-01T02:03:04.005Z");

            // A negative year is the only hole in this format that can carry a sign.
            AssertEvaluates(engine, "new Date(Date.UTC(-1,0,1)).toISOString()", "-000001-01-01T00:00:00.000Z");
            AssertEvaluates(engine, "new Date(-8639999999999999).toISOString()", "-271821-04-20T00:00:00.001Z");

            // And the expanded year above 9999, whose sign this code writes itself.
            AssertEvaluates(engine, "new Date(8639999999999999).toISOString()", "+275760-09-12T23:59:59.999Z");

            // toJSON and JSON.stringify both go through toISOString.
            AssertEvaluates(engine, "JSON.stringify(new Date(Date.UTC(-1,0,1)))", "\"-000001-01-01T00:00:00.000Z\"");
        });
    }

    [TestCaseSource(nameof(Cultures))]
    public void ToExponentialIsAscii(string cultureName)
    {
        RunUnderCulture(cultureName, engine =>
        {
            AssertEvaluates(engine, "(0.0001).toExponential()", "1e-4");
            AssertEvaluates(engine, "(0.0001).toExponential(3)", "1.000e-4");
            AssertEvaluates(engine, "(-1.5e21).toExponential()", "-1.5e+21");
            AssertEvaluates(engine, "(123.456).toPrecision(2)", "1.2e+2");
            AssertEvaluates(engine, "(-1.5).toFixed(1)", "-1.5");
        });
    }

    [TestCaseSource(nameof(Cultures))]
    public void NumberToStringWithRadixIsAscii(string cultureName)
    {
        RunUnderCulture(cultureName, engine =>
        {
            AssertEvaluates(engine, "(-1).toString()", "-1");
            AssertEvaluates(engine, "(-1).toString(2)", "-1");
            AssertEvaluates(engine, "(-255).toString(16)", "-ff");
            AssertEvaluates(engine, "(-1.5).toString(36)", "-1.i");
            AssertEvaluates(engine, "(-1e21).toString()", "-1e+21");
            AssertEvaluates(engine, "(-1e-7).toString()", "-1e-7");
            AssertEvaluates(engine, "String(-1)", "-1");
            AssertEvaluates(engine, "[-1,-2].join()", "-1,-2");
        });
    }

    /// <summary>
    /// Unlike everything above, an <c>Intl.NumberFormat</c>'s output is <em>supposed</em> to be locale-sensitive:
    /// PartitionNotationSubPattern takes the exponent's minus sign from locale data
    /// (<see href="https://tc39.es/ecma402/#sec-partitionnotationsubpattern"/>). The locale it must come from is
    /// the one the object resolved, though — never the host process's. So the assertion here is not that the
    /// output is ASCII, but that it does not move when the ambient culture does.
    /// </summary>
    [TestCaseSource(nameof(Cultures))]
    public void IntlNumberFormatExponentFollowsTheResolvedLocaleNotTheAmbientOne(string cultureName)
    {
        RunUnderCulture(cultureName, engine =>
        {
            // en-US spells its negative sign U+002D, so this stays ASCII under every host culture.
            AssertEvaluates(engine, "new Intl.NumberFormat('en-US',{notation:'scientific'}).format(0.00012)", "1.2E-4");
            AssertEvaluates(engine, "new Intl.NumberFormat('en-US',{notation:'engineering'}).format(0.00012)", "120E-6");
            AssertEvaluates(engine, "new Intl.NumberFormat('en-US',{notation:'scientific'}).format(12000)", "1.2E4");
        });
    }

    private static void AssertEvaluates(Engine engine, string script, string expected)
    {
        var actual = engine.Evaluate(script).AsString();
        actual.Should().Be(expected, "`{0}` must not depend on CultureInfo.CurrentCulture (got {1})", script, Describe(actual));
    }

    private static void AssertAscii(string value, string what)
    {
        foreach (var c in value)
        {
            if (c > 0x7e)
            {
                throw new AssertionException($"{what} contains the non-ASCII character U+{(int) c:X4}: {Describe(value)}");
            }
        }
    }

    private static string Describe(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            if (c is < (char) 0x20 or > (char) 0x7e)
            {
                sb.Append("\\u").Append(((int) c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.Append('"').ToString();
    }

    private static void RunUnderCulture(string cultureName, Action<Engine> assertions)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CreateCulture(cultureName);

            // Built inside the block so that anything the engine formats while starting up is covered too.
            assertions(new Engine());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static CultureInfo CreateCulture(string cultureName)
    {
        if (!string.Equals(cultureName, Hostile, StringComparison.Ordinal))
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }

        var culture = (CultureInfo) CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.NegativeSign = "−";
        culture.NumberFormat.PositiveSign = "➕";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        return culture;
    }
}
