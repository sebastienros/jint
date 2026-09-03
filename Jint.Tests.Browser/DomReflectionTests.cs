using AngleSharp;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Browser;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes">HTML
/// §2.6.1's reflection</a>: the algorithms in <c>ReflectedAttribute</c>, and the members
/// <c>overrides.json</c>'s <c>reflected</c> list wires them onto.
/// </summary>
/// <remarks>
/// <para>
/// The corpus checks this exhaustively — <c>html/dom/reflection-misc.html</c> is 4,877 assertions of it — so
/// what is here is the half a vendored document cannot reach: the types no <c>reflected</c> row uses <em>yet</em>,
/// which would otherwise be machinery nothing runs. Those are tested against
/// <see cref="ReflectedAttribute"/> directly, which is also the only way to state a type's rule without an
/// element that happens to carry an attribute of that type.
/// </para>
/// <para>
/// The rest is end-to-end, through the generated accessor pair and a real engine, because that is where the
/// wiring can be wrong in ways the algorithm cannot: a member on the wrong interface, a getter over the wrong
/// content attribute, a setter the shape never declared.
/// </para>
/// </remarks>
public sealed class DomReflectionTests
{
    [Test]
    public void ADomStringAttributeReflectsTheContentAttributeAndDefaultsToEmpty()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Text("document.querySelector('#a').title").Should().BeEmpty();

        fixture.Evaluate("document.querySelector('#a').title = 'hello'");
        fixture.Text("document.querySelector('#a').getAttribute('title')").Should().Be("hello");
        fixture.Text("document.querySelector('#a').title").Should().Be("hello");

