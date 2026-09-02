using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// One jar per context, two doors: a response's <c>Set-Cookie</c>, the next request's <c>Cookie</c> header,
/// and <c>document.cookie</c> — with <c>HttpOnly</c> enforced against the last of the three.
/// </summary>
public sealed class CookieTests
{
    [Test]
    public async Task ACookieAResponseSetIsSentOnTheNextRequestAndVisibleToScript()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/login", _ => LoopbackResponse.Html("<title>in</title>").With("Set-Cookie", "session=abc; Path=/"))
            .MapHtml("/next", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/login"));

        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().Be("session=abc");

        await fixture.Page.NavigateAsync(fixture.Url("/next"));

        fixture.Server.Received.Single(r => r.Path == "/next").Header("Cookie").Should().Be("session=abc");
    }

    [Test]
    public async Task AnHttpOnlyCookieIsSentButHiddenFromScript()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/login", _ => LoopbackResponse.Html("<title>in</title>")
                .With("Set-Cookie", "session=secret; Path=/; HttpOnly")
                .With("Set-Cookie", "theme=dark; Path=/"))
            .MapHtml("/next", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/login"));

        var visible = await fixture.Page.EvaluateAsync<string>("document.cookie");
        visible.Should().Contain("theme=dark");
        visible.Should().NotContain("secret", "RFC 6265bis §5.5: a non-HTTP API must not see an HttpOnly cookie");

        await fixture.Page.NavigateAsync(fixture.Url("/next"));

        var sent = fixture.Server.Received.Single(r => r.Path == "/next").Header("Cookie");
        sent.Should().Contain("session=secret", "the HTTP door still sees it");
        sent.Should().Contain("theme=dark");
    }

    [Test]
    public async Task ScriptCannotOverwriteAnHttpOnlyCookieAndCannotSetOne()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/login", _ => LoopbackResponse.Html("<title>in</title>").With("Set-Cookie", "session=secret; Path=/; HttpOnly"))
            .MapHtml("/next", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/login"));

        await fixture.Page.EvaluateAsync("document.cookie = 'session=stolen; Path=/'");
        await fixture.Page.EvaluateAsync("document.cookie = 'sneaky=1; Path=/; HttpOnly'");

        await fixture.Page.NavigateAsync(fixture.Url("/next"));

        var sent = fixture.Server.Received.Single(r => r.Path == "/next").Header("Cookie");
        sent.Should().Contain("session=secret", "a non-HTTP API may not replace an HttpOnly cookie");
        sent.Should().NotContain("stolen");
        sent.Should().NotContain("sneaky", "a Set-Cookie carrying HttpOnly from a non-HTTP API is ignored entirely");
    }

    [Test]
    public async Task ACookieAScriptSetReachesTheJarAndTheNextRequest()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/index.html", "<title>index</title>")
            .MapHtml("/next", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("document.cookie = 'chosen=by-script; Path=/'");

        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().Be("chosen=by-script");

        await fixture.Page.NavigateAsync(fixture.Url("/next"));

        fixture.Server.Received.Single(r => r.Path == "/next").Header("Cookie").Should().Be("chosen=by-script");
    }

    [Test]
    public async Task ARedirectStoresAndResendsCookiesPerHop()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/one", _ => LoopbackResponse.Redirect(302, "/two").With("Set-Cookie", "hop=one; Path=/"))
            .Map("/two", _ => LoopbackResponse.Redirect(302, "/three").With("Set-Cookie", "second=two; Path=/"))
            .MapHtml("/three", "<title>three</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/one"));

        var two = fixture.Server.Received.Single(r => r.Path == "/two");
        var three = fixture.Server.Received.Single(r => r.Path == "/three");

        two.Header("Cookie").Should().Be("hop=one", "the jar is consulted per hop, not per fetch");
        three.Header("Cookie").Should().Contain("hop=one");
        three.Header("Cookie").Should().Contain("second=two");
    }

    [Test]
    public async Task TwoPagesOfOneContextShareAJarAndTwoContextsDoNot()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/login", _ => LoopbackResponse.Html("<title>in</title>").With("Set-Cookie", "session=shared; Path=/"))
            .MapHtml("/read", "<title>read</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/login"));

        var sibling = await fixture.NewPageAsync();
        await sibling.NavigateAsync(fixture.Url("/read"));
        (await sibling.EvaluateAsync<string>("document.cookie")).Should().Be("session=shared");

        var stranger = await fixture.NewIsolatedPageAsync();
        await stranger.NavigateAsync(fixture.Url("/read"));
        (await stranger.EvaluateAsync<string>("document.cookie")).Should().BeEmpty("a second context is a second visitor");
    }

    [Test]
    public async Task AHostCanSeedTheContextsJarBeforeAPageLoads()
    {
        var jar = new Jint.WebApi.Fetch.CookieContainerCookieJar();

        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", "<title>seeded</title>"),
            options => options.CookieJar = jar);

        jar.StoreResponseCookies(new Uri(fixture.Url("/")), ["seed=from-host; Path=/"]);

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        fixture.Server.Received.Single().Header("Cookie").Should().Be("seed=from-host");
        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().Be("seed=from-host");
        fixture.Context.CookieJar.Should().BeSameAs(jar);
    }

    [Test]
    public async Task ADocumentWithNoOriginHasNoCookies()
    {
        await using var fixture = await LoopbackPage.CreateAsync();

        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().BeEmpty();

        // Writing one is not an error either; it simply has no request URL to be stored against.
        await fixture.Page.EvaluateAsync("document.cookie = 'x=1'");
        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().BeEmpty();
        fixture.Page.Errors.Should().BeEmpty();
    }
}
