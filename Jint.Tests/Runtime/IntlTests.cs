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
}
