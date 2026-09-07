using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// The paths a fixture asks the server to <i>compute</i> rather than serve — a redirect, a session cookie.
/// </summary>
/// <remarks>
/// They are here rather than in the case that needs them because the same fixtures are driven three ways: in
/// process, by PuppeteerSharp and by Playwright. A route written out three times would be three servers that
/// could disagree about what the fixture's origin does.
/// </remarks>
internal static class FixtureRoutes
{
    internal static LoopbackServer Scalar(LoopbackServer server)
    {
        var html = FixtureCorpus.Read("scalar-openapi/index.html");
        server.MapHtml("/scalar-openapi/index.html",
            html.Replace("%2Fscalar%2Fv1", "%2Fscalar-openapi%2Findex.html", StringComparison.Ordinal));
        foreach (var prefix in new[] { "", "/tenant" })
        {
            server.MapHtml(prefix + "/scalar/v1", html);
            server.Map(prefix + "/swagger/v1/swagger.json", _ => new LoopbackResponse
            {
                Body = FixtureCorpus.Read("scalar-openapi/schema.json"),
            }.With("Content-Type", "application/json"));
        }

        return server;
    }

    /// <summary>The tenant-relative paths configured by Orchard's unmodified ocmonaco.js.</summary>
    internal static LoopbackServer Monaco(LoopbackServer server)
    {
        const string prefix = "/monaco-amd/tenant/OrchardCore.Resources/Scripts/monaco/";
        server.Map(prefix + "vs/editor/editor.main.js",
            _ => LoopbackResponse.Script(FixtureCorpus.Read("vendor/monaco-0.52.2/vs/editor/editor.main.js")));
        server.Map(prefix + "vs/editor/editor.main.css",
            _ => LoopbackResponse.Css(FixtureCorpus.Read("vendor/monaco-0.52.2/vs/editor/editor.main.css")));
        return server;
    }

    /// <summary>
    /// The <c>cookie-login</c> fixture's origin: a sign-in that answers <c>Set-Cookie</c> on a <c>303</c>,
    /// and a protected page that reads the cookie back off the request.
    /// </summary>
    internal static LoopbackServer CookieLogin(LoopbackServer server)
    {
        server.Map("/cookie-login/session", request =>
        {
            var credentials = request.Body.Contains("user=ada", StringComparison.Ordinal)
                && request.Body.Contains("password=lovelace", StringComparison.Ordinal);

            if (!credentials)
            {
                return LoopbackResponse.Redirect(303, "/cookie-login/index.html");
            }

            // Two cookies rather than one, because the difference between them is half of what the fixture is
            // for: a client reads both out of the jar and the document reads only the second.
            return LoopbackResponse.Redirect(303, "/cookie-login/secret")
                .With("Set-Cookie", "session=ada-is-in; Path=/; HttpOnly")
                .With("Set-Cookie", "theme=dark; Path=/");
        });

        server.Map("/cookie-login/secret", request =>
        {
            var cookie = request.Header("Cookie") ?? "";

            return cookie.Contains("session=ada-is-in", StringComparison.Ordinal)
                ? LoopbackResponse.Html(
                    "<!doctype html><html><head><title>Secret</title></head><body>"
                    + "<p id='welcome'>welcome, ada</p>"
                    + "<p id='cookies'></p>"
                    + "<script>document.getElementById('cookies').textContent = document.cookie;</script>"
                    + "</body></html>")
                : new LoopbackResponse
                {
                    Status = 401,
                    Reason = "Unauthorized",
                    Body = "<!doctype html><html><body><p id='denied'>no session</p></body></html>",
                }.With("Content-Type", "text/html; charset=utf-8");
        });

        return server;
    }

    /// <summary>
    /// The <c>form-redirect</c> fixture's origin: a <c>POST</c> answered with a <c>303</c> that carries the
    /// body back as a query string, and <paramref name="seen"/> told what the server read.
    /// </summary>
    internal static LoopbackServer FormRedirect(LoopbackServer server, Action<string, string> seen)
        => server.Map("/form-redirect/submit", request =>
        {
            seen(request.Method, request.Body);
            return LoopbackResponse.Redirect(303, "/form-redirect/done.html?" + request.Body);
        });
}
