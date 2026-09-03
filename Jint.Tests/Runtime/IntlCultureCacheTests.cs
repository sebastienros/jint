#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// The process-wide <c>IntlUtilities</c> culture cache is keyed on a locale tag a script chose, and every
/// engine in the process shares it. These tests pin that a script cannot grow it without bound.
/// </summary>
/// <remarks>
/// Non-parallelizable because the assertions are about a static shared by the whole test run: a fixture
/// running beside this one would add its own tags to the same dictionary.
/// </remarks>
[NonParallelizable]
public class IntlCultureCacheTests
{
    /// <summary>
    /// <c>toLocaleLowerCase</c> is core <c>String.prototype</c> -- no <c>Intl</c> opt-in, no WebApi, nothing --
    /// and ECMA-402 requires a structurally valid but unknown tag to be accepted rather than rejected, so
    /// every tag the loop invents used to become a permanent entry no engine's <c>LimitMemory</c> could see.
    /// </summary>
    [Test]
    public void ScriptSuppliedLocaleTagsDoNotGrowTheProcessWideCultureCache()
    {
        var engine = new Engine();
        engine.Execute("for (var i = 0; i < 2000; i++) { 'x'.toLocaleLowerCase('en-abcd' + i); }");

        IntlUtilities.CultureCacheCount.Should().BeLessThanOrEqualTo(IntlUtilities.CultureCacheBound);
    }

    /// <summary>
    /// The same key space reached through <c>Intl</c> itself, where <c>BestAvailableLocale</c> additionally
    /// asks for every truncated prefix of the requested tag.
    /// </summary>
    [Test]
    public void RequestedLocalesOfAnIntlFormatterDoNotGrowTheProcessWideCultureCache()
    {
        var engine = new Engine();
        engine.Execute("for (var i = 0; i < 2000; i++) { new Intl.NumberFormat('en-abcd' + i + '-efgh' + i).format(1); }");

        IntlUtilities.CultureCacheCount.Should().BeLessThanOrEqualTo(IntlUtilities.CultureCacheBound);
    }

    /// <summary>
    /// The <c>supportedLocalesOf</c> sweep <c>intl402</c> performs must not resolve a culture per candidate
    /// prefix of every tag it asks about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see href="https://github.com/sebastienros/jint/issues/3609">#3609</see>:
    /// <c>intl402/supportedLocalesOf-unicode-extensions-ignored.js</c> asks every <c>Intl</c> constructor
    /// about 640 generated tags with two matchers, three <c>supportedLocalesOf</c> calls each, and took two
    /// seconds alone and thirty under a saturated build — a wall-clock timeout on work that had not changed.
    /// <c>BestAvailableLocale</c> was resolving a <see cref="System.Globalization.CultureInfo"/> for every
    /// candidate it tried, which is about 15 µs behind a cache bounded, on purpose, well below the number of
    /// distinct tags such a sweep invents. Seventy thousand lookups, of which sixty-four thousand were that
    /// probe, and it decided the answer in 0.8% of them.
    /// </para>
    /// <para>
    /// <b>A count and not a duration</b>, which is the point: the defect only ever showed itself as a
    /// timeout, so a timing test would be the same measurement that could not tell a regression from a busy
    /// machine. The bound is one lookup per <c>supportedLocalesOf</c> call. This sweep makes <i>none at
    /// all</i> now — every one of its tags is answered by the available-locale set, bare or truncated — and
    /// made 6,913 before, so the bound has two orders of magnitude of room and still fails the moment the
    /// per-candidate resolution comes back.
    /// </para>
    /// </remarks>
    [Test]
    public void ASupportedLocalesOfSweepDoesNotResolveACultureForEveryCandidate()
    {
        const int Tags = 640;
        const int CallsPerTag = 3;

        var engine = new Engine();

        // Warm: the first pass resolves what it has to, and the assertion is about the sweep rather than
        // about which tags this process had already seen.
        engine.Execute(Sweep);

        var before = IntlUtilities.CultureLookupCount;
        engine.Execute(Sweep);
        var lookups = IntlUtilities.CultureLookupCount - before;

        lookups.Should().BeLessThan(
            Tags * CallsPerTag,
            "a sweep of {0} tags asking {1} times each must not resolve a culture per call, let alone per "
            + "candidate prefix of one",
            Tags,
            CallsPerTag);
    }

