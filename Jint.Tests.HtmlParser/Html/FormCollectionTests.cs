#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class FormCollectionTests
{
    [Test]
    public void ThreeSnapshotsHaveDistinctMembershipAndStableIdentity()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var fieldset = document.CreateElement("fieldset");
        form.AppendChild(fieldset);
        var input = document.CreateElement("input");
        fieldset.AppendChild(input);
        var imageInput = document.CreateElement("input");
        imageInput.SetAttribute("type", "ImAgE");
        fieldset.AppendChild(imageInput);
        var image = document.CreateElement("img");
        fieldset.AppendChild(image);
        var foreign = document.CreateElement("input");
        foreign.SetAttribute("form", "other");
        fieldset.AppendChild(foreign);
        var external = document.CreateElement("input");
        external.SetAttribute("form", "f");
        root.AppendChild(external);

        var associated = HtmlFormState.SnapshotAssociatedElements(form, default);
        associated.Should().Equal(fieldset, input, imageInput, image, external);
        HtmlFormState.SnapshotFormControls(form, default).Should().Equal(fieldset, input, external);
        HtmlFormState.SnapshotFieldsetControls(fieldset, default).Should().Equal(input, imageInput, foreign);
        Assert.That(associated is IList<Element> list && list.IsReadOnly, Is.True);
        external.RemoveAttribute("form");
        associated.Should().Contain(external);
        HtmlFormState.SnapshotAssociatedElements(form, default).Should().NotContain(external);
        imageInput.SetAttribute("type", "text");
        HtmlFormState.SnapshotFormControls(form, default).Should().Contain(imageInput);
    }

    [Test]
    public void InvalidReceiversAndCancellationAreRejected()
    {
        var document = Document.CreateHtml();
        var div = document.CreateElement("div");
        Assert.Throws<ArgumentException>(() => HtmlFormState.SnapshotAssociatedElements(div, default));
        Assert.Throws<ArgumentException>(() => HtmlFormState.SnapshotFieldsetControls(div, default));
        Assert.Throws<ArgumentNullException>(() => HtmlFormState.SnapshotFormControls(null!, default));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            HtmlFormState.SnapshotAssociatedElements(document.CreateElement("form"), canceled.Token));
    }
}
