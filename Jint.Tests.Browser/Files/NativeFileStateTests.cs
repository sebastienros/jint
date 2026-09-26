using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;

namespace Jint.Tests.Browser.Files;

[NonParallelizable]
public sealed class NativeFileStateTests
{
    [Test]
    public void FileKeywordsAndMultipleIgnoreForeignNamespaceAttributes()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.SetAttributeNS("urn:test", "type", "file");
        FileSelection.IsFileInput(input).Should().BeFalse();
        input.SetAttributeNS(null, "type", "file");
        FileSelection.IsFileInput(input).Should().BeTrue();
        input.SetAttributeNS("urn:test", "multiple", "");
        FileSelection.Allowed(input, new[] { 1, 2 }).Should().Equal(1);
        input.SetAttributeNS(null, "multiple", "");
        FileSelection.Allowed(input, new[] { 1, 2 }).Should().Equal(1, 2);
    }

    [Test]
    public async Task SharedListChangesInvalidateOnlyItsCurrentlyAssignedInputs()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input type=file id=upload>");
        await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            engine.Execute("var transfer = new DataTransfer(); document.getElementById('upload').files = transfer.files;");
            var assigned = document.MutationStamp;
            var assignedRevision = BrowserSelectorSemanticRevision.Read(document);
            engine.Execute("transfer.items.add(new File(['x'], 'x.txt'));");
            document.MutationStamp.Should().BeGreaterThan(assigned);
            BrowserSelectorSemanticRevision.Read(document).Should().BeGreaterThan(assignedRevision);
            engine.Execute("document.getElementById('upload').files = new DataTransfer().files;");
            var reassigned = document.MutationStamp;
            var reassignedRevision = BrowserSelectorSemanticRevision.Read(document);
            engine.Execute("transfer.items.clear();");
            document.MutationStamp.Should().Be(reassigned);
            BrowserSelectorSemanticRevision.Read(document).Should().Be(reassignedRevision);
            return true;
        });
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ClearingAnInputDetachesTheSharedListAndEngineDisposalReleasesNotifications()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input type=file id=upload>");
        var retained = await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            engine.Execute("var transfer = new DataTransfer(); transfer.items.add(new File(['x'], 'x.txt')); document.getElementById('upload').files = transfer.files;");
            engine.Execute("document.getElementById('upload').value = '';");
            var cleared = document.MutationStamp;
            var clearedRevision = BrowserSelectorSemanticRevision.Read(document);
            engine.Execute("transfer.items.add(new File(['y'], 'y.txt'));");
            document.MutationStamp.Should().Be(cleared);
            BrowserSelectorSemanticRevision.Read(document).Should().Be(clearedRevision);

            var realm = FileTransferRealm.Of(engine);
            var input = document.CreateElement("input");
            input.SetAttributeNS(null, "type", "file");
            var list = realm.NewFileList();
            realm.SetInputFiles(input, list);
            var file = new Jint.WebApi.Files.JsFile(engine, new byte[] { 1 }, "text/plain", "z.txt", 0);
            return (Document: document, Files: list, File: file);
        });
        page.Errors.Should().BeEmpty();
        await page.CloseAsync();
        var disposed = retained.Document.MutationStamp;
        var disposedRevision = BrowserSelectorSemanticRevision.Read(retained.Document);
        retained.Files.Add(retained.File);
        retained.Document.MutationStamp.Should().Be(disposed);
        BrowserSelectorSemanticRevision.Read(retained.Document).Should().Be(disposedRevision);
    }

    [Test]
    public void ASharedListReleasesCollectedInputsInsteadOfAccumulatingChangeHandlers()
    {
        using var fixture = DomTestFixture.Create("");
        var realm = FileTransferRealm.Of(fixture.Engine);
        var list = realm.NewFileList();
        var references = Enumerable.Range(0, 256).Select(_ => AttachTransientInput(fixture.Document, realm, list)).ToArray();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        references.All(reference => !reference.TryGetTarget(out _)).Should().BeTrue();

        list.Add(new Jint.WebApi.Files.JsFile(fixture.Engine, new byte[] { 1 }, "text/plain", "x.txt", 0));
        typeof(JsFileList).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(list).Should().BeNull();
        GC.KeepAlive(fixture);
        GC.KeepAlive(list);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Element> AttachTransientInput(Document document, FileTransferRealm realm, JsFileList list)
    {
        var input = document.CreateElement("input");
        input.SetAttribute("type", "file");
        realm.SetInputFiles(input, list);
        return new WeakReference<Element>(input);
    }

    [Test]
    public void AnInterruptedFileStateCompactionRetainsEachLiveOwnerOnceAcrossRetry()
    {
        using var fixture = DomTestFixture.Create("");
        var realm = FileTransferRealm.Of(fixture.Engine);
        var list = realm.NewFileList();
        var live = PopulateCompactionOwners(fixture.Document, realm, list);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var checks = 0;
        Assert.Throws<OperationCanceledException>(() => realm.CompactFileStates(() =>
        {
            if (++checks == 2) throw new OperationCanceledException();
        }));
        var states = (ICollection) typeof(FileTransferRealm).GetField("_fileStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(realm)!;
        states.Count.Should().Be(live.Length);
        realm.CompactFileStates(() => { });
        states.Count.Should().Be(live.Length);
        GC.KeepAlive(live);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Element[] PopulateCompactionOwners(Document document, FileTransferRealm realm, JsFileList list)
    {
        var transient = new Element[64];
        var live = new Element[384];
        for (var i = 0; i < transient.Length + live.Length; i++)
        {
            var input = document.CreateElement("input");
            input.SetAttribute("type", "file");
            realm.SetInputFiles(input, list);
            if (i < transient.Length) transient[i] = input;
            else live[i - transient.Length] = input;
        }
        GC.KeepAlive(transient);
        return live;
    }
}