    /// <summary>
    /// The sweep from <c>intl402/supportedLocalesOf-unicode-extensions-ignored.js</c>, one constructor and
    /// one matcher: the 640 generated tags, each asked about bare, with a valid Unicode extension sequence
    /// and with an invalid one.
    /// </summary>
    private const string Sweep = """
        var languages = ["zh", "es", "en", "hi", "ur", "ar", "ja", "pa"];
        var scripts = ["Latn", "Hans", "Deva", "Arab", "Jpan", "Hant", "Guru"];
        var countries = ["CN", "IN", "US", "PK", "JP", "TW", "HK", "SG", "419"];

        var allTags = [];
        for (var i = 0; i < languages.length; i++) {
          var language = languages[i];
          allTags.push(language);
          for (var j = 0; j < scripts.length; j++) {
            var script = scripts[j];
            allTags.push(language + "-" + script);
            for (var k = 0; k < countries.length; k++) {
              allTags.push(language + "-" + script + "-" + countries[k]);
            }
          }
          for (var c = 0; c < countries.length; c++) {
            allTags.push(language + "-" + countries[c]);
          }
        }

        var opt = { localeMatcher: "lookup" };
        for (var t = 0; t < allTags.length; t++) {
          var locale = allTags[t];
          Intl.Collator.supportedLocalesOf([locale], opt);
          Intl.Collator.supportedLocalesOf([locale + "-u-co-phonebk-nu-latn"], opt);
          Intl.Collator.supportedLocalesOf([locale + "-u-nu-invalid"], opt);
        }
        """;

    /// <summary>
    /// Leaving the culture probe out of the first pass may not change what is reported as supported, which
    /// is what the sweep above is actually asserting when test262 runs it.
    /// </summary>
    /// <remarks>
    /// The three rows are the shapes the probe used to answer for: a tag the available set holds verbatim, a
    /// tag carrying a Unicode extension sequence — which step 2.d's singleton rule truncates to the same
    /// prefix the probe reached by stripping it — and a tag nothing can match, which still falls through to
    /// the pass that resolves cultures.
    /// </remarks>
    [TestCase("en-US", true)]
    [TestCase("en-US-u-co-phonebk-nu-latn", true)]
    [TestCase("en-US-u-nu-invalid", true)]
    [TestCase("zz-Zzzz-ZZ", false)]
    public void AUnicodeExtensionSequenceDoesNotChangeWhetherALocaleIsSupported(string locale, bool supported)
    {
        var engine = new Engine();
        engine.SetValue("locale", locale);

        engine.Evaluate("Intl.Collator.supportedLocalesOf([locale]).length === 1").AsBoolean().Should().Be(supported);
        engine.Evaluate("Intl.NumberFormat.supportedLocalesOf([locale]).length === 1").AsBoolean().Should().Be(supported);
        engine.Evaluate("Intl.DateTimeFormat.supportedLocalesOf([locale]).length === 1").AsBoolean().Should().Be(supported);
    }

    /// <summary>
    /// The bound is not allowed to become a correctness problem: a tag resolved after the cache overflowed
    /// answers exactly as it did before, because the value is a total function of the key.
    /// </summary>
    [Test]
    public void AnEvictedTagResolvesToTheSameCultureItDidBefore()
    {
        var before = IntlUtilities.GetCultureInfo("de-DE");
        before.Should().NotBeNull();

        var engine = new Engine();
        engine.Execute("for (var i = 0; i < 2000; i++) { 'x'.toLocaleLowerCase('en-abcd' + i); }");

        var after = IntlUtilities.GetCultureInfo("de-DE");
        after.Should().NotBeNull();
        after!.Name.Should().Be(before!.Name);
        after.IsReadOnly.Should().BeTrue();
        after.NumberFormat.NumberDecimalSeparator.Should().Be(before.NumberFormat.NumberDecimalSeparator);
    }
}
