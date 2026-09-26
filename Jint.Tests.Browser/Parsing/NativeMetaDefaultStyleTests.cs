#nullable enable
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.Constraints;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Parsing;

public sealed class NativeMetaDefaultStyleTests
{
    [TestCase("remove")]
    [TestCase("attributes")]
    [TestCase("adopt")]
    public async Task AnInsertionUsesItsOriginalDocumentAndContentBeforeDelivery(string transition)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var meta = Meta(document, "original");
            DomDocumentElements.Body(document)!.AppendChild(meta);
            switch (transition)
            {
                case "remove": meta.ParentNode!.RemoveChild(meta); break;
                case "attributes": meta.SetAttribute("content", "later"); meta.SetAttribute("http-equiv", "other"); break;
                case "adopt": new Document(DocumentKind.Html).AdoptNode(meta); break;
            }
            return true;
        });
        (await page.EvaluateAsync<string>("document.preferredStyleSheetSet")).Should().Be("original");
        page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AttachedShadowsParticipateAndTemplateContentsDoNot(bool declarative)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(declarative
            ? "<body><div id=host><template shadowrootmode=open></template></div></body>"
            : "<body><div id=host></div></body>");
        await page.EvaluateAsync("""
            const host = document.getElementById('host');
            const root = host.shadowRoot ?? host.attachShadow({mode:'open'});
            root.innerHTML = '<meta http-equiv="default-style" content="shadow">';
            const template = document.createElement('template');
            template.innerHTML = '<meta http-equiv="default-style" content="template">';
            document.body.append(template);
            """);
        (await page.EvaluateAsync<string>("document.preferredStyleSheetSet")).Should().Be("shadow");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ParserInsertionPrecedesTheNextScriptAndReinsertionUsesFreshContent()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <meta id=preference http-equiv=default-style content=first>
            <script>window.firstPreference=document.preferredStyleSheetSet;</script>
            <body></body>
            """);
        (await page.EvaluateAsync<string>("firstPreference")).Should().Be("first");
        (await page.EvaluateAsync<string>("""
            const element = preference;
            element.content = 'second';
            const unchanged = document.preferredStyleSheetSet;
            element.remove();
            document.body.append(element);
            unchanged + '|' + document.preferredStyleSheetSet;
            """)).Should().Be("first|second");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFailedNativePrefixKeepsItsOriginalExceptionAndRecoversOnANewRequest()
    {
        var probe = new Probe();
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        var original = new OperationCanceledException("native prefix cancellation");
        var failure = await Caught.ExceptionAsync(() => page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var body = DomDocumentElements.Body(document)!;
            var fragment = document.CreateDocumentFragment();
            var first = Meta(document, "committed");
            var second = Meta(document, "uncommitted");
            fragment.AppendChild(first); fragment.AppendChild(second);
            probe.Checking = () => { if (ReferenceEquals(first.ParentNode, body)) throw original; };
            try { body.AppendChild(fragment); }
            finally { probe.Checking = null; }
            return true;
        }));
        failure.Should().BeSameAs(original);
        (await page.EvaluateAsync<string>("document.preferredStyleSheetSet")).Should().Be("committed");
        (await page.EvaluateAsync<int>("document.querySelectorAll('meta').length")).Should().Be(1);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AThrowingPendingCallbackLeavesRecoveryIndexedForItsRetry()
    {
        var probe = new Probe();
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var body = DomDocumentElements.Body(document)!;
            using var subscription = document.ObserveMutations(body, new MutationObserverOptions { ChildList = true });
            var original = new OperationCanceledException("original prefix");
            var notification = new InvalidOperationException("notification sentinel");
            var calls = 0;
            var unwinding = true;
            subscription.PendingRecord = _ =>
            {
                unwinding.Should().BeFalse();
                calls++;
                throw notification;
            };
            var fragment = document.CreateDocumentFragment();
            var first = Meta(document, "retained");
            fragment.AppendChild(first); fragment.AppendChild(Meta(document, "uncommitted"));
            probe.Checking = () => { if (ReferenceEquals(first.ParentNode, body)) throw original; };
            try { Caught.Exception(() => body.AppendChild(fragment)).Should().BeSameAs(original); }
            finally { probe.Checking = null; unwinding = false; }
            calls.Should().Be(0);
            Caught.Exception(runtime.Parser!.RecoverNativeMutationNotifications).Should().BeSameAs(notification);
            calls.Should().Be(1);
            runtime.Parser.RecoverNativeMutationNotifications();
            calls.Should().Be(1);
            NativeCssStyleSheets.SetsOf(document).Preferred(new CssValueWork(default)).Should().Be("retained");
            return true;
        });
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InterruptedFactApplicationDoesNotReplayACommittedFact()
    {
        var probe = new Probe();
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var sets = NativeCssStyleSheets.SetsOf(document);
            var fragment = document.CreateDocumentFragment();
            fragment.AppendChild(Meta(document, "one")); fragment.AppendChild(Meta(document, "two"));
            DomDocumentElements.Body(document)!.AppendChild(fragment);
            var original = new OperationCanceledException("between facts");
            probe.Checking = () => { if (sets.Preferred(new CssValueWork(default)) == "one") throw original; };
            Caught.Exception(runtime.Parser!.RecoverNativeMutationNotifications).Should().BeSameAs(original);
            probe.Checking = null;
            sets.SetDefaultStyle("between", new CssValueWork(default));
            probe.Checking = () => { if (sets.Preferred(new CssValueWork(default)) == "one") throw new InvalidOperationException("committed fact replayed"); };
            try { runtime.Parser.RecoverNativeMutationNotifications(); }
            finally { probe.Checking = null; }
            sets.Preferred(new CssValueWork(default)).Should().Be("two");
            return true;
        });
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnIdleRequestBoundsItsRecoveryAndRetriesThePendingFactsAfterFailure()
    {
        var clock = new RecoveryClock();
        var probe = new Probe();
        var options = new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(1) }
            .ConfigureEngine(engine => engine.RemoveConstraints(static constraint => constraint is OperationDeadlineConstraint)
                .AddConstraint(probe).AddConstraint(() => new OperationDeadlineConstraint(clock)));
        await using var browser = new global::Jint.Browser.Browser(options);
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var original = new OperationCanceledException("failed insertion before idle request");
        var failed = page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var body = DomDocumentElements.Body(document)!;
            var fragment = document.CreateDocumentFragment();
            var first = Meta(document, "idle-recovered");
            fragment.AppendChild(first); fragment.AppendChild(Meta(document, "uncommitted"));
            probe.Checking = () => { if (ReferenceEquals(first.ParentNode, body)) throw original; };
            try { body.AppendChild(fragment); }
            catch
            {
                probe.Checking = () => clock.Advance(TimeSpan.FromSeconds(2));
                ready.TrySetResult(true);
                release.Wait(TestBudgets.WedgeCeiling).Should().BeTrue();
                throw;
            }
            return true;
        });
        try
        {
            await ready.Task.WaitAsync(TestBudgets.WedgeCeiling);
            // Queue while the failed entry is still held, so this request precedes the next task pump.
            var idle = page.WaitForIdleAsync(TestBudgets.WedgeCeiling);
            release.Set();
            (await Caught.ExceptionAsync(() => failed)).Should().BeSameAs(original);
            (await Caught.ExceptionAsync(() => idle)).Should().BeOfType<TimeoutException>();
        }
        finally { release.Set(); probe.Checking = null; }
        (await page.WaitForIdleAsync(TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.preferredStyleSheetSet")).Should().Be("idle-recovered");
    }

    private static Element Meta(Document document, string content)
    {
        var element = document.CreateElement("meta");
        element.SetAttribute("http-equiv", "default-style");
        element.SetAttribute("content", content);
        return element;
    }

    private sealed class Probe : Constraint
    {
        internal Action? Checking;
        public override void Check() => Checking?.Invoke();
        public override void Reset() { }
    }

    private sealed class RecoveryClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);
        internal void Advance(TimeSpan amount) => Interlocked.Add(ref _timestamp, amount.Ticks);
    }
}