        // A number assigned to a DOMString attribute is stringified, per WebIDL's ToString.
        fixture.Evaluate("document.querySelector('#a').title = 42");
        fixture.Text("document.querySelector('#a').getAttribute('title')").Should().Be("42");
    }

    /// <summary>
    /// <c>lang</c> answers what <b>this</b> element declares, which is what makes it a reflected attribute
    /// rather than a computed one.
    /// </summary>
    /// <remarks>
    /// AngleSharp's <c>Language</c> walks to the nearest ancestor carrying the attribute and falls back to
    /// the current culture, so before this it answered <c>"en-US"</c> for an element with no <c>lang</c>
    /// anywhere above it — a page asking what language a paragraph declares was told what machine it was
    /// running on, and the answer moved with the runner's locale.
    /// </remarks>
    [Test]
    public void ADomStringAttributeIsNotInheritedFromAnAncestorOrFromTheCulture()
    {
        using var fixture = DomTestFixture.Create("<html lang='fr'><body><p id='p'>x</p></body></html>");

        fixture.Text("document.querySelector('#p').lang").Should().BeEmpty();
        fixture.Text("document.documentElement.lang").Should().Be("fr");

        fixture.Evaluate("document.querySelector('#p').lang = 'de'");
        fixture.Text("document.querySelector('#p').getAttribute('lang')").Should().Be("de");
    }

    [Test]
    public void AUrlAttributeReflectsAnAbsoluteUrl()
    {
        using var fixture = DomTestFixture.Create("<a id='a' href='/relative'>x</a><script id='s' src='sub/one.js'></script>");

        // The content attribute keeps what was written; the IDL attribute answers what it resolves to.
        fixture.Text("document.querySelector('#s').getAttribute('src')").Should().Be("sub/one.js");
        fixture.Text("document.querySelector('#s').src").Should().Be("http://localhost/sub/one.js");

        fixture.Evaluate("document.querySelector('#a').href = 'https://example.com/x?y#z'");
        fixture.Text("document.querySelector('#a').href").Should().Be("https://example.com/x?y#z");
        fixture.Text("document.querySelector('#a').getAttribute('href')").Should().Be("https://example.com/x?y#z");
    }

    /// <summary>An absent URL attribute is <c>""</c>, and one the URL parser refuses is itself.</summary>
    [Test]
    public void AUrlAttributeAnswersEmptyWhenAbsentAndTheRawValueWhenUnparseable()
    {
        using var fixture = DomTestFixture.Create("<script id='s'></script>");

        fixture.Text("document.querySelector('#s').src").Should().BeEmpty();

        // A URL whose parse fails is returned as it stands rather than as the empty string, which is the one
        // place this differs from AngleSharp's own GetUrlAttribute.
        fixture.Evaluate("document.querySelector('#s').setAttribute('src', 'http://[bad')");
        fixture.Text("document.querySelector('#s').src").Should().Be("http://[bad");
    }

    [Test]
    public void ABooleanAttributeReflectsPresence()
    {
        using var fixture = DomTestFixture.Create("<input id='i'>");

        fixture.Bool("document.querySelector('#i').disabled").Should().BeFalse();

        fixture.Evaluate("document.querySelector('#i').disabled = true");
        fixture.Bool("document.querySelector('#i').hasAttribute('disabled')").Should().BeTrue();
        fixture.Text("document.querySelector('#i').getAttribute('disabled')").Should().BeEmpty();

        // Presence, not value: `disabled="false"` is still disabled.
        fixture.Evaluate("document.querySelector('#i').setAttribute('disabled', 'false')");
        fixture.Bool("document.querySelector('#i').disabled").Should().BeTrue();

        fixture.Evaluate("document.querySelector('#i').disabled = false");
        fixture.Bool("document.querySelector('#i').hasAttribute('disabled')").Should().BeFalse();
    }

    /// <summary><c>autofocus</c> is HTMLElement's, so a <c>&lt;div&gt;</c> has it too.</summary>
    [Test]
    public void ABooleanGlobalAttributeIsOnEveryHtmlElement()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Bool("document.querySelector('#a').autofocus").Should().BeFalse();

        // WebIDL's `boolean` is ToBoolean, so any truthy value sets the attribute to the empty string.
        fixture.Evaluate("document.querySelector('#a').autofocus = 'anything'");
        fixture.Text("document.querySelector('#a').getAttribute('autofocus')").Should().BeEmpty();
        fixture.Bool("document.querySelector('#a').autofocus").Should().BeTrue();
    }

    [Test]
    public void ALongAttributeConvertsThroughToInt32()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(0);

        fixture.Evaluate("document.querySelector('#a').tabIndex = 5");
        fixture.Text("document.querySelector('#a').getAttribute('tabindex')").Should().Be("5");

        // WebIDL's `long` is ToInt32, so a string, a float and a boolean all have defined answers.
        fixture.Evaluate("document.querySelector('#a').tabIndex = '7'");
        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(7);

        fixture.Evaluate("document.querySelector('#a').tabIndex = 3.9");
        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(3);
    }

    /// <summary>
    /// HTML's rules for parsing integers take a <em>prefix</em>: they stop at the first character that is not
    /// a digit and keep what they had, so <c>tabindex="1.5"</c> is 1 and <c>tabindex="5%"</c> is 5.
    /// </summary>
    /// <remarks>
    /// Only ASCII whitespace is skipped, which is why <c>"7"</c> — a vertical tab, whitespace to
    /// <see cref="char.IsWhiteSpace(char)"/> and not to HTML — is an error and answers the default.
    /// </remarks>
    [TestCase("7", 7)]
    [TestCase("  7  ", 7)]
    [TestCase("\t7", 7)]
    [TestCase("1.5", 1)]
    [TestCase("5%", 5)]
    [TestCase("+100", 100)]
    [TestCase("-36", -36)]
    [TestCase("-0", 0)]
    [TestCase("", 0)]
    [TestCase("-", 0)]
    [TestCase(".5", 0)]
    [TestCase("7", 0)]
    [TestCase(" 7", 0)]
    [TestCase("2147483648", 0)]
    public void ALongAttributeTakesHtmlsRulesForParsingIntegers(string attribute, int expected)
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Evaluate($"document.querySelector('#a').setAttribute('tabindex', {Literal(attribute)})");
        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(expected);
    }

    [Test]
    public void AnEnumeratedAttributeFallsBackToItsInvalidValueDefault()
    {
        using var fixture = DomTestFixture.Create("<input id='i' type='email'>");

        fixture.Text("document.querySelector('#i').type").Should().Be("email");

        // https://html.spec.whatwg.org/multipage/input.html#attr-input-type — the invalid value default and
        // the missing value default are both "text".
        fixture.Evaluate("document.querySelector('#i').setAttribute('type', 'not-a-type')");
        fixture.Text("document.querySelector('#i').type").Should().Be("text");

        fixture.Evaluate("document.querySelector('#i').removeAttribute('type')");
        fixture.Text("document.querySelector('#i').type").Should().Be("text");

        // And the reflected value is ASCII-lowercased.
        fixture.Evaluate("document.querySelector('#i').setAttribute('type', 'CHECKBOX')");
        fixture.Text("document.querySelector('#i').type").Should().Be("checkbox");
    }

    /// <summary>
    /// An enumerated attribute's getter answers the keyword in its canonical case, its invalid value default
    /// for anything else, and its missing value default when the attribute is absent — while its setter is
    /// transparent, writing whatever it was given.
    /// </summary>
    [Test]
    public void AnEnumeratedAttributeGetsByStateAndSetsVerbatim()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        // `dir` has no invalid value default and no missing value default, so both are "".
        fixture.Text("document.querySelector('#a').dir").Should().BeEmpty();

        fixture.Evaluate("document.querySelector('#a').setAttribute('dir', 'RTL')");
        fixture.Text("document.querySelector('#a').dir").Should().Be("rtl");

        fixture.Evaluate("document.querySelector('#a').setAttribute('dir', '5%')");
        fixture.Text("document.querySelector('#a').dir").Should().BeEmpty();

        // The setter writes the value it was given, including one no keyword matches: the state the element
        // is in is the getter's answer, not the attribute's content.
        fixture.Evaluate("document.querySelector('#a').dir = 'AUTO'");
        fixture.Text("document.querySelector('#a').getAttribute('dir')").Should().Be("AUTO");
        fixture.Text("document.querySelector('#a').dir").Should().Be("auto");
    }

    /// <summary>
    /// The keyword match is <b>ASCII</b> case-insensitive. U+017F LATIN SMALL LETTER LONG S case-folds to
    /// <c>s</c> under Unicode's rules, so every comparison but an ASCII one would call <c>ſearch</c> the
    /// <c>search</c> keyword — which is a row of the corpus and was worth stating here as well.
    /// </summary>
    [Test]
    public void AnEnumeratedAttributesKeywordMatchIsAsciiCaseInsensitiveOnly()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Evaluate("document.querySelector('#a').setAttribute('inputmode', 'SEARCH')");
        fixture.Text("document.querySelector('#a').inputMode").Should().Be("search");

        fixture.Evaluate("document.querySelector('#a').setAttribute('inputmode', 'ſearch')");
        fixture.Text("document.querySelector('#a').inputMode").Should().BeEmpty();

        // The same trap in the other direction: U+212A KELVIN SIGN for the "k" of a keyword.
        fixture.Evaluate("document.querySelector('#a').setAttribute('inputmode', 'tel')");
        fixture.Text("document.querySelector('#a').inputMode").Should().Be("tel");
    }

    /// <summary>
    /// A CORS settings attribute is the nullable enumeration: its IDL type is <c>DOMString?</c>, an absent
    /// attribute is <c>null</c>, <b>any</b> value that is not a keyword — the empty string included — is the
    /// Anonymous state, and setting <c>null</c> removes the attribute.
    /// </summary>
    [Test]
    public void ANullableEnumeratedAttributeIsNullWhenAbsentAndRemovedWhenSetToNull()
    {
        using var fixture = DomTestFixture.Create("<script id='s'></script>");

        fixture.Text("document.querySelector('#s').crossOrigin").Should().BeNull();

        fixture.Evaluate("document.querySelector('#s').setAttribute('crossorigin', '')");
        fixture.Text("document.querySelector('#s').crossOrigin").Should().Be("anonymous");

        fixture.Evaluate("document.querySelector('#s').setAttribute('crossorigin', 'USE-CREDENTIALS')");
        fixture.Text("document.querySelector('#s').crossOrigin").Should().Be("use-credentials");

        fixture.Evaluate("document.querySelector('#s').crossOrigin = null");
        fixture.Bool("document.querySelector('#s').hasAttribute('crossorigin')").Should().BeFalse();
        fixture.Text("document.querySelector('#s').crossOrigin").Should().BeNull();

        // undefined reaches a `DOMString?` the same way null does.
        fixture.Evaluate("document.querySelector('#s').crossOrigin = 'anonymous'");
        fixture.Evaluate("document.querySelector('#s').crossOrigin = undefined");
        fixture.Bool("document.querySelector('#s').hasAttribute('crossorigin')").Should().BeFalse();
    }

    /// <summary>
    /// The two directions are the same write, so nothing can drift: the parser's attribute is visible through
    /// the IDL attribute, and a write through either is visible through the other.
    /// </summary>
    [Test]
    public void TheContentAttributeIsTheOnlyStorage()
    {
        using var fixture = DomTestFixture.Create("<div id='a' dir='rtl' tabindex='3' autofocus></div>");

        fixture.Text("document.querySelector('#a').dir").Should().Be("rtl");
        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(3);
        fixture.Bool("document.querySelector('#a').autofocus").Should().BeTrue();

        // The CLR side of the same element sees every write the IDL attribute made.
        fixture.Evaluate("document.querySelector('#a').dir = 'ltr'");
        fixture.Document.QuerySelector("#a")!.GetAttribute("dir").Should().Be("ltr");
    }

    /// <summary>
    /// A reflected member is WebIDL's attribute like any other generated one: an enumerable, configurable
    /// accessor pair on the interface prototype, refusing a receiver that is not of the interface.
    /// </summary>
    [Test]
    public void AReflectedMemberIsAnAccessorPairOnTheInterfacePrototype()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Text("""
            (function () {
              const own = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'dir');
              const el = document.querySelector('#a');
              return [
                'dir' in el,
                el.hasOwnProperty('dir'),
                typeof own.get,
                typeof own.set,
                own.enumerable,
                own.configurable,
              ].join('|');
            })()
            """).Should().Be("true|false|function|function|true|true");

        fixture.Text("""
            (function () {
              try { Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'dir').get.call({}); return 'no throw'; }
              catch (e) { return e.constructor.name + ': ' + e.message; }
            })()
            """).Should().Be("TypeError: Failed to execute 'HTMLElement.dir': Illegal invocation");
    }

    // ---------------------------------------------------------------------------------------------------
    // The types no `reflected` row wires up yet. They are the numeric half of HTML §2.6.1 plus the nullable
    // string, and every one of them is #3770's remaining documents: `colSpan` and `span` are clamped unsigned
    // longs, `maxLength` is a limited long, `select.size` a limited unsigned long, `progress.value` a double
    // and `input.size` a limited unsigned long with fallback. Testing the algorithm here is what stops this
    // change shipping machinery nothing runs, and it is the only way to state a type's rule before there is
    // an element carrying an attribute of that type.
    // ---------------------------------------------------------------------------------------------------

    /// <summary>A <c>DOMString?</c>: absent is <c>null</c>, and setting <c>null</c> removes.</summary>
    [Test]
    public void ANullableDomStringIsNullWhenAbsent()
    {
        using var element = Element();
        var reflected = ReflectedAttribute.Text("X.y", "y", nullable: true);

        reflected.Get(element.Value).Should().Be(JsValue.Null);

        element.Value.SetAttribute("y", "");
        reflected.Get(element.Value).AsString().Should().BeEmpty();
    }

    /// <summary>
    /// A <c>long</c> limited to only non-negative numbers: the getter refuses a negative content attribute
    /// and answers the default, which is −1 when the standard names none.
    /// </summary>
    [TestCase("7", 7d)]
    [TestCase("0", 0d)]
    [TestCase("-3", -1d)]
    [TestCase("junk", -1d)]
    [TestCase("2147483648", -1d)]
    public void ALimitedLongRefusesANegativeContentAttribute(string attribute, double expected)
        => Reflects(ReflectedKind.LimitedLong, fallback: -1, attribute, expected);

    /// <summary>An <c>unsigned long</c>: the same parse, defaulting to 0 and capped at the long range.</summary>
    [TestCase("7", 7d)]
    [TestCase("0", 0d)]
    [TestCase("-3", 0d)]
    [TestCase("2147483647", 2147483647d)]
    [TestCase("2147483648", 0d)]
    public void AnUnsignedLongDefaultsToZero(string attribute, double expected)
        => Reflects(ReflectedKind.UnsignedLong, fallback: 0, attribute, expected);

    /// <summary>
    /// An <c>unsigned long</c> limited to only positive numbers: zero is out of range, so it answers the
    /// default, which is 1 when the standard names none.
    /// </summary>
    [TestCase("7", 7d)]
    [TestCase("0", 1d)]
    [TestCase("-3", 1d)]
    [TestCase("", 1d)]
    public void ALimitedUnsignedLongRefusesZero(string attribute, double expected)
        => Reflects(ReflectedKind.LimitedUnsignedLong, fallback: 1, attribute, expected);

    /// <summary>A <c>clamped unsigned long</c> answers min, max or the value — and the default on a parse error.</summary>
    [TestCase("0", 1d)]
    [TestCase("1", 1d)]
    [TestCase("500", 500d)]
    [TestCase("1001", 1000d)]
    [TestCase("junk", 1d)]
    public void AClampedUnsignedLongClampsToItsRange(string attribute, double expected)
        => Reflects(ReflectedKind.ClampedUnsignedLong, fallback: 1, attribute, expected, min: 1, max: 1000);

    /// <summary>
    /// A <c>double</c> takes HTML's rules for parsing floating-point number values, which — like the integer
    /// rules — take a prefix and skip only ASCII whitespace.
    /// </summary>
    [TestCase("1.5", 1.5)]
    [TestCase(".5", 0.5)]
    [TestCase("1e2", 100d)]
    [TestCase("1E-2", 0.01)]
    [TestCase("1.5abc", 1.5)]
    [TestCase("1 .1", 1d)]
    [TestCase("-1.5", -1.5)]
    [TestCase("1.8e308", 0d)]
    [TestCase("", 0d)]
    [TestCase("junk", 0d)]
    public void ADoubleTakesHtmlsRulesForParsingFloatingPointNumbers(string attribute, double expected)
        => Reflects(ReflectedKind.Double, fallback: 0, attribute, expected);

    /// <summary>A <c>limited double</c> answers its default for anything not greater than zero.</summary>
    [TestCase("1.5", 1.5)]
    [TestCase("0", 1d)]
    [TestCase("-1", 1d)]
    public void ALimitedDoubleRefusesANonPositiveValue(string attribute, double expected)
        => Reflects(ReflectedKind.LimitedDouble, fallback: 1, attribute, expected, min: 0, max: 0);

    /// <summary>
    /// A <c>limited unsigned long with fallback</c> is the one setter that rewrites what it is given: an
    /// out-of-range value is written as the default rather than refused.
    /// </summary>
    [Test]
    public void ALimitedUnsignedLongWithFallbackWritesTheDefaultForAnOutOfRangeValue()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");
        using var element = Element();

        var realm = DomRealm.Of(fixture.Engine);
        var reflected = ReflectedAttribute.Numeric("X.y", "y", ReflectedKind.LimitedUnsignedLongWithFallback, 20);

        reflected.Set(realm, element.Value, [JsNumber.Create(7)]);
        element.Value.GetAttribute("y").Should().Be("7");

        reflected.Set(realm, element.Value, [JsNumber.Create(0)]);
        element.Value.GetAttribute("y").Should().Be("20");
    }

    /// <summary>
    /// The two setters that refuse rather than clamp: HTML makes a negative <c>limited long</c> and a zero
    /// <c>limited unsigned long</c> an <c>IndexSizeError</c>, which is the one place a reflected attribute
    /// can throw.
    /// </summary>
    [Test]
    public void TheLimitedIntegerSettersRefuseAnOutOfRangeValueWithAnIndexSizeError()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");
        using var element = Element();

        var realm = DomRealm.Of(fixture.Engine);

        var limitedLong = ReflectedAttribute.Numeric("X.y", "y", ReflectedKind.LimitedLong, -1);
        var refusal = Assert.Throws<JavaScriptException>(() => limitedLong.Set(realm, element.Value, [JsNumber.Create(-1)]));
        refusal!.Message.Should().Be("Failed to execute 'X.y': the value must not be negative.");
        element.Value.HasAttribute("y").Should().BeFalse();

        var limitedUnsigned = ReflectedAttribute.Numeric("X.z", "z", ReflectedKind.LimitedUnsignedLong, 1);
        Assert.Throws<JavaScriptException>(() => limitedUnsigned.Set(realm, element.Value, [JsNumber.Create(0)]))!
            .Message.Should().Be("Failed to execute 'X.z': the value must be greater than zero.");
    }

    private static void Reflects(ReflectedKind kind, double fallback, string attribute, double expected, long min = 0, long max = 0)
    {
        using var element = Element();

        element.Value.SetAttribute("y", attribute);
        ReflectedAttribute.Numeric("X.y", "y", kind, fallback, min, max)
            .Get(element.Value).AsNumber().Should().Be(expected);
    }

    /// <summary>One detached element to read and write attributes on, with the document that owns it.</summary>
    private static Owned Element()
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(response => response.Content("<div></div>")).GetAwaiter().GetResult();
        return new Owned(document, document.QuerySelector("div")!);
    }

    private static string Literal(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    private sealed record Owned(IDocument Document, IElement Value) : IDisposable
    {
        public void Dispose() => Document.Dispose();
    }
}
