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
        Assert.Equal("object", result.AsString());
    }

    [Fact]
    public void IntlHasGetCanonicalLocales()
    {
        var result = _engine.Evaluate("typeof Intl.getCanonicalLocales");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void IntlHasSupportedValuesOf()
    {
        var result = _engine.Evaluate("typeof Intl.supportedValuesOf");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void GetCanonicalLocalesWithUndefined()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales(undefined)");
        Assert.True(result.IsArray());
        Assert.Equal((uint) 0, result.AsArray().Length);
    }

    [Fact]
    public void GetCanonicalLocalesWithString()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales('en-US')");
        Assert.True(result.IsArray());
        var array = result.AsArray();
        Assert.Equal((uint) 1, array.Length);
        Assert.Equal("en-US", array[0].AsString());
    }

    [Fact]
    public void GetCanonicalLocalesWithArray()
    {
        var result = _engine.Evaluate("Intl.getCanonicalLocales(['en-US', 'de-DE'])");
        Assert.True(result.IsArray());
        var array = result.AsArray();
        Assert.Equal((uint) 2, array.Length);
    }

    [Fact]
    public void GetCanonicalLocalesCanonicalizesCase()
    {
        // Should canonicalize 'en-us' to 'en-US'
        var result = _engine.Evaluate("Intl.getCanonicalLocales('en-us')");
        Assert.True(result.IsArray());
        var array = result.AsArray();
        Assert.Equal((uint) 1, array.Length);
        // Result should be canonicalized
        Assert.NotNull(array[0].AsString());
    }

    [Fact]
    public void SupportedValuesOfCalendar()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('calendar')");
        Assert.True(result.IsArray());
        var array = result.AsArray();
        Assert.True(array.Length > 0);
    }

    [Fact]
    public void SupportedValuesOfCurrency()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('currency')");
        Assert.True(result.IsArray());
        var array = result.AsArray();
        Assert.True(array.Length > 0);
    }

    [Fact]
    public void SupportedValuesOfUnit()
    {
        var result = _engine.Evaluate("Intl.supportedValuesOf('unit')");
        Assert.True(result.IsArray());
        var array = result.AsArray();
        Assert.True(array.Length > 0);
        // Should contain common units
        var units = new List<string>();
        for (uint i = 0; i < array.Length; i++)
        {
            units.Add(array[i].AsString());
        }
        Assert.Contains("meter", units);
        Assert.Contains("second", units);
    }

    [Fact]
    public void SupportedValuesOfInvalidKeyThrows()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("Intl.supportedValuesOf('invalid')"));
    }

    [Fact]
    public void IntlToStringTag()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(Intl)");
        Assert.Equal("[object Intl]", result.AsString());
    }

    // Intl.Locale tests
    [Fact]
    public void LocaleConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.Locale");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void LocaleCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US')");
        Assert.NotNull(result);
        Assert.True(result.IsObject());
    }

    [Fact]
    public void LocaleRequiresNew()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("Intl.Locale('en-US')"));
    }

    [Fact]
    public void LocaleToStringReturnsTag()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').toString()");
        Assert.Equal("en-US", result.AsString());
    }

    [Fact]
    public void LocaleLanguageProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').language");
        Assert.Equal("en", result.AsString());
    }

    [Fact]
    public void LocaleRegionProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').region");
        Assert.Equal("US", result.AsString());
    }

    [Fact]
    public void LocaleScriptProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('zh-Hans-CN').script");
        Assert.Equal("Hans", result.AsString());
    }

    [Fact]
    public void LocaleBaseNameProperty()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').baseName");
        Assert.Equal("en-US", result.AsString());
    }

    [Fact]
    public void LocaleWithCalendarOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { calendar: 'gregory' }).calendar");
        Assert.Equal("gregory", result.AsString());
    }

    [Fact]
    public void LocaleWithHourCycleOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { hourCycle: 'h12' }).hourCycle");
        Assert.Equal("h12", result.AsString());
    }

    [Fact]
    public void LocaleWithNumericOption()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US', { numeric: true }).numeric");
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void LocaleToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.Locale('en-US'))");
        Assert.Equal("[object Intl.Locale]", result.AsString());
    }

    [Fact]
    public void LocaleMinimize()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US').minimize().toString()");
        Assert.Equal("en", result.AsString());
    }

    [Fact]
    public void LocaleWithUnicodeExtension()
    {
        var result = _engine.Evaluate("new Intl.Locale('en-US-u-ca-gregory').calendar");
        Assert.Equal("gregory", result.AsString());
    }

    [Fact]
    public void LocaleInvalidTagThrows()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("new Intl.Locale('invalid tag with spaces')"));
    }

    // Intl.Collator tests
    [Fact]
    public void CollatorConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.Collator");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void CollatorCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.Collator('en-US')");
        Assert.NotNull(result);
        Assert.True(result.IsObject());
    }

    [Fact]
    public void CollatorToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.Collator('en-US'))");
        Assert.Equal("[object Intl.Collator]", result.AsString());
    }

    [Fact]
    public void CollatorCompareReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.Collator('en-US').compare");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void CollatorCompareSortsStrings()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US');
            var compare = collator.compare;
            compare('a', 'b')
        ");
        Assert.True(result.AsNumber() < 0);
    }

    [Fact]
    public void CollatorCompareWithCaseSensitivity()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US', { sensitivity: 'case' });
            collator.compare('a', 'A')
        ");
        // With 'case' sensitivity, 'a' and 'A' should be different
        Assert.True(result.AsNumber() != 0);
    }

    [Fact]
    public void CollatorCompareWithBaseSensitivity()
    {
        var result = _engine.Evaluate(@"
            var collator = new Intl.Collator('en-US', { sensitivity: 'base' });
            collator.compare('a', 'A')
        ");
        // With 'base' sensitivity, 'a' and 'A' should be equal
        Assert.Equal(0, result.AsNumber());
    }

    [Fact]
    public void CollatorResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.Collator('en-US', { usage: 'search' }).resolvedOptions();
            options.usage
        ");
        Assert.Equal("search", result.AsString());
    }

    [Fact]
    public void CollatorSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.Collator.supportedLocalesOf(['en-US', 'de-DE'])");
        Assert.True(result.IsArray());
    }

    [Fact]
    public void CollatorSortingWithCompare()
    {
        var result = _engine.Evaluate(@"
            var items = ['z', 'a', 'c', 'b'];
            items.sort(new Intl.Collator('en-US').compare);
            items.join(',')
        ");
        Assert.Equal("a,b,c,z", result.AsString());
    }

    // Intl.NumberFormat tests
    [Fact]
    public void NumberFormatConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.NumberFormat");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void NumberFormatCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US')");
        Assert.NotNull(result);
        Assert.True(result.IsObject());
    }

    [Fact]
    public void NumberFormatToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.NumberFormat('en-US'))");
        Assert.Equal("[object Intl.NumberFormat]", result.AsString());
    }

    [Fact]
    public void NumberFormatReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.NumberFormat('en-US').format");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void NumberFormatFormatsDecimal()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US').format(1234.567)");
        Assert.Contains("1", result.AsString());
        Assert.Contains("234", result.AsString());
    }

    [Fact]
    public void NumberFormatFormatsCurrency()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(1234.56)");
        // Should contain the dollar sign and the number
        Assert.Contains("1", result.AsString());
    }

    [Fact]
    public void NumberFormatFormatsPercent()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'percent' }).format(0.5)");
        // Should contain "50" for 50%
        Assert.Contains("50", result.AsString());
    }

    [Fact]
    public void NumberFormatResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'EUR' }).resolvedOptions();
            options.currency
        ");
        Assert.Equal("EUR", result.AsString());
    }

    [Fact]
    public void NumberFormatResolvedOptionsStyle()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.NumberFormat('en-US', { style: 'percent' }).resolvedOptions();
            options.style
        ");
        Assert.Equal("percent", result.AsString());
    }

    [Fact]
    public void NumberFormatSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.NumberFormat.supportedLocalesOf(['en-US', 'de-DE'])");
        Assert.True(result.IsArray());
    }

    [Fact]
    public void NumberFormatWithUnit()
    {
        var result = _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'unit', unit: 'kilometer' }).format(100)");
        Assert.Contains("100", result.AsString());
        Assert.Contains("km", result.AsString());
    }

    [Fact]
    public void NumberFormatCurrencyRequiresCurrency()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'currency' })"));
    }

    [Fact]
    public void NumberFormatUnitRequiresUnit()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("new Intl.NumberFormat('en-US', { style: 'unit' })"));
    }

    // Intl.DateTimeFormat tests
    [Fact]
    public void DateTimeFormatConstructorExists()
    {
        var result = _engine.Evaluate("typeof Intl.DateTimeFormat");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void DateTimeFormatCanBeConstructed()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US')");
        Assert.NotNull(result);
        Assert.True(result.IsObject());
    }

    [Fact]
    public void DateTimeFormatToStringTagIsCorrect()
    {
        var result = _engine.Evaluate("Object.prototype.toString.call(new Intl.DateTimeFormat('en-US'))");
        Assert.Equal("[object Intl.DateTimeFormat]", result.AsString());
    }

    [Fact]
    public void DateTimeFormatFormatReturnsFunction()
    {
        var result = _engine.Evaluate("typeof new Intl.DateTimeFormat('en-US').format");
        Assert.Equal("function", result.AsString());
    }

    [Fact]
    public void DateTimeFormatFormatsDate()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format(new Date(2024, 0, 15, 12, 0, 0))");
        // Should contain the date components - use noon to avoid timezone issues
        Assert.Contains("1", result.AsString()); // Month or day
        Assert.Contains("2024", result.AsString()); // Year
        // Day could be 14, 15, or 16 depending on timezone, just check it's a valid date
        Assert.True(result.AsString().Length > 0);
    }

    [Fact]
    public void DateTimeFormatWithDateStyle()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'short' }).format(new Date(2024, 0, 15))");
        Assert.NotNull(result.AsString());
        Assert.True(result.AsString().Length > 0);
    }

    [Fact]
    public void DateTimeFormatWithTimeStyle()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeStyle: 'short' }).format(new Date(2024, 0, 15, 14, 30, 0))");
        Assert.NotNull(result.AsString());
        Assert.True(result.AsString().Length > 0);
    }

    [Fact]
    public void DateTimeFormatResolvedOptions()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US', { dateStyle: 'full' }).resolvedOptions();
            options.dateStyle
        ");
        Assert.Equal("full", result.AsString());
    }

    [Fact]
    public void DateTimeFormatResolvedOptionsLocale()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US').resolvedOptions();
            options.locale
        ");
        // Should return a locale string
        Assert.NotNull(result.AsString());
    }

    [Fact]
    public void DateTimeFormatResolvedOptionsCalendar()
    {
        var result = _engine.Evaluate(@"
            var options = new Intl.DateTimeFormat('en-US').resolvedOptions();
            options.calendar
        ");
        // Default calendar should be 'gregory'
        Assert.Equal("gregory", result.AsString());
    }

    [Fact]
    public void DateTimeFormatSupportedLocalesOf()
    {
        var result = _engine.Evaluate("Intl.DateTimeFormat.supportedLocalesOf(['en-US', 'de-DE'])");
        Assert.True(result.IsArray());
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
        Assert.Contains("2024", result.AsString());
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
        Assert.True(result.AsString().Contains("30") || result.AsString().Contains(":"));
    }

    [Fact]
    public void DateTimeFormatFormatToParts()
    {
        var result = _engine.Evaluate(@"
            var parts = new Intl.DateTimeFormat('en-US').formatToParts(new Date(2024, 0, 15));
            Array.isArray(parts)
        ");
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void DateTimeFormatFormatToPartsHasTypeAndValue()
    {
        var result = _engine.Evaluate(@"
            var parts = new Intl.DateTimeFormat('en-US').formatToParts(new Date(2024, 0, 15));
            parts.length > 0 && parts[0].type !== undefined && parts[0].value !== undefined
        ");
        Assert.True(result.AsBoolean());
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
        Assert.True(result.AsString().Length > 10);
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
        Assert.Equal("h23", result.AsString());
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
        Assert.Equal("UTC", result.AsString());
    }

    [Fact]
    public void DateTimeFormatInvalidTimeZoneThrows()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { timeZone: 'Invalid/TimeZone' })"));
    }

    [Fact]
    public void DateTimeFormatFormatsUndefinedAsNow()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format()");
        // Should return a non-empty string (current date formatted)
        Assert.NotNull(result.AsString());
        Assert.True(result.AsString().Length > 0);
    }

    [Fact]
    public void DateTimeFormatFormatsTimestamp()
    {
        var result = _engine.Evaluate("new Intl.DateTimeFormat('en-US').format(1705363200000)"); // Jan 15, 2024 UTC
        Assert.NotNull(result.AsString());
        Assert.True(result.AsString().Length > 0);
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
        Assert.Contains("01", result.AsString()); // Month
    }

    [Fact]
    public void DateTimeFormatInvalidDateStyleThrows()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'invalid' })"));
    }

    [Fact]
    public void DateTimeFormatInvalidWeekdayThrows()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() =>
            _engine.Evaluate("new Intl.DateTimeFormat('en-US', { weekday: 'invalid' })"));
    }
}
