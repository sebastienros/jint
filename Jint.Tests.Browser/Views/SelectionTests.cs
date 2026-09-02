namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Selection</c>: one range, no direction, and no user to move it.
/// </summary>
public sealed class SelectionTests
{
    [Test]
    public async Task TheDocumentHasOneSelectionAndBothEntriesAnswerIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("getSelection() === window.getSelection()")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.getSelection() === window.getSelection()")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("getSelection() instanceof Selection")).Should().BeTrue();
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(getSelection())")).Should().Be("[object Selection]");

        (await page.EvaluateAsync<string>(
            "(() => { try { new Selection(); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");
    }

    [Test]
    public async Task AnEmptySelectionAnswersTheEmptyState()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<int>("getSelection().rangeCount")).Should().Be(0);
        (await page.EvaluateAsync("getSelection().anchorNode")).Should().BeNull();
        (await page.EvaluateAsync("getSelection().focusNode")).Should().BeNull();
        (await page.EvaluateAsync<bool>("getSelection().isCollapsed")).Should().BeTrue();
        (await page.EvaluateAsync<string>("getSelection().type")).Should().Be("None");
        (await page.EvaluateAsync<string>("getSelection().toString()")).Should().BeEmpty();
        (await page.EvaluateAsync<string>("String(getSelection())")).Should().BeEmpty();
    }

    [Test]
    public async Task ARangeCanBeAddedReadBackAndRemoved()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const range = document.createRange();
              range.selectNodeContents(host);

              const selection = getSelection();
              selection.removeAllRanges();
              selection.addRange(range);

              window.after = [
                selection.rangeCount,
                selection.getRangeAt(0) === range,
                selection.anchorNode === host,
                selection.anchorOffset,
                selection.focusNode === host,
                selection.focusOffset,
                selection.isCollapsed,
                selection.type,
                selection.toString(),
              ].join('|');

              selection.removeAllRanges();
              window.emptied = [selection.rangeCount, selection.type].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.after")).Should().Be("1|true|true|0|true|2|false|Range|onetwo");
        (await page.EvaluateAsync<string>("window.emptied")).Should().Be("0|None");
    }

    [Test]
    public async Task CollapseAndSelectAllChildrenMoveTheSelection()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const selection = getSelection();

              selection.collapse(host, 1);
              window.collapsed = [selection.rangeCount, selection.isCollapsed, selection.anchorOffset, selection.type].join('|');

              selection.selectAllChildren(host);
              window.children = [selection.toString(), selection.isCollapsed, selection.containsNode(host.querySelector('b'))].join('|');

              selection.collapseToStart();
              window.start = [selection.isCollapsed, selection.anchorOffset].join('|');

              selection.collapse(null);
              window.cleared = selection.rangeCount;
            </script>
            """);

        (await page.EvaluateAsync<string>("window.collapsed")).Should().Be("1|true|1|Caret");
        (await page.EvaluateAsync<string>("window.children")).Should().Be("onetwo|false|true");
        (await page.EvaluateAsync<string>("window.start")).Should().Be("true|0");
        (await page.EvaluateAsync<int>("window.cleared")).Should().Be(0);
    }

    [Test]
    public async Task ASecondRangeIsIgnoredAndAnUnknownIndexIsARangeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="a">one</div><div id="b">two</div>
            <script>
              const selection = getSelection();
              const first = document.createRange();
              first.selectNodeContents(document.getElementById('a'));
              const second = document.createRange();
              second.selectNodeContents(document.getElementById('b'));

              selection.addRange(first);
              selection.addRange(second);
              window.kept = [selection.rangeCount, selection.toString()].join('|');

              selection.removeRange(second);
              window.stillThere = selection.rangeCount;
              selection.removeRange(first);
              window.gone = selection.rangeCount;
            </script>
            """);

        // "If the selection's range list is not empty, abort": the first range wins.
        (await page.EvaluateAsync<string>("window.kept")).Should().Be("1|one");
        (await page.EvaluateAsync<int>("window.stillThere")).Should().Be(1);
        (await page.EvaluateAsync<int>("window.gone")).Should().Be(0);

        (await page.EvaluateAsync<string>(
            "(() => { try { getSelection().getRangeAt(0); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("RangeError");
    }

    [Test]
    public async Task DeleteFromDocumentRemovesTheSelectedContent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><b>one</b><i>two</i></div>
            <script>
              const host = document.getElementById('host');
              const selection = getSelection();
              selection.selectAllChildren(host);
              selection.deleteFromDocument();
              window.left = host.innerHTML;
            </script>
            """);

        (await page.EvaluateAsync<string>("window.left")).Should().BeEmpty();
    }
}
