using System.Diagnostics;
using Jint.Browser;
using Jint.Constraints;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// What bounds a page: the per-turn budget, the page-sized web-API limits, the DOM-node ceiling, and the
/// untrusted-content posture.
/// </summary>
/// <remarks>
/// Every case here is about something that cannot be bounded by an ordinary engine constraint. A page is a
/// host-driven sequence of entries and its event loop is pumped, so <c>LimitExecutionTime</c> re-arms per
/// entry and never fires inside a job chain at all; what a page is bounded by is the two constraints whose
/// window the host owns, armed around every turn.
/// </remarks>
public sealed class PageConstraintTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(200);

    [Test]
    public async Task ARunawayTimerEndsItsTurnAndThePageGoesOn()
    {
        await using var browser = new Browser(new BrowserOptions { MaxTaskDuration = Budget });
        var page = await browser.NewPageAsync();

        // Both timers are due at once, so one drain runs both: the first records that it ran, the second
        // never returns. A page whose budget worked only for the runaway would be a page that lost the
        // callback queued in front of it.
        await page.SetContentAsync(
            """
            <script>
              window.early = false;
              setTimeout(function () { window.early = true }, 0);
              setTimeout(function () { while (true) { } }, 0);
            </script>
            """);

        await page.WaitForIdleAsync(TimeSpan.FromSeconds(10));

        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<bool>("window.early")).Should().BeTrue();
        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task ARunawayInlineScriptEndsAndTheDocumentStillLoads()
    {
        await using var browser = new Browser(new BrowserOptions { MaxTaskDuration = Budget });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <p id='before'>seen</p>
            <script>while (true) { }</script>
            <p id='after'>also seen</p>
            <script>window.later = 'ran'</script>
            """);

        // The script ends, the parse goes on with a budget of its own, and the rest of the document is
        // there — which is the whole difference between bounding a script and failing a navigation.
        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<string>("document.getElementById('after').textContent")).Should().Be("also seen");
        (await page.EvaluateAsync<string>("window.later")).Should().Be("ran");
        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task ARunawayEvaluateFailsItsOwnTaskRatherThanThePage()
    {
        await using var browser = new Browser(new BrowserOptions { MaxTaskDuration = Budget });
        var page = await browser.NewPageAsync();

        // A mailbox request is a turn too, and the bracket sits outside the request's own try/catch — so
        // the caller is told rather than the diagnostics log.
        var act = async () => await page.EvaluateAsync("while (true) { }");

        await act.Should().ThrowAsync<TimeoutException>();

        page.Errors.Should().BeEmpty();
        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task AnInfiniteMaxTaskDurationLeavesTheTurnUnbounded()
    {
        // The engine's own spelling of "no limit", and the page's: nothing is armed, so a script that would
        // have been cut short is not. Bounded here by a token instead, which is what closing a page uses.
        await using var browser = new Browser(new BrowserOptions { MaxTaskDuration = Timeout.InfiniteTimeSpan });
        var page = await browser.NewPageAsync();

        var evaluation = page.EvaluateAsync("while (true) { }");
        var completed = await Task.WhenAny(evaluation, Task.Delay(TimeSpan.FromSeconds(1)));

        completed.Should().NotBeSameAs(evaluation, "no budget was armed, so nothing cut the loop short");

        await page.CloseAsync();

        var act = async () => await evaluation;
        await act.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task AMemoryBombInATimerIsBounded()
    {
        if (MemoryLimitConstraint.Accuracy == MemoryLimitAccuracy.Unavailable)
        {
            Assert.Ignore("This runtime exposes no per-thread allocation counter, so no memory limit can be enforced.");
        }

        await using var browser = new Browser(new BrowserOptions
        {
            MemoryLimit = 4_000_000,

            // Long enough that the allocation budget is what ends the turn and not the clock.
            MaxTaskDuration = TimeSpan.FromSeconds(30),
        });

        var page = await browser.NewPageAsync();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        await page.SetContentAsync(
            """
            <script>
              window.kept = [];
              setTimeout(function () {
                for (var i = 0; i < 100000; i++) { window.kept.push(new Array(4096).fill(i)) }
                window.finished = true;
              }, 0);
            </script>
            """);

        await page.WaitForIdleAsync(TimeSpan.FromSeconds(30));

        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<string>("typeof window.finished")).Should().Be("undefined");

        // The absolute reading is the coarse half of the claim and the error above is the exact one: an
        // unbounded run of that loop is gigabytes, so anything in the tens of megabytes says it was stopped.
        var after = GC.GetTotalMemory(forceFullCollection: true);
        (after - before).Should().BeLessThan(200_000_000);

        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task ClosingThePageWhileAJobSpinsCompletesWithinTheBudget()
    {
        var browser = new Browser(new BrowserOptions { MaxTaskDuration = Budget });

        try
        {
            var page = await browser.NewPageAsync();
            await page.SetContentAsync("<script>setInterval(function () { while (true) { } }, 1)</script>");

            // Long enough for the interval to be in a spin when the close arrives.
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var started = Stopwatch.GetTimestamp();
            await page.CloseAsync();
            var elapsed = Stopwatch.GetElapsedTime(started);

            // Two things end the turn and either is enough: the page's cancellation token, which the engine
            // observes through its own constraint, and the turn's own deadline. What must not happen is a
            // close that waits out an unbounded job.
            elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            page.IsClosed.Should().BeTrue();
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    [Test]
    public async Task ARunawayWorkerIsBoundedAndThePageIsTold()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server
                .Map("/worker.js", _ => LoopbackResponse.Bytes(
                    """
                    self.addEventListener('message', function () { while (true) { } });
                    """,
                    "text/javascript"))
                .MapHtml(
                    "/index.html",
                    """
                    <script>
                      var worker = new Worker('/worker.js', { type: 'module' });
                      worker.postMessage('spin');
                    </script>
                    """),
            configureBrowser: options => options.MaxTaskDuration = Budget);

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        // A worker's pump is bracketed exactly as the page's is, so a worker that never returns from a
        // message handler ends that turn instead of burning a thread for the life of the page.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (fixture.Page.Errors.Count == 0 && DateTime.UtcNow < deadline)
        {
            await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(25));
        }

        fixture.Page.Errors.Should().Contain(e => e.Kind == PageErrorKind.WorkerError);
        (await fixture.Page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task MaxActiveTimersBoundsWhatOnePageMaySchedule()
    {
        await using var browser = new Browser(new BrowserOptions { MaxActiveTimers = 2 });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.refused = null;
              setTimeout(function () { }, 60000);
              setTimeout(function () { }, 60000);
              try { setTimeout(function () { }, 60000) } catch (e) { window.refused = e.name }
            </script>
            """);

        (await page.EvaluateAsync<string>("window.refused")).Should().Be("QuotaExceededError");
    }

    [Test]
    public async Task MaxDomNodesRefusesADocumentLargerThanItself()
    {
        await using var browser = new Browser(new BrowserOptions { MaxDomNodes = 40 });
        var page = await browser.NewPageAsync();

        var markup = "<div>" + string.Concat(Enumerable.Repeat("<p>x</p>", 200)) + "</div>";

        var act = async () => await page.SetContentAsync(markup);

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*MaxDomNodes*");

        // Nothing is shown, and the page is still a page.
        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task MaxDomNodesStopsAScriptGrowingTheDomAndThePageSurvives()
    {
        await using var browser = new Browser(new BrowserOptions { MaxDomNodes = 60 });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='host'></div>");

        var refused = await page.EvaluateAsync<string>(
            """
            (function () {
              window.made = 0;
              try {
                for (var i = 0; i < 500; i++) {
                  document.getElementById('host').appendChild(document.createElement('div'));
                  window.made++;
                }
                return 'unbounded';
              } catch (e) { return e.name }
            })()
            """);

        refused.Should().Be("RangeError");

        // Bounded, and by the wrappers this document has handed to script rather than by the nodes it was
        // parsed with — the two quantities the one number is checked against.
        (await page.EvaluateAsync<double>("window.made")).Should().BeLessThan(60);
        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
        page.Errors.Should().BeEmpty("the script caught the error, so nothing reached the sink");
    }

    [Test]
    public async Task ADocumentThatItsOwnScriptGrewPastMaxDomNodesIsNotShown()
    {
        await using var browser = new Browser(new BrowserOptions { MaxDomNodes = 60 });
        var page = await browser.NewPageAsync();

        // The parse cannot be stopped part way, so a document its own inline script grew past the ceiling is
        // refused at the end of the parse the same way one that arrived that large is.
        var act = async () => await page.SetContentAsync(
            """
            <div id='host'></div>
            <script>
              try {
                for (var i = 0; i < 500; i++) {
                  document.getElementById('host').appendChild(document.createElement('div'));
                }
              } catch (e) { }
            </script>
            """);

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*MaxDomNodes*");

        (await page.EvaluateAsync<double>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task ForUntrustedContentPutsTheProfileInForceOnEveryPageEngine()
    {
        var limits = UntrustedCodeLimits.Default with { MaxStatements = 4321 };
        await using var browser = new Browser(new BrowserOptions().ForUntrustedContent(limits));
        var page = await browser.NewPageAsync();

        // The observable the engine's own tests use: the profile's statement budget is the engine's.
        var configured = await page.RunOnLoopAsync(
            engine => engine.Constraints.Find<MaxStatementsConstraint>()?.MaxStatements ?? -1);

        configured.Should().Be(4321);
        (await page.EvaluateAsync<string>("(function () { try { eval('1 + 1'); return 'allowed' } catch (e) { return 'refused' } })()"))
            .Should().Be("refused");
    }

    [Test]
    public async Task ForUntrustedContentWinsOverAHostCallbackThatLoosensIt()
    {
        var options = new BrowserOptions().ForUntrustedContent();
        options.ConfigureEngine(o =>
        {
            o.Host.StringCompilationAllowed = true;
            o.Constraints.MaxRecursionDepth = int.MaxValue;
        });

        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        // The profile is declared from inside the package's own construction callback, and the engine
        // re-expands it over whatever the host's callbacks wrote — so a callback cannot reopen what it closed.
        (await page.EvaluateAsync<string>("(function () { try { eval('1 + 1'); return 'allowed' } catch (e) { return 'refused' } })()"))
            .Should().Be("refused");

        // And a limit the callback saturated is the profile's again, not "unlimited".
        (await page.RunOnLoopAsync(engine => engine.Options.Constraints.MaxRecursionDepth))
            .Should().Be(UntrustedCodeLimits.Default.MaxRecursionDepth);
    }

    [Test]
    public async Task AHostCallbackThatReopensTheProfileAfterConstructionIsRefusedRatherThanHalfApplied()
    {
        var options = new BrowserOptions().ForUntrustedContent();

        // ConfigureEngine runs while the options are still being written, so a profile declared there would
        // simply be honoured. What cannot work is reaching for one from an Options.Configure callback, which
        // runs after the realm and the host have been built from the options as they then stood — and the
        // engine refuses that outright rather than leaving a half-hardened engine behind.
        options.ConfigureEngine(o => o.Configure(engine =>
            engine.Options.ForUntrustedCode(UntrustedCodeLimits.Default with { MaxStatements = 1 })));

        await using var browser = new Browser(options);

        var act = async () => await browser.NewPageAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task AHostCallbackMayDeclareTheProfileItself()
    {
        // The other half of the same rule, and the reason ForUntrustedContent can be implemented as a
        // package-owned ConfigureEngine callback at all: those callbacks run before the engine is built.
        var options = new BrowserOptions();
        options.ConfigureEngine(o => o.ForUntrustedCode(UntrustedCodeLimits.Default));

        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>("(function () { try { eval('1 + 1'); return 'allowed' } catch (e) { return 'refused' } })()"))
            .Should().Be("refused");
    }

    [Test]
    public void ForUntrustedContentTakesItsBudgetsFromTheLimitsUnlessTheHostSetThem()
    {
        var limits = UntrustedCodeLimits.Default;

        var derived = new BrowserOptions().ForUntrustedContent(limits);
        derived.MaxTaskDuration.Should().Be(limits.MaxOperationDuration);
        derived.MemoryLimit.Should().Be(limits.MemoryLimit);

        var chosen = new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(3), MemoryLimit = 999_999 };
        chosen.ForUntrustedContent(limits);
        chosen.MaxTaskDuration.Should().Be(TimeSpan.FromSeconds(3));
        chosen.MemoryLimit.Should().Be(999_999);
    }

    [Test]
    public async Task ForUntrustedContentBoundsThePageEngineByTheMemoryLimitTheHostChose()
    {
        if (MemoryLimitConstraint.Accuracy == MemoryLimitAccuracy.Unavailable)
        {
            Assert.Ignore("This runtime exposes no per-thread allocation counter, so no memory limit can be enforced.");
        }

        var options = new BrowserOptions { MemoryLimit = 7_000_000 };
        options.ForUntrustedContent();

        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        // The host's number and the profile's must never be two numbers: the turn bracket arms the very
        // constraint the profile registered, so they have to be the same one.
        var configured = await page.RunOnLoopAsync(
            engine => engine.Constraints.Find<MemoryLimitConstraint>()?.MemoryLimit ?? -1);
        var statements = await page.RunOnLoopAsync(
            engine => engine.Constraints.Find<MaxStatementsConstraint>()?.MaxStatements ?? -1);

        configured.Should().Be(7_000_000);
        statements.Should().Be(UntrustedCodeLimits.Default.MaxStatements, "the profile is what the rest of the budget came from");
    }

    [Test]
    public async Task ForUntrustedContentBlocksThePrivateNetworkAndAnExplicitChoiceSurvives()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/index.html", "<title>served</title>");

        await using var blocked = new Browser(new BrowserOptions().ForUntrustedContent());
        var blockedPage = await (await blocked.NewContextAsync(
            new BrowserContextOptions { UrlFilter = server.Owns })).NewPageAsync();

        var act = async () => await blockedPage.NavigateAsync(server.Url("/index.html"));

        await act.Should().ThrowAsync<NavigationFailedException>();

        server.Received.Should().BeEmpty("the loopback origin is a private-network address");

        await using var allowed = new Browser(new BrowserOptions().ForUntrustedContent());
        var allowedPage = await (await allowed.NewContextAsync(
            new BrowserContextOptions { UrlFilter = server.Owns, BlockPrivateNetwork = false })).NewPageAsync();

        await allowedPage.NavigateAsync(server.Url("/index.html"));

        (await allowedPage.TitleAsync()).Should().Be("served");
    }

    [Test]
    public async Task UnderForUntrustedContentEveryKindOfLoadIsRefusedTogether()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("payload"));
        server.Map("/worker.js", _ => LoopbackResponse.Bytes("self.postMessage('reached');", "text/javascript"));

        var refused = await LoadsFrom(server, blockPrivateNetwork: null);

        refused.Should().Be("fetch:refused|worker:refused|xhr:refused");
        server.Received.Should().BeEmpty("one filter covers the document, fetch, XMLHttpRequest and a worker's module load");

        var reached = await LoadsFrom(server, blockPrivateNetwork: false);

        reached.Should().Be("fetch:ok|worker:ok|xhr:ok");
        server.Received.Should().Contain(r => r.Path == "/data");
        server.Received.Should().Contain(r => r.Path == "/worker.js");
    }

    /// <summary>
    /// Loads a page whose document reached no network — <see cref="Page.SetContentAsync"/> with the server's
    /// own base URL — and has it try each kind of load against that server.
    /// </summary>
    /// <remarks>
    /// The base URL matters twice: it is what a relative <c>fetch</c> resolves against, and it is what makes
    /// the page's workers get a module loader at all. Reaching no network to set it up is what lets the same
    /// script run in a context that refuses the server.
    /// </remarks>
    private static async Task<string> LoadsFrom(LoopbackServer server, bool? blockPrivateNetwork)
    {
        var contextOptions = new BrowserContextOptions { UrlFilter = server.Owns };
        if (blockPrivateNetwork is { } choice)
        {
            contextOptions.BlockPrivateNetwork = choice;
        }

        await using var browser = new Browser(new BrowserOptions().ForUntrustedContent());
        var context = await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.results = [];
              fetch('/data')
                .then(function () { window.results.push('fetch:ok') })
                .catch(function () { window.results.push('fetch:refused') });

              var xhr = new XMLHttpRequest();
              xhr.open('GET', '/data');
              xhr.onload = function () { window.results.push('xhr:ok') };
              xhr.onerror = function () { window.results.push('xhr:refused') };
              xhr.send();

              var worker = new Worker('/worker.js', { type: 'module' });
              worker.addEventListener('message', function () { window.results.push('worker:ok') });
              worker.addEventListener('error', function () { window.results.push('worker:refused') });
            </script>
            """,
            server.Url("/index.html"));

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await page.EvaluateAsync<double>("window.results.length") < 3 && DateTime.UtcNow < deadline)
        {
            await page.WaitForIdleAsync(TimeSpan.FromMilliseconds(25));
        }

        var results = await page.EvaluateAsync<string>("window.results.slice().sort().join('|')");
        return results ?? "";
    }
}
