#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>URL</c> class as the WHATWG URL Standard specifies it — https://url.spec.whatwg.org/#url-class.
/// </summary>
/// <remarks>
/// The parsing itself is covered exhaustively by <see cref="UrlCorpusTests"/>, which runs the Web Platform
/// Tests corpus against the parser without an engine. What is tested here is the JavaScript binding on top of
/// it: the WebIDL skin, the brand checks, the attribute flags, and the two-way live sync with
/// <c>searchParams</c> that no corpus row reaches.
/// </remarks>
public class UrlTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Url));

    [Fact]
    public void ParsesAnAbsoluteUrlIntoItsComponents()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://user:pw@example.com:8080/a/b?x=1#frag');");

        engine.Evaluate("url.href").AsString().Should().Be("https://user:pw@example.com:8080/a/b?x=1#frag");
        engine.Evaluate("url.protocol").AsString().Should().Be("https:");
        engine.Evaluate("url.username").AsString().Should().Be("user");
        engine.Evaluate("url.password").AsString().Should().Be("pw");
        engine.Evaluate("url.host").AsString().Should().Be("example.com:8080");
        engine.Evaluate("url.hostname").AsString().Should().Be("example.com");
        engine.Evaluate("url.port").AsString().Should().Be("8080");
        engine.Evaluate("url.pathname").AsString().Should().Be("/a/b");
        engine.Evaluate("url.search").AsString().Should().Be("?x=1");
        engine.Evaluate("url.hash").AsString().Should().Be("#frag");
        engine.Evaluate("url.origin").AsString().Should().Be("https://example.com:8080");
    }

    [Fact]
    public void ResolvesAgainstABase()
    {
        var engine = WebEngine();

        engine.Evaluate("new URL('/c/d', 'https://example.com/a/b').href").AsString().Should().Be("https://example.com/c/d");
        engine.Evaluate("new URL('e', 'https://example.com/a/b').href").AsString().Should().Be("https://example.com/a/e");
        engine.Evaluate("new URL('../x', 'https://example.com/a/b/c').href").AsString().Should().Be("https://example.com/a/x");

        // A URL object stringifies to its href, so it can be passed where the IDL asks for a string.
        engine.Evaluate("new URL('f', new URL('https://example.com/a/b')).href").AsString().Should().Be("https://example.com/a/f");
    }

    [Fact]
    public void ThrowsATypeErrorForAUrlThatDoesNotParse()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URL('not a url')"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        // A relative input with no base, and a base that does not itself parse.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URL('/x')"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new URL('/x', 'not a url')"));
    }

    [Fact]
    public void ParseAnswersNullWhereTheConstructorThrows()
    {
        var engine = WebEngine();

        engine.Evaluate("URL.parse('not a url')").IsNull().Should().BeTrue();
        engine.Evaluate("URL.parse('/x', 'https://example.com/a/b').href").AsString().Should().Be("https://example.com/x");
        engine.Evaluate("URL.parse('/x') === null").AsBoolean().Should().BeTrue();

        engine.Evaluate("URL.canParse('https://example.com/')").AsBoolean().Should().BeTrue();
        engine.Evaluate("URL.canParse('not a url')").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('/x', 'https://example.com/')").AsBoolean().Should().BeTrue();

        engine.Evaluate("URL.parse('https://example.com/') instanceof URL").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void TreatsAnUndefinedArgumentOfTheStaticsAsTheStringUndefined()
    {
        var engine = WebEngine();

        // Both arguments are USVString, so an explicitly passed undefined coerces rather than raising. These
        // are the rows of WPT's url-statics-canparse.any.js, which exists to pin exactly that.
        engine.Evaluate("URL.canParse(undefined, undefined)").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('aaa:b', undefined)").AsBoolean().Should().BeTrue();
        engine.Evaluate("URL.canParse(undefined, 'aaa:b')").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse(undefined, 'https://test:test/')").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('aaa:/b', undefined)").AsBoolean().Should().BeTrue();
        engine.Evaluate("URL.canParse(undefined, 'aaa:/b')").AsBoolean().Should().BeTrue();
        engine.Evaluate("URL.canParse('https://test:test', undefined)").AsBoolean().Should().BeFalse();
        engine.Evaluate("URL.canParse('a', 'https://b/')").AsBoolean().Should().BeTrue();

        engine.Evaluate("URL.parse(undefined, 'aaa:/b').href").AsString().Should().Be("aaa:/undefined");
    }

    [Fact]
    public void SupportsSubclassing()
    {
        var engine = WebEngine();
        engine.Execute("""
            class MyUrl extends URL {
                constructor(input) { super(input); this.tag = 'mine'; }
                get double() { return this.href + this.href; }
            }
            const sub = new MyUrl('https://example.com/');
            """);

        engine.Evaluate("sub instanceof MyUrl").AsBoolean().Should().BeTrue();
        engine.Evaluate("sub instanceof URL").AsBoolean().Should().BeTrue();
        engine.Evaluate("sub.href").AsString().Should().Be("https://example.com/");
        engine.Evaluate("sub.tag").AsString().Should().Be("mine");
        engine.Evaluate("sub.double").AsString().Should().Be("https://example.com/https://example.com/");
        engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(sub)) === URL.prototype").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void EachSetterReRunsTheParserInItsOwnState()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/a?x=1#f');");

        engine.Execute("url.protocol = 'http'");
        engine.Evaluate("url.protocol").AsString().Should().Be("http:");

        engine.Execute("url.hostname = 'other.example'");
        engine.Evaluate("url.host").AsString().Should().Be("other.example");

        engine.Execute("url.port = '8080'");
        engine.Evaluate("url.host").AsString().Should().Be("other.example:8080");

        engine.Execute("url.pathname = '/b/c'");
        engine.Evaluate("url.pathname").AsString().Should().Be("/b/c");

        engine.Execute("url.search = 'y=2'");
        engine.Evaluate("url.search").AsString().Should().Be("?y=2");

        engine.Execute("url.hash = 'g'");
        engine.Evaluate("url.hash").AsString().Should().Be("#g");

        engine.Evaluate("url.href").AsString().Should().Be("http://other.example:8080/b/c?y=2#g");
    }

    [Fact]
    public void DropsTheQueryOrFragmentWhenTheSetterIsGivenTheEmptyString()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/?x=1#f');");

        engine.Execute("url.search = ''; url.hash = '';");

        engine.Evaluate("url.search").AsString().Should().Be("");
        engine.Evaluate("url.hash").AsString().Should().Be("");
        engine.Evaluate("url.href").AsString().Should().Be("https://example.com/");
    }

    [Fact]
    public void AnOpaquePathIgnoresTheSettersThatCannotApply()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('data:text/plain,hello');");

        engine.Evaluate("url.pathname").AsString().Should().Be("text/plain,hello");

        // "If this's URL has an opaque path, then return" — the assignment is a silent no-op, not an error.
        engine.Execute("url.pathname = '/other'; url.host = 'example.com'; url.hostname = 'example.com';");

        engine.Evaluate("url.href").AsString().Should().Be("data:text/plain,hello");

        // Credentials and port are refused for the same reason, through cannot-have-a-username/password/port.
        engine.Execute("url.username = 'u'; url.port = '99';");
        engine.Evaluate("url.href").AsString().Should().Be("data:text/plain,hello");
    }

    [Fact]
    public void ThePortSetterOverflowStillCommitsTheHost()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('http://example.net/path'); url.host = 'example.com:65536';");

        // The spec mutates the URL in place as the state machine runs; the host is committed before the port
        // state fails. WPT pins this with "Port numbers are 16 bit integers, overflowing is an error. Hostname
        // is still set, though."
        engine.Evaluate("url.href").AsString().Should().Be("http://example.com/path");
        engine.Evaluate("url.port").AsString().Should().Be("");
    }

    [Fact]
    public void TheHrefSetterReplacesEverything()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/a?x=1#f');");

        engine.Execute("url.href = 'http://other.example/b'");
        engine.Evaluate("url.href").AsString().Should().Be("http://other.example/b");
        engine.Evaluate("url.search").AsString().Should().Be("");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("url.href = 'not a url'"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void SearchParamsIsTheSameObjectEveryTime()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/?a=1&b=2');");

        engine.Evaluate("url.searchParams === url.searchParams").AsBoolean().Should().BeTrue();
        engine.Evaluate("url.searchParams.get('a')").AsString().Should().Be("1");
        engine.Evaluate("url.searchParams.size").AsNumber().Should().Be(2);
    }

    [Fact]
    public void SearchParamsMutationsRewriteTheUrl()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/?a=1');");

        engine.Execute("url.searchParams.append('b', '2')");
        engine.Evaluate("url.href").AsString().Should().Be("https://example.com/?a=1&b=2");

        engine.Execute("url.searchParams.set('a', '9')");
        engine.Evaluate("url.search").AsString().Should().Be("?a=9&b=2");

        engine.Execute("url.searchParams.delete('a')");
        engine.Evaluate("url.search").AsString().Should().Be("?b=2");

        // Emptying the list clears the query entirely rather than leaving a bare "?".
        engine.Execute("url.searchParams.delete('b')");
        engine.Evaluate("url.search").AsString().Should().Be("");
        engine.Evaluate("url.href").AsString().Should().Be("https://example.com/");
    }

    [Fact]
    public void UrlMutationsRewriteSearchParams()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/?a=1'); const params = url.searchParams;");

        engine.Execute("url.search = 'b=2&c=3'");
        engine.Evaluate("params === url.searchParams").AsBoolean().Should().BeTrue();
        engine.Evaluate("params.get('a')").IsNull().Should().BeTrue();
        engine.Evaluate("params.get('b')").AsString().Should().Be("2");
        engine.Evaluate("[...params.keys()].join(',')").AsString().Should().Be("b,c");

        engine.Execute("url.href = 'https://example.com/?z=9'");
        engine.Evaluate("params.get('z')").AsString().Should().Be("9");
        engine.Evaluate("params.size").AsNumber().Should().Be(1);

        engine.Execute("url.search = ''");
        engine.Evaluate("params.size").AsNumber().Should().Be(0);
    }

    [Fact]
    public void SearchParamsEncodesAsFormUrlEncodedWhereTheQueryDoesNot()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/?a=b ~');");

        // The specification's own example: the two serializers differ, so a sort can change href without
        // changing any name or value.
        engine.Evaluate("url.href").AsString().Should().Be("https://example.com/?a=b%20~");
        engine.Execute("url.searchParams.sort()");
        engine.Evaluate("url.href").AsString().Should().Be("https://example.com/?a=b+%7E");
    }

    [Fact]
    public void ProvidesTheStringifierAndToJson()
    {
        var engine = WebEngine();
        engine.Execute("const url = new URL('https://example.com/a?x=1#f');");

        engine.Evaluate("url.toString()").AsString().Should().Be("https://example.com/a?x=1#f");
        engine.Evaluate("url.toJSON()").AsString().Should().Be("https://example.com/a?x=1#f");
        engine.Evaluate("`${url}`").AsString().Should().Be("https://example.com/a?x=1#f");
        engine.Evaluate("JSON.stringify(url)").AsString().Should().Be("\"https://example.com/a?x=1#f\"");
        engine.Evaluate("Object.prototype.toString.call(url)").AsString().Should().Be("[object URL]");
    }

    [Fact]
    public void EveryMemberBrandChecksItsReceiver()
    {
        var engine = WebEngine();

        // URL.prototype is not itself a URL, which is what makes it the sharpest probe.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URL.prototype.href"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(URL.prototype, 'href').get.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URL.prototype.toString.call({})"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("Object.getOwnPropertyDescriptor(URL.prototype, 'search').set.call({}, 'x')"));
    }

    [Fact]
    public void ExposesItsAttributesAsWebIdlAccessorsOnThePrototype()
    {
        var engine = WebEngine();

        // Attributes are accessors on the prototype, so an instance has no own property at all.
        engine.Evaluate("Object.getOwnPropertyNames(new URL('https://a.example/')).length").AsNumber().Should().Be(0);

        engine.Execute("const d = Object.getOwnPropertyDescriptor(URL.prototype, 'href');");
        engine.Evaluate("typeof d.get").AsString().Should().Be("function");
        engine.Evaluate("typeof d.set").AsString().Should().Be("function");
        engine.Evaluate("d.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.configurable").AsBoolean().Should().BeTrue();

        // A read-only attribute has no setter.
        engine.Execute("const o = Object.getOwnPropertyDescriptor(URL.prototype, 'origin');");
        engine.Evaluate("typeof o.get").AsString().Should().Be("function");
        engine.Evaluate("o.set").IsUndefined().Should().BeTrue();

        engine.Execute("const s = Object.getOwnPropertyDescriptor(URL.prototype, 'searchParams');");
        engine.Evaluate("s.set").IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void HasNeitherUrlPatternNorTheBlobUrlStore()
    {
        var engine = WebEngine();

        // Absent, not present and throwing: feature detection has to be able to see they are missing.
        engine.Evaluate("typeof URLPattern").AsString().Should().Be("undefined");
        engine.Evaluate("typeof URL.createObjectURL").AsString().Should().Be("undefined");
        engine.Evaluate("typeof URL.revokeObjectURL").AsString().Should().Be("undefined");
    }

    [Fact]
    public void HandlesComponentsLongerThanTheParsersInitialBuffer()
    {
        var engine = WebEngine();

        // The parser's buffer starts at 64 characters and grows through the array pool; every one of these is
        // one buffer's worth on its own, so the growth path is exercised for a host, a path segment, a query
        // and a fragment in turn.
        var segment = new string('a', 300);
        engine.SetValue("segment", segment);
        engine.Execute("const url = new URL(`https://${segment}.example/${segment}?${segment}#${segment}`);");

        engine.Evaluate("url.hostname").AsString().Should().Be(segment + ".example");
        engine.Evaluate("url.pathname").AsString().Should().Be("/" + segment);
        engine.Evaluate("url.search").AsString().Should().Be("?" + segment);
        engine.Evaluate("url.hash").AsString().Should().Be("#" + segment);
        engine.Evaluate("url.href").AsString().Should().Be($"https://{segment}.example/{segment}?{segment}#{segment}");

        // The same for an opaque path, which accumulates through the parser's other builder.
        engine.Execute("const opaque = new URL(`data:${segment}#${segment}`);");
        engine.Evaluate("opaque.pathname").AsString().Should().Be(segment);
        engine.Evaluate("opaque.hash").AsString().Should().Be("#" + segment);
    }

    [Fact]
    public void LowercasesAnAsciiDomainWithoutConsultingIdna()
    {
        var engine = WebEngine();

        // Step 4 of the domain parser returns an ASCII domain lowercased "regardless of Unicode ToASCII's
        // outcome, due to web compatibility". The spec's own example of why that matters is xn--8i7caa, which
        // decodes to ｗｗｗ — code points whose UTS-46 status is "mapped", so a round trip through IDNA would
        // reject it. Every ASCII host therefore has to survive verbatim, which is also what keeps the
        // platform's IDNA implementation out of the overwhelming majority of parses.
        engine.Evaluate("new URL('https://XN--8I7CAA.example/').hostname").AsString().Should().Be("xn--8i7caa.example");
        engine.Evaluate("new URL('https://EXAMPLE.COM/').hostname").AsString().Should().Be("example.com");
    }

    [Fact]
    public void ReportsAnOpaqueOriginAsTheStringNull()
    {
        var engine = WebEngine();

        engine.Evaluate("new URL('data:text/plain,x').origin").AsString().Should().Be("null");
        engine.Evaluate("new URL('file:///tmp/x').origin").AsString().Should().Be("null");
        engine.Evaluate("new URL('https://example.com:443/').origin").AsString().Should().Be("https://example.com");

        // A blob URL reports the origin of the URL it wraps.
        engine.Evaluate("new URL('blob:https://whatwg.org/d0360e2f').origin").AsString().Should().Be("https://whatwg.org");
        engine.Evaluate("new URL('blob:nonsense').origin").AsString().Should().Be("null");
    }

    [Fact]
    public void CarriesTheInterfaceObjectShapeWebIdlAsksFor()
    {
        var engine = WebEngine();

        engine.Evaluate("URL.name").AsString().Should().Be("URL");
        engine.Evaluate("URL.length").AsNumber().Should().Be(1);
        engine.Evaluate("URL.prototype.constructor === URL").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(URL) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("URL.prototype[Symbol.toStringTag]").AsString().Should().Be("URL");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("URL('https://example.com/')"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }
}
#endif
