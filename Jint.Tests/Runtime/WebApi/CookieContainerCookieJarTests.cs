#if NET8_0_OR_GREATER
#nullable enable
using Jint.WebApi.Fetch;

namespace Jint.Tests.Runtime.WebApi;

public sealed class CookieContainerCookieJarTests
{
    [Test]
    public void ScriptSnapshotsHideHttpOnlyAndCannotModifyTheLiveContainer()
    {
        var jar = new CookieContainerCookieJar();
        var url = new Uri("https://example.test/dir/page");
        jar.StoreResponseCookies(url, ["hidden=secret; Path=/; HttpOnly", "visible=one; Path=/"]);
        var snapshot = jar.GetScriptCookies(url);
        snapshot.Should().ContainSingle().Which.Value.Should().Be("one");
        jar.StoreResponseCookies(url, ["visible=two; Path=/"]);
        snapshot[0].Value.Should().Be("one");
        jar.GetScriptCookies(url)[0].Value.Should().Be("two");
    }

    [Test]
    public void ScriptCannotOverwriteAnHttpOnlyCookieOutsideItsCurrentPath()
    {
        var jar = new CookieContainerCookieJar();
        var url = new Uri("https://example.test/page");
        jar.StoreResponseCookies(url, ["hidden=secret; Path=/private; HttpOnly"]);
        jar.StoreScriptCookie(url, new SetCookie { Name = "hidden", Value = "stolen", Path = "/private" });
        jar.GetCookieHeader(new Uri(url, "/private/page")).Should().Be("hidden=secret");
        jar.StoreScriptCookie(url, new SetCookie { Name = "hidden", Value = "allowed", Path = "/" });
        jar.GetScriptCookies(url).Should().ContainSingle().Which.Value.Should().Be("allowed");
    }

    [Test]
    public void StorageRefusalsNeverEscapeAsClrExceptions()
    {
        var jar = new CookieContainerCookieJar();
        var url = new Uri("https://example.test/");
        jar.StoreResponseCookies(url, ["nameless", "=value", "bad,name=value"]);
        jar.GetCookieHeader(url).Should().BeEmpty();
    }

    [TestCase("http://127.0.0.1/", true)]
    [TestCase("http://localhost/", true)]
    [TestCase("http://[::1]/", true)]
    [TestCase("http://example.test/", false)]
    [TestCase("https://example.test/", true)]
    public void SecureCookieRetrievalTrustsOnlyTlsOrLoopback(string address, bool visible)
    {
        var jar = new CookieContainerCookieJar();
        var url = new Uri(address);
        jar.StoreResponseCookies(url, ["secure=one; Secure; Path=/"]);
        jar.GetCookieHeader(url).Should().Be(visible ? "secure=one" : "");
        jar.GetScriptCookies(url).Count.Should().Be(visible ? 1 : 0);
    }
}
#endif
