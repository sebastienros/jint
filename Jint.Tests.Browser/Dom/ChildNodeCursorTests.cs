using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class ChildNodeCursorTests
{
    [TestCase("forward")]
    [TestCase("reverse")]
    [TestCase("attributes")]
    [TestCase("unrelated")]
    [TestCase("append")]
    [TestCase("prepend")]
    [TestCase("insert")]
    [TestCase("remove")]
    [TestCase("reverse-remove")]
    [TestCase("forward-remove")]
    public void IndexedLoopsChargeLinearSiblingWork(string mode)
    {
        var small = Walk(512, mode);
        var large = Walk(1024, mode);
        large.Should().BeLessThanOrEqualTo(3 * small + 4);
        large.Should().BeLessThanOrEqualTo(1024);
    }

    private static long Walk(int count, string mode)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var other = document.CreateElement("div");
        var expected = new List<Node>();
        for (var i = 0; i < count; i++)
        {
            var node = document.CreateComment(i.ToString());
            expected.Add(node);
            root.AppendChild(node);
        }
        var list = DomChildNodeList.Of(root);
        long charged = 0;
        for (var i = 0; i < (mode == "forward-remove" ? count / 2 : count); i++)
        {
            var index = mode is "reverse" or "reverse-remove" ? count - i - 1
                : mode == "remove" ? 0 : mode is "prepend" or "insert" ? i * 2 : i;
            var current = list.ReadItem((uint) index, units => charged += units, default);
            current.Should().BeSameAs(expected[mode is "reverse" or "reverse-remove" ? count - i - 1 : mode == "forward-remove" ? i * 2 : i]);
            switch (mode)
            {
                case "attributes": root.SetAttribute("data-x", i.ToString()); break;
                case "unrelated": other.AppendChild(document.CreateComment("")); break;
                case "append": root.AppendChild(document.CreateComment("")); break;
                case "prepend": root.InsertBefore(document.CreateComment(""), root.FirstChild); break;
                case "insert": root.InsertBefore(document.CreateComment(""), current); break;
                case "remove": case "reverse-remove": case "forward-remove": root.RemoveChild(current!); break;
            }
        }
        return charged;
    }

    [Test]
    public void NativeMutationsReparentingAndReplacementMatchCurrentMembership()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var other = document.CreateElement("div");
        var list = DomChildNodeList.Of(root);
        var random = new Random(123);
        for (var i = 0; i < 1000; i++)
        {
            var children = root.ChildNodes.ToArray();
            if (children.Length != 0)
                list.ReadItem((uint) random.Next(children.Length), null, default).Should().BeOneOf(children);
            var reference = children.Length == 0 ? null : children[random.Next(children.Length)];
            switch (random.Next(5))
            {
                case 0: root.InsertBefore(document.CreateComment(""), reference); break;
                case 1 when reference is not null: other.AppendChild(reference); break;
                case 2 when reference is not null: root.ReplaceChild(document.CreateTextNode("x"), reference); break;
                case 3 when other.FirstChild is not null: root.InsertBefore(other.FirstChild, reference); break;
                case 4 when children.Length > 1: root.InsertBefore(children[^1], reference); break;
            }
            children = root.ChildNodes.ToArray();
            for (var j = children.Length - 1; j >= 0; j--)
                list.ReadItem((uint) j, null, default).Should().BeSameAs(children[j]);
            list.ReadItem((uint) children.Length, null, default).Should().BeNull();
            list.ReadItem(uint.MaxValue, null, default).Should().BeNull();
        }
    }

    [Test]
    public void WarmAndOutOfBoundsReadsStillCheckCancellationAndReadValidation()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        root.AppendChild(document.CreateComment(""));
        var list = DomChildNodeList.Of(root);
        list.ReadItem(0, null, default).Should().BeSameAs(root.FirstChild);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreach (var index in new[] { 0u, 1u, uint.MaxValue })
            Caught.Exception(() => list.ReadItem(index, null, cancellation.Token))
                .Should().BeOfType<OperationCanceledException>();
        var checks = 0;
        Caught.Exception(() => list.ReadItem(0, _ =>
        {
            if (++checks == 2) root.SetAttribute("data-x", "changed");
        }, default)).Should().BeOfType<InvalidOperationException>();
        list.ReadItem(0, null, default).Should().BeSameAs(root.FirstChild);
    }

    [Test]
    public void LongTraversalRejectsCheckpointMutationAndPreservesHostExceptions()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 2048; i++) root.AppendChild(document.CreateComment(""));
        var list = DomChildNodeList.Of(root);
        Caught.Exception(() => list.ReadItem(1024, units =>
        {
            if (units >= 256) root.RemoveChild(root.FirstChild!);
        }, default)).Should().BeOfType<InvalidOperationException>();
        Caught.Exception(() => list.ReadItem(1024, _ => throw new OperationCanceledException(), default))
            .Should().BeOfType<OperationCanceledException>();
    }

    [Test]
    public async Task ScriptAccessKeepsLiveMembershipWrapperIdentityAndBounds()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='root'><i></i>text<b></b></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
                const root = document.getElementById('root'), list = root.childNodes;
                const last = list[2]; last.marker = 42;
                const other = document.implementation.createHTMLDocument('other');
                other.body.appendChild(root);
                root.insertBefore(last, root.firstChild);
                if (list !== root.childNodes || list[0] !== last || list.item(0).marker !== 42) return false;
                const seen = [];
                for (let i = list.length - 1; i >= 0; i--) {
                    seen.push(list[i]); root.removeChild(list[i]);
                }
                return seen.length === 3 && seen[2] === last && list.length === 0 &&
                    list[0] === undefined && list[-1] === undefined && list[4294967295] === undefined &&
                    list.item(0) === null;
            })()
            """)).Should().BeTrue();
    }
}
