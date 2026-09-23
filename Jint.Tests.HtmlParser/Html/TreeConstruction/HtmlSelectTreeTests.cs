#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.6.4.7 and §13.2.6.4.9–15, revision 2026-09-22,
    // inspected 2026-09-23. Historical html5lib comparison pin:
    // 9329e64694e7835d0dcff9811e22856ef6ad16f9.
    // These literal trees follow the current in-body rules. Historical
    // select-mode parsers discard some ordinary descendants instead.
    [TestCase("<select><option>one<option>two</select>tail",
        "<html><head></head><body><select><option>one</option><option>two</option></select>tail</body></html>")]
    [TestCase("<select><optgroup><option>a<optgroup><option>b</select>",
        "<html><head></head><body><select><optgroup><option>a</option></optgroup><optgroup><option>b</option></optgroup></select></body></html>")]
    [TestCase("<select><option><span>x</span><div>y</div></option></select>",
        "<html><head></head><body><select><option><span>x</span><div>y</div></option></select></body></html>")]
    [TestCase("<select><div><option>x</option></div></select>",
        "<html><head></head><body><select><div><option>x</option></div></select></body></html>")]
    [TestCase("<select><option>a<select><option>b",
        "<html><head></head><body><select><option>a</option></select><option>b</option></body></html>")]
    [TestCase("<select><option>a<input id=i><option>b",
        "<html><head></head><body><select><option>a</option></select><input></input><option>b</option></body></html>")]
    [TestCase("<select><optgroup><option>a<hr><option>b</select>",
        "<html><head></head><body><select><optgroup><option>a</option></optgroup><hr></hr><option>b</option></select></body></html>")]
    [TestCase("<option>a<option>b</option>",
        "<html><head></head><body><option>a</option><option>b</option></body></html>")]
    [TestCase("<select><option><span>x</option>y</select>",
        "<html><head></head><body><select><option><span>x</span></option>y</select></body></html>")]
    [TestCase("<select><optgroup><option><span>x</option></optgroup>y</select>",
        "<html><head></head><body><select><optgroup><option><span>x</span></option></optgroup>y</select></body></html>")]
    [TestCase("<b><select><option>x</select>y</b>",
        "<html><head></head><body><b><select><option>x</option></select>y</b></body></html>")]
    [TestCase("<select><b><option>x<select>y",
        "<html><head></head><body><select><b><option>x</option></b></select><b>y</b></body></html>")]
    [TestCase("</option></optgroup></select><p>x",
        "<html><head></head><body><p>x</p></body></html>")]
    [TestCase("<table><select><option>x</select><tr><td>y</table>",
        "<html><head></head><body><select><option>x</option></select><table><tbody><tr><td>y</td></tr></tbody></table></body></html>")]
    [TestCase("<table><tr><td><select><option>x</select>y</td></tr></table>",
        "<html><head></head><body><table><tbody><tr><td><select><option>x</option></select>y</td></tr></tbody></table></body></html>")]
    public void CurrentSelectTreesAtEveryQuota(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void SelectCommentsAndProcessingInstructionsKeepNativeIdentity()
    {
        var parsed = Parse("<select><!--a--><?Pi yes?><option>x</option></select>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var select = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        select.FirstChild.Should().BeOfType<Comment>();
        select.FirstChild!.NextSibling.Should().BeOfType<ProcessingInstruction>();
        ((ProcessingInstruction) select.FirstChild.NextSibling!).Target.Should().Be("Pi");
        ((Element) select.LastChild!).LocalName.Should().Be("option");
    }

    [Test]
    public void HrInsideInterveningMarkupReportsOptionScopeAtTokenStart()
    {
        const string source = "<select><option><span><hr>";
        var parsed = Parse(source, 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        parsed.Diagnostics.Items.Should().Contain(item =>
            item.Code == "html/tree-hr-in-select-option" &&
            item.Offset == source.IndexOf("<hr>", StringComparison.Ordinal));
    }

    [Test]
    public void SplitsAndQuotaOnePreserveSelectTreeAndOriginalNodes()
    {
        const string source = "<select><optgroup><option>one<b>two</b><option>three</select>tail";
        var expected = Serialize(Parse(source).Document);
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            DrainToNeedInput(session, 1);
            session.AppendInput(source[split..], isFinal: true);
            DrainToCompletion(session, 1);
            Serialize(document).Should().Be(expected, $"split {split}");
        }
    }

    [Test]
    public void LongInterveningMarkupUsesIndexedScopeAndResumesClosure()
    {
        static long Work(int depth)
        {
            var source = "<select><option>" + string.Concat(Enumerable.Repeat("<span>", depth)) +
                "x</select>y";
            var parsed = Parse(source, 1);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            var select = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
            select.LocalName.Should().Be("select");
            ((Text) select.NextSibling!).Data.Should().Be("y");
            return parsed.Session.WorkCount;
        }

        var smaller = Work(64);
        Work(128).Should().BeLessThan(smaller * 3);
    }

    [Test]
    public void InputClosureKeepsPreviouslyInsertedSelectNodes()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<select><option>a", isFinal: false);
        DrainToNeedInput(session, 1);
        var body = (Element) document.DocumentElement!.LastChild!;
        var select = (Element) body.FirstChild!;
        var option = (Element) select.FirstChild!;
        session.AppendInput("<input><span>b", isFinal: true);
        DrainToCompletion(session, 1);
        body.FirstChild.Should().BeSameAs(select);
        select.FirstChild.Should().BeSameAs(option);
        ((Element) select.NextSibling!).LocalName.Should().Be("input");
    }

    [Test]
    public void CancellationDuringResumableSelectClosureKeepsCommittedTree()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        var spans = string.Concat(Enumerable.Repeat("<span>", 32));
        session.AppendInput("<select><option>" + spans + "x<input>", isFinal: true);
        var builder = BuilderOf(session);
        var popTarget = typeof(HtmlTreeBuilder).GetField("_pendingPopTarget",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var open = (System.Collections.ICollection) typeof(HtmlTreeBuilder).GetField("_open",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(builder)!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            var step = session.Drive(1, CancellationToken.None);
            if ((int) popTarget.GetValue(builder)! >= 0 && open.Count < 36) break;
            if (step.Kind != HtmlParseStepKind.Yielded || turn == 99_999)
                throw new InvalidOperationException("Select closure did not reach a resumed stack pop.");
        }

        var before = Serialize(document);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancellation.Token));
        Serialize(document).Should().Be(before);
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
    }
}
