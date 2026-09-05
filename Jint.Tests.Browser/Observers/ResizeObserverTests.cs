using Jint.Browser;

namespace Jint.Tests.Browser.Observers;

using Browser = global::Jint.Browser.Browser;

public class ResizeObserverTests
{
    [Test]
    public async Task AHiddenTargetIsReportedWhenItsAncestorBecomesVisible()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="parent" style="display:none"><div id="target"><span>Files</span></div></div>
            <script>
              window.sizes = [];
              new ResizeObserver(entries => sizes.push(entries[0].contentRect.height))
                .observe(document.getElementById('target'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("0");
        await page.EvaluateAsync("document.getElementById('parent').style.display = ''");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("0,32");

        await page.EvaluateAsync("document.getElementById('parent').style.display = 'none'");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("0,32,0");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task EntriesKeepTheirMeasuredSizeAcrossLaterMutations()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="target"></div>
            <script>
              window.entries = [];
              new ResizeObserver(batch => entries.push(batch[0]))
                .observe(document.getElementById('target'));
            </script>
            """);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        await page.EvaluateAsync("document.getElementById('target').appendChild(document.createElement('span'))");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>(
            "entries.map(e => [e.contentRect.height, e.borderBoxSize[0].blockSize, e.contentBoxSize[0].blockSize, e.devicePixelContentBoxSize[0].blockSize].join(':')).join('|')"))
            .Should().Be("16:16:16:16|32:32:32:32");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("parent.classList.add('hidden')", "parent.classList.remove('hidden')")]
    [TestCase("document.styleSheets[0].insertRule('#parent { display:none }', 0)", "document.styleSheets[0].deleteRule(0)")]
    [TestCase("parent.remove()", "document.body.appendChild(parent)")]
    public async Task ChangesWithoutAnObservedTargetMutationAreReported(string hide, string show)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>.hidden { display:none }</style>
            <div id="parent"><div id="target"></div></div>
            <script>
              const parent = document.getElementById('parent');
              window.sizes = [];
              new ResizeObserver(entries => sizes.push(entries[0].contentRect.height))
                .observe(document.getElementById('target'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        await page.EvaluateAsync(hide);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        await page.EvaluateAsync(show);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("16,0,16");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ViewportChangesButNotPositionChangesResizeATarget()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="target"></div>
            <script>
              window.sizes = [];
              new ResizeObserver(entries => sizes.push(entries[0].contentRect.width))
                .observe(document.getElementById('target'));
            </script>
            """);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        await page.EvaluateAsync("document.body.prepend(document.createElement('div'))");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("1280");

        await page.SetViewportAsync(new Viewport(640, 480));
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("1280,640");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CallbackChangesAreDeliveredInALaterTaskAndThenBecomeIdle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="target"></div>
            <script>
              window.log = [];
              new ResizeObserver(entries => {
                const entry = entries[0];
                log.push(entry.contentRect.height);
                if (entry.contentRect.height === 16) {
                  entry.target.appendChild(document.createElement('span'));
                  log.push('snapshot:' + entry.contentRect.height);
                  Promise.resolve().then(() => log.push('microtask'));
                }
              }).observe(document.getElementById('target'));
              Promise.resolve().then(() => log.push('before'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join(',')")).Should().Be("before,16,snapshot:16,microtask,32");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task UnobserveDisconnectAndReobserveControlFutureNotifications()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="a"></div><div id="b"></div>
            <script>
              const a = document.getElementById('a'), b = document.getElementById('b');
              window.log = [];
              const observer = new ResizeObserver(entries => {
                log.push(entries.map(e => e.target.id + ':' + e.contentRect.height).join(','));
              });
              observer.observe(a);
              observer.observe(b);
              observer.observe(a);
            </script>
            """);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("a:16,b:16");

        await page.EvaluateAsync(
            "observer.unobserve(a); a.appendChild(document.createElement('span')); b.appendChild(document.createElement('span'))");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("a:16,b:16|b:32");

        await page.EvaluateAsync("observer.disconnect(); b.appendChild(document.createElement('span'))");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("a:16,b:16|b:32");

        await page.EvaluateAsync("observer.observe(a)");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join('|')")).Should().Be("a:16,b:16|b:32|a:32");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OneCallbackCanDisconnectAnotherBeforeItIsDelivered()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="target"></div>
            <script>
              window.log = [];
              const target = document.getElementById('target');
              const first = new ResizeObserver(() => {
                log.push('first');
                second.disconnect();
                Promise.resolve().then(() => log.push('microtask'));
              });
              const second = new ResizeObserver(() => log.push('second'));
              const third = new ResizeObserver(() => log.push('third'));
              first.observe(target);
              second.observe(target);
              third.observe(target);
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join(',')")).Should().Be("first,microtask,third");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ThrowingCallbacksDoNotStopOtherObserversOrFutureChanges()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id="target"></div>
            <script>
              const target = document.getElementById('target');
              window.sizes = [];
              new ResizeObserver(() => { throw new Error('resize failed'); }).observe(target);
              new ResizeObserver(entries => sizes.push(entries[0].contentRect.height)).observe(target);
            </script>
            """);
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        await page.EvaluateAsync("target.appendChild(document.createElement('span'))");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("sizes.join(',')")).Should().Be("16,32");
        page.Errors.Should().HaveCount(2).And.OnlyContain(error => error.Kind == PageErrorKind.UncaughtCallbackError);
    }
}
