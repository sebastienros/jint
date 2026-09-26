using Jint.Browser.Dom.Files;
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
    public void SharedListChangesInvalidateOnlyItsCurrentlyAssignedInputs()
    {
        using var fixture = DomTestFixture.Create("<input type=file id=upload>");
        fixture.Execute("var transfer = new DataTransfer(); document.getElementById('upload').files = transfer.files;");
        var assigned = fixture.Document.MutationStamp;
        fixture.Execute("transfer.items.add(new File(['x'], 'x.txt'));");
        fixture.Document.MutationStamp.Should().BeGreaterThan(assigned);
        fixture.Execute("document.getElementById('upload').files = new DataTransfer().files;");
        var reassigned = fixture.Document.MutationStamp;
        fixture.Execute("transfer.items.clear();");
        fixture.Document.MutationStamp.Should().Be(reassigned);
    }

    [Test]
    public void ClearingAnInputDetachesTheSharedListAndEngineDisposalReleasesNotifications()
    {
        var fixture = DomTestFixture.Create("<input type=file id=upload>");
        fixture.Execute("var transfer = new DataTransfer(); transfer.items.add(new File(['x'], 'x.txt')); document.getElementById('upload').files = transfer.files;");
        fixture.Execute("document.getElementById('upload').value = '';");
        var cleared = fixture.Document.MutationStamp;
        fixture.Execute("transfer.items.add(new File(['y'], 'y.txt'));");
        fixture.Document.MutationStamp.Should().Be(cleared);

        var realm = FileTransferRealm.Of(fixture.Engine);
        var input = fixture.Document.CreateElement("input");
        input.SetAttributeNS(null, "type", "file");
        var list = realm.NewFileList();
        realm.SetInputFiles(input, list);
        var file = new Jint.WebApi.Files.JsFile(fixture.Engine, new byte[] { 1 }, "text/plain", "z.txt", 0);
        fixture.Dispose();
        var disposed = fixture.Document.MutationStamp;
        list.Add(file);
        fixture.Document.MutationStamp.Should().Be(disposed);
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
