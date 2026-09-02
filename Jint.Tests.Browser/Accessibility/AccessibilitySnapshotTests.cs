using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// The compact snapshot, checked against approved output for four whole pages.
/// </summary>
/// <remarks>
/// A golden file rather than an inline assertion, because this is the artefact an agent reads: its size and
/// its shape are the product, and a diff is the only honest way to review a change to either.
/// </remarks>
public sealed class AccessibilitySnapshotTests
{
    [TestCase("form")]
    [TestCase("article")]
    [TestCase("nav")]
    [TestCase("todomvc")]
    public void RendersThePage(string page)
    {
        using var document = PageFixture.Parse(GoldenFiles.Page(page), "https://jint.test/" + page + ".html");
        var snapshot = AccessibilitySnapshot.Render(AccessibilityTree.Build(document, AccessibilityOptions.Snapshot));

        GoldenFiles.Approve(page + ".snapshot.txt", snapshot);
    }

    [Test]
    public void RendersARoleANameAndTheAttributesThatChangeWhatACallerWouldDo()
    {
        using var document = PageFixture.Parse("<h2>Title</h2><input type=checkbox checked disabled aria-label=Agree>");

        var expected =
            """
            - RootWebArea:
              - heading "Title" [level=2]
              - checkbox "Agree" [checked] [disabled]
            """.Replace("\r\n", "\n") + "\n";

        AccessibilitySnapshot.Render(AccessibilityTree.Build(document)).Should().Be(expected);
    }

    [Test]
    public void OmitsAPropertyWhoseValueIsFalse()
    {
        using var document = PageFixture.Parse("<input type=checkbox aria-label=Agree>");

        AccessibilitySnapshot.Render(AccessibilityTree.Build(document))
            .Should().Contain("- checkbox \"Agree\"")
            .And.NotContain("[checked=false]");
    }

    [Test]
    public void RendersAWidgetsValueAfterTheColonWhenItHasNoChildren()
    {
        using var document = PageFixture.Parse("<label for=t>Name</label><input id=t value=Ada>");

        AccessibilitySnapshot.Render(AccessibilityTree.Build(document)).Should().Contain("- textbox \"Name\": Ada");
    }

    [Test]
    public void ANodeWhoseWholeContentIsOneRunOfTextSaysItOnItsOwnLine()
    {
        using var document = PageFixture.Parse("<p>Hello there</p>");
        var tree = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot);

        AccessibilitySnapshot.Render(tree).Should().Contain("- paragraph: Hello there").And.NotContain("- text:");
    }

    [Test]
    public void TextBetweenNodesIsItsOwnLine()
    {
        using var document = PageFixture.Parse("<p>before <a href='/x'>a link</a> after</p>");
        var tree = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot);

        AccessibilitySnapshot.Render(tree).Should().Contain("- text: before").And.Contain("- text: after");
    }

    [Test]
    public void TextThatIsAlreadyANodesNameIsNotStatedTwice()
    {
        using var document = PageFixture.Parse(
            "<button>Save</button><label for=t>Name</label><input id=t><figure><figcaption>Cap</figcaption></figure>");
        var tree = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot);
        var snapshot = AccessibilitySnapshot.Render(tree);

        snapshot.Should().Contain("- button \"Save\"").And.NotContain("text: Save");
        snapshot.Should().Contain("- textbox \"Name\"").And.NotContain("text: Name");
        snapshot.Should().Contain("- figure \"Cap\"").And.NotContain("text: Cap");
    }

    [Test]
    public void EscapesAQuoteInsideAName()
    {
        using var document = PageFixture.Parse("<button aria-label='Say &quot;hello&quot;'>x</button>");

        AccessibilitySnapshot.Render(AccessibilityTree.Build(document)).Should().Contain("- button \"Say \\\"hello\\\"\"");
    }

    [Test]
    public void TheSnapshotIsSmallerThanTheProtocolJson()
    {
        using var document = PageFixture.Parse(GoldenFiles.Page("todomvc"));
        var tree = AccessibilityTree.Build(document, AccessibilityOptions.Snapshot);

        var snapshot = AccessibilitySnapshot.Render(tree);
        var json = AccessibilityTree.ToJson(tree);

        // Not decoration: the snapshot exists because an agent's budget is tokens, and this is the claim.
        snapshot.Length.Should().BeLessThan(json.Length / 2);
    }
}
