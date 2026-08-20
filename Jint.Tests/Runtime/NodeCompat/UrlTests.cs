#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;
using Jint.WebApi.Url.Parsing;

namespace Jint.Tests.Runtime.NodeCompat;

/// <summary>
/// The opt-in <c>node:url</c> builtin module - https://nodejs.org/api/url.html - against the real Node
/// implementation.
/// </summary>
/// <remarks>
/// Every case in <see cref="MatchesNode"/> passes the <c>windows</c> option explicitly, so none of them
/// depends on which platform the test run happens on; the platform default is asserted separately.
/// </remarks>
public class UrlTests
{
    /// <summary>
    /// An engine with <c>node:url</c> imported and its default export bound to the global <c>url</c>, plus the
    /// <c>URL</c> constructor it re-exports - which is the shape <c>const url = require('node:url')</c> gives a
    /// script. The <c>URL</c> global is deliberately <em>not</em> enabled through
    /// <c>options.UseWebApis</c>: the module has to work without it.
    /// </summary>
    private static Engine UrlEngine(string platform = "linux")
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.Platform = platform));

        var module = engine.Modules.Import("node:url");
        engine.SetValue("url", module.Get("default"));
        engine.SetValue("URL", module.Get("URL"));
        return engine;
    }

    [Theory]
    [InlineData("url.fileURLToPath('file:///hello world', { windows: false })", "/hello world")]
    [InlineData("url.fileURLToPath('file:///%E4%BD%A0%E5%A5%BD.txt', { windows: false })", "/\u4f60\u597d.txt")]
    [InlineData("url.fileURLToPath('file:///foo/bar', { windows: false })", "/foo/bar")]
    [InlineData("url.fileURLToPath('file:///', { windows: false })", "/")]
    [InlineData("url.fileURLToPath('file://localhost/foo', { windows: false })", "/foo")]
    [InlineData("url.fileURLToPath('file:///a/%2e%2e/b', { windows: false })", "/b")]
    [InlineData("url.fileURLToPath(new URL('file:///foo/bar'), { windows: false })", "/foo/bar")]
    [InlineData("url.fileURLToPath('file:///C:/path/', { windows: false })", "/C:/path/")]
    [InlineData("url.fileURLToPath('file:///C:/path/', { windows: true })", "C:\\path\\")]
    [InlineData("url.fileURLToPath('file:///C:/path/file.txt', { windows: true })", "C:\\path\\file.txt")]
    [InlineData("url.fileURLToPath('file://nas/foo.txt', { windows: true })", "\\\\nas\\foo.txt")]
    [InlineData("url.fileURLToPath('file:///c:/foo%20bar', { windows: true })", "c:\\foo bar")]
    [InlineData("url.fileURLToPath('file:///D:/a/b/c', { windows: true })", "D:\\a\\b\\c")]
    [InlineData("url.pathToFileURL('/foo#1', { windows: false }).href", "file:///foo%231")]
    [InlineData("url.pathToFileURL('/some/path%.c', { windows: false }).href", "file:///some/path%25.c")]
    [InlineData("url.pathToFileURL('/foo/bar', { windows: false }).href", "file:///foo/bar")]
    [InlineData("url.pathToFileURL('/foo/bar/', { windows: false }).href", "file:///foo/bar/")]
    [InlineData("url.pathToFileURL('/hello world', { windows: false }).href", "file:///hello%20world")]
    [InlineData("url.pathToFileURL('/a?b', { windows: false }).href", "file:///a%3Fb")]
    [InlineData("url.pathToFileURL('/\\u4e2d\\u6587.txt', { windows: false }).href", "file:///%E4%B8%AD%E6%96%87.txt")]
    [InlineData("url.pathToFileURL('/a/../b', { windows: false }).href", "file:///b")]
    [InlineData("url.pathToFileURL('C:\\\\path\\\\', { windows: true }).href", "file:///C:/path/")]
    [InlineData("url.pathToFileURL('C:\\\\path\\\\file.txt', { windows: true }).href", "file:///C:/path/file.txt")]
    [InlineData("url.pathToFileURL('\\\\\\\\nas\\\\share\\\\file.txt', { windows: true }).href", "file://nas/share/file.txt")]
    [InlineData("url.pathToFileURL('C:\\\\a b\\\\c#d', { windows: true }).href", "file:///C:/a%20b/c%23d")]
    [InlineData("url.fileURLToPath(url.pathToFileURL('/a b/c#d', { windows: false }), { windows: false })", "/a b/c#d")]
    [InlineData("url.fileURLToPath(url.pathToFileURL('C:\\\\a b\\\\c#d', { windows: true }), { windows: true })", "C:\\a b\\c#d")]
    public void MatchesNode(string expression, string expected)
    {
        var engine = UrlEngine();

        engine.Evaluate(expression).AsString().Should().Be(expected, expression);
    }

    /// <summary>
    /// Whether the platform's IDNA implementation is the one the URL Standard asks for. The two domain
    /// mappings are the platform's, so a machine doing transitional processing — or none at all — is not a
    /// machine these assertions can be made on. <see cref="Idna"/> documents the divergences in full.
    /// </summary>
    public static bool FullIdna => Idna.Fidelity == IdnaFidelity.Full;

    /// <summary>
    /// <c>url.domainToASCII(domain)</c>, with the documentation's own examples.
    /// </summary>
    [Theory(Skip = "The platform's IDNA implementation is not the URL Standard's", SkipUnless = nameof(FullIdna))]
    [InlineData("espa\u00f1ol.com", "xn--espaol-zwa.com")]
    [InlineData("\u4e2d\u6587.com", "xn--fiq228c.com")]
    [InlineData("xn--i\u00f1valid.com", "")]
    public void DomainToAsciiMatchesNode(string domain, string expected)
    {
        var engine = UrlEngine();

        engine.SetValue("domain", domain);

        engine.Evaluate("url.domainToASCII(domain)").AsString().Should().Be(expected);
    }

    /// <summary>
    /// <c>url.domainToUnicode(domain)</c>, "the inverse operation to <c>url.domainToASCII()</c>".
    /// </summary>
    [Theory(Skip = "The platform's IDNA implementation is not the URL Standard's", SkipUnless = nameof(FullIdna))]
    [InlineData("xn--espaol-zwa.com", "espa\u00f1ol.com")]
    [InlineData("xn--fiq228c.com", "\u4e2d\u6587.com")]
    [InlineData("xn--i\u00f1valid.com", "")]
    public void DomainToUnicodeMatchesNode(string domain, string expected)
    {
        var engine = UrlEngine();

        engine.SetValue("domain", domain);

        engine.Evaluate("url.domainToUnicode(domain)").AsString().Should().Be(expected);
    }

    /// <summary>
    /// An ASCII domain needs no IDNA at all, so these hold on every machine — including the empty string,
    /// which is the failure value both mappings report.
    /// </summary>
    [Theory]
    [InlineData("url.domainToASCII('example.com')", "example.com")]
    [InlineData("url.domainToASCII('EXAMPLE.com')", "example.com")]
    [InlineData("url.domainToASCII('')", "")]
    [InlineData("url.domainToUnicode('example.com')", "example.com")]
    [InlineData("url.domainToUnicode('')", "")]
    public void TheDomainMappingsHandleAnAsciiDomain(string expression, string expected)
    {
        var engine = UrlEngine();

        engine.Evaluate(expression).AsString().Should().Be(expected, expression);
    }

    /// <summary>
    /// Node's only argument check on the two mappings is that one was passed at all — everything else is
    /// coerced.
    /// </summary>
    [Fact]
    public void TheDomainMappingsRequireAnArgument()
    {
        var engine = UrlEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("url.domainToASCII()"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("url.domainToUnicode()"));

        engine.Evaluate("url.domainToASCII(123)").AsString().Should().Be("123");
    }

    /// <summary>
    /// The failures <c>fileURLToPath</c> reports, each of which is a <c>TypeError</c> in Node too.
    /// </summary>
    [Theory]
    [InlineData("url.fileURLToPath('https://example.com/x')", "scheme file")]
    [InlineData("url.fileURLToPath('file://host/x', { windows: false })", "must be \"localhost\" or empty")]
    [InlineData("url.fileURLToPath('file:///a%2Fb', { windows: false })", "encoded / characters")]
    [InlineData("url.fileURLToPath('file:///a%2fb', { windows: true })", "encoded \\ or / characters")]
    [InlineData("url.fileURLToPath('file:///a%5Cb', { windows: true })", "encoded \\ or / characters")]
    [InlineData("url.fileURLToPath('file:///foo', { windows: true })", "must be absolute")]
    [InlineData("url.fileURLToPath('not a url')", "Invalid URL")]
    [InlineData("url.fileURLToPath(42)", "string or URL")]
    public void FileUrlToPathReportsTheReasonItRefused(string expression, string fragment)
    {
        var engine = UrlEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate(expression));

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        exception.Message.Should().Contain(fragment);
    }

    /// <summary>
    /// A percent sequence that is not valid UTF-8 is what <c>decodeURIComponent</c> raises a <c>URIError</c>
    /// for, and Node lets that one escape <c>fileURLToPath</c> as itself.
    /// </summary>
    [Fact]
    public void FileUrlToPathRaisesAUriErrorForAMalformedSequence()
    {
        var engine = UrlEngine();

        var exception = Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("url.fileURLToPath('file:///a%C3%28b', { windows: false })"));

        exception.Error.Get("name").AsString().Should().Be("URIError");
    }

    /// <summary>
    /// The UNC refusals of <c>pathToFileURL</c>, which need a server and a resource path.
    /// </summary>
    [Theory]
    [InlineData(@"url.pathToFileURL('\\\\server', { windows: true })", "missing UNC resource path")]
    [InlineData(@"url.pathToFileURL('\\\\\\share', { windows: true })", "empty UNC servername")]
    public void PathToFileUrlRefusesAnIncompleteUncPath(string expression, string fragment)
    {
        var engine = UrlEngine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate(expression));

        exception.Message.Should().Contain(fragment);
    }

    /// <summary>
    /// "<c>windows</c>: <c>true</c> for windows path, <c>false</c> for posix path, <c>undefined</c> for system
    /// default" — and the default here is the configured platform rather than the host's.
    /// </summary>
    [Theory]
    [InlineData("win32", "C:\\path\\file.txt")]
    [InlineData("linux", "/C:/path/file.txt")]
    public void TheDefaultConversionFollowsTheConfiguredPlatform(string platform, string expected)
    {
        var engine = UrlEngine(platform);

        engine.Evaluate("url.fileURLToPath('file:///C:/path/file.txt')").AsString().Should().Be(expected);
    }

    /// <summary>
    /// The <c>URL</c> the module re-exports is the engine's own, so it is the very interface object the
    /// <c>URL</c> global names when the host also enabled that web API — one class, not two.
    /// </summary>
    [Fact]
    public void ReExportsTheEnginesOwnUrlInterfaceObjects()
    {
        var engine = new Engine(options => options
            .UseNodeBuiltinModules()
            .UseWebApis(WebApiFeatures.Url));

        var module = engine.Modules.Import("node:url");
        engine.SetValue("moduleUrl", module.Get("URL"));
        engine.SetValue("moduleSearchParams", module.Get("URLSearchParams"));

        engine.Evaluate("moduleUrl === URL").AsBoolean().Should().BeTrue();
        engine.Evaluate("moduleSearchParams === URLSearchParams").AsBoolean().Should().BeTrue();
        engine.Evaluate("new moduleUrl('https://example.org/') instanceof URL").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// And importing the module is itself the opt-in: <c>URL</c> works through it in an engine that enabled no
    /// web API at all, where the global is absent.
    /// </summary>
    [Fact]
    public void WorksWithoutTheUrlGlobal()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        engine.Evaluate("typeof globalThis.URL").AsString().Should().Be("undefined");

        engine.SetValue("ModuleUrl", engine.Modules.Import("node:url").Get("URL"));

        engine.Evaluate("new ModuleUrl('https://example.org/a?b=c').search").AsString().Should().Be("?b=c");
    }

    /// <summary>
    /// Named exports beside the default one, so <c>import { fileURLToPath } from 'node:url'</c> works.
    /// </summary>
    [Fact]
    public void ExposesNamedExports()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.Platform = "linux"));
        engine.Modules.Add("main", "import { fileURLToPath, pathToFileURL, domainToASCII, domainToUnicode, URL, URLSearchParams } from 'node:url'; export const result = fileURLToPath('file:///a/b') + ',' + typeof URL + ',' + typeof URLSearchParams + ',' + [pathToFileURL, domainToASCII, domainToUnicode].every(f => typeof f === 'function');");

        engine.Modules.Import("main").Get("result").AsString().Should().Be("/a/b,function,function,true");
    }

    /// <summary>
    /// The legacy URL API is deliberately absent — Node documents it as legacy, its parser diverges from the
    /// WHATWG one, and the <c>URL</c> class beside it does the same job.
    /// </summary>
    [Fact]
    public void TheLegacyApiIsAbsent()
    {
        var engine = UrlEngine();

        engine.Evaluate("typeof url.parse").AsString().Should().Be("undefined");
        engine.Evaluate("typeof url.resolve").AsString().Should().Be("undefined");
        engine.Evaluate("typeof url.format").AsString().Should().Be("undefined");
        engine.Evaluate("typeof url.Url").AsString().Should().Be("undefined");
        engine.Evaluate("typeof url.urlToHttpOptions").AsString().Should().Be("undefined");
    }
}
#endif
