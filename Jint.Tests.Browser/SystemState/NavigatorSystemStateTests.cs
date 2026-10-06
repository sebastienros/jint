using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.SystemState;

using Browser = global::Jint.Browser.Browser;

// HTML's system-state section (https://html.spec.whatwg.org/multipage/system-state.html), Permissions,
// Storage, Notifications, UA Client Hints, CSSOM View's Screen and VisualViewport, Screen Orientation: the
// answers a page reads to decide what kind of browser it runs in.
public sealed class NavigatorSystemStateTests
{
    private static Task SetViewportAsync(Page page, Viewport viewport)
        => page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.SetViewport(viewport);
            return 0;
        });

    [Test]
    public async Task TheCompatibilityConstantsAreTheOnesHtmlAllows()
    {
        await using var browser = new Browser(new BrowserOptions { UserAgent = "Mozilla/5.0 (X11) Test/1" });
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>("""
            [navigator.appCodeName, navigator.appName, navigator.appVersion, navigator.product,
             navigator.productSub, navigator.vendor, navigator.vendorSub].join('|')
            """)).Should().Be("Mozilla|Netscape|5.0 (X11) Test/1|Gecko|20030107||");
        (await page.EvaluateAsync<string>("""
            [navigator.webdriver, navigator.pdfViewerEnabled, navigator.javaEnabled(), navigator.deviceMemory,
             navigator.doNotTrack, navigator.globalPrivacyControl, navigator.getGamepads().length].join('|')
            """)).Should().Be("false|false|false|8||false|0");
        (await page.EvaluateAsync<bool>("Object.keys(Object.getPrototypeOf(navigator)).includes('javaEnabled')")).Should().BeTrue();
    }

    [Test]
    public async Task PluginsAndMimeTypesAreEmptyAndStable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>("""
            [navigator.plugins.length, navigator.mimeTypes.length, navigator.plugins === navigator.plugins,
             navigator.plugins instanceof PluginArray, navigator.mimeTypes instanceof MimeTypeArray,
             navigator.plugins.item(0), navigator.plugins.namedItem('x'), [...navigator.plugins].length,
             Object.prototype.toString.call(navigator.plugins)].join('|')
            """)).Should().Be("0|0|true|true|true|||0|[object PluginArray]");
        (await page.EvaluateAsync<string>("(() => { try { navigator.plugins.item(); } catch (e) { return e.name; } })()"))
            .Should().Be("TypeError");
        (await page.EvaluateAsync<string>("(() => { try { new Plugin(); } catch (e) { return e.name; } })()"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task PermissionsAreAllPromptAndUnknownNamesReject()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAndAwaitAsync<string>("""
            navigator.permissions.query({ name: 'geolocation' }).then(s =>
              [s.name, s.state, s instanceof PermissionStatus, s instanceof EventTarget, s.onchange].join('|'))
            """)).Should().Be("geolocation|prompt|true|true|");
        (await page.EvaluateAndAwaitAsync<string>("navigator.permissions.query({ name: 'bogus' }).catch(e => e.name)"))
            .Should().Be("TypeError");
        (await page.EvaluateAndAwaitAsync<string>("navigator.permissions.query().catch(e => e.name)"))
            .Should().Be("TypeError");
        (await page.EvaluateAsync<string>("Notification.permission")).Should().Be("default");
        (await page.EvaluateAndAwaitAsync<string>("Notification.requestPermission()")).Should().Be("default");
        (await page.EvaluateAndAwaitAsync<string>(
            "new Promise(r => Notification.requestPermission(p => r('callback:' + p)))")).Should().Be("callback:default");
    }

    [Test]
    public async Task ANotificationReadsItsOptionsAndReportsItCouldNotBeShown()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAndAwaitAsync<string>("""
            new Promise(resolve => {
              const log = [];
              const options = {};
              const values = { vibrate: [200, 100], tag: 'tag', silent: undefined, body: 'body', data: { a: [1] } };
              for (const k of Object.keys(values))
                Object.defineProperty(options, k, { get() { log.push(k); return values[k]; } });
              const n = new Notification('Hello', options);
              n.onerror = e => resolve([log.join(), n.title, n.body, n.tag, n.silent, n.vibrate.join(), Object.isFrozen(n.vibrate),
                n.vibrate === n.vibrate, n.data.a[0], n.data !== n.data, n.dir, n.requireInteraction, e.isTrusted,
                Notification.maxActions].join('|'));
            })
            """)).Should().Be("body,data,silent,tag,vibrate|Hello|body|tag||200,100|true|true|1|true|auto|false|true|2");
        (await page.EvaluateAsync<string>("(() => { try { new Notification('x', { silent: true, vibrate: [1] }); } catch (e) { return e.name; } })()"))
            .Should().Be("TypeError");
        (await page.EvaluateAsync<string>("(() => { try { new Notification('x', { renotify: true }); } catch (e) { return e.name; } })()"))
            .Should().Be("TypeError");
        (await page.EvaluateAsync<string>("(() => { try { Notification('x'); } catch (e) { return e.name; } })()"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task StorageEstimatesAQuotaAndNeverPersists()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAndAwaitAsync<string>("""
            Promise.all([navigator.storage.estimate(), navigator.storage.persisted(), navigator.storage.persist()])
              .then(([e, a, b]) => [e.quota, e.usage, a, b, navigator.storage instanceof StorageManager].join('|'))
            """)).Should().Be("1073741824|0|false|false|true");
    }

    [Test]
    public async Task UserAgentDataNamesJintBrowserUntilTheUserAgentIsOverridden()
    {
        await using (var browser = new Browser())
        {
            var page = await browser.NewPageAsync();

            (await page.EvaluateAsync<string>("""
                [navigator.userAgentData.brands.map(b => b.brand).join(), navigator.userAgentData.mobile,
                 navigator.userAgentData.platform, Object.isFrozen(navigator.userAgentData.brands),
                 navigator.userAgentData.brands === navigator.userAgentData.brands,
                 JSON.stringify(navigator.userAgentData.toJSON()) === JSON.stringify({ brands: navigator.userAgentData.brands, mobile: false, platform: '' })].join('|')
                """)).Should().Be("Jint.Browser|false||true|true|true");
            (await page.EvaluateAndAwaitAsync<string>("""
                navigator.userAgentData.getHighEntropyValues(['wow64', 'bogus', 'architecture'])
                  .then(v => Object.keys(v).join())
                """)).Should().Be("architecture,brands,mobile,platform,wow64");
            (await page.EvaluateAndAwaitAsync<string>("navigator.userAgentData.getHighEntropyValues(1).catch(e => e.name)"))
                .Should().Be("TypeError");
        }

        await using (var overridden = new Browser(new BrowserOptions { UserAgent = "Custom/1" }))
        {
            var page = await overridden.NewPageAsync();
            (await page.EvaluateAsync<int>("navigator.userAgentData.brands.length")).Should().Be(0);
        }
    }

    [Test]
    public async Task ScreenAndTheVisualViewportFollowTheViewport()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>("""
            window.log = [];
            screen.orientation.onchange = () => log.push('orientation:' + screen.orientation.type);
            visualViewport.addEventListener('resize', e => log.push('resize:' + visualViewport.width + 'x' + visualViewport.height + ':' + e.isTrusted));
            [screen instanceof Screen, screen.width, screen.height, screen.colorDepth, screen.orientation.type,
             screen.orientation.angle, screen.orientation === screen.orientation, visualViewport instanceof VisualViewport,
             visualViewport === window.visualViewport, visualViewport.scale, visualViewport.offsetLeft].join('|')
            """)).Should().Be("true|1280|720|24|landscape-primary|0|true|true|true|1|0");

        await SetViewportAsync(page, new Viewport(400, 800));

        (await page.EvaluateAsync<string>("[screen.width, screen.orientation.type, log.join()].join('|')"))
            .Should().Be("400|portrait-primary|orientation:portrait-primary,resize:400x800:true");
        (await page.EvaluateAndAwaitAsync<string>("screen.orientation.lock('landscape').catch(e => e.name)"))
            .Should().Be("NotSupportedError");
        (await page.EvaluateAndAwaitAsync<string>("screen.orientation.lock('sideways').catch(e => e.name)"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task RegisteringAProtocolHandlerValidatesItsArguments()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>("""
            (() => {
              const results = [];
              for (const [scheme, url] of [
                ['mailto', '/compose?to=%s'], ['web+coffee', '/brew?%s'], ['http', '/x?%s'], ['web+c0ffee', '/x?%s'],
                ['mailto', '/compose'], ['mailto', 'https://elsewhere.example/?%s']]) {
                try { navigator.registerProtocolHandler(scheme, url); results.push('ok'); }
                catch (e) { results.push(e.name); }
              }
              try { navigator.unregisterProtocolHandler('mailto', '/compose?to=%s'); results.push('ok'); }
              catch (e) { results.push(e.name); }
              return results.join();
            })()
            """)).Should().Be("ok,ok,SecurityError,SecurityError,SyntaxError,SecurityError,ok");
    }
}
