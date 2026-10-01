using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class InertCharacterRecordTests
{
    [Test]
    public void OnlyTheOptedInSubscriptionOmitsCharacterChangesOutsideRawTextOwners()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var style = document.CreateElement("style");
        root.AppendChild(style);
        var options = new MutationObserverOptions { ChildList = true, CharacterData = true, Subtree = true };
        using var all = document.ObserveMutations(document, options);
        using var resources = document.ObserveMutations(document, options);
        resources.OmitInertCharacterRecords = true;

        var text = document.CreateTextNode("a");
        root.AppendChild(text);
        var span = document.CreateElement("span");
        root.AppendChild(span);
        var sheet = document.CreateTextNode("p{}");
        style.AppendChild(sheet);
        text.Data = "b";
        sheet.Data = "q{}";
        span.AppendChild(document.CreateComment("c"));
        root.RemoveChild(text);

        all.TakeRecords().Should().HaveCount(7);
        resources.TakeRecords().Select(record => (record.Kind, record.Target)).Should().Equal(
            (MutationRecordKind.ChildList, (Node) root),
            (MutationRecordKind.ChildList, style),
            (MutationRecordKind.CharacterData, sheet));
    }
}
