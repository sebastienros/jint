using Jint.Browser;

namespace Jint.Tests.Browser.Popups;

using Browser = global::Jint.Browser.Browser;

public sealed class PageLimitTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    [Test]
    public async Task PendingPopupCountsAndNamedReuseStillWorksAtCapacity()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = false;
        var options = new BrowserOptions { MaxPages = 2 }.ConfigureEngine(_ =>
        {
            if (!Volatile.Read(ref hold)) return;
            entered.TrySetResult();
            if (!release.Wait(Ceiling)) throw new TimeoutException("Popup initialization did not resume.");
        });
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();
        var opened = new TaskCompletionSource<Page>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Popup += (_, popup) => opened.TrySetResult(popup);
        Volatile.Write(ref hold, true);
        try
        {
            await page.EvaluateAsync("window.w = open('', 'report'); void 0");
            await entered.Task.WaitAsync(Ceiling);
            (await page.EvaluateAsync<bool>("w !== null && w === open('', 'report') && open() === null && open('', 'other') === null"))
                .Should().BeTrue();
            var create = () => browser.NewPageAsync();
            await create.Should().ThrowAsync<InvalidOperationException>();
            browser.DefaultContext.Pages.Should().ContainSingle();
        }
        finally
        {
            Volatile.Write(ref hold, false);
            release.Set();
        }
        var popup = await opened.Task.WaitAsync(Ceiling);
        (await popup.EvaluateAsync<bool>("open() === null")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("w === open('', 'report')")).Should().BeTrue();
        await popup.CloseAsync();
        var replacement = await browser.NewPageAsync();
        browser.DefaultContext.Pages.Should().HaveCount(2);
        replacement.IsClosed.Should().BeFalse();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PendingHostPageCountsAgainstBothCreationPathsAndCloseWaitsForIt()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = false;
        var options = new BrowserOptions { MaxPages = 2 }.ConfigureEngine(_ =>
        {
            if (!Volatile.Read(ref hold)) return;
            entered.TrySetResult();
            if (!release.Wait(Ceiling)) throw new TimeoutException("Page initialization did not resume.");
        });
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();
        Volatile.Write(ref hold, true);
        var pending = browser.NewPageAsync();
        Task? closing = null;
        try
        {
            await entered.Task.WaitAsync(Ceiling);
            var create = () => browser.NewPageAsync();
            await create.Should().ThrowAsync<InvalidOperationException>();
            (await page.EvaluateAsync<bool>("open() === null")).Should().BeTrue();
            closing = browser.DefaultContext.CloseAsync();
            closing.IsCompleted.Should().BeFalse();
        }
        finally
        {
            release.Set();
        }
        var result = async () => await pending;
        await result.Should().ThrowAsync<ObjectDisposedException>();
        await closing!.WaitAsync(Ceiling);
        browser.DefaultContext.Pages.Should().BeEmpty();
    }

    [Test]
    public async Task FailedHostInitializationReleasesItsReservation()
    {
        var fail = true;
        await using var browser = new Browser(new BrowserOptions { MaxPages = 1 }.ConfigureEngine(_ =>
        {
            if (fail) throw new InvalidOperationException("Initialization failure");
        }));
        var create = () => browser.NewPageAsync();
        await create.Should().ThrowAsync<InvalidOperationException>();
        fail = false;
        var page = await browser.NewPageAsync();
        page.IsClosed.Should().BeFalse();
        browser.DefaultContext.Pages.Should().ContainSingle();
    }

    [Test]
    public async Task FailedPopupInitializationReleasesItsReservation()
    {
        var fail = false;
        await using var browser = new Browser(new BrowserOptions { MaxPages = 2 }.ConfigureEngine(_ =>
        {
            if (Volatile.Read(ref fail)) throw new InvalidOperationException("Popup initialization failure");
        }));
        var page = await browser.NewPageAsync();
        Volatile.Write(ref fail, true);
        await page.EvaluateAsync("window.w = open(); void 0");
        (await page.WaitForAsync("w.closed", Ceiling)).Should().BeTrue();
        page.Errors.Should().ContainSingle();
        Volatile.Write(ref fail, false);
        // The failed handle can become closed just before its reservation is released. Wait for the
        // replacement using the normal task queue rather than treating that publication as the cleanup.
        await page.EvaluateAsync("window.next = null; var retry = setInterval(() => { next = open(); if (next) clearInterval(retry); }, 1); void 0");
        (await page.WaitForAsync("next !== null", Ceiling)).Should().BeTrue();
        await browser.DefaultContext.CloseAsync();
    }

    [Test]
    public async Task ContextOverrideIsIndependentAndReadAtCreation()
    {
        var options = new BrowserContextOptions { MaxPages = 1 };
        await using var browser = new Browser(new BrowserOptions { MaxPages = 2 });
        await using var context = await browser.NewContextAsync(options);
        options.MaxPages = 0;
        var page = await context.NewPageAsync();
        (await page.EvaluateAsync<bool>("open() === null")).Should().BeTrue();
        await browser.NewPageAsync();
        await browser.NewPageAsync();
        browser.DefaultContext.Pages.Should().HaveCount(2);
        context.Pages.Should().ContainSingle();
    }

    [Test]
    public async Task LinkAndFormTargetsAreRefusedAtCapacity()
    {
        await using var browser = new Browser(new BrowserOptions { MaxPages = 1 });
        var page = await browser.NewPageAsync();
        var announcements = 0;
        page.Popup += (_, _) => Interlocked.Increment(ref announcements);
        await page.SetContentAsync("<a id='link' href='about:blank' target='_blank'>open</a><form id='form' action='about:blank' target='_blank'></form>");
        await page.EvaluateAsync("document.getElementById('link').click(); document.getElementById('form').submit(); void 0");
        // Close waits for every pending popup too, so this observes creation rather than racing registration.
        await browser.DefaultContext.CloseAsync();
        browser.DefaultContext.Pages.Should().BeEmpty();
        announcements.Should().Be(0);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task UntrustedDefaultsAndOverridesBoundEachContextIndependently()
    {
        var options = new BrowserOptions().ForUntrustedContent();
        options.MaxPages.Should().Be(16);
        options.MaxPages = int.MaxValue;
        options.MaxPages.Should().Be(16);
        options.MaxPages = 0;
        options.MaxPages.Should().Be(16);
        options.MaxPages = 1;
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<bool>("open() === null")).Should().BeTrue();
        foreach (var limit in new[] { 0, int.MaxValue, 1 })
        {
            await using var context = await browser.NewContextAsync(new BrowserContextOptions { MaxPages = limit });
            var other = await context.NewPageAsync();
            (await other.EvaluateAsync<bool>("open() === null")).Should().BeTrue();
            context.Pages.Should().ContainSingle();
        }
    }

    [Test]
    public async Task UntrustedDefaultCapsABurstIncludingPopupDescendants()
    {
        await using var browser = new Browser(new BrowserOptions().ForUntrustedContent());
        var page = await browser.NewPageAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var announcements = 0;
        page.Popup += (_, _) =>
        {
            if (Interlocked.Increment(ref announcements) == 15) ready.TrySetResult();
        };
        (await page.EvaluateAsync<double>("var count = 0; for (var i = 0; i < 50; i++) { if (open() !== null) count++; } count"))
            .Should().Be(15);
        await ready.Task.WaitAsync(Ceiling);
        browser.DefaultContext.Pages.Should().HaveCount(16);
        var popup = browser.DefaultContext.Pages.First(p => !ReferenceEquals(p, page));
        (await popup.EvaluateAsync<bool>("open() === null")).Should().BeTrue();
    }

    [Test]
    public void NegativeLimitsAreRejected()
    {
        var browser = () => new BrowserOptions { MaxPages = -1 };
        browser.Should().Throw<ArgumentOutOfRangeException>();
        var context = () => new BrowserContextOptions { MaxPages = -1 };
        context.Should().Throw<ArgumentOutOfRangeException>();
    }
}
