using Jint.Browser;

namespace Jint.Tests.Browser.Observers;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>IntersectionObserver</c> and <c>ResizeObserver</c>: one notification per observed target, delivered as
/// a task, with the geometry a page with no layout can honestly be told.
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

    [Test]
    public async Task AnEntryCarriesZeroRectanglesAndATimestamp()
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
        (await page.EvaluateAsync<string>("window.seen")).Should().Be("true|0|0|0|0|number");
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
    public async Task EveryResizeObservedTargetIsReportedOnce()
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
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("a:true:0:1:0:0|b:true:0:1:0:0");
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

        // Nothing is reported a second time: with no layout, no size can change.
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
