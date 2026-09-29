using System.Text;
using System.Reflection;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Browser.Runtime.Parsing;
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public sealed class NativeCssImportLoadingTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task ACommittedCssomEditSurvivesACheckpointInterruptionWithoutReplayingTheMutation(bool duringPreparation, bool cancellation)
    {
        var probe = new ImportInterruptionProbe();
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<style id=s>p{display:block}</style>")
            .Map("/child.css", _ => LoopbackResponse.Css("p{color:red}")),
            configureBrowser: options => options.ConfigureEngine(engineOptions => engineOptions.AddConstraint(probe)));
        var observer = new ImportRuntimeObserver();
        fixture.Page.Observe(observer);
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.RunOnLoopAsync(engine =>
        {
            var owner = DomBindings.Bind<Element>(engine.GetValue("s"), "durable import notification").Target;
            var source = NativeCssStyleSheets.CaptureImportSource(owner.OwnerDocument!, owner, new CssValueWork(default))!;
            var sheet = NativeCssStyleSheets.EnsureSheet(source, new CssValueWork(default));
            NativeCssParsing.ReadRules(sheet.Rules, new CssValueWork(default));
            // Commit the real producer once, then interrupt at the notification boundary.
            sheet.InsertRule("@import '/child.css';", 0);
            var import = (CssImportRule) sheet.Rules[0];
            engine.SetValue("committedImport", DomRealm.Of(engine).Wrap(import));
            var driver = observer.Runtime!.Parser!;
            var failure = cancellation ? (Exception) new OperationCanceledException(ImportInterruptionProbe.Marker)
                : new InvalidOperationException(ImportInterruptionProbe.Marker);
            probe.Failure = failure;
            probe.Armed = true;
            if (duringPreparation)
            {
                driver.QueueCssImports(sheet);
                var notifications = (System.Collections.IDictionary) typeof(ParserDriver)
                    .GetField("_cssImportNotifications", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(driver)!;
                var notification = notifications[sheet]!;
                var running = notification.GetType().GetField("Running", BindingFlags.Instance | BindingFlags.NonPublic)!;
                // Ignore task-start recovery checks. Fail only after the actual queued
                // RunCssImportNotification enters its charged preparation path.
                probe.When = () => (bool) running.GetValue(notification)!;
                Caught.Exception(engine.Tasks.ProcessTask).Should().BeSameAs(failure);
                notifications.Count.Should().Be(1, "preparation failure must retain its durable owner");
                driver.HasPendingNativeRecovery.Should().BeTrue();
            }
            else
            {
                Caught.Exception(() =>
                {
                    driver.QueueCssImports(sheet);
                    engine.Constraints.Check();
                }).Should().BeSameAs(failure);
            }
            import.StyleSheet.Should().BeNull();
            var pending = typeof(ParserDriver).GetField("_pendingResourceEvents", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((int) pending.GetValue(driver)!).Should().BeGreaterThan(0);
            return true;
        });
        (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<bool>("s.sheet.cssRules[0]===committedImport && committedImport.styleSheet!==null")).Should().BeTrue();
        fixture.Server.Received.Count(request => request.Path == "/child.css").Should().Be(1);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InlineTextRoundTripsAtArrivalRetireAGraphBeforeDeferredResourceDelivery()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        var held = 0;
        const string body = "p{color:red}";
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<style id=s>p{display:block}</style>")
            .Map("/child.css", _ => LoopbackResponse.Css("@import '/pending.css';"))
            .Map("/pending.css", _ =>
            {
                if (Interlocked.Increment(ref requests) != 1) return LoopbackResponse.Css(body);
                Volatile.Write(ref held, 1);
                return new LoopbackResponse
                {
                    Body = body,
                    WriteBodyAsync = async (stream, token) =>
                    {
                        await release.Task.WaitAsync(token);
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(body), token);
                    }
                }.With("Content-Type", "text/css");
            }));
        var observer = new ImportRuntimeObserver();
        fixture.Page.Observe(observer);
        try
        {
            await fixture.Page.NavigateAsync(fixture.Url("/"));
            await fixture.Page.RunOnLoopAsync(engine =>
            {
                var driver = observer.Runtime!.Parser!;
                var owner = DomBindings.Bind<Element>(engine.GetValue("s"), "inline import arrival witness").Target;
                var work = new CssValueWork(default, engine.Constraints.Check);
                const string original = "@import '/child.css';";
                NativeCssStyleSheets.Install(owner.OwnerDocument!, owner, original, fixture.Url("/"), fixture.Url("/"), work);
                var source = NativeCssStyleSheets.CaptureImportSource(owner.OwnerDocument!, owner, work)!;
                engine.SetValue("importText", original);
                var draining = typeof(ParserDriver).GetField("_drainingResourceRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var previous = (bool) draining.GetValue(driver)!;
                draining.SetValue(driver, true);
                void MutateWhenHeld()
                {
                    if (Volatile.Read(ref held) == 0) { engine.Tasks.Post(MutateWhenHeld); return; }
                    try
                    {
                        engine.Execute("window.oldPending=s.sheet.cssRules[0].styleSheet.cssRules[0]; s.textContent='p{color:blue}'; s.textContent=importText;");
                    }
                    finally { release.TrySetResult(); }
                }
                engine.Tasks.Post(MutateWhenHeld);
                try
                {
                    var load = typeof(ParserDriver).GetMethod("LoadCssImports", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    Func<bool> current = () => true;
                    load.Invoke(driver, [owner, current, true, null, current])!.ToString().Should().Be("Stale");
                    NativeCssStyleSheets.IsCurrent(source).Should().BeFalse();
                    engine.Evaluate("oldPending.styleSheet===null").AsBoolean().Should().BeTrue();
                }
                finally
                {
                    draining.SetValue(driver, previous);
                    typeof(ParserDriver).GetMethod("DrainResourceRecords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(driver, null);
                }
                return true;
            }).WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
            fixture.Page.Errors.Should().BeEmpty();
        }
        finally { release.TrySetResult(); }
    }

    [TestCase(8, 64)]
    [TestCase(4097, 64)]
    [TestCase(8, 8192)]
    [TestCase(4097, 8192)]
    public async Task OwnerEligibilityProofsDoNotMultiplyCssParsingByOwnerDepth(int depth, int rules)
    {
        var css = string.Concat(Enumerable.Repeat("p{color:red}", rules));
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<style id=s>p{display:block}</style>")
            .Map("/child.css", _ => LoopbackResponse.Css(css)));
        var observer = new ImportRuntimeObserver();
        fixture.Page.Observe(observer);
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.RunOnLoopAsync(engine =>
        {
            var owner = DomBindings.Bind<Element>(engine.GetValue("s"), "import work witness").Target;
            var document = owner.OwnerDocument!;
            var parent = document.DocumentElement!;
            for (var i = 0; i < depth; i++)
            {
                var child = document.CreateElement("div");
                parent.AppendChild(child);
                parent = child;
            }
            parent.AppendChild(owner);
            var work = new CssValueWork(default, engine.Constraints.Check);
            NativeCssStyleSheets.Install(document, owner, "@import '/child.css';", fixture.Url("/"), fixture.Url("/"), work);
            var source = NativeCssStyleSheets.CaptureImportSource(document, owner, work)!;
            var proofs = 0;
            Func<bool> eligibility = () => { proofs++; return true; };
            Func<bool> identity = () => NativeCssStyleSheets.IsCurrent(source);
            // Exercise the private loader with a countable eligibility predicate, keeping its
            // production visibility. Real fetch, parser, registry and connectivity proof are used.
            var load = typeof(ParserDriver).GetMethod("LoadCssImports", BindingFlags.Instance | BindingFlags.NonPublic)!;
            load.Invoke(observer.Runtime!.Parser!, [owner, eligibility, false, null, identity])!.ToString().Should().Be("Completed");
            proofs.Should().BeLessThanOrEqualTo(12, "eligibility is checked at graph boundaries, never per CSS parser checkpoint");
            return true;
        });
    }

    [Test]
    public async Task ARedirectedCssOriginDoesNotBecomeTheOwnersCredentialOrigin()
    {
        using var other = new LoopbackServer();
        other.Map("/root.css", _ => LoopbackResponse.Css("@import 'child.css';"));
        other.Map("/child.css", _ => LoopbackResponse.Css("p{color:red}"));
        LoopbackServer ownerServer = null!;
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
            ownerServer = server;
            server.Map("/page", _ => LoopbackResponse.Html("<link rel=stylesheet href=/entry.css><p>text</p>")
                .With("Set-Cookie", "owner=present; Path=/"));
            server.Map("/entry.css", _ => LoopbackResponse.Redirect(302, other.Url("/root.css")));
        }, configureContext: options => options.UrlFilter = uri => ownerServer.Owns(uri) || other.Owns(uri));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        fixture.Server.Received.Single(request => request.Path == "/entry.css").Header("Cookie").Should().Be("owner=present");
        other.Received.Should().HaveCount(2).And.OnlyContain(request => request.Header("Cookie") == null);
        other.Received.Single(request => request.Path == "/child.css").Header("Referer").Should().Be(other.Url("/root.css"));
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ResponseEncodingIsInheritedByImportsAndRetainedForLaterCssomScans()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<link id=s rel=stylesheet href=/root.css><p id=café>text</p>")
            .Map("/root.css", _ => LoopbackResponse.Raw(Encoding.Latin1.GetBytes("@import 'child.css';"), "text/css; charset=iso-8859-1"))
            .Map("/child.css", _ => LoopbackResponse.Raw(Encoding.Latin1.GetBytes("#café{color:red}"), "text/css"))
            .Map("/late.css", _ => LoopbackResponse.Raw(Encoding.Latin1.GetBytes("#café{display:none}"), "text/css")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('café')).color")).Should().Be("red");
        await fixture.Page.EvaluateAsync("s.sheet.cssRules[0].styleSheet.insertRule(\"@import 'late.css';\",0)");
        (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('café')).display")).Should().Be("none");
        fixture.Server.Received.Count(request => request.Path == "/child.css").Should().Be(1);
        fixture.Page.Errors.Should().BeEmpty();
    }

    // CSS Syntax §3.2: BOM, then transport charset, then @charset (utf-16 read as utf-8), then the
    // environment encoding, which an import takes from its parent's selected encoding.
    [Test]
    public async Task StyleSheetBytesAreDecodedWithBomTransportCharsetDeclarationAndEnvironmentEncoding()
    {
        static byte[] Bytes(params byte[][] parts) => parts.SelectMany(part => part).ToArray();
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<meta charset=utf-8><link rel=stylesheet href=/declared.css><link rel=stylesheet href=/bom.css>" +
                "<link rel=stylesheet href=/utf16.css><p id=café>a</p><p id=naïve>b</p><p id=señor>c</p><p id=über>d</p>")
            .Map("/declared.css", _ => LoopbackResponse.Raw(
                Encoding.Latin1.GetBytes("@charset \"windows-1252\";@import 'child.css';#café{color:red}"), "text/css"))
            .Map("/child.css", _ => LoopbackResponse.Raw(Encoding.Latin1.GetBytes("#naïve{color:red}"), "text/css"))
            .Map("/bom.css", _ => LoopbackResponse.Raw(
                Bytes([0xEF, 0xBB, 0xBF], Encoding.UTF8.GetBytes("#señor{color:red}")), "text/css; charset=iso-8859-1"))
            .Map("/utf16.css", _ => LoopbackResponse.Raw(
                Encoding.UTF8.GetBytes("@charset \"utf-16\";#über{color:red}"), "text/css")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        foreach (var id in new[] { "café", "naïve", "señor", "über" })
            (await fixture.Page.EvaluateAsync<string>($"getComputedStyle(document.getElementById('{id}')).color"))
                .Should().Be("red", id);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnOpaqueFrameImportCannotBorrowTheTopDocumentsCredentials()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/page", _ => LoopbackResponse.Html("""
                <iframe sandbox=allow-scripts srcdoc="<style>@import '/child.css';</style>"></iframe>
                """).With("Set-Cookie", "owner=present; Path=/"))
            .Map("/child.css", _ => LoopbackResponse.Css("p{color:red}")));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        fixture.Server.Received.Single(request => request.Path == "/child.css").Header("Cookie").Should().BeNull();
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RedirectedImportsUseTheFinalParentUrlAndEachSiblingOwnsItsChild()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", """
                <script>window.events=[]; window.onload=()=>events.push('window');</script>
                <link id=s rel=stylesheet href=/entry.css onload="events.push('owner')">
                <p id=p>text</p>
                """)
            .Map("/entry.css", _ => LoopbackResponse.Redirect(302, "/css/root.css"))
            .Map("/css/root.css", _ => LoopbackResponse.Css("@import 'alias.css'; @import 'alias.css'; p{color:blue}"))
            .Map("/css/alias.css", _ => LoopbackResponse.Redirect(302, "/deep/child.css"))
            .Map("/deep/child.css", _ => LoopbackResponse.Css("@import 'leaf.css'; p{color:red}"))
            .Map("/deep/leaf.css", _ => LoopbackResponse.Css("p{display:block}")));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAsync<bool>("""
            (()=>{
                const root=s.sheet, a=root.cssRules[0], b=root.cssRules[1];
                const x=a.styleSheet, y=b.styleSheet;
                return root===document.styleSheets[0] && x!==y && x.ownerRule===a && y.ownerRule===b &&
                    x.parentStyleSheet===root && x.ownerNode===null &&
                    x.cssRules[0].styleSheet!==y.cssRules[0].styleSheet &&
                    x.href.endsWith('/deep/child.css') && x.cssRules[0].styleSheet.href.endsWith('/deep/leaf.css');
            })()
            """)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("events.join(',')")).Should().Be("owner,window");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(p).color")).Should().Be("blue");
        fixture.Server.Received.Count(request => request.Path == "/css/alias.css").Should().Be(2);
        fixture.Server.Received.Where(request => request.Path == "/deep/leaf.css").Should().HaveCount(2)
            .And.OnlyContain(request => request.Header("Referer") == fixture.Url("/deep/child.css"));
        fixture.Server.Received.Where(request => request.Path == "/css/alias.css").Should()
            .OnlyContain(request => request.Header("Referer") == fixture.Url("/css/root.css"));
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InlineImportsResolveAgainstTheInstalledBaseAndNonmatchingMediaStillLoads()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/page", """
                <base href=/assets/>
                <style id=s>@import 'child.css' print;</style><p id=p>text</p>
                """)
            .Map("/assets/child.css", _ => LoopbackResponse.Css("p{color:red}")));
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        var request = fixture.Server.Received.Single(item => item.Path == "/assets/child.css");
        request.Header("Referer").Should().Be(fixture.Url("/page"));
        (await fixture.Page.EvaluateAsync<bool>("s.sheet.cssRules[0].styleSheet !== null")).Should().BeTrue();
        await fixture.Page.EvaluateAsync("s.sheet.cssRules[0].media.mediaText='all'");
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(p).color")).Should().Be("red");
        fixture.Server.Received.Count(item => item.Path == "/assets/child.css").Should().Be(1);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RequestedAndRedirectedAncestryCyclesTerminateWithoutDeduplicatingSiblings()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<link id=s rel=stylesheet href=/root.css><p id=p>text</p>")
            .Map("/root.css", _ => LoopbackResponse.Css("@import 'root.css#direct'; @import 'back.css'; @import 'child.css'; @import 'child.css';"))
            .Map("/back.css", _ => LoopbackResponse.Redirect(302, "/root.css#redirect"))
            .Map("/child.css", _ => LoopbackResponse.Css("@import 'root.css'; p{color:red}")));
        await fixture.Page.NavigateAsync(fixture.Url("/")).WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
        (await fixture.Page.EvaluateAsync<bool>("""
            s.sheet.cssRules[0].styleSheet===null && s.sheet.cssRules[1].styleSheet===null &&
            s.sheet.cssRules[2].styleSheet!==s.sheet.cssRules[3].styleSheet &&
            s.sheet.cssRules[2].styleSheet.cssRules[0].styleSheet===null
            """)).Should().BeTrue();
        fixture.Server.Received.Count(item => item.Path == "/root.css").Should().Be(2);
        fixture.Server.Received.Count(item => item.Path == "/child.css").Should().Be(2);
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(p).color")).Should().Be("red");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task FailedChildrenReportOneOwnerErrorAndLeaveSuccessfulSiblingsUsableBeforeWindowLoad()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <script>window.events=[]; window.onload=()=>events.push('window');</script>
                <link id=s rel=stylesheet href=/root.css onload="events.push('load')" onerror="events.push('error')">
                <p id=p>text</p>
                """)
            .Map("/root.css", _ => LoopbackResponse.Css("@import 'missing.css'; @import 'child.css';"))
            .Map("/child.css", _ => LoopbackResponse.Css("p{color:red}")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("events.join(',')")).Should().Be("error,window");
        (await fixture.Page.EvaluateAsync<bool>("s.sheet.cssRules[0].styleSheet===null && s.sheet.cssRules[1].styleSheet!==null")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(p).color")).Should().Be("red");
        fixture.Page.Errors.Should().ContainSingle(error => error.Message.Contains("missing.css", StringComparison.Ordinal));
    }

    [Test]
    public async Task CssomTopologyLoadsAfterMicrotasksWithoutRefiringOwnerEventsAndKeepsHistoricalWrappers()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <script>window.loads=0;</script>
                <link id=s rel=stylesheet href=/root.css onload="loads++"><p id=p>text</p>
                """)
            .Map("/root.css", _ => LoopbackResponse.Css("p{display:block}"))
            .Map("/child.css", _ => LoopbackResponse.Css("p{color:red}"))
            .Map("/deleted.css", _ => LoopbackResponse.Css("p{color:blue}")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.EvaluateAsync("""
            window.events=[];
            s.sheet.insertRule("@import 'child.css';",0);
            window.imported=s.sheet.cssRules[0];
            events.push(imported.styleSheet===null?'script':'early');
            queueMicrotask(()=>events.push(imported.styleSheet===null?'microtask':'early'));
            s.sheet.insertRule("@import 'deleted.css';",1);
            s.sheet.deleteRule(1);
            """);
        (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("events.join(',')")).Should().Be("script,microtask");
        (await fixture.Page.EvaluateAsync<bool>("imported.styleSheet!==null && loads===1")).Should().BeTrue();
        await fixture.Page.EvaluateAsync("window.historical=imported.styleSheet; s.sheet.deleteRule(0)");
        (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<bool>("imported.parentStyleSheet===null && imported.styleSheet===historical && historical.ownerRule===imported")).Should().BeTrue();
        fixture.Server.Received.Count(item => item.Path == "/child.css").Should().Be(1);
        fixture.Server.Received.Should().NotContain(item => item.Path == "/deleted.css");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("remove")]
    [TestCase("adopt")]
    [TestCase("replace")]
    [TestCase("delete-ancestor")]
    [TestCase("unrelated")]
    [TestCase("round-trip")]
    public async Task DescendantPublicationFollowsSourceAndOwnershipIdentityDuringAGatedFetch(string change)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = 0;
        var pendingRequests = 0;
        const string body = "p{color:red}";
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <script>
                window.loads=0; window.errors=0;
                function changeGraph() {
                    if (!childRequested()) {setTimeout(changeGraph,0); return;}
                    window.root=s.sheet; window.child=root.cssRules[0].styleSheet;
                    window.pending=child.cssRules[0];
                    try { CHANGE } finally {releaseCssBody();}
                }
                setTimeout(changeGraph,0);
                </script>
                <link id=s rel=stylesheet href=/root.css onload="loads++" onerror="errors++">
                """.Replace("CHANGE", change switch
                {
                    "remove" => "s.remove();",
                    "adopt" => "document.implementation.createHTMLDocument().head.appendChild(s);",
                    "replace" => "s.href='/replacement.css';",
                    "round-trip" => "s.href='/replacement.css'; s.href='/root.css';",
                    "unrelated" => "child.cssRules[1].style.color='blue'; pending.media.mediaText='all'; document.head.appendChild(document.createElement('meta'));",
                    _ => "root.deleteRule(0);"
                }, StringComparison.Ordinal))
            .Map("/root.css", _ => LoopbackResponse.Css("@import 'child.css';"))
            .Map("/child.css", _ => LoopbackResponse.Css("@import 'pending.css'; p{display:block}"))
            .Map("/replacement.css", _ => LoopbackResponse.Css("p{color:blue}"))
            .Map("/pending.css", _ =>
            {
                // A replacement graph can start while the old graph's fetch pumps. Only its old
                // response is gated: a current replacement must be able to finish independently.
                if (Interlocked.Increment(ref pendingRequests) != 1) return LoopbackResponse.Css(body);
                Volatile.Write(ref requested, 1);
                return new LoopbackResponse
                {
                    Body = body,
                    WriteBodyAsync = async (stream, token) =>
                    {
                        await release.Task.WaitAsync(token);
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(body), token);
                    }
                }.With("Content-Type", "text/css");
            }));
        fixture.Page.Observe(new ImportGateObserver(release, () => Volatile.Read(ref requested) != 0));
        try
        {
            await fixture.Page.NavigateAsync(fixture.Url("/")).WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
            (await fixture.Page.EvaluateAsync<bool>(change == "unrelated"
                ? "pending.styleSheet!==null && errors===0" : "pending.styleSheet===null && errors===0")).Should().BeTrue();
            (await fixture.Page.EvaluateAsync<int>("loads")).Should().Be(change is "replace" or "unrelated" or "round-trip" ? 1 : 0);
            if (change == "round-trip")
                (await fixture.Page.EvaluateAsync<bool>("s.sheet!==root && s.sheet.cssRules[0].styleSheet.cssRules[0].styleSheet!==null")).Should().BeTrue();
            fixture.Page.Errors.Should().BeEmpty();
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    public async Task DeepAndWideGraphsRetainSourceOrderWithoutRecursiveLoading()
    {
        const int depth = 96;
        const int width = 24;
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
            server.MapHtml("/", "<link id=s rel=stylesheet href=/root.css><p id=p>text</p>");
            server.Map("/root.css", _ => LoopbackResponse.Css("@import 'deep0.css';" +
                string.Concat(Enumerable.Range(0, width).Select(i => "@import 'wide" + i + ".css';"))));
            for (var i = 0; i < depth; i++)
            {
                var index = i;
                server.Map("/deep" + index + ".css", _ => LoopbackResponse.Css(index == depth - 1
                    ? "p{color:red}" : "@import 'deep" + (index + 1) + ".css';"));
            }
            for (var i = 0; i < width; i++)
                server.Map("/wide" + i + ".css", _ => LoopbackResponse.Css("p{color:blue}"));
        });
        await fixture.Page.NavigateAsync(fixture.Url("/")).WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
        (await fixture.Page.EvaluateAsync<int>("(()=>{let n=0,x=s.sheet.cssRules[0].styleSheet; while(x.cssRules[0] instanceof CSSImportRule){x=x.cssRules[0].styleSheet;n++} return n+1})()"))
            .Should().Be(depth);
        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(p).color")).Should().Be("blue");
        fixture.Server.Received.Should().HaveCount(depth + width + 2);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ClosingDuringAQueuedImportFetchReleasesTheTaskWithoutAnOwnerEvent()
    {
        var events = 0;
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const string body = "p{color:red}";
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<style id=s>p{display:block}</style>")
            .Map("/pending.css", _ => new LoopbackResponse
            {
                Body = body,
                WriteBodyAsync = async (stream, token) =>
                {
                    requested.TrySetResult();
                    await release.Task.WaitAsync(token);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(body), token);
                }
            }.With("Content-Type", "text/css")),
            configureBrowser: options => options.ConfigureEngine(engineOptions =>
                engineOptions.Configure(engine => engine.SetValue("recordCssEvent", (Action) (() => Interlocked.Increment(ref events))))));
        try
        {
            await fixture.Page.NavigateAsync(fixture.Url("/"));
            await fixture.Page.EvaluateAsync("s.onload=recordCssEvent; s.onerror=recordCssEvent; s.sheet.insertRule(\"@import 'pending.css';\",0)");
            await requested.Task.WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            await fixture.Page.CloseAsync().WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            Volatile.Read(ref events).Should().Be(0);
        }
        finally { release.TrySetResult(); }
    }

    private sealed class ImportGateObserver(TaskCompletionSource release, Func<bool> requested) : IPageObserver
    {
        public void DocumentCreated(PageRuntime runtime, string loaderId)
        {
            runtime.Engine.SetValue("childRequested", requested);
            runtime.Engine.SetValue("releaseCssBody", (Action) (() => release.TrySetResult()));
        }
    }

    private sealed class ImportRuntimeObserver : IPageObserver
    {
        internal PageRuntime? Runtime;
        public void DocumentCreated(PageRuntime runtime, string loaderId) => Runtime = runtime;
    }

    private sealed class ImportInterruptionProbe : Constraint
    {
        internal const string Marker = "CSS import notification checkpoint interruption";
        internal bool Armed;
        internal Exception Failure = new InvalidOperationException(Marker);
        internal Func<bool>? When;
        public override void Check()
        {
            if (!Armed || !(When?.Invoke() ?? true)) return;
            Armed = false;
            throw Failure;
        }
        public override void Reset() { }
    }
}
