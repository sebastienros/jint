using Jint.Browser;

namespace Jint.Tests.Browser.Observers;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>IntersectionObserver</c> and <c>ResizeObserver</c>: task delivery and the flat model's geometry.
/// </summary>
public sealed class ViewportObserverTests
{
    [Test]
    public async Task EveryObservedTargetIntersectsOnce()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div><div id="b"></div>
            <script>
              window.log = [];
              const observer = new IntersectionObserver(entries => {
                for (const e of entries) {
                  window.log.push(e.target.id + ':' + e.isIntersecting + ':' + e.intersectionRatio);
                }
              });
              observer.observe(document.getElementById('a'));
              observer.observe(document.getElementById('b'));
              window.duringScript = window.log.length;
            </script>
            """);

        // Delivery is a task, not a microtask: nothing has been reported by the time the script ends.
        (await page.EvaluateAsync<int>("window.duringScript")).Should().Be(0);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("a:true:1|b:true:1");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The rectangles are the flat box model's, which is what C4 promised the observers when it had none:
    /// the target is the third rendered element (<c>html</c>, <c>body</c>, then the <c>div</c>), so its box
    /// starts at the third row and is one row tall and the viewport wide.
    /// </summary>
    [Test]
    public async Task AnEntryCarriesTheTargetsFlatBoxAndATimestamp()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div>
            <script>
              window.seen = null;
              new IntersectionObserver(entries => {
                const e = entries[0];
                window.seen = [
                  e instanceof IntersectionObserverEntry,
                  e.boundingClientRect.width, e.boundingClientRect.height,
                  e.intersectionRect.top, e.rootBounds.left,
                  typeof e.time,
                ].join('|');
              }).observe(document.getElementById('a'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.seen")).Should().Be("true|1280|16|32|0|number");
    }

    [Test]
    public async Task RootMarginAndThresholdsAreParsedAndReflected()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='a'></div>");

        (await page.EvaluateAsync<string>("new IntersectionObserver(() => {}).rootMargin"))
            .Should().Be("0px 0px 0px 0px");
        (await page.EvaluateAsync<string>("new IntersectionObserver(() => {}, { rootMargin: '10px 20%' }).rootMargin"))
            .Should().Be("10px 20% 10px 20%");
        (await page.EvaluateAsync<string>("new IntersectionObserver(() => {}, { rootMargin: '1px 2px 3px' }).rootMargin"))
            .Should().Be("1px 2px 3px 2px");
        (await page.EvaluateAsync<string>("new IntersectionObserver(() => {}).thresholds.join(',')"))
            .Should().Be("0");
        (await page.EvaluateAsync<string>("new IntersectionObserver(() => {}, { threshold: [1, 0.5, 0] }).thresholds.join(',')"))
            .Should().Be("0,0.5,1");
        (await page.EvaluateAsync<string>("new IntersectionObserver(() => {}, { threshold: 0.25 }).thresholds.join(',')"))
            .Should().Be("0.25");
        (await page.EvaluateAsync("new IntersectionObserver(() => {}).root"))
            .Should().BeNull();
        (await page.EvaluateAsync<bool>("new IntersectionObserver(() => {}, { root: document.getElementById('a') }).root.id === 'a'"))
            .Should().BeTrue();

        (await page.EvaluateAsync<string>(
            "(() => { try { new IntersectionObserver(() => {}, { threshold: 2 }); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("RangeError");

        // A malformed rootMargin crossed WebIDL's DOMString conversion and failed the interface's own parse,
        // which is a SyntaxError DOMException rather than a TypeError.
        (await page.EvaluateAsync<string>(
            "(() => { try { new IntersectionObserver(() => {}, { rootMargin: '1em' }); return 'no throw' } catch (e) { return e.name } })()"))
            .Should().Be("SyntaxError");
        (await page.EvaluateAsync<string>(
            "(() => { try { new IntersectionObserver(() => {}, { rootMargin: '1px 2px 3px 4px 5px' }); return 'no throw' } catch (e) { return e.name } })()"))
            .Should().Be("SyntaxError");
    }

    /// <summary>
    /// IntersectionObserver v2's <c>delay</c> and <c>trackVisibility</c>, and <c>scrollMargin</c>, are read,
    /// converted and reflected; every expectation was taken from headless Chromium (sebastienros/jint#4532).
    /// </summary>
    [Test]
    public async Task DelayTrackVisibilityAndScrollMarginAreReadAndReflected()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='a'></div>");

        static string Attempt(string options, string read)
            => "(() => { try { return String(new IntersectionObserver(() => {}, " + options + ")." + read + ") } catch (e) { return e.name } })()";

        // The dictionary's members are read in lexicographic order, each converted as it is read.
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const log = [], options = {};
              for (const k of ['root', 'rootMargin', 'scrollMargin', 'threshold', 'delay', 'trackVisibility'])
                Object.defineProperty(options, k, { get() { log.push(k); return undefined; } });
              new IntersectionObserver(() => {}, options);
              return log.join();
            })()
            """)).Should().Be("delay,root,rootMargin,scrollMargin,threshold,trackVisibility");
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const log = [];
              new IntersectionObserver(() => {}, {
                get delay() { log.push('delay'); return { valueOf() { log.push('valueOf'); return 1; } }; },
                get root() { log.push('root'); return null; },
              });
              return log.join();
            })()
            """)).Should().Be("delay,valueOf,root");

        // The callback is converted before the options, and a primitive is not a dictionary.
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const log = [];
              try { new IntersectionObserver(5, { get delay() { log.push('delay'); return 0; } }) } catch (e) { log.push(e.name) }
              return log.join();
            })()
            """)).Should().Be("TypeError");
        (await page.EvaluateAsync<string>(Attempt("5", "delay"))).Should().Be("TypeError");
        (await page.EvaluateAsync<string>(Attempt("'x'", "delay"))).Should().Be("TypeError");
        (await page.EvaluateAsync<string>(Attempt("null", "delay"))).Should().Be("0");

        // Defaults.
        (await page.EvaluateAsync<string>(
            "(() => { const o = new IntersectionObserver(() => {}); return [o.delay, o.trackVisibility, o.scrollMargin].join('|') })()"))
            .Should().Be("0|false|0px 0px 0px 0px");

        // delay is a restricted double, truncated toward zero; a non-finite value or a Symbol fails the conversion.
        (await page.EvaluateAsync<string>(Attempt("{ delay: 12.9 }", "delay"))).Should().Be("12");
        (await page.EvaluateAsync<string>(Attempt("{ delay: -5.5 }", "delay"))).Should().Be("-5");
        (await page.EvaluateAsync<string>(Attempt("{ delay: '7' }", "delay"))).Should().Be("7");
        (await page.EvaluateAsync<string>(Attempt("{ delay: 2 ** 33 }", "delay"))).Should().Be("8589934592");
        (await page.EvaluateAsync<string>(Attempt("{ delay: NaN }", "delay"))).Should().Be("TypeError");
        (await page.EvaluateAsync<string>(Attempt("{ delay: Infinity }", "delay"))).Should().Be("TypeError");
        (await page.EvaluateAsync<string>(Attempt("{ delay: Symbol() }", "delay"))).Should().Be("TypeError");

        // trackVisibility is a boolean, and needs a delay of at least 100.
        (await page.EvaluateAsync<string>(
            "(() => { const o = new IntersectionObserver(() => {}, { trackVisibility: 1, delay: 100.7 }); return o.trackVisibility + '|' + o.delay })()"))
            .Should().Be("true|100");
        (await page.EvaluateAsync<string>(Attempt("{ trackVisibility: true }", "delay"))).Should().Be("NotSupportedError");
        (await page.EvaluateAsync<string>(Attempt("{ trackVisibility: true, delay: 99.9 }", "delay"))).Should().Be("NotSupportedError");

        // scrollMargin follows rootMargin's grammar and serialization.
        (await page.EvaluateAsync<string>(Attempt("{ scrollMargin: '10px 5%' }", "scrollMargin"))).Should().Be("10px 5% 10px 5%");
        (await page.EvaluateAsync<string>(Attempt("{ scrollMargin: '1px 2px 3px' }", "scrollMargin"))).Should().Be("1px 2px 3px 2px");
        (await page.EvaluateAsync<string>(Attempt("{ scrollMargin: '1em' }", "scrollMargin"))).Should().Be("SyntaxError");
        (await page.EvaluateAsync<string>(Attempt("{ scrollMargin: 'bogus' }", "scrollMargin"))).Should().Be("SyntaxError");

        // After the conversion, the delay check runs first, then rootMargin, scrollMargin and the threshold range.
        (await page.EvaluateAsync<string>(Attempt("{ trackVisibility: true, rootMargin: 'x', threshold: 5 }", "delay")))
            .Should().Be("NotSupportedError");
        (await page.EvaluateAsync<string>(Attempt("{ trackVisibility: true, root: 5 }", "delay"))).Should().Be("TypeError");
        (await page.EvaluateAsync<string>(
            "(() => { try { new IntersectionObserver(() => {}, { rootMargin: 'bad1', scrollMargin: 'bad2' }) } catch (e) { return e.message } })()"))
            .Should().Contain("rootMargin");
        (await page.EvaluateAsync<string>(
            "(() => { try { new IntersectionObserver(() => {}, { scrollMargin: 'bad2', threshold: 5 }) } catch (e) { return e.name + ':' + e.message } })()"))
            .Should().StartWith("SyntaxError:").And.Contain("scrollMargin");
    }

    [Test]
    public async Task UnobserveAndDisconnectDropAWaitingNotification()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div><div id="b"></div><div id="c"></div>
            <script>
              window.log = [];
              const observer = new IntersectionObserver(entries => {
                for (const e of entries) { window.log.push(e.target.id) }
              });
              observer.observe(document.getElementById('a'));
              observer.observe(document.getElementById('b'));
              observer.unobserve(document.getElementById('a'));

              const other = new IntersectionObserver(() => { window.log.push('other') });
              other.observe(document.getElementById('c'));
              other.disconnect();
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("b");
    }

    [Test]
    public async Task TakeRecordsEmptiesTheQueueBeforeItIsDelivered()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div>
            <script>
              window.log = [];
              const observer = new IntersectionObserver(entries => { window.log.push('cb:' + entries.length) });
              observer.observe(document.getElementById('a'));
              window.taken = observer.takeRecords().length;
            </script>
            """);

        (await page.EvaluateAsync<int>("window.taken")).Should().Be(1);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().BeEmpty();
    }

    [Test]
    public async Task EveryResizeObservedTargetReceivesAnInitialNotification()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div><div id="b"></div>
            <script>
              window.log = [];
              const observer = new ResizeObserver(entries => {
                for (const e of entries) {
                  window.log.push([
                    e.target.id,
                    e instanceof ResizeObserverEntry,
                    e.contentRect.width,
                    e.borderBoxSize.length,
                    e.contentBoxSize[0].inlineSize,
                    e.devicePixelContentBoxSize[0].blockSize,
                  ].join(':'));
                }
              });
              observer.observe(document.getElementById('a'));
              observer.observe(document.getElementById('b'), { box: 'border-box' });
              window.duringScript = window.log.length;
            </script>
            """);

        (await page.EvaluateAsync<int>("window.duringScript")).Should().Be(0);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        // Both boxes are one row tall and the viewport wide, and a content rectangle is measured from its own
        // origin — so the two targets report the same size even though they sit on different rows.
        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("a:true:1280:1:1280:16|b:true:1280:1:1280:16");
    }

    [Test]
    public async Task AResizeObserverStopsAfterUnobserveAndDisconnect()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div><div id="b"></div>
            <script>
              window.log = [];
              const observer = new ResizeObserver(entries => {
                for (const e of entries) { window.log.push(e.target.id) }
              });
              observer.observe(document.getElementById('a'));
              observer.observe(document.getElementById('b'));
              observer.unobserve(document.getElementById('a'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("b");

        await page.EvaluateAsync("window.log.length = 0");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        // No dimensions changed, so an idle observer reports nothing more.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().BeEmpty();
    }

    [Test]
    public async Task NeitherObserverIsConstructibleWithoutACallbackAndTheEntriesAreNotConstructibleAtAll()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        foreach (var source in new[] { "new IntersectionObserver()", "new ResizeObserver()", "new IntersectionObserverEntry()", "new ResizeObserverEntry()" })
        {
            (await page.EvaluateAsync<string>(
                "(() => { try { " + source + "; return 'no throw' } catch (e) { return e.constructor.name } })()"))
                .Should().Be("TypeError", "{0} is a TypeError", source);
        }
    }

    [Test]
    public async Task ACallbackThatThrowsIsReportedAndThePageSurvives()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a"></div>
            <script>
              new IntersectionObserver(() => { throw new Error('from an observer') }).observe(document.getElementById('a'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        page.Errors.Should().ContainSingle();
        (await page.EvaluateAsync<int>("1 + 1")).Should().Be(2);
    }
}
