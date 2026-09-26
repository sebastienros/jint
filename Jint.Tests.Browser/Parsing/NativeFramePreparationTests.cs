using System.Collections;
using System.Reflection;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Constraints;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Parsing;

public sealed class NativeFramePreparationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task APreparationFailureRetainsTheUnstartedFrameForHealthyRecovery(bool cancelled)
    {
        var probe = new Probe();
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        Exception original = cancelled ? new OperationCanceledException("frame preparation") : new TimeoutException("frame preparation");
        var retained = false;
        var failure = await Caught.ExceptionAsync(() => page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var frame = document.CreateElement("iframe");
            DomDocumentElements.Body(document)!.AppendChild(frame);
            probe.Checking = () =>
            {
                if (!IsPreparing(runtime.Parser)) return;
                probe.Checking = null;
                throw original;
            };
            try { runtime.Parser.RecoverNativeMutationNotifications(); }
            catch
            {
                Pending(runtime.Parser).Cast<object>().Should().ContainSingle();
                DomBrowsingContext.OfFrame(frame).Should().BeNull();
                retained = true;
                throw;
            }
            return true;
        }));
        probe.Checking = null;
        failure.Should().BeSameAs(original);
        retained.Should().BeTrue();
        (await page.WaitForIdleAsync(TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<bool>("frames.length === 1 && frames[0].document.body !== null")).Should().BeTrue();
    }

    [Test]
    public async Task ReentrantRecoveryLeavesThePreparingFrontAndBothLaterNavigationsIntact()
    {
        var probe = new Probe();
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { MaxFrameDocuments = 2 }
            .ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        var reentered = false;
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var body = DomDocumentElements.Body(document)!;
            body.AppendChild(document.CreateElement("iframe"));
            body.AppendChild(document.CreateElement("iframe"));
            probe.Checking = () =>
            {
                if (!IsPreparing(runtime.Parser)) return;
                probe.Checking = null;
                runtime.Parser.RecoverNativeMutationNotifications();
                reentered = true;
            };
            runtime.Parser.RecoverNativeMutationNotifications();
            return true;
        });
        reentered.Should().BeTrue();
        (await page.EvaluateAsync<bool>("frames.length === 2 && frames[0] !== frames[1] && frames[0].document.body !== null && frames[1].document.body !== null"))
            .Should().BeTrue();
        page.Requests.Should().BeEmpty();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AdoptionDuringPreparationDoesNotCreateANavigableInAnInertDocument()
    {
        var probe = new Probe();
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(probe)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<body></body>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var frame = document.CreateElement("iframe");
            DomDocumentElements.Body(document)!.AppendChild(frame);
            var inert = new Document(DocumentKind.Html);
            var root = inert.CreateElement("html");
            inert.AppendChild(root);
            var adopted = false;
            probe.Checking = () =>
            {
                if (!IsPreparing(runtime.Parser)) return;
                probe.Checking = null;
                inert.AdoptNode(frame);
                root.AppendChild(frame);
                adopted = true;
            };
            runtime.Parser.RecoverNativeMutationNotifications();
            adopted.Should().BeTrue();
            frame.OwnerDocument.Should().BeSameAs(inert);
            DomBrowsingContext.OfFrame(frame).Should().BeNull();
            return true;
        });
        (await page.EvaluateAsync<int>("frames.length")).Should().Be(0);
        page.Requests.Should().BeEmpty();
        page.Errors.Should().BeEmpty();
    }

    // Observe the private preparation boundary without adding a production callback or host API.
    private static IEnumerable Pending(object parser)
        => (IEnumerable) parser.GetType().GetField("_pendingFrameDocuments", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(parser)!;

    private static bool IsPreparing(object parser)
        => Pending(parser).Cast<object>().Any(pending => (bool) pending.GetType()
            .GetField("Preparing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pending)!);

    private sealed class Probe : Constraint
    {
        internal Action? Checking;
        public override void Check() => Checking?.Invoke();
        public override void Reset() { }
    }
}
