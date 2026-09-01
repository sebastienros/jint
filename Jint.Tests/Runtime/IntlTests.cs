using System.Globalization;
using Jint.Native.Intl;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class IntlTests
{
    private readonly Engine _engine;

    public IntlTests()
    {
        _engine = new Engine();
    }

    [Fact]
    public void IntlObjectExists()
    {
        var result = _engine.Evaluate("typeof Intl");
        result.AsString().Should().Be("object");
    }

    [Fact]
    public void IntlHasGetCanonicalLocales()
    {
        var result = _engine.Evaluate("typeof Intl.getCanonicalLocales");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void IntlHasSupportedValuesOf()
    {
        var result = _engine.Evaluate("typeof Intl.supportedValuesOf");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void GetCanonicalLocalesWithUndefined()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales(undefined)");
        result.IsArray().Should().BeTrue();
        result.AsArray().Length.Should().Be((uint) 0);
    }

    [Fact]
    public void GetCanonicalLocalesWithString()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales('en-US')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().Be((uint) 1);
        array[0].AsString().Should().Be("en-US");
    }

    [Fact]
    public void GetCanonicalLocalesWithArray()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().Be((uint) 2);
    }

    [Fact]
    public void GetCanonicalLocalesCanonicalizesCase()
    {
        // Should canonicalize 'en-us' to 'en-US'
        var result = _engine.Evaluate("Intl.getCanonicalLocales('en-us')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().Be((uint) 1);
        // Result should be canonicalized
        array[0].AsString().Should().NotBeNull();
    }

    [Fact]
    public void SupportedValuesOfCalendar()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('calendar')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SupportedValuesOfCurrency()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('currency')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SupportedValuesOfUnit()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('unit')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().BeGreaterThan(0);
        // Should contain common units
        var units = new List<string>();
        for (uint i = 0; i < array.Length; i++)
        {
            units.Add(array[i].AsString());
        }
        units.Should().Contain("meter");
        units.Should().Contain("second");
    }

    [Fact]
    public void SupportedValuesOfInvalidKeyThrows()
    {
        Invoking(() =>
            _engine.Evaluate("Intl.supportedValuesOf('invalid')")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void IntlToStringTag()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(Intl)");
        result.AsString().Should().Be("[object Intl]");
    }

    // Intl.Locale tests
    [Fact]
    public void LocaleConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.Locale");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void LocaleCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Fact]
    public void LocaleRequiresNew()
    {
        Invoking(() =>
            _engine.Evaluate("Intl.Locale('en-US')")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void LocaleToStringReturnsTag()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').toString()");
        result.AsString().Should().Be("en-US");
    }

    [Fact]
    public void LocaleLanguageProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').language");
        result.AsString().Should().Be("en");
    }

    [Fact]
    public void LocaleRegionProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').region");
        result.AsString().Should().Be("US");
    }

    [Fact]
    public void LocaleScriptProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('zh-Hans-CN').script");
        result.AsString().Should().Be("Hans");
    }

    [Fact]
    public void LocaleBaseNameProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').baseName");
        result.AsString().Should().Be("en-US");
    }

    [Fact]
    public void LocaleWithCalendarOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { calendar: 'gregory' }).calendar");
        result.AsString().Should().Be("gregory");
    }

    [Fact]
    public void LocaleWithHourCycleOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { hourCycle: 'h12' }).hourCycle");
        result.AsString().Should().Be("h12");
    }

    [Fact]
    public void LocaleWithNumericOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { numeric: true }).numeric");
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void LocaleToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.Locale('en-US'))");
        result.AsString().Should().Be("[object Intl.Locale]");
    }

    [Fact]
    public void LocaleMinimize()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').minimize().toString()");
        result.AsString().Should().Be("en");
    }

    [Fact]
    public void LocaleWithUnicodeExtension()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US-u-ca-gregory').calendar");
        result.AsString().Should().Be("gregory");
    }

    [Fact]
    public void LocaleInvalidTagThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.Locale('invalid tag with spaces')")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    // Intl.Collator tests
    [Fact]
    public void CollatorConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.Collator");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void CollatorCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.Collator('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Fact]
    public void CollatorToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.Collator('en-US'))");
        result.AsString().Should().Be("[object Intl.Collator]");
    }

    [Fact]
    public void CollatorCompareReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.Collator('en-US').compare");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void CollatorCompareSortsStrings()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US');
            var compare = collator.compare;
            compare('a', 'b')
        ");
        result.AsNumber().Should().BeLessThan(0);
    }

    [Fact]
    public void CollatorCompareWithCaseSensitivity()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US', { sensitivity: 'case' });
            collator.compare('a', 'A')
        ");
        // With 'case' sensitivity, 'a' and 'A' should be different
        result.AsNumber().Should().NotBe(0);
    }

    [Fact]
    public void CollatorCompareWithBaseSensitivity()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US', { sensitivity: 'base' });
            collator.compare('a', 'A')
        ");
        // With 'base' sensitivity, 'a' and 'A' should be equal
        result.AsNumber().Should().Be(0);
    }

    [Fact]
    public void CollatorResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.Collator('en-US', { usage: 'search' }).resolvedOptions();
            options.usage
        ");
        result.AsString().Should().Be("search");
    }

    [Fact]
    public void CollatorSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.Collator.supportedLocalesOf(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
    }

    [Fact]
    public void CollatorSortingWithCompare()
    {
        var result = _engine.Evaluate(@"
            var items = ['z', 'a', 'c', 'b'];
            items.sort(new Intl.Collator('en-US').compare);
            items.join(',')
        ");
        result.AsString().Should().Be("a,b,c,z");
    }

    [Theory]
    // Empty, and the two lengths below the three-character minimum.
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    // Nine characters, one past the maximum, alone and as the second subtag.
    [InlineData("abcdefghi")]
    [InlineData("abc-abcdefghi")]
    // Characters outside alphanum, ASCII and not.
    [InlineData("!invalid!")]
    [InlineData("de-DE!")]
    [InlineData("phonebké")]
    [InlineData("phonebkсорт")]
    // A separator with nothing on one side of it, so a subtag is empty.
    [InlineData("-phonebk-")]
    [InlineData("phonebk-")]
    [InlineData("phonebk--")]
    // A two-character subtag among well-formed ones.
    [InlineData("phonebk-nu")]
    [InlineData("phonebk-nu-")]
    [InlineData("phonebk-nu-latn")]
    public void CollatorRejectsACollationOptionTheTypeNonterminalCannotMatch(string collation)
    {
        // ResolveOptions (9.2.8) step 6.d.ii throws a RangeError for a resolution option value that
        // cannot be matched by the "type" Unicode locale nonterminal, and Intl.Collator's
        // [[ResolutionOptionDescriptors]] (10.2.3) carries { [[Key]]: "co", [[Property]]: "collation" }
        // with no [[Values]] field, so "collation" is read through exactly that step. The nonterminal
        // is alphanum{3,8} (sep alphanum{3,8})* - one or more subtags of three to eight ASCII letters
        // and digits, separated by "-".
        _engine.Evaluate($"(() => {{ try {{ new Intl.Collator('de', {{ collation: '{collation}' }}); return 'no throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .AsString().Should().Be("RangeError");
    }

    [Theory]
    // The lengths the nonterminal admits, and the digits it admits alongside letters.
    [InlineData("abc")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    [InlineData("1234abcd")]
    [InlineData("1234abcd-abc123")]
    // Well formed and no locale reports it: a syntax check is not a lookup.
    [InlineData("zzzzz")]
    public void CollatorAcceptsACollationOptionTheTypeNonterminalMatches(string collation)
    {
        // Only the syntax check of 9.2.8 step 6.d.ii throws. A well-formed value the locale's [[co]]
        // list does not contain is resolved by ResolveLocale (9.2.7 step 10), which simply leaves
        // [[co]] null - so Intl.Collator (10.1.1) reports "default" rather than throwing.
        _engine.Evaluate($"new Intl.Collator('de', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be("default");
    }

    [Theory]
    [InlineData("PHONEBK")]
    [InlineData("Phonebk")]
    public void CollatorDoesNotReadCasingInACollationOptionAsASyntaxError(string collation)
    {
        // The "type" nonterminal is built from alphanum, [0-9 A-Z a-z], and Unicode locale
        // identifiers are case-insensitive, so casing cannot make a value unmatchable and 9.2.8
        // step 6.d.ii cannot fire on it. Which collation such a value then resolves to is
        // ResolveLocale's business, not this check's; that half is pinned by
        // CollatorCanonicalizesACollationOptionBeforeMatchingIt below.
        _engine.Evaluate($"(() => {{ try {{ new Intl.Collator('de', {{ collation: '{collation}' }}); return 'no throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .AsString().Should().Be("no throw");
    }

    [Theory]
    [InlineData("phonebk")]
    [InlineData("PHONEBK")]
    [InlineData("Phonebk")]
    [InlineData("pHoNeBk")]
    public void CollatorCanonicalizesACollationOptionBeforeMatchingIt(string collation)
    {
        // https://tc39.es/ecma402/#sec-resolvelocale (9.2.7) step 10 runs an option value through
        // CanonicalizeUValue before asking whether keyLocaleData contains it, and
        // https://tc39.es/ecma402/#sec-canonicalizeuvalue (9.2.2) step 1 is "Let lowerValue be the
        // ASCII-lowercase of uvalue". Jint compared the raw string against the [[co]] list with
        // StringComparison.Ordinal, so every spelling but the all-lowercase one missed the list and
        // fell through to "default" - a silently wrong collator rather than an error.
        _engine.Evaluate($"new Intl.Collator('de', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be("phonebk");
    }

    [Fact]
    public void CollatorKeepsAnOptionSourcedCollationOutOfTheResolvedLocale()
    {
        // ResolveLocale only copies a keyword onto [[locale]] when the value came from the requested
        // locale's own extension sequence (supportedKeyword is set to empty as soon as an options
        // value supersedes it), so canonicalizing the option must not start emitting "-u-co-".
        _engine.Evaluate("new Intl.Collator('de', { collation: 'PHONEBK' }).resolvedOptions().locale")
            .AsString().Should().Be("de");
    }

    // Intl.NumberFormat tests
    [Fact]
    public void NumberFormatConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.NumberFormat");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void NumberFormatCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Fact]
    public void NumberFormatToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.NumberFormat('en-US'))");
        result.AsString().Should().Be("[object Intl.NumberFormat]");
    }

    [Fact]
    public void NumberFormatReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.NumberFormat('en-US').format");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void NumberFormatFormatsDecimal()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format(1234.567)");
        result.AsString().Should().Contain("1");
        result.AsString().Should().Contain("234");
    }

    [Fact]
    public void NumberFormatFormatsCurrency()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(1234.56)");
        // Should contain the dollar sign and the number
        result.AsString().Should().Contain("1");
    }

    [Fact]
    public void NumberFormatFormatsPercent()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'percent' }).format(0.5)");
        // Should contain "50" for 50%
        result.AsString().Should().Contain("50");
    }

    [Fact]
    public void NumberFormatResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'EUR' }).resolvedOptions();
            options.currency
        ");
        result.AsString().Should().Be("EUR");
    }

    [Fact]
    public void NumberFormatResolvedOptionsStyle()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.NumberFormat('en-US', { style: 'percent' }).resolvedOptions();
            options.style
        ");
        result.AsString().Should().Be("percent");
    }

    [Fact]
    public void NumberFormatSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.NumberFormat.supportedLocalesOf(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
    }

    [Fact]
    public void NumberFormatWithUnit()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'unit', unit: 'kilometer' }).format(100)");
        result.AsString().Should().Contain("100");
        result.AsString().Should().Contain("km");
    }

    [Fact]
    public void NumberFormatCurrencyRequiresCurrency()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'currency' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void NumberFormatUnitRequiresUnit()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'unit' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    // Intl.DateTimeFormat tests
    [Fact]
    public void DateTimeFormatConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.DateTimeFormat");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void DateTimeFormatCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Fact]
    public void DateTimeFormatToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.DateTimeFormat('en-US'))");
        result.AsString().Should().Be("[object Intl.DateTimeFormat]");
    }

    [Fact]
    public void DateTimeFormatFormatReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.DateTimeFormat('en-US').format");
        result.AsString().Should().Be("function");
    }

    [Fact]
    public void DateTimeFormatFormatsDate()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format(new Date(2024, 0, 15, 12, 0, 0))");
        // Should contain the date components - use noon to avoid timezone issues
        result.AsString().Should().Contain("1"); // Month or day
        result.AsString().Should().Contain("2024"); // Year
        // Day could be 14, 15, or 16 depending on timezone, just check it's a valid date
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DateTimeFormatWithDateStyle()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'short' }).format(new Date(2024, 0, 15))");
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DateTimeFormatWithTimeStyle()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeStyle: 'short' }).format(new Date(2024, 0, 15, 14, 30, 0))");
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DateTimeFormatResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US', { dateStyle: 'full' }).resolvedOptions();
            options.dateStyle
        ");
        result.AsString().Should().Be("full");
    }

    [Fact]
    public void DateTimeFormatResolvedOptionsLocale()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US').resolvedOptions();
            options.locale
        ");
        // Should return a locale string
        result.AsString().Should().NotBeNull();
    }

    [Fact]
    public void DateTimeFormatResolvedOptionsCalendar()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US').resolvedOptions();
            options.calendar
        ");
        // Default calendar should be 'gregory'
        result.AsString().Should().Be("gregory");
    }

    [Fact]
    public void DateTimeFormatSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.DateTimeFormat.supportedLocalesOf(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
    }

    [Fact]
    public void DateTimeFormatWithYearMonthDay()
    {
        var result = _engine.Evaluate(@"
            new Intl.DateTimeFormat('en-US', {
                year: 'numeric',
                month: 'numeric',
                day: 'numeric'
            }).format(new Date(2024, 0, 15))
        ");
        result.AsString().Should().Contain("2024");
    }

    [Fact]
    public void DateTimeFormatWithHourMinute()
    {
        var result = _engine.Evaluate(@"
            new Intl.DateTimeFormat('en-US', {
                hour: 'numeric',
                minute: '2-digit'
            }).format(new Date(2024, 0, 15, 14, 30, 0))
        ");
        // Should contain time components
        (result.AsString().Contains("30") || result.AsString().Contains(":")).Should().BeTrue();
    }

    [Fact]
    public void DateTimeFormatFormatToParts()
    {
        var result = _engine.Evaluate(@"
            var parts = new Intl.DateTimeFormat('en-US').formatToParts(new Date(2024, 0, 15));
            Array.isArray(parts)
        ");
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DateTimeFormatFormatToPartsHasTypeAndValue()
    {
        var result = _engine.Evaluate(@"
            var parts = new Intl.DateTimeFormat('en-US').formatToParts(new Date(2024, 0, 15));
            parts.length > 0 && parts[0].type !== undefined && parts[0].value !== undefined
        ");
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DateTimeFormatWithWeekday()
    {
        var result = _engine.Evaluate(@"
            new Intl.DateTimeFormat('en-US', {
                weekday: 'long',
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            }).format(new Date(2024, 0, 15))
        ");
        // Should contain a weekday name (Monday, January 15, 2024)
        result.AsString().Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public void DateTimeFormatWithHourCycle()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US', {
                hour: 'numeric',
                hourCycle: 'h23'
            }).resolvedOptions();
            options.hourCycle
        ");
        result.AsString().Should().Be("h23");
    }

    [Fact]
    public void DateTimeFormatWithTimeZone()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US', {
                timeZone: 'UTC'
            }).resolvedOptions();
            options.timeZone
        ");
        result.AsString().Should().Be("UTC");
    }

    [Fact]
    public void DateTimeFormatInvalidTimeZoneThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'Invalid/TimeZone' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void DateTimeFormatFormatsUndefinedAsNow()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format()");
        // Should return a non-empty string (current date formatted)
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DateTimeFormatFormatsTimestamp()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format(1705363200000)"); // Jan 15, 2024 UTC
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DateTimeFormatWithMonth2Digit()
    {
        var result = _engine.Evaluate(@"
            new Intl.DateTimeFormat('en-US', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit'
            }).format(new Date(2024, 0, 5))
        ");
        // With 2-digit, should have leading zeros
        result.AsString().Should().Contain("01"); // Month
    }

    [Fact]
    public void DateTimeFormatInvalidDateStyleThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'invalid' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void DateTimeFormatInvalidWeekdayThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { weekday: 'invalid' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    // Boundary tests for the precision-aware string-input paths in NumberFormatPrototype.
    // These cover the 16/17 digit thresholds that route to FormatExactDecimal / Format(BigInteger),
    // so a regression in TryParseLargeInteger / TryParseHighPrecisionDecimal is caught even when
    // test262 isn't running.

    [Fact]
    public void NumberFormat_LargeIntegerString_PreservesAllDigits()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('987654321987654321')");
        result.AsString().Should().Be("987,654,321,987,654,321");
    }

    [Fact]
    public void NumberFormat_NegativeLargeIntegerString_PreservesAllDigits()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('-987654321987654321')");
        result.AsString().Should().Be("-987,654,321,987,654,321");
    }

    [Fact]
    public void NumberFormat_LargeIntegerString_BelowThreshold_StaysOnDoublePath()
    {
        // 16 digits — under the 17-digit boundary; goes through TypeConverter.ToNumber and
        // the existing double pipeline. The chosen value is exactly representable as a
        // double (≤ 2^53) so this also asserts no regression in the small-integer path.
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('1000000000000000')");
        result.AsString().Should().Be("1,000,000,000,000,000");
    }

    [Fact]
    public void NumberFormat_HighPrecisionDecimal_PreservesAllFractionDigits()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', {maximumFractionDigits: 20}).format('1.0000000000000001')");
        result.AsString().Should().Be("1.0000000000000001");
    }

    [Fact]
    public void NumberFormat_HighPrecisionDecimal_BelowThreshold_StaysOnDoublePath()
    {
        // 15 total digits — under the 16-digit precision boundary; format result still matches
        // because the double can represent 1.23 exactly enough for a 2-fraction-digit display.
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('1.23')");
        result.AsString().Should().Be("1.23");
    }

    [Fact]
    public void NumberFormat_NegativeTinyDecimalRoundsToZero_KeepsMinus()
    {
        // -1.23e-30 with maxFracDigits:3 rounds to -0.000 (displayed minimumFractionDigits=3).
        // The originally-negative tracking in FormatExactDecimal must keep the leading '-'.
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US', {minimumFractionDigits: 3, maximumFractionDigits: 3})" +
            ".format('-0.00000000000000000000000000000123')");
        result.AsString().Should().Be("-0.000");
    }

    [Fact]
    public void NumberFormat_SignificantDigitsOnHugeInteger_RoundsHalfExpand()
    {
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US', {useGrouping: false, maximumSignificantDigits: 5})" +
            ".format('12344501000000000000000000000000000')");
        result.AsString().Should().Be("12345000000000000000000000000000000");
    }

    [Fact]
    public void NumberFormat_FormatRange_StringInputs_PreservePrecision()
    {
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US').formatRange('987654321987654321', '987654321987654322')");
        result.AsString().Should().Be("987,654,321,987,654,321–987,654,321,987,654,322");
    }

    [Fact]
    public void NumberFormat_FormatRange_NaN_Throws()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US').formatRange(NaN, 5)")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void NumberFormat_FormatRange_PrefixCurrencyCollapse_TightSeparator()
    {
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US', {style: 'currency', currency: 'USD', signDisplay: 'always'})" +
            ".formatRange(2.9, 3.1)");
        result.AsString().Should().Be("+$2.90–3.10");
    }

    [Theory]
    // a tag matching no available locale falls back to the hardcoded root collations
    [InlineData("und", """["emoji","eor"]""")]
    [InlineData("und-Latn-US", """["emoji","eor"]""")]
    [InlineData("qtz-CN", """["emoji","eor"]""")]
    // a matched locale reports the root collations plus its own, in code unit order
    [InlineData("tr", """["emoji","eor"]""")]
    [InlineData("de", """["emoji","eor","phonebk"]""")]
    [InlineData("ko", """["emoji","eor","searchjl","unihan"]""")]
    // an explicitly requested collation is the whole answer, however it was requested
    [InlineData("de-u-co-phonebk", """["phonebk"]""")]
    [InlineData("und-u-co-pinyin", """["pinyin"]""")]
    public void LocaleGetCollations(string tag, string expected)
    {
        var result = _engine.Evaluate($"JSON.stringify(new Intl.Locale('{tag}').getCollations())");
        result.AsString().Should().Be(expected);
    }

    [Fact]
    public void LocaleGetCollationsNeverReportsStandardOrSearch()
    {
        var result = _engine.Evaluate("""
            ['ar', 'de', 'en', 'ja', 'ko', 'sv', 'tr', 'zh', 'und'].every(tag => {
                const collations = new Intl.Locale(tag).getCollations();
                return collations.length > 0
                    && !collations.includes('standard')
                    && !collations.includes('search')
                    && collations.join() === [...collations].sort().join();
            });
            """);
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CollatorAcceptsEveryCollationGetCollationsReports()
    {
        // ECMA-402 gives a locale one [[co]] list: CollationsOfLocale (15.5.10 step 3.c) reports it,
        // and ResolveLocale (9.2.7 step 10) — reached from Intl.Collator (10.1.1) through
        // ResolveOptions — resolves a requested "co" against the same list. So everything
        // getCollations() advertises has to construct, through the options bag and through the
        // -u-co- extension alike, and resolve back to itself.
        var result = _engine.Evaluate("""
            const tags = ['ar', 'da', 'de', 'en', 'es', 'fi', 'hi', 'ja', 'ko', 'ln', 'si', 'sv', 'vi', 'yue', 'zh', 'tr', 'fr', 'und'];
            const mismatches = [];
            for (const tag of tags) {
                for (const collation of new Intl.Locale(tag).getCollations()) {
                    const viaOption = new Intl.Collator(tag, { collation }).resolvedOptions().collation;
                    const viaExtension = new Intl.Collator(`${tag}-u-co-${collation}`).resolvedOptions().collation;
                    if (viaOption !== collation || viaExtension !== collation) {
                        mismatches.push(`${tag}/${collation}: option=${viaOption} extension=${viaExtension}`);
                    }
                }
            }
            JSON.stringify(mismatches);
            """);
        result.AsString().Should().Be("[]");
    }

    [Theory]
    // The thirteen locales whose getCollations() advertised the root "emoji" collation while
    // Intl.Collator refused it: the eleven carrying a collation table of their own besides "en",
    // plus "tr" and "fr", which carry none.
    [InlineData("ar")]
    [InlineData("da")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("hi")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("ln")]
    [InlineData("si")]
    [InlineData("sv")]
    [InlineData("zh")]
    [InlineData("tr")]
    [InlineData("fr")]
    // ...plus the two that were already consistent, so the row keeps watching them too.
    [InlineData("en")]
    [InlineData("und")]
    // ...plus the three languages CLDR gives a collation of their own that the table used to omit.
    [InlineData("fi")]
    [InlineData("vi")]
    [InlineData("yue")]
    public void CollatorAcceptsTheRootCollationsForEveryLocale(string tag)
    {
        var result = _engine.Evaluate($$"""
            ['emoji', 'eor'].map(collation => [
                new Intl.Collator('{{tag}}', { collation }).resolvedOptions().collation,
                new Intl.Collator(`{{tag}}-u-co-${collation}`).resolvedOptions().collation,
            ].join('/')).join(' ');
            """);
        result.AsString().Should().Be("emoji/emoji eor/eor");
    }

    [Theory]
    [InlineData("de", "phonebk")]
    [InlineData("es", "trad")]
    [InlineData("ko", "searchjl")]
    [InlineData("zh", "pinyin")]
    [InlineData("fi", "trad")]
    [InlineData("sv", "trad")]
    [InlineData("vi", "trad")]
    public void CollatorStillAcceptsLocaleSpecificCollations(string tag, string collation)
    {
        _engine.Evaluate($"new Intl.Collator('{tag}', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be(collation);
        _engine.Evaluate($"new Intl.Collator('{tag}-u-co-{collation}').resolvedOptions().collation")
            .AsString().Should().Be(collation);
    }

    [Fact]
    public void CollatorRefusesACollationTheLocaleDoesNotReport()
    {
        // The acceptance set is the reported list, not the union of every collation identifier that
        // exists: "phonebk" is German data and Turkish never advertises it.
        _engine.Evaluate("new Intl.Locale('tr').getCollations().includes('phonebk')")
            .AsBoolean().Should().BeFalse();
        _engine.Evaluate("new Intl.Collator('tr', { collation: 'phonebk' }).resolvedOptions().collation")
            .AsString().Should().Be("default");
        _engine.Evaluate("new Intl.Collator('tr-u-co-phonebk').resolvedOptions().collation")
            .AsString().Should().Be("default");
    }

    [Fact]
    public void CollatorAcceptsDefaultAsARequestThatNoLocaleEverReports()
    {
        // "default" is how Intl.Collator (10.1.1) spells a null resolved [[co]], so it stays a
        // usable request — but it is not an element of any [[co]] list, so 15.5.10 never reports it
        // and it never reaches the resolved locale.
        _engine.Evaluate("new Intl.Collator('de', { collation: 'default' }).resolvedOptions().collation")
            .AsString().Should().Be("default");
        _engine.Evaluate("new Intl.Collator('de-u-co-default').resolvedOptions().collation")
            .AsString().Should().Be("default");
        _engine.Evaluate("new Intl.Collator('de-u-co-default').resolvedOptions().locale")
            .AsString().Should().Be("de");
        _engine.Evaluate("['de', 'en', 'zh', 'und'].some(tag => new Intl.Locale(tag).getCollations().includes('default'))")
            .AsBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("search")]
    public void CollatorRejectsTheCollationsNoLocaleDataMayContain(string collation)
    {
        // ECMA-402 10.2.3: "standard" and "search" must not be used as elements in any
        // [[SortLocaleData]].[[<locale>]].[[co]] or [[SearchLocaleData]].[[<locale>]].[[co]] List,
        // which is what makes them unreportable and unrequestable by the same act.
        _engine.Evaluate($"new Intl.Collator('de', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be("default");
        _engine.Evaluate($"new Intl.Collator('de-u-co-{collation}').resolvedOptions().collation")
            .AsString().Should().Be("default");
        _engine.Evaluate($"new Intl.Collator('de-u-co-{collation}').resolvedOptions().locale")
            .AsString().Should().Be("de");
    }

    [Fact]
    public void CollatorReflectsAResolvedCollationExtensionInTheResolvedLocale()
    {
        // ResolveLocale (9.2.7 step 10) inserts the keyword back into the locale only when it came
        // from the requested Unicode extension sequence; a value supplied through the options bag
        // sets supportedKeyword to empty and so is never inserted.
        _engine.Evaluate("new Intl.Collator('de-u-co-emoji').resolvedOptions().locale")
            .AsString().Should().Be("de-u-co-emoji");
        _engine.Evaluate("new Intl.Collator('de-u-co-phonebk').resolvedOptions().locale")
            .AsString().Should().Be("de-u-co-phonebk");
        _engine.Evaluate("new Intl.Collator('de', { collation: 'emoji' }).resolvedOptions().locale")
            .AsString().Should().Be("de");
    }

    [Theory]
    // Every language CLDR gives a collation of its own, plus four that carry none. The expected list
    // is what common/collation/<language>.xml defines, minus the "standard" and "search" types those
    // files carry and the "private-" types CLDR keeps only for [import], plus the "emoji" and "eor"
    // that common/collation/root.xml contributes to every locale, in code unit order.
    [InlineData("ar", "compat,emoji,eor")]
    [InlineData("da", "emoji,eor")]
    [InlineData("de", "emoji,eor,phonebk")]
    [InlineData("en", "emoji,eor")]
    [InlineData("es", "emoji,eor,trad")]
    [InlineData("fi", "emoji,eor,trad")]
    [InlineData("fr", "emoji,eor")]
    [InlineData("hi", "emoji,eor")]
    [InlineData("ja", "emoji,eor,unihan")]
    [InlineData("ko", "emoji,eor,searchjl,unihan")]
    [InlineData("ln", "emoji,eor,phonetic")]
    [InlineData("si", "dict,emoji,eor")]
    [InlineData("sv", "emoji,eor,trad")]
    [InlineData("tr", "emoji,eor")]
    [InlineData("vi", "emoji,eor,trad")]
    [InlineData("zh", "emoji,eor,pinyin,stroke,unihan,zhuyin")]
    public void LocaleReportsTheCollationsCldrDefinesForTheLanguage(string tag, string expected)
    {
        _engine.Evaluate($"new Intl.Locale('{tag}').getCollations().join(',')")
            .AsString().Should().Be(expected);
    }

    [Theory]
    // Deprecated in common/bcp47/collation.xml, which is what makes them unavailable rather than
    // merely absent, and defined by no locale in common/collation.
    [InlineData("zh", "big5han")]
    [InlineData("hi", "direct")]
    [InlineData("zh", "gb2312")]
    [InlineData("sv", "reformed")]
    // Registered and current in common/bcp47/collation.xml, but no locale defines it and CLDR ships
    // no data for it, so it is in no locale's [[co]] list either.
    [InlineData("en", "ducet")]
    public void CollatorRefusesACollationCldrShipsNoDataFor(string tag, string collation)
    {
        _engine.Evaluate($"new Intl.Locale('{tag}').getCollations().includes('{collation}')")
            .AsBoolean().Should().BeFalse();
        _engine.Evaluate($"new Intl.Collator('{tag}', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be("default");
        _engine.Evaluate($"new Intl.Collator('{tag}-u-co-{collation}').resolvedOptions().collation")
            .AsString().Should().Be("default");

        // AvailableCanonicalCollations (https://tc39.es/ecma402/#sec-availablecanonicalcollations)
        // contains the collations the implementation provides Intl.Collator functionality for, so an
        // identifier no locale resolves may not appear there either - which
        // intl402/Intl/supportedValuesOf/collations-accepted-by-Collator.js checks from the other
        // side, against the ten locales it names.
        _engine.Evaluate($"Intl.supportedValuesOf('collation').includes('{collation}')")
            .AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void LocaleWithoutACollationRowStaysConsistentWithTheCollator()
    {
        // CLDR does give yue zh's collations, through the collation parent zh_Hant that
        // supplementalData.xml assigns it - but Jint's table deliberately has no yue row, because
        // whether yue is an available Collator locale depends on the platform's culture source:
        // ICU (Linux, macOS) surfaces it through CultureInfo.GetCultures, NLS-sourced Windows does
        // not. Reporting the zh set for a tag the Collator resolves to "default" would recreate the
        // report-vs-accept disagreement #2974 removed, so what this pins is the invariant itself,
        // in both directions, on every platform: the report is the rowless root pair, and every
        // collation it reports is one the Collator actually resolves. Giving yue the zh set waits
        // on wiring CollationsOfLocale through the collation parent, not just on availability.
        _engine.Evaluate("new Intl.Locale('yue').getCollations().join(',')").AsString().Should().Be("emoji,eor");

        foreach (var collation in new[] { "emoji", "eor" })
        {
            _engine.Evaluate($"new Intl.Collator('yue', {{ collation: '{collation}' }}).resolvedOptions().collation")
                .AsString().Should().Be(collation, "the Collator must accept every collation getCollations reports");
        }
    }

    [Fact]
    public void SupportedValuesOfCollationIsTheUnionOfEveryLocalesCollations()
    {
        // AvailableCanonicalCollations (https://tc39.es/ecma402/#sec-availablecanonicalcollations)
        // contains "the collations for which the implementation provides the functionality of
        // Intl.Collator objects", which is the union of the one [[co]] list every locale has and
        // nothing besides. Both inclusions matter and each has a way of going wrong on its own: a
        // value listed here that no locale resolves is what
        // intl402/Intl/supportedValuesOf/collations-accepted-by-Collator.js fails on, and a
        // collation some locale reports but this omits is invisible to test262 entirely.
        var result = _engine.Evaluate("""
            // Every language Jint carries collation data for, plus five that carry none.
            const tags = ['ar', 'de', 'es', 'fi', 'ja', 'ko', 'ln', 'si', 'sv', 'vi', 'zh',
                          'da', 'en', 'fr', 'hi', 'tr', 'und'];
            const union = new Set();
            for (const tag of tags) {
                for (const collation of new Intl.Locale(tag).getCollations()) {
                    union.add(collation);
                }
            }
            JSON.stringify([...union].sort());
            """);

        var supported = _engine.Evaluate("JSON.stringify(Intl.supportedValuesOf('collation'))");
        supported.AsString().Should().Be(result.AsString());
    }

    [Fact]
    public void SupportedValuesOfCollationIsSortedUniqueAndFreeOfTheUnrequestableTypes()
    {
        // The three properties intl402/Intl/supportedValuesOf/collations.js checks, restated here so
        // the derivation cannot lose one of them quietly.
        var result = _engine.Evaluate("""
            const collations = Intl.supportedValuesOf('collation');
            const sorted = JSON.stringify(collations) === JSON.stringify([...collations].sort());
            const unique = new Set(collations).size === collations.length;
            const clean = !collations.includes('standard') && !collations.includes('search')
                && !collations.includes('default');
            [sorted, unique, clean, collations.join(',')].join(' ');
            """);
        result.AsString().Should().Be(
            "true true true compat,dict,emoji,eor,phonebk,phonetic,pinyin,searchjl,stroke,trad,unihan,zhuyin");
    }

    [Theory]
    // Case folding alone.
    [InlineData("de", "collation", "PHONEBK", "de-u-co-phonebk")]
    [InlineData("en", "calendar", "GREGORY", "en-u-ca-gregory")]
    [InlineData("en", "numberingSystem", "LATN", "en-u-nu-latn")]
    // Alias substitution, which already worked, kept here so a regression in either half shows.
    [InlineData("en", "calendar", "islamicc", "en-u-ca-islamic-civil")]
    [InlineData("en", "calendar", "ethiopic-amete-alem", "en-u-ca-ethioaa")]
    // Both at once: the fold has to happen before the alias lookup, not instead of it.
    [InlineData("en", "calendar", "ISLAMICC", "en-u-ca-islamic-civil")]
    [InlineData("en", "calendar", "Ethiopic-Amete-Alem", "en-u-ca-ethioaa")]
    public void LocaleCanonicalizesAUnicodeExtensionOptionValue(string tag, string option, string value, string expected)
    {
        // https://tc39.es/ecma402/#sec-makelocalerecord (15.1.3) sets the keyword's value to
        // CanonicalizeUValue(key, overrideValue) for every option that overrides the tag, so the
        // extension sequence Intl.Locale builds carries the canonical spelling and never the one the
        // caller happened to type.
        _engine.Evaluate($"new Intl.Locale('{tag}', {{ {option}: '{value}' }}).toString()")
            .AsString().Should().Be(expected);
    }

    [Fact]
    public void LocaleReportsACanonicalizedOptionValueFromItsAccessors()
    {
        // The accessors read the same [[Collation]]/[[NumberingSystem]] slots MakeLocaleRecord filled,
        // so an uncanonicalized option leaked out of getCollations() as well - Intl.Locale advertising
        // a collation identifier that exists in no CLDR data at all.
        _engine.Evaluate("new Intl.Locale('de', { collation: 'PHONEBK' }).collation").AsString().Should().Be("phonebk");
        _engine.Evaluate("JSON.stringify(new Intl.Locale('de', { collation: 'PHONEBK' }).getCollations())")
            .AsString().Should().Be("""["phonebk"]""");
        _engine.Evaluate("new Intl.Locale('en', { numberingSystem: 'LATN' }).numberingSystem").AsString().Should().Be("latn");
        _engine.Evaluate("new Intl.Locale('en', { calendar: 'GREGORY' }).calendar").AsString().Should().Be("gregory");
    }

    [Theory]
    [InlineData("latn", "latn")]
    [InlineData("LATN", "latn")]
    [InlineData("Latn", "latn")]
    [InlineData("arab", "arab")]
    [InlineData("ARAB", "arab")]
    // Well formed by the "type" nonterminal and matched by no numbering system: 9.2.7 step 10 only
    // replaces the resolved value when keyLocaleData contains the canonicalized option, so this has
    // to fall back to the locale's default rather than throw.
    [InlineData("abc-def", "latn")]
    [InlineData("zzzzz", "latn")]
    public void NumberFormatCanonicalizesANumberingSystemOption(string numberingSystem, string expected)
    {
        // The validator here was stricter than the grammar it was standing in for: it required
        // char.IsAsciiLetterLower, so "LATN" was a RangeError instead of "latn", and it rejected the
        // hyphen outright, so a well-formed multi-subtag value threw where it should merely have
        // failed to match.
        _engine.Evaluate($"new Intl.NumberFormat('en', {{ numberingSystem: '{numberingSystem}' }}).resolvedOptions().numberingSystem")
            .AsString().Should().Be(expected);
    }

    [Theory]
    // The invalid list of intl402/NumberFormat/constructor-options-numberingSystem-invalid.js: what
    // relaxing the validator must not start accepting.
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abcdefghi")]
    [InlineData("abc-abcdefghi")]
    [InlineData("!invalid!")]
    [InlineData("-latn-")]
    [InlineData("latn-")]
    [InlineData("latn--")]
    [InlineData("latn-ca")]
    [InlineData("latn-ca-")]
    [InlineData("latn-ca-gregory")]
    public void NumberFormatRejectsANumberingSystemOptionTheTypeNonterminalDoesNotMatch(string numberingSystem)
    {
        _engine.Evaluate($"(() => {{ try {{ new Intl.NumberFormat('en', {{ numberingSystem: '{numberingSystem}' }}); return 'no throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .AsString().Should().Be("RangeError");
    }

    [Theory]
    [InlineData("LATN", "latn")]
    [InlineData("ARAB", "arab")]
    [InlineData("arab", "arab")]
    public void DurationFormatCanonicalizesANumberingSystemOption(string numberingSystem, string expected)
    {
        _engine.Evaluate($"new Intl.DurationFormat('en', {{ numberingSystem: '{numberingSystem}' }}).resolvedOptions().numberingSystem")
            .AsString().Should().Be(expected);
    }

    [Theory]
    [InlineData("LATN", "latn")]
    [InlineData("ARAB", "arab")]
    [InlineData("arab", "arab")]
    public void RelativeTimeFormatCanonicalizesANumberingSystemOption(string numberingSystem, string expected)
    {
        _engine.Evaluate($"new Intl.RelativeTimeFormat('en', {{ numberingSystem: '{numberingSystem}' }}).resolvedOptions().numberingSystem")
            .AsString().Should().Be(expected);
    }

    [Fact]
    public void DateTimeFormatCanonicalizesItsUnicodeExtensionOptions()
    {
        // DateTimeFormat already adopted the canonical spelling, because it matches an option against
        // its supported list with StringComparison.OrdinalIgnoreCase and then keeps the list's entry
        // rather than the caller's. Pinned so the shared helper cannot regress it.
        _engine.Evaluate("new Intl.DateTimeFormat('en', { numberingSystem: 'LATN' }).resolvedOptions().numberingSystem")
            .AsString().Should().Be("latn");
        _engine.Evaluate("new Intl.DateTimeFormat('en', { calendar: 'GREGORY' }).resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
    }

    [Theory]
    // The maximum time value: 275760-09-13T00:00:00.000Z.
    [InlineData(8640000000000000L, "9/13/275760")]
    // The minimum time value: -271821-04-20T00:00:00.000Z.
    [InlineData(-8640000000000000L, "4/20/-271821")]
    // One millisecond past what DateTime can hold: +010000-01-01T00:00:00.001Z.
    [InlineData(253402300800001L, "1/1/10000")]
    // One millisecond before it: 0000-12-31T23:59:59.999Z.
    [InlineData(-62135596800001L, "12/31/0")]
    public void DateTimeFormatKeepsEveryDateFieldOfAValueOutsideDateTimeRange(long timeValue, string expected)
    {
        // The conversion clamped an out-of-range time value to DateTime.MinValue/MaxValue and carried
        // only the year out beside it, so month and day came from the clamp: the maximum time value
        // formatted as December 31 rather than September 13. It now decomposes the time value itself
        // and formats on a representative year congruent mod 400, which is where the real month, day,
        // time and weekday survive.
        _engine.Evaluate($"new Intl.DateTimeFormat('en-US', {{ timeZone: 'UTC' }}).format(new Date({timeValue}))")
            .AsString().Should().Be(expected);
    }

    [Fact]
    public void DateTimeFormatKeepsTheTimeFieldsOfAValueOutsideDateTimeRange()
    {
        // The year was re-inserted as a quoted literal, and BuildFormatString classifies a part whose
        // first character is an apostrophe as an hour - so it inserted the date/time separator in front
        // of the year and then ran the real time fields together behind it:
        // "12/31, 27576011:59:59 PM".
        _engine.Evaluate("""
            new Intl.DateTimeFormat('en-US', {
                timeZone: 'UTC', year: 'numeric', month: 'numeric', day: 'numeric',
                hour: 'numeric', minute: 'numeric', second: 'numeric'
            }).format(new Date(8640000000000000))
            """).AsString().Should().Be("9/13/275760, 12:00:00 AM");

        _engine.Evaluate("""
            new Intl.DateTimeFormat('en-US', {
                timeZone: 'UTC', year: 'numeric', month: 'numeric', day: 'numeric',
                hour: 'numeric', minute: 'numeric', second: 'numeric'
            }).format(new Date(-62135596800001))
            """).AsString().Should().Be("12/31/0, 11:59:59 PM");
    }

    [Fact]
    public void DateTimeFormatToPartsReportsTheRealFieldsOfAValueOutsideDateTimeRange()
    {
        _engine.Evaluate("""
            JSON.stringify(new Intl.DateTimeFormat('en-US', { timeZone: 'UTC' })
                .formatToParts(new Date(8640000000000000))
                .filter(p => p.type !== 'literal')
                .map(p => p.type + '=' + p.value))
            """).AsString().Should().Be("""["month=9","day=13","year=275760"]""");
    }

    [Fact]
    public void DateTimeFormatCarriesTheRealYearThroughDateStyle()
    {
        // FormatStyleToParts took originalYear and dropped it on the floor, so a styled format of an
        // out-of-range date printed the clamp's year 9999 with no sign that anything was wrong.
        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'long' }).format(new Date(8640000000000000))")
            .AsString().Should().Be("September 13, 275760");

        // "full" additionally pins the weekday, which is what the representative year being congruent
        // mod 400 buys: the maximum time value really is a Saturday.
        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'full' }).format(new Date(8640000000000000))")
            .AsString().Should().Be("Saturday, September 13, 275760");

        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'full', timeStyle: 'short' }).format(new Date(8640000000000000))")
            .AsString().Should().Be("Saturday, September 13, 275760, 12:00 AM");
    }

    [Fact]
    public void DateTimeFormatStillAgreesWithItselfInsideDateTimeRange()
    {
        // The control: nothing above may move a date the platform can hold.
        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC' }).format(new Date(Date.UTC(2024, 2, 15)))")
            .AsString().Should().Be("3/15/2024");
        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'full' }).format(new Date(Date.UTC(2024, 2, 15)))")
            .AsString().Should().Be("Friday, March 15, 2024");
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma402/#sec-resolveoptions">ResolveOptions</see> step 3 reads the
    /// <c>localeMatcher</c> option through <c>GetOption</c>, which admits only <c>"lookup"</c> and
    /// <c>"best fit"</c> and raises a <c>RangeError</c> for anything else. <c>Intl.PluralRules</c> left the
    /// read to <c>ResolveLocale</c>, which takes the raw string and never validates it, so a bad value was
    /// accepted in silence — the one assertion in
    /// <c>staging/sm/extensions/quote-string-for-nul-character.js</c> that Jint failed.
    /// </summary>
    [Theory]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: 'lookup\0cookie' })")]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: 'nonsense' })")]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: 'Lookup' })")]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: '' })")]
    public void PluralRulesRejectsAnInvalidLocaleMatcher(string script)
    {
        Invoking(() => _engine.Evaluate(script))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Invalid value*for option 'localeMatcher'");
    }

    /// <summary>
    /// The control: both accepted values, and the absent option, still construct.
    /// </summary>
    [Theory]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: 'lookup' })")]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: 'best fit' })")]
    [InlineData("new Intl.PluralRules('en', { localeMatcher: undefined })")]
    [InlineData("new Intl.PluralRules('en')")]
    public void PluralRulesAcceptsAValidLocaleMatcher(string script)
    {
        _engine.Evaluate($"typeof {script}").AsString().Should().Be("object");
    }

    /// <summary>
    /// The option is read exactly once. It used to be read twice by <c>Intl.Collator</c> — once to validate
    /// it and once inside <c>ResolveLocale</c> — which a user-supplied getter can see.
    /// </summary>
    [Theory]
    [InlineData("Collator")]
    [InlineData("PluralRules")]
    [InlineData("DateTimeFormat")]
    [InlineData("NumberFormat")]
    [InlineData("RelativeTimeFormat")]
    public void TheLocaleMatcherGetterRunsOnce(string constructor)
    {
        var script = $$"""
            var reads = 0;
            var options = { get localeMatcher() { reads++; return 'lookup'; } };
            new Intl.{{constructor}}('en', options);
            reads;
            """;

        _engine.Evaluate(script).AsNumber().Should().Be(1);
    }

    /// <summary>
    /// The <c>CultureInfo</c> instances <c>IntlUtilities</c> caches are process-wide: one per locale tag,
    /// shared by every engine, never evicted. A <c>CultureInfo</c> is writable unless it is explicitly made
    /// read-only -- <c>useUserOverride: false</c> only suppresses the Windows user regional overrides -- so
    /// without this the two places that adjust a format for one formatter could instead have adjusted it for
    /// every engine in the process, and nothing would have reported it.
    /// </summary>
    /// <remarks>
    /// Both of those places clone before writing (<c>DateTimeFormatConstructor</c> clones the culture and its
    /// <c>DateTimeFormatInfo</c>, <c>NumberFormatConstructor</c> clones the <c>NumberFormatInfo</c>) and a
    /// clone of a read-only instance is writable, which is why the whole <c>Intl</c> suite is the other half
    /// of this test: it exercises both of those paths on every construction.
    /// </remarks>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    public void ACachedCultureIsReadOnly(string locale)
    {
        var culture = IntlUtilities.GetCultureInfo(locale);

        culture.Should().NotBeNull();
        culture!.IsReadOnly.Should().BeTrue();
        IntlUtilities.GetCultureInfo(locale).Should().BeSameAs(culture);

        // what the two format-adjusting call sites do, and what has to keep working
        ((CultureInfo) culture.Clone()).IsReadOnly.Should().BeFalse();
        ((DateTimeFormatInfo) culture.DateTimeFormat.Clone()).IsReadOnly.Should().BeFalse();
        ((NumberFormatInfo) culture.NumberFormat.Clone()).IsReadOnly.Should().BeFalse();
    }
}
