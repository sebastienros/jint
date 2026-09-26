using Jint.Browser.Dom.Files;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Files;

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
        fixture.Execute("window.transfer = new DataTransfer(); document.getElementById('upload').files = transfer.files;");
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
        fixture.Execute("window.transfer = new DataTransfer(); transfer.items.add(new File(['x'], 'x.txt')); document.getElementById('upload').files = transfer.files;");
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
}
