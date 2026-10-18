#if NET8_0_OR_GREATER
#nullable enable

using System.Buffers;
using System.Reflection;
using System.Text.RegularExpressions;
using Jint.WebApi.Url.Pattern;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <see cref="UrlPatternMatcher"/>: a URL pattern compiled on an engine's thread and matched with no engine, which
/// is what a host deciding requests on a transport thread needs — <c>Jint.Browser</c>'s
/// <c>Network.emulateNetworkConditionsByRule</c>.
/// </summary>
/// <remarks>
/// It generates a matcher of its own from each component's part list rather than running the engine's
/// <c>RegExp</c>, so the property that matters is that it answers what <c>new URLPattern(string).test(url)</c>
/// answers. <see cref="AgreesWithUrlPatternTest"/> checks exactly that, pattern by pattern, over the same URLs.
/// </remarks>
public class UrlPatternMatcherTests
{
    private static readonly string[] _urls =
    [
        "https://example.com/",
        "http://example.com/a/b?c=d#e",
        "https://www.example.com/",
        "https://example.org/",
        "https://evil.com/",
        "https://cdn.evil.com/x",
        "https://a.b.evil.com/x?y",
        "https://notevil.com/x",
        "https://example.com/foo",
        "https://example.com/foo/",
        "https://example.com/foo/42",
        "https://example.com/foo/42/43",
        "https://example.com/style.css",
        "https://example.com/a/b.css",
        "https://example.com/css",
        "http://127.0.0.1:8080/data",
        "http://127.0.0.1:8081/data",
        "http://127.0.0.1:8080/data?q=1",
        "https://example.com:443/x",
        "https://example.com:8443/x",
        "https://example.com/x",
        "https://example.com/a",
        "https://example.com/a/b",
        "https://example.com/a/b/b",
        "https://example.com/?q=1",
        "https://example.com/?q=",
        "https://example.com/?r=1",
        "https://example.com/#top",
        "https://example.com/#",
        "https://user:pass@example.com/p",
        "https://user@example.com/p",
        "https://[::1]/x",
        "http://[::1]:8080/x",
        "file:///C:/x",
        "data:text/plain,hi",
        "data:,hello",
        "data:,",
        "https://example.com/café",
        "https://example.com/caf%C3%A9",
        "https://EXAMPLE.com/Foo",
        "https://example.com/foo%2Fbar",
        "https://example.com/files",
        "https://example.com/files/",
        "https://example.com/files/x/y",
        "ws://example.com/socket",
        "not a url",
        "",
    ];

    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Url));

    /// <summary>
    /// For every URL, the matcher answers what <c>URLPattern.prototype.test</c> answers for the same constructor
    /// string — the generated .NET expression, the direct comparisons and the URL parsing all included.
    /// </summary>
    [TestCase("*://example.com/*")]
    [TestCase("*://*.evil.com/*")]
    [TestCase("*://*:*/*")]
    [TestCase("https://example.com/foo")]
    [TestCase("https://example.com/foo/:id")]
    [TestCase("https://example.com/foo/:id?")]
    [TestCase("https://example.com/foo/:rest*")]
    [TestCase("https://example.com/foo/:rest+")]
    [TestCase("https://example.com/*.css")]
    [TestCase("https://example.com/:name.css")]
    [TestCase("http://127.0.0.1:8080/data")]
    [TestCase("http://127.0.0.1:*/data")]
    [TestCase("https://example.com:443/x")]
    [TestCase("https://{www.}?example.com/*")]
    [TestCase("https://:sub.example.com/*")]
    [TestCase("https://example.com/files/*?")]
    [TestCase("https://example.com/a{/b}*")]
    [TestCase("https://example.com/a{/b}+")]
    [TestCase("https://example.com/a{/b}?")]
    [TestCase("https://example.com/?q=:value")]
    [TestCase("https://example.com/?q=*")]
    [TestCase("https://example.com/#:frag")]
    [TestCase("https://example.com/#")]
    [TestCase("https://user:pass@example.com/*")]
    [TestCase("https://:user@example.com/*")]
    [TestCase("https://[\\:\\:1]/*")]
    [TestCase("http://[\\:\\:1]:8080/*")]
    [TestCase("file:///*")]
    [TestCase("data:*")]
    [TestCase("data:,:body")]
    [TestCase("https://example.com/café")]
    [TestCase("https://EXAMPLE.com/Foo")]
    [TestCase("https://example.com/foo%2Fbar")]
    [TestCase("http{s}?://example.com/*")]
    [TestCase("ws{s}?://*/*")]
    [TestCase("https://example.com")]
    public void AgreesWithUrlPatternTest(string pattern)
    {
        var engine = WebEngine();
        var compiled = UrlPatternMatcher.TryCompile(engine, pattern, out var matcher, out var error);
        error.Should().BeNull();
        compiled.Should().BeTrue();

        engine.SetValue("pattern", pattern);
        foreach (var url in _urls)
        {
            engine.SetValue("url", url);
            var expected = engine.Evaluate("new URLPattern(pattern).test(url)").AsBoolean();

            matcher!.Matches(url).Should().Be(expected, "new URLPattern({0}).test({1}) answers {2}", pattern, url, expected);
        }
    }

    /// <summary>The two block-list patterns PuppeteerSharp's own documentation uses, spelled out.</summary>
    [Test]
    public void MatchesWhatABlockListMeans()
    {
        var engine = WebEngine();
        UrlPatternMatcher.TryCompile(engine, "*://*.evil.com/*", out var evil, out _).Should().BeTrue();
        UrlPatternMatcher.TryCompile(engine, "*://example.com/*", out var example, out _).Should().BeTrue();

        evil!.Matches("https://cdn.evil.com/tracker.js").Should().BeTrue();
        evil.Matches("http://a.b.evil.com/?x#y").Should().BeTrue();
        evil.Matches("https://evil.com/").Should().BeFalse("the pattern requires a subdomain, as URLPattern reads it");
        evil.Matches("https://notevil.com/").Should().BeFalse();

        example!.Matches("http://example.com/data").Should().BeTrue();
        example.Matches("https://EXAMPLE.com:443/").Should().BeTrue("the URL is canonicalized before it is matched");
        example.Matches("https://example.com:8443/").Should().BeFalse("a port the pattern does not name must be the default");
        example.Matches("https://www.example.com/").Should().BeFalse();
    }

    /// <summary>
    /// A regexp group is refused, never compiled: its body is JavaScript regular-expression source, and only the
    /// engine can be trusted to read that. Chromium's network service refuses one too.
    /// </summary>
    [TestCase(@"https://example.com/(\d+)")]
    [TestCase(@"https://example.com/:id(\d+)")]
    [TestCase(@"https://(www|cdn).example.com/*")]
    [TestCase("(https|http)://example.com/*")]
    public void RefusesARegexpGroup(string pattern)
    {
        UrlPatternMatcher.TryCompile(WebEngine(), pattern, out var matcher, out var error).Should().BeFalse();

        matcher.Should().BeNull();
        error.Should().Be("Regexp groups are not supported");
    }

    /// <summary>A string that is not an absolute URL pattern is refused with the parser's own reason.</summary>
    [TestCase("/relative/*", "base URL")]
    [TestCase("example.com/*", "base URL")]
    [TestCase("https://example.com/(", null)]
    [TestCase("https://example.com/{", null)]
    [TestCase("https://exa mple.com/*", null)]
    public void RefusesWhatIsNotAnAbsolutePattern(string pattern, string? reason)
    {
        UrlPatternMatcher.TryCompile(WebEngine(), pattern, out var matcher, out var error).Should().BeFalse();

        matcher.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        if (reason is not null)
        {
            error.Should().Contain(reason);
        }
    }

    /// <summary>
    /// A matcher needs the engine that compiled it for nothing: it still answers after that engine is disposed, and
    /// from several threads at once.
    /// </summary>
    [Test]
    public void OutlivesTheEngineThatCompiledItAndMatchesFromAnyThread()
    {
        UrlPatternMatcher? matcher;
        using (var engine = WebEngine())
        {
            UrlPatternMatcher.TryCompile(engine, "*://*.evil.com/*", out matcher, out _).Should().BeTrue();
        }

        var answers = new bool[64];
        Parallel.For(0, answers.Length, i => answers[i] = matcher!.Matches(i % 2 == 0 ? "https://cdn.evil.com/" + i : "https://example.com/" + i));

        for (var i = 0; i < answers.Length; i++)
        {
            answers[i].Should().Be(i % 2 == 0);
        }
    }

    /// <summary>
    /// What a transport thread is handed holds nothing of the engine: every field reachable from a matcher is a
    /// string, an enum, a <see cref="Regex"/>, a <see cref="SearchValues{T}"/> or one of the matcher's own types.
    /// </summary>
    /// <remarks>
    /// This is what <c>Jint.Browser</c>'s claim — a URL-scoped network condition is decided off the page loop
    /// without touching the engine — rests on. A field of any other type fails here, so a future change that keeps
    /// the <see cref="UrlPatternRecord"/>, a <c>JsRegExp</c> or a realm around has to answer for it.
    /// </remarks>
    [Test]
    public void TheMatcherHoldsNothingOfTheEngine()
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(typeof(UrlPatternMatcher));

        while (pending.Count > 0)
        {
            var type = pending.Pop();
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

                if (fieldType == typeof(string) || fieldType.IsEnum || fieldType.IsPrimitive
                    || fieldType == typeof(Regex) || fieldType == typeof(SearchValues<char>))
                {
                    continue;
                }

                if (fieldType == typeof(UrlPatternMatcher) || fieldType.DeclaringType == typeof(UrlPatternMatcher))
                {
                    pending.Push(fieldType);
                    continue;
                }

                Assert.Fail($"{type.Name}.{field.Name} is a {fieldType.FullName}, which nothing shows a transport thread may share.");
            }
        }

        seen.Should().HaveCountGreaterThan(1, "the walk reached the component matchers");
    }
}
#endif
