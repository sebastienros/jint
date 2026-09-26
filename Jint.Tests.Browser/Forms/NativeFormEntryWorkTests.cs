using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Forms;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeFormEntryWorkTests
{
    [Test]
    public async Task EntryConstructionChecksTheEngineBudgetDuringColdOptionTextReads()
    {
        var constraint = new ReadProbe();
        await using var browser = new Browser(new BrowserOptions().ConfigureEngine(options => options.AddConstraint(constraint)));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f'><select name='choice'><option>" + new string('a', 131072) + "</option></select></form>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var form = DomDocumentReads.ById(runtime.Dom, document, "f")!;
            var nativeWork = document.SelectWorkProbe = new HtmlSelectWorkProbe();
            constraint.Remaining = 32;
            Assert.Throws<OperationCanceledException>(() => FormSubmitter.ConstructEntryList(runtime, form, null));
            nativeWork.Units.Should().BeGreaterThan(256);
            nativeWork.Units.Should().BeLessThan(131072);
            runtime.SubmittingForms.Should().NotContain(form);
            constraint.Remaining = 0;
            FormSubmitter.ConstructEntryList(runtime, form, null)!.Should().ContainSingle();
            return true;
        });
    }

    private sealed class ReadProbe : Constraint
    {
        internal int Remaining;
        public override void Check()
        {
            if (Remaining > 0 && --Remaining == 0) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }
}
