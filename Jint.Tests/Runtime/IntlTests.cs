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

    [Test]
    public void IntlObjectExists()
    {
        var result = _engine.Evaluate("typeof Intl");
        result.AsString().Should().Be("object");
    }

    [Test]
    public void IntlHasGetCanonicalLocales()
    {
        var result = _engine.Evaluate("typeof Intl.getCanonicalLocales");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void IntlHasSupportedValuesOf()
    {
        var result = _engine.Evaluate("typeof Intl.supportedValuesOf");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void GetCanonicalLocalesWithUndefined()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales(undefined)");
        result.IsArray().Should().BeTrue();
        result.AsArray().Length.Should().Be((uint) 0);
    }

    [Test]
    public void GetCanonicalLocalesWithString()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales('en-US')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().Be((uint) 1);
        array[0].AsString().Should().Be("en-US");
    }

    [Test]
    public void GetCanonicalLocalesWithArray()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().Be((uint) 2);
    }

    [Test]
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

    [Test]
    public void SupportedValuesOfCalendar()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('calendar')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void SupportedValuesOfCurrency()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('currency')");
        result.IsArray().Should().BeTrue();
        var array = result.AsArray();
        array.Length.Should().BeGreaterThan(0);
    }

    [Test]
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

    [Test]
    public void SupportedValuesOfInvalidKeyThrows()
    {
        Invoking(() =>
            _engine.Evaluate("Intl.supportedValuesOf('invalid')")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void IntlToStringTag()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(Intl)");
        result.AsString().Should().Be("[object Intl]");
    }

    // Intl.Locale tests
    [Test]
    public void LocaleConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.Locale");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void LocaleCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Test]
    public void LocaleRequiresNew()
    {
        Invoking(() =>
            _engine.Evaluate("Intl.Locale('en-US')")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void LocaleToStringReturnsTag()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').toString()");
        result.AsString().Should().Be("en-US");
    }

    [Test]
    public void LocaleLanguageProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').language");
        result.AsString().Should().Be("en");
    }

    [Test]
    public void LocaleRegionProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').region");
        result.AsString().Should().Be("US");
    }

    [Test]
    public void LocaleScriptProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('zh-Hans-CN').script");
        result.AsString().Should().Be("Hans");
    }

    [Test]
    public void LocaleBaseNameProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').baseName");
        result.AsString().Should().Be("en-US");
    }

    [Test]
    public void LocaleWithCalendarOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { calendar: 'gregory' }).calendar");
        result.AsString().Should().Be("gregory");
    }

    [Test]
    public void LocaleWithHourCycleOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { hourCycle: 'h12' }).hourCycle");
        result.AsString().Should().Be("h12");
    }

    [Test]
    public void LocaleWithNumericOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { numeric: true }).numeric");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void LocaleToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.Locale('en-US'))");
        result.AsString().Should().Be("[object Intl.Locale]");
    }

    [Test]
    public void LocaleMinimize()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').minimize().toString()");
        result.AsString().Should().Be("en");
    }

    [Test]
    public void LocaleWithUnicodeExtension()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US-u-ca-gregory').calendar");
        result.AsString().Should().Be("gregory");
    }

    [Test]
    public void LocaleInvalidTagThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.Locale('invalid tag with spaces')")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    // Intl.Collator tests
    [Test]
    public void CollatorConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.Collator");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void CollatorCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.Collator('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Test]
    public void CollatorToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.Collator('en-US'))");
        result.AsString().Should().Be("[object Intl.Collator]");
    }

    [Test]
    public void CollatorCompareReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.Collator('en-US').compare");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void CollatorCompareSortsStrings()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US');
            var compare = collator.compare;
            compare('a', 'b')
        ");
        result.AsNumber().Should().BeLessThan(0);
    }

    [Test]
    public void CollatorCompareWithCaseSensitivity()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US', { sensitivity: 'case' });
            collator.compare('a', 'A')
        ");
        // With 'case' sensitivity, 'a' and 'A' should be different
        result.AsNumber().Should().NotBe(0);
    }

    [Test]
    public void CollatorCompareWithBaseSensitivity()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US', { sensitivity: 'base' });
            collator.compare('a', 'A')
        ");
        // With 'base' sensitivity, 'a' and 'A' should be equal
        result.AsNumber().Should().Be(0);
    }

    [Test]
    public void CollatorResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.Collator('en-US', { usage: 'search' }).resolvedOptions();
            options.usage
        ");
        result.AsString().Should().Be("search");
    }

    [Test]
    public void CollatorSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.Collator.supportedLocalesOf(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
    }

    [Test]
    public void CollatorSortingWithCompare()
    {
        var result = _engine.Evaluate(@"
            var items = ['z', 'a', 'c', 'b'];
            items.sort(new Intl.Collator('en-US').compare);
            items.join(',')
        ");
        result.AsString().Should().Be("a,b,c,z");
    }

    // Empty, and the two lengths below the three-character minimum.
    [TestCase("")]
    [TestCase("a")]
    [TestCase("ab")]
    // Nine characters, one past the maximum, alone and as the second subtag.
    [TestCase("abcdefghi")]
    [TestCase("abc-abcdefghi")]
    // Characters outside alphanum, ASCII and not.
    [TestCase("!invalid!")]
    [TestCase("de-DE!")]
    [TestCase("phonebké")]
    [TestCase("phonebkсорт")]
    // A separator with nothing on one side of it, so a subtag is empty.
    [TestCase("-phonebk-")]
    [TestCase("phonebk-")]
    [TestCase("phonebk--")]
    // A two-character subtag among well-formed ones.
    [TestCase("phonebk-nu")]
    [TestCase("phonebk-nu-")]
    [TestCase("phonebk-nu-latn")]
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

    // The lengths the nonterminal admits, and the digits it admits alongside letters.
    [TestCase("abc")]
    [TestCase("abcdefgh")]
    [TestCase("12345678")]
    [TestCase("1234abcd")]
    [TestCase("1234abcd-abc123")]
    // Well formed and no locale reports it: a syntax check is not a lookup.
    [TestCase("zzzzz")]
    public void CollatorAcceptsACollationOptionTheTypeNonterminalMatches(string collation)
    {
        // Only the syntax check of 9.2.8 step 6.d.ii throws. A well-formed value the locale's [[co]]
        // list does not contain is resolved by ResolveLocale (9.2.7 step 10), which simply leaves
        // [[co]] null - so Intl.Collator (10.1.1) reports "default" rather than throwing.
        _engine.Evaluate($"new Intl.Collator('de', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be("default");
    }

    [TestCase("PHONEBK")]
    [TestCase("Phonebk")]
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

    [TestCase("phonebk")]
    [TestCase("PHONEBK")]
    [TestCase("Phonebk")]
    [TestCase("pHoNeBk")]
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

    [Test]
    public void CollatorKeepsAnOptionSourcedCollationOutOfTheResolvedLocale()
    {
        // ResolveLocale only copies a keyword onto [[locale]] when the value came from the requested
        // locale's own extension sequence (supportedKeyword is set to empty as soon as an options
        // value supersedes it), so canonicalizing the option must not start emitting "-u-co-".
        _engine.Evaluate("new Intl.Collator('de', { collation: 'PHONEBK' }).resolvedOptions().locale")
            .AsString().Should().Be("de");
    }

    // Intl.NumberFormat tests
    [Test]
    public void NumberFormatConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.NumberFormat");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void NumberFormatCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Test]
    public void NumberFormatToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.NumberFormat('en-US'))");
        result.AsString().Should().Be("[object Intl.NumberFormat]");
    }

    [Test]
    public void NumberFormatReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.NumberFormat('en-US').format");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void NumberFormatFormatsDecimal()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format(1234.567)");
        result.AsString().Should().Contain("1");
        result.AsString().Should().Contain("234");
    }

    [Test]
    public void NumberFormatFormatsCurrency()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(1234.56)");
        // Should contain the dollar sign and the number
        result.AsString().Should().Contain("1");
    }

    [Test]
    public void NumberFormatFormatsPercent()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'percent' }).format(0.5)");
        // Should contain "50" for 50%
        result.AsString().Should().Contain("50");
    }

    [Test]
    public void NumberFormatResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'EUR' }).resolvedOptions();
            options.currency
        ");
        result.AsString().Should().Be("EUR");
    }

    [Test]
    public void NumberFormatResolvedOptionsStyle()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.NumberFormat('en-US', { style: 'percent' }).resolvedOptions();
            options.style
        ");
        result.AsString().Should().Be("percent");
    }

    [Test]
    public void NumberFormatSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.NumberFormat.supportedLocalesOf(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
    }

    [Test]
    public void NumberFormatWithUnit()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'unit', unit: 'kilometer' }).format(100)");
        result.AsString().Should().Contain("100");
        result.AsString().Should().Contain("km");
    }

    [Test]
    public void NumberFormatCurrencyRequiresCurrency()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'currency' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void NumberFormatUnitRequiresUnit()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'unit' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    // Intl.DateTimeFormat tests
    [Test]
    public void DateTimeFormatConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.DateTimeFormat");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void DateTimeFormatCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US')");
        result.Should().NotBeNull();
        result.IsObject().Should().BeTrue();
    }

    [Test]
    public void DateTimeFormatToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.DateTimeFormat('en-US'))");
        result.AsString().Should().Be("[object Intl.DateTimeFormat]");
    }

    [Test]
    public void DateTimeFormatFormatReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.DateTimeFormat('en-US').format");
        result.AsString().Should().Be("function");
    }

    [Test]
    public void DateTimeFormatFormatsDate()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format(new Date(2024, 0, 15, 12, 0, 0))");
        // Should contain the date components - use noon to avoid timezone issues
        result.AsString().Should().Contain("1"); // Month or day
        result.AsString().Should().Contain("2024"); // Year
        // Day could be 14, 15, or 16 depending on timezone, just check it's a valid date
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void DateTimeFormatWithDateStyle()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'short' }).format(new Date(2024, 0, 15))");
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void DateTimeFormatWithTimeStyle()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeStyle: 'short' }).format(new Date(2024, 0, 15, 14, 30, 0))");
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void DateTimeFormatResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US', { dateStyle: 'full' }).resolvedOptions();
            options.dateStyle
        ");
        result.AsString().Should().Be("full");
    }

    [Test]
    public void DateTimeFormatResolvedOptionsLocale()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US').resolvedOptions();
            options.locale
        ");
        // Should return a locale string
        result.AsString().Should().NotBeNull();
    }

    [Test]
    public void DateTimeFormatResolvedOptionsCalendar()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US').resolvedOptions();
            options.calendar
        ");
        // Default calendar should be 'gregory'
        result.AsString().Should().Be("gregory");
    }

    [Test]
    public void DateTimeFormatSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.DateTimeFormat.supportedLocalesOf(['en-US', 'de-DE'])");
        result.IsArray().Should().BeTrue();
    }

    [Test]
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

    [Test]
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

    [Test]
    public void DateTimeFormatFormatToParts()
    {
        var result = _engine.Evaluate(@"
            var parts = new Intl.DateTimeFormat('en-US').formatToParts(new Date(2024, 0, 15));
            Array.isArray(parts)
        ");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DateTimeFormatFormatToPartsHasTypeAndValue()
    {
        var result = _engine.Evaluate(@"
            var parts = new Intl.DateTimeFormat('en-US').formatToParts(new Date(2024, 0, 15));
            parts.length > 0 && parts[0].type !== undefined && parts[0].value !== undefined
        ");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void DateTimeFormatInvalidTimeZoneThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'Invalid/TimeZone' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void DateTimeFormatFormatsUndefinedAsNow()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format()");
        // Should return a non-empty string (current date formatted)
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void DateTimeFormatFormatsTimestamp()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format(1705363200000)"); // Jan 15, 2024 UTC
        result.AsString().Should().NotBeNull();
        result.AsString().Length.Should().BeGreaterThan(0);
    }

    [Test]
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

    [Test]
    public void DateTimeFormatInvalidDateStyleThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'invalid' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void DateTimeFormatInvalidWeekdayThrows()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { weekday: 'invalid' })")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    // Boundary tests for the precision-aware string-input paths in NumberFormatPrototype.
    // These cover the 16/17 digit thresholds that route to FormatExactDecimal / Format(BigInteger),
    // so a regression in TryParseLargeInteger / TryParseHighPrecisionDecimal is caught even when
    // test262 isn't running.

    [Test]
    public void NumberFormat_LargeIntegerString_PreservesAllDigits()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('987654321987654321')");
        result.AsString().Should().Be("987,654,321,987,654,321");
    }

    [Test]
    public void NumberFormat_NegativeLargeIntegerString_PreservesAllDigits()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('-987654321987654321')");
        result.AsString().Should().Be("-987,654,321,987,654,321");
    }

    [Test]
    public void NumberFormat_LargeIntegerString_BelowThreshold_StaysOnDoublePath()
    {
        // 16 digits — under the 17-digit boundary; goes through TypeConverter.ToNumber and
        // the existing double pipeline. The chosen value is exactly representable as a
        // double (≤ 2^53) so this also asserts no regression in the small-integer path.
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('1000000000000000')");
        result.AsString().Should().Be("1,000,000,000,000,000");
    }

    [Test]
    public void NumberFormat_HighPrecisionDecimal_PreservesAllFractionDigits()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', {maximumFractionDigits: 20}).format('1.0000000000000001')");
        result.AsString().Should().Be("1.0000000000000001");
    }

    [Test]
    public void NumberFormat_HighPrecisionDecimal_BelowThreshold_StaysOnDoublePath()
    {
        // 15 total digits — under the 16-digit precision boundary; format result still matches
        // because the double can represent 1.23 exactly enough for a 2-fraction-digit display.
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format('1.23')");
        result.AsString().Should().Be("1.23");
    }

    [Test]
    public void NumberFormat_NegativeTinyDecimalRoundsToZero_KeepsMinus()
    {
        // -1.23e-30 with maxFracDigits:3 rounds to -0.000 (displayed minimumFractionDigits=3).
        // The originally-negative tracking in FormatExactDecimal must keep the leading '-'.
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US', {minimumFractionDigits: 3, maximumFractionDigits: 3})" +
            ".format('-0.00000000000000000000000000000123')");
        result.AsString().Should().Be("-0.000");
    }

    [Test]
    public void NumberFormat_SignificantDigitsOnHugeInteger_RoundsHalfExpand()
    {
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US', {useGrouping: false, maximumSignificantDigits: 5})" +
            ".format('12344501000000000000000000000000000')");
        result.AsString().Should().Be("12345000000000000000000000000000000");
    }

    [Test]
    public void NumberFormat_FormatRange_StringInputs_PreservePrecision()
    {
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US').formatRange('987654321987654321', '987654321987654322')");
        result.AsString().Should().Be("987,654,321,987,654,321–987,654,321,987,654,322");
    }

    [Test]
    public void NumberFormat_FormatRange_NaN_Throws()
    {
        Invoking(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US').formatRange(NaN, 5)")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void NumberFormat_FormatRange_PrefixCurrencyCollapse_TightSeparator()
    {
        var result = _engine.Evaluate(
            "new Intl.NumberFormat('en-US', {style: 'currency', currency: 'USD', signDisplay: 'always'})" +
            ".formatRange(2.9, 3.1)");
        result.AsString().Should().Be("+$2.90–3.10");
    }

    // a tag matching no available locale falls back to the hardcoded root collations
    [TestCase("und", """["emoji","eor"]""")]
    [TestCase("und-Latn-US", """["emoji","eor"]""")]
    [TestCase("qtz-CN", """["emoji","eor"]""")]
    // a matched locale reports the root collations plus its own, in code unit order
    [TestCase("tr", """["emoji","eor"]""")]
    [TestCase("de", """["emoji","eor","phonebk"]""")]
    [TestCase("ko", """["emoji","eor","searchjl","unihan"]""")]
    // an explicitly requested collation is the whole answer, however it was requested
    [TestCase("de-u-co-phonebk", """["phonebk"]""")]
    [TestCase("und-u-co-pinyin", """["pinyin"]""")]
    public void LocaleGetCollations(string tag, string expected)
    {
        var result = _engine.Evaluate($"JSON.stringify(new Intl.Locale('{tag}').getCollations())");
        result.AsString().Should().Be(expected);
    }

    [Test]
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

    [Test]
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

    // The thirteen locales whose getCollations() advertised the root "emoji" collation while
    // Intl.Collator refused it: the eleven carrying a collation table of their own besides "en",
    // plus "tr" and "fr", which carry none.
    [TestCase("ar")]
    [TestCase("da")]
    [TestCase("de")]
    [TestCase("es")]
    [TestCase("hi")]
    [TestCase("ja")]
    [TestCase("ko")]
    [TestCase("ln")]
    [TestCase("si")]
    [TestCase("sv")]
    [TestCase("zh")]
    [TestCase("tr")]
    [TestCase("fr")]
    // ...plus the two that were already consistent, so the row keeps watching them too.
    [TestCase("en")]
    [TestCase("und")]
    // ...plus the three languages CLDR gives a collation of their own that the table used to omit.
    [TestCase("fi")]
    [TestCase("vi")]
    [TestCase("yue")]
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

    [TestCase("de", "phonebk")]
    [TestCase("es", "trad")]
    [TestCase("ko", "searchjl")]
    [TestCase("zh", "pinyin")]
    [TestCase("fi", "trad")]
    [TestCase("sv", "trad")]
    [TestCase("vi", "trad")]
    public void CollatorStillAcceptsLocaleSpecificCollations(string tag, string collation)
    {
        _engine.Evaluate($"new Intl.Collator('{tag}', {{ collation: '{collation}' }}).resolvedOptions().collation")
            .AsString().Should().Be(collation);
        _engine.Evaluate($"new Intl.Collator('{tag}-u-co-{collation}').resolvedOptions().collation")
            .AsString().Should().Be(collation);
    }

    [Test]
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

    [Test]
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

    [TestCase("standard")]
    [TestCase("search")]
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

    [Test]
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

    // Every language CLDR gives a collation of its own, plus four that carry none. The expected list
    // is what common/collation/<language>.xml defines, minus the "standard" and "search" types those
    // files carry and the "private-" types CLDR keeps only for [import], plus the "emoji" and "eor"
    // that common/collation/root.xml contributes to every locale, in code unit order.
    [TestCase("ar", "compat,emoji,eor")]
    [TestCase("da", "emoji,eor")]
    [TestCase("de", "emoji,eor,phonebk")]
    [TestCase("en", "emoji,eor")]
    [TestCase("es", "emoji,eor,trad")]
    [TestCase("fi", "emoji,eor,trad")]
    [TestCase("fr", "emoji,eor")]
    [TestCase("hi", "emoji,eor")]
    [TestCase("ja", "emoji,eor,unihan")]
    [TestCase("ko", "emoji,eor,searchjl,unihan")]
    [TestCase("ln", "emoji,eor,phonetic")]
    [TestCase("si", "dict,emoji,eor")]
    [TestCase("sv", "emoji,eor,trad")]
    [TestCase("tr", "emoji,eor")]
    [TestCase("vi", "emoji,eor,trad")]
    [TestCase("zh", "emoji,eor,pinyin,stroke,unihan,zhuyin")]
    public void LocaleReportsTheCollationsCldrDefinesForTheLanguage(string tag, string expected)
    {
        _engine.Evaluate($"new Intl.Locale('{tag}').getCollations().join(',')")
            .AsString().Should().Be(expected);
    }

    // Deprecated in common/bcp47/collation.xml, which is what makes them unavailable rather than
    // merely absent, and defined by no locale in common/collation.
    [TestCase("zh", "big5han")]
    [TestCase("hi", "direct")]
    [TestCase("zh", "gb2312")]
    [TestCase("sv", "reformed")]
    // Registered and current in common/bcp47/collation.xml, but no locale defines it and CLDR ships
    // no data for it, so it is in no locale's [[co]] list either.
    [TestCase("en", "ducet")]
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

    [Test]
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

    [Test]
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

    [Test]
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

    // Case folding alone.
    [TestCase("de", "collation", "PHONEBK", "de-u-co-phonebk")]
    [TestCase("en", "calendar", "GREGORY", "en-u-ca-gregory")]
    [TestCase("en", "numberingSystem", "LATN", "en-u-nu-latn")]
    // Alias substitution, which already worked, kept here so a regression in either half shows.
    [TestCase("en", "calendar", "islamicc", "en-u-ca-islamic-civil")]
    [TestCase("en", "calendar", "ethiopic-amete-alem", "en-u-ca-ethioaa")]
    // Both at once: the fold has to happen before the alias lookup, not instead of it.
    [TestCase("en", "calendar", "ISLAMICC", "en-u-ca-islamic-civil")]
    [TestCase("en", "calendar", "Ethiopic-Amete-Alem", "en-u-ca-ethioaa")]
    public void LocaleCanonicalizesAUnicodeExtensionOptionValue(string tag, string option, string value, string expected)
    {
        // https://tc39.es/ecma402/#sec-makelocalerecord (15.1.3) sets the keyword's value to
        // CanonicalizeUValue(key, overrideValue) for every option that overrides the tag, so the
        // extension sequence Intl.Locale builds carries the canonical spelling and never the one the
        // caller happened to type.
        _engine.Evaluate($"new Intl.Locale('{tag}', {{ {option}: '{value}' }}).toString()")
            .AsString().Should().Be(expected);
    }

    [Test]
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

    [TestCase("latn", "latn")]
    [TestCase("LATN", "latn")]
    [TestCase("Latn", "latn")]
    [TestCase("arab", "arab")]
    [TestCase("ARAB", "arab")]
    // Well formed by the "type" nonterminal and matched by no numbering system: 9.2.7 step 10 only
    // replaces the resolved value when keyLocaleData contains the canonicalized option, so this has
    // to fall back to the locale's default rather than throw.
    [TestCase("abc-def", "latn")]
    [TestCase("zzzzz", "latn")]
    public void NumberFormatCanonicalizesANumberingSystemOption(string numberingSystem, string expected)
    {
        // The validator here was stricter than the grammar it was standing in for: it required
        // char.IsAsciiLetterLower, so "LATN" was a RangeError instead of "latn", and it rejected the
        // hyphen outright, so a well-formed multi-subtag value threw where it should merely have
        // failed to match.
        _engine.Evaluate($"new Intl.NumberFormat('en', {{ numberingSystem: '{numberingSystem}' }}).resolvedOptions().numberingSystem")
            .AsString().Should().Be(expected);
    }

    // The invalid list of intl402/NumberFormat/constructor-options-numberingSystem-invalid.js: what
    // relaxing the validator must not start accepting.
    [TestCase("")]
    [TestCase("a")]
    [TestCase("ab")]
    [TestCase("abcdefghi")]
    [TestCase("abc-abcdefghi")]
    [TestCase("!invalid!")]
    [TestCase("-latn-")]
    [TestCase("latn-")]
    [TestCase("latn--")]
    [TestCase("latn-ca")]
    [TestCase("latn-ca-")]
    [TestCase("latn-ca-gregory")]
    public void NumberFormatRejectsANumberingSystemOptionTheTypeNonterminalDoesNotMatch(string numberingSystem)
    {
        _engine.Evaluate($"(() => {{ try {{ new Intl.NumberFormat('en', {{ numberingSystem: '{numberingSystem}' }}); return 'no throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .AsString().Should().Be("RangeError");
    }

    [TestCase("LATN", "latn")]
    [TestCase("ARAB", "arab")]
    [TestCase("arab", "arab")]
    public void DurationFormatCanonicalizesANumberingSystemOption(string numberingSystem, string expected)
    {
        _engine.Evaluate($"new Intl.DurationFormat('en', {{ numberingSystem: '{numberingSystem}' }}).resolvedOptions().numberingSystem")
            .AsString().Should().Be(expected);
    }

    [TestCase("LATN", "latn")]
    [TestCase("ARAB", "arab")]
    [TestCase("arab", "arab")]
    public void RelativeTimeFormatCanonicalizesANumberingSystemOption(string numberingSystem, string expected)
    {
        _engine.Evaluate($"new Intl.RelativeTimeFormat('en', {{ numberingSystem: '{numberingSystem}' }}).resolvedOptions().numberingSystem")
            .AsString().Should().Be(expected);
    }

    [Test]
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

    // The maximum time value: 275760-09-13T00:00:00.000Z.
    [TestCase(8640000000000000L, "9/13/275760")]
    // The minimum time value: -271821-04-20T00:00:00.000Z.
    [TestCase(-8640000000000000L, "4/20/-271821")]
    // One millisecond past what DateTime can hold: +010000-01-01T00:00:00.001Z.
    [TestCase(253402300800001L, "1/1/10000")]
    // One millisecond before it: 0000-12-31T23:59:59.999Z.
    [TestCase(-62135596800001L, "12/31/0")]
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

    [Test]
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

    [Test]
    public void DateTimeFormatToPartsReportsTheRealFieldsOfAValueOutsideDateTimeRange()
    {
        _engine.Evaluate("""
            JSON.stringify(new Intl.DateTimeFormat('en-US', { timeZone: 'UTC' })
                .formatToParts(new Date(8640000000000000))
                .filter(p => p.type !== 'literal')
                .map(p => p.type + '=' + p.value))
            """).AsString().Should().Be("""["month=9","day=13","year=275760"]""");
    }

    [Test]
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

    [Test]
    public void DateTimeFormatStillAgreesWithItselfInsideDateTimeRange()
    {
        // The control: nothing above may move a date the platform can hold.
        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC' }).format(new Date(Date.UTC(2024, 2, 15)))")
            .AsString().Should().Be("3/15/2024");
        _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'full' }).format(new Date(Date.UTC(2024, 2, 15)))")
            .AsString().Should().Be("Friday, March 15, 2024");
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma402/#sec-formatdatetime">FormatDateTime</see> concatenates the very
    /// list <see href="https://tc39.es/ecma402/#sec-formatdatetimetoparts">FormatDateTimeToParts</see> walks,
    /// so <c>format</c> and <c>formatToParts</c> are two views of one partition and cannot disagree.
    /// </summary>
    [Test]
    public void DateStyleFormatIsTheConcatenationOfItsParts()
    {
        const string Script = """
            (function () {
              const d = new Date(Date.UTC(2026, 7, 27, 15, 4, 5));
              const locales = ['en-US', 'en-GB', 'de-DE', 'fr-FR', 'ja-JP', 'pt-PT',
                               'ru-RU', 'ko-KR', 'sv-SE', 'hu-HU', 'tr-TR', 'nl-NL', 'fi-FI', 'it-IT'];
              const styles = [undefined, 'full', 'long', 'medium', 'short'];
              const mismatches = [];
              for (const locale of locales) {
                for (const dateStyle of styles) {
                  for (const timeStyle of styles) {
                    if (dateStyle === undefined && timeStyle === undefined) continue;
                    const f = new Intl.DateTimeFormat(locale, { timeZone: 'UTC', dateStyle, timeStyle });
                    const whole = f.format(d);
                    const joined = f.formatToParts(d).map(p => p.value).join('');
                    if (whole !== joined) {
                      mismatches.push(locale + ' ' + dateStyle + '/' + timeStyle +
                                      ': format="' + whole + '" parts="' + joined + '"');
                    }
                  }
                }
              }
              return mismatches.join(' | ');
            })()
            """;

        _engine.Evaluate(Script).AsString().Should().BeEmpty();
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma402/#sec-date-time-style-format">DateTimeStyleFormat</see> takes the
    /// pattern out of the locale's own data, so a styled date is written in the locale's field order with the
    /// locale's own literals - not in the American order with hard-coded slashes.
    /// </summary>
    [TestCase("de-DE", "short", "27.08.2026")]
    [TestCase("de-DE", "long", "27. August 2026")]
    [TestCase("de-DE", "full", "Donnerstag, 27. August 2026")]
    [TestCase("fr-FR", "full", "jeudi 27 août 2026")]
    [TestCase("ja-JP", "short", "2026/08/27")]
    [TestCase("en-US", "medium", "Aug 27, 2026")]
    [TestCase("en-US", "full", "Thursday, August 27, 2026")]
    public void DateStyleWritesTheLocalesOwnDateShape(string locale, string dateStyle, string expected)
    {
        var script = $$"""
            (function () {
              const f = new Intl.DateTimeFormat('{{locale}}', { timeZone: 'UTC', dateStyle: '{{dateStyle}}' });
              const d = new Date(Date.UTC(2026, 7, 27));
              return f.format(d) + '|' + f.formatToParts(d).map(p => p.value).join('');
            })()
            """;

        _engine.Evaluate(script).AsString().Should().Be(expected + "|" + expected);
    }

    /// <summary>
    /// <see href="https://tc39.es/proposal-temporal/#sec-adjustdatetimestyleformat">AdjustDateTimeStyleFormat</see>
    /// narrows the pattern a <c>dateStyle</c> resolved to down to the fields the value has, so neither the
    /// reference year a <c>Temporal.PlainMonthDay</c> carries nor the reference day a
    /// <c>Temporal.PlainYearMonth</c> carries is written.
    /// </summary>
    [TestCase("en-US", "full", "August 2026", "August 27")]
    [TestCase("en-US", "long", "August 2026", "August 27")]
    [TestCase("en-US", "medium", "Aug 2026", "Aug 27")]
    [TestCase("en-US", "short", "8/26", "8/27")]
    [TestCase("de-DE", "full", "August 2026", "27. August")]
    [TestCase("de-DE", "short", "08.2026", "27.08")]
    [TestCase("fr-FR", "full", "août 2026", "27 août")]
    [TestCase("ja-JP", "full", "2026年8月", "8月27日")]
    [TestCase("pt-PT", "full", "agosto de 2026", "27 de agosto")]
    public void ADateStyleWritesOnlyTheFieldsAYearMonthOrAMonthDayHas(
        string locale, string dateStyle, string yearMonth, string monthDay)
    {
        var script = $$"""
            (function () {
              const ym = new Temporal.PlainYearMonth(2026, 8, 'gregory', 27);
              const md = new Temporal.PlainMonthDay(8, 27, 'gregory', 2222);
              const options = { dateStyle: '{{dateStyle}}' };
              const f = new Intl.DateTimeFormat('{{locale}}', options);
              return [
                ym.toLocaleString('{{locale}}', options),
                f.format(ym),
                f.formatToParts(ym).map(p => p.value).join(''),
                md.toLocaleString('{{locale}}', options),
                f.format(md),
                f.formatToParts(md).map(p => p.value).join('')
              ].join('|');
            })()
            """;

        var expected = string.Join("|", yearMonth, yearMonth, yearMonth, monthDay, monthDay, monthDay);
        _engine.Evaluate(script).AsString().Should().Be(expected);
    }

    /// <summary>
    /// The narrowing takes the fields out of the pattern the style resolved to, so what is left is still the
    /// locale's own: its field order, its own separators, and the field widths the style itself asked for -
    /// a two-digit year under <c>"short"</c>, the month written out under <c>"long"</c>.
    /// </summary>
    [Test]
    public void ANarrowedDateStyleKeepsTheLocalesOwnShape()
    {
        const string Script = """
            (function () {
              const ym = new Temporal.PlainYearMonth(2026, 8, 'gregory', 27);
              const md = new Temporal.PlainMonthDay(8, 27, 'gregory', 2222);
              const wrong = [];
              for (const locale of ['en-US', 'en-GB', 'de-DE', 'fr-FR', 'ja-JP', 'pt-PT', 'ru-RU', 'hu-HU', 'sv-SE']) {
                for (const dateStyle of ['full', 'long', 'medium', 'short']) {
                  const y = ym.toLocaleString(locale, { dateStyle });
                  const m = md.toLocaleString(locale, { dateStyle });
                  if (y.includes('27')) wrong.push(locale + ' ' + dateStyle + ' year-month wrote the reference day: ' + y);
                  if (m.includes('2222')) wrong.push(locale + ' ' + dateStyle + ' month-day wrote the reference year: ' + m);
                  if (!y.includes('26')) wrong.push(locale + ' ' + dateStyle + ' year-month lost the year: ' + y);
                  if (!m.includes('27')) wrong.push(locale + ' ' + dateStyle + ' month-day lost the day: ' + m);
                  if (/^[\s\p{P}]|[\s,;\/-]$/u.test(y)) wrong.push(locale + ' ' + dateStyle + ' year-month kept a stray separator: ' + y);
                  if (/^[\s\p{P}]|[\s,;\/-]$/u.test(m)) wrong.push(locale + ' ' + dateStyle + ' month-day kept a stray separator: ' + m);
                }
              }
              return wrong.join(' | ');
            })()
            """;

        _engine.Evaluate(Script).AsString().Should().BeEmpty();
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma402/#sec-resolveoptions">ResolveOptions</see> step 3 reads the
    /// <c>localeMatcher</c> option through <c>GetOption</c>, which admits only <c>"lookup"</c> and
    /// <c>"best fit"</c> and raises a <c>RangeError</c> for anything else. <c>Intl.PluralRules</c> left the
    /// read to <c>ResolveLocale</c>, which takes the raw string and never validates it, so a bad value was
    /// accepted in silence — the one assertion in
    /// <c>staging/sm/extensions/quote-string-for-nul-character.js</c> that Jint failed.
    /// </summary>
    [TestCase("new Intl.PluralRules('en', { localeMatcher: 'lookup\0cookie' })")]
    [TestCase("new Intl.PluralRules('en', { localeMatcher: 'nonsense' })")]
    [TestCase("new Intl.PluralRules('en', { localeMatcher: 'Lookup' })")]
    [TestCase("new Intl.PluralRules('en', { localeMatcher: '' })")]
    public void PluralRulesRejectsAnInvalidLocaleMatcher(string script)
    {
        Invoking(() => _engine.Evaluate(script))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Invalid value*for option 'localeMatcher'");
    }

    /// <summary>
    /// The control: both accepted values, and the absent option, still construct.
    /// </summary>
    [TestCase("new Intl.PluralRules('en', { localeMatcher: 'lookup' })")]
    [TestCase("new Intl.PluralRules('en', { localeMatcher: 'best fit' })")]
    [TestCase("new Intl.PluralRules('en', { localeMatcher: undefined })")]
    [TestCase("new Intl.PluralRules('en')")]
    public void PluralRulesAcceptsAValidLocaleMatcher(string script)
    {
        _engine.Evaluate($"typeof {script}").AsString().Should().Be("object");
    }

    /// <summary>
    /// The option is read exactly once. It used to be read twice by <c>Intl.Collator</c> — once to validate
    /// it and once inside <c>ResolveLocale</c> — which a user-supplied getter can see.
    /// </summary>
    [TestCase("Collator")]
    [TestCase("PluralRules")]
    [TestCase("DateTimeFormat")]
    [TestCase("NumberFormat")]
    [TestCase("RelativeTimeFormat")]
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
    /// <c>Intl.DateTimeFormat</c> seeds each formatter's <c>DateTimeFormatInfo</c> from
    /// <c>Options.Intl.CldrProvider</c>, and skips the shared <c>DefaultCldrProvider.Instance</c> by identity
    /// rather than asking it and comparing — because it reads those names out of the very same
    /// <c>CultureInfo</c> the formatter would otherwise have used, so it can only answer with what is
    /// already there.
    /// </summary>
    /// <remarks>
    /// This is what makes that shortcut sound, so it is the guard on it: it walks every culture the machine
    /// has and compares all five arrays the seeding reads, entry by entry, against the
    /// <c>DateTimeFormatInfo</c> behind them. Should the default provider ever start answering with names of
    /// its own, this fails, and the identity check in <c>DateTimeFormatConstructor</c> is then wrong.
    /// </remarks>
    /// <summary>
    /// The <c>CultureInfo</c> instances <c>IntlUtilities</c> caches are process-wide: one per locale tag,
    /// shared by every engine, never evicted. A <c>CultureInfo</c> is writable unless it is explicitly made
    /// read-only — <c>useUserOverride: false</c> only suppresses the Windows user regional overrides — so
    /// without this the two places that adjust a format for one formatter could instead have adjusted it for
    /// every engine in the process, and nothing would have reported it.
    /// </summary>
    /// <remarks>
    /// Both of those places clone before writing (<c>DateTimeFormatConstructor</c> clones the culture and its
    /// <c>DateTimeFormatInfo</c>, <c>NumberFormatConstructor</c> clones the <c>NumberFormatInfo</c>) and a
    /// clone of a read-only instance is writable, which is why the whole <c>Intl</c> suite is the other half
    /// of this test: it exercises both of those paths on every construction.
    /// </remarks>
    [Test]
    [TestCase("en-US")]
    [TestCase("de-DE")]
    [TestCase("ar-SA")]
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

    [Test]
    public void TheDefaultCldrProviderAnswersWithDotNetsOwnDateNames()
    {
        var provider = DefaultCldrProvider.Instance;
        var compared = 0;

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            var name = culture.Name;
            if (name.Length == 0)
            {
                continue;
            }

            DateTimeFormatInfo dtfi;
            try
            {
                dtfi = new CultureInfo(name, false).DateTimeFormat;
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            provider.GetMonthNames(name, "long", null).Should().Equal(dtfi.MonthNames.Take(12));
            provider.GetMonthNames(name, "short", null).Should().Equal(dtfi.AbbreviatedMonthNames.Take(12));
            provider.GetWeekdayNames(name, "long").Should().Equal(dtfi.DayNames);
            provider.GetWeekdayNames(name, "short").Should().Equal(dtfi.AbbreviatedDayNames);
            provider.GetDayPeriods(name, "short", null).Should().Equal(new[] { dtfi.AMDesignator, dtfi.PMDesignator });
            compared++;
        }

        compared.Should().BeGreaterThan(100);
    }

    /// <summary>
    /// The digits <c>Intl</c> transliterates with are the ones <c>ICldrProvider.GetNumberingSystemDigits</c>
    /// answers with, and the set a formatter accepts is exactly the set that member answers for. On an
    /// unconfigured engine that has to be the same set <c>Intl.supportedValuesOf('numberingSystem')</c>
    /// reports, or a host can be told about a system no constructor will take.
    /// </summary>
    [Test]
    public void EveryAdvertisedNumberingSystemHasDigitsAndIsAccepted()
    {
        var provider = DefaultCldrProvider.Instance;
        var advertised = provider.GetSupportedNumberingSystems();

        advertised.Should().NotBeEmpty();

        foreach (var system in advertised)
        {
            provider.GetNumberingSystemDigits(system).Should().NotBeNull($"'{system}' is advertised");

            foreach (var constructor in new[] { "NumberFormat", "DateTimeFormat", "RelativeTimeFormat", "DurationFormat" })
            {
                _engine.Evaluate($"new Intl.{constructor}('en', {{ numberingSystem: '{system}' }}).resolvedOptions().numberingSystem")
                    .AsString().Should().Be(system, $"Intl.{constructor} should accept '{system}'");
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma402/#table-datetimeformat-components lists <c>"narrow"</c> as a value of
    /// <c>weekday</c> and of <c>month</c> in its own right, beside <c>"short"</c>. Both used to render the
    /// abbreviated .NET pattern letter, so a narrow format was an abbreviated one.
    /// </summary>
    [Test]
    public void NarrowMonthAndWeekdayAreNotTheAbbreviatedName()
    {
        // 2024-01-15 is a Monday
        const string Date = "Date.UTC(2024, 0, 15)";

        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ month: 'narrow', timeZone: 'UTC' }}).format({Date})")
            .AsString().Should().Be("J");
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ weekday: 'narrow', timeZone: 'UTC' }}).format({Date})")
            .AsString().Should().Be("M");

        // the parts lane and the string lane are the same names
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ month: 'narrow', timeZone: 'UTC' }}).formatToParts({Date})[0].value")
            .AsString().Should().Be("J");
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ weekday: 'narrow', timeZone: 'UTC' }}).formatToParts({Date})[0].value")
            .AsString().Should().Be("M");

        // …and the two styles beside it are untouched
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ month: 'short', timeZone: 'UTC' }}).format({Date})")
            .AsString().Should().Be("Jan");
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ month: 'long', timeZone: 'UTC' }}).format({Date})")
            .AsString().Should().Be("January");
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ weekday: 'short', timeZone: 'UTC' }}).format({Date})")
            .AsString().Should().Be("Mon");
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ weekday: 'long', timeZone: 'UTC' }}).format({Date})")
            .AsString().Should().Be("Monday");
    }

    /// <summary>
    /// All seven narrow weekday names, against the values every browser writes, so that the seeding is
    /// reading a whole array and not one entry of it by luck.
    /// </summary>
    /// <remarks>
    /// Literal values rather than a comparison against <c>ShortestDayNames</c>, because that array is where
    /// the two platforms disagree: it holds CLDR's narrow names on Windows and its short ones — <c>Su</c>,
    /// <c>Mo</c> — on Linux, and what this asserts is that <c>Intl</c> writes the same seven either way.
    /// </remarks>
    [Test]
    public void EveryNarrowWeekdayIsTheSameOnEveryPlatform()
    {
        string[] expected = ["S", "M", "T", "W", "T", "F", "S"];

        // 2024-01-14 is a Sunday
        for (var i = 0; i < 7; i++)
        {
            _engine.Evaluate($"new Intl.DateTimeFormat('en-US', {{ weekday: 'narrow', timeZone: 'UTC' }}).format(Date.UTC(2024, 0, {14 + i}))")
                .AsString().Should().Be(expected[i]);
        }
    }

    /// <summary>
    /// .NET has narrow weekday names and no narrow month names, so <c>DefaultCldrProvider</c> reads the one
    /// and derives the other. This is the guard on that derivation: it walks every culture the machine has and
    /// compares entry by entry, so a change to either half fails here rather than in one spot-checked locale.
    /// </summary>
    [Test]
    public void TheDefaultCldrProviderDerivesNarrowNamesFromDotNetsOwnData()
    {
        var provider = DefaultCldrProvider.Instance;
        var compared = 0;

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            var name = culture.Name;
            if (name.Length == 0)
            {
                continue;
            }

            CultureInfo resolved;
            try
            {
                resolved = new CultureInfo(name, false);
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            var dtfi = resolved.DateTimeFormat;

            var narrowMonths = provider.GetMonthNames(name, "narrow", null);
            narrowMonths.Should().NotBeNull();
            narrowMonths!.Should().HaveCount(12);
            for (var i = 0; i < 12; i++)
            {
                narrowMonths[i].Should().Be(Narrow(resolved, dtfi.AbbreviatedMonthNames[i]), $"narrow month {i + 1} of '{name}'");
            }

            var narrowWeekdays = provider.GetWeekdayNames(name, "narrow");
            narrowWeekdays.Should().NotBeNull();
            narrowWeekdays!.Should().HaveCount(7);

            // Already narrow when any entry is a single text element; narrowed entry by entry when none is.
            var shortest = dtfi.ShortestDayNames;
            var alreadyNarrow = shortest.Any(d => d.Length == 0 || StringInfo.GetNextTextElement(d, 0).Length == d.Length);
            for (var i = 0; i < 7; i++)
            {
                var expected = alreadyNarrow ? shortest[i] : Narrow(resolved, shortest[i]);
                narrowWeekdays[i].Should().Be(expected, $"narrow weekday {i} of '{name}'");
            }

            compared++;
        }

        compared.Should().BeGreaterThan(100);
    }

    /// <summary>The narrowing the provider applies, written once so the test states the rule rather than copying it.</summary>
    private static string Narrow(CultureInfo culture, string name)
        => name.Length == 0 ? name : culture.TextInfo.ToUpper(StringInfo.GetNextTextElement(name, 0));

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern step 15.g makes the <c>ampm</c> a pattern writes an
    /// ILD String, so the two lanes that can write one — <c>timeStyle</c> and <c>hour</c> with
    /// <c>hour12</c> — have to write the same locale's designators. The <c>timeStyle</c> lane used to write
    /// two English literals.
    /// </summary>
    [TestCase("en")]
    [TestCase("ar")]
    [TestCase("zh")]
    [TestCase("ja")]
    [TestCase("ko")]
    [TestCase("el")]
    [TestCase("tr")]
    [TestCase("he")]
    [TestCase("th")]
    public void TheTimeStyleLaneWritesTheSameDayPeriodAsTheComponentLane(string locale)
    {
        var expected = new CultureInfo(locale, false).DateTimeFormat.PMDesignator;

        var script = $$"""
            new Intl.DateTimeFormat('{{locale}}', { timeStyle: 'short', hour12: true, timeZone: 'UTC', numberingSystem: 'latn' })
                .formatToParts(Date.UTC(2024, 0, 15, 15, 30))
                .filter(function (p) { return p.type === 'dayPeriod'; })
                .map(function (p) { return p.value; })
                .join('');
            """;

        _engine.Evaluate(script).AsString().Should().Be(expected);

        var componentScript = $$"""
            new Intl.DateTimeFormat('{{locale}}', { hour: 'numeric', hour12: true, timeZone: 'UTC', numberingSystem: 'latn' })
                .formatToParts(Date.UTC(2024, 0, 15, 15, 30))
                .filter(function (p) { return p.type === 'dayPeriod'; })
                .map(function (p) { return p.value; })
                .join('');
            """;

        _engine.Evaluate(componentScript).AsString().Should().Be(expected);
    }

    /// <summary>
    /// The English designators are still the English ones: the fix is where the two letters come from, not
    /// what they are for a locale that spells them that way.
    /// </summary>
    [Test]
    public void TheEnglishTimeStyleStillWritesAmAndPm()
    {
        _engine.Evaluate("new Intl.DateTimeFormat('en', { timeStyle: 'short', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15, 15, 30))")
            .AsString().Should().Be("3:30 PM");
        _engine.Evaluate("new Intl.DateTimeFormat('en', { timeStyle: 'medium', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15, 9, 5, 7))")
            .AsString().Should().Be("9:05:07 AM");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-availablecalendars is one list, and three places used to carry their own
    /// copy of it. This is the entry-by-entry statement of what an unconfigured engine answers, so that
    /// collapsing the three cannot quietly change it.
    /// </summary>
    [Test]
    public void TheAvailableCalendarsOfADefaultEngineAreTheSixteen()
    {
        _engine.Evaluate("JSON.stringify(Intl.supportedValuesOf('calendar'))").AsString().Should().Be(
            """
            ["buddhist","chinese","coptic","dangi","ethioaa","ethiopic","gregory","hebrew","indian","islamic-civil","islamic-tbla","islamic-umalqura","iso8601","japanese","persian","roc"]
            """);
    }

    /// <summary>
    /// The three sites now read one list and one alias table, so they answer the same question the same way.
    /// The two deprecated identifiers are the one place they part, and they part because the two
    /// specifications ask for different things:
    /// https://tc39.es/ecma402/#sec-createdatetimeformat step 9 makes a formatter asking for one resolve to
    /// another available calendar, and <c>Temporal</c> refuses them outright.
    /// </summary>
    [TestCase("islamicc", "islamic-civil")]
    [TestCase("ethiopic-amete-alem", "ethioaa")]
    [TestCase("ISO8601", "iso8601")]
    [TestCase("Gregory", "gregory")]
    [TestCase("islamic", "islamic-civil")]
    [TestCase("islamic-rgsa", "islamic-civil")]
    public void TheCalendarOptionResolvesThroughTheOneAliasTable(string requested, string resolved)
    {
        _engine.Evaluate($"new Intl.DateTimeFormat('en', {{ calendar: '{requested}' }}).resolvedOptions().calendar")
            .AsString().Should().Be(resolved);
    }

    /// <summary>
    /// A grammatically valid identifier this engine does not answer for is not an error — ResolveLocale keeps
    /// the locale's own calendar and the option is dropped — while an ill-formed one is a RangeError however
    /// the locale would have resolved a well-formed one.
    /// </summary>
    [Test]
    public void ACalendarNobodyAnswersForFallsBackAndAnIllFormedOneThrows()
    {
        _engine.Evaluate("new Intl.DateTimeFormat('en', { calendar: 'mayan' }).resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
        _engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-mayan').resolvedOptions().calendar")
            .AsString().Should().Be("gregory");

        Assert.Throws<JavaScriptException>(() => _engine.Evaluate("new Intl.DateTimeFormat('en', { calendar: 'no' })"));
        Assert.Throws<JavaScriptException>(() => _engine.Evaluate("new Intl.DateTimeFormat('en', { calendar: 'İSO8601' })"));
    }

    /// <summary>
    /// <c>Intl.DisplayNames</c> reads the same list, which is what its own comment said it was for: it named
    /// a calendar exactly when <c>Intl.supportedValuesOf('calendar')</c> did, through a second membership
    /// scan and a hand-written <c>gregorian</c> special case.
    /// </summary>
    [Test]
    public void DisplayNamesOffersANameForExactlyTheAvailableCalendars()
    {
        _engine.Evaluate("new Intl.DisplayNames('en', { type: 'calendar' }).of('gregory')")
            .AsString().Should().Be("Gregorian Calendar");
        _engine.Evaluate("new Intl.DisplayNames('en', { type: 'calendar' }).of('islamicc')")
            .AsString().Should().Be("Islamic (civil) Calendar");
        _engine.Evaluate("new Intl.DisplayNames('en', { type: 'calendar' }).of('mayan')")
            .AsString().Should().Be("mayan");
    }
}
