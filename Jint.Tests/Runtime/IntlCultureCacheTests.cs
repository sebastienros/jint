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
