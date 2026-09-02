using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// The tree walk itself: what it prunes, what it hides, what it publishes and what it keeps stable.
/// </summary>
public sealed class AccessibilityTreeTests
{
    [Test]
    public void TheRootIsAWebAreaNamedByTheDocumentTitle()
    {
        using var document = PageFixture.Parse("<html><head><title>  My   page </title></head><body><h1>Hi</h1></body></html>");
        var root = AccessibilityTree.Build(document);

        root.Role.Should().Be("RootWebArea");
        root.Name.Should().Be("My page");
        ImplicitRoleTests.Find(root, "heading").Should().NotBeNull();
    }

    [Test]
    public void GenericWrappersArePrunedAndReplacedByTheirChildren()
    {
        using var document = PageFixture.Parse("<div><div><span><button>Save</button></span></div></div>");
        var root = AccessibilityTree.Build(document);

        // html, body and every div and span in between are generic; only the button survives.
        root.Children.Should().ContainSingle();
        root.Children[0].Role.Should().Be("button");
        root.Children[0].Name.Should().Be("Save");
    }

    [Test]
    public void IncludeGenericKeepsEveryWrapper()
    {
        using var document = PageFixture.Parse("<div><button>Save</button></div>");
        var root = AccessibilityTree.Build(document, AccessibilityOptions.Default with { IncludeGeneric = true });

        ImplicitRoleTests.FindAll(root, "generic").Should().NotBeEmpty();
    }

    [Test]
    public void AGenericElementThatIsNamedOrFocusableSurvivesThePruning()
    {
        using var document = PageFixture.Parse("<div aria-label='Named box'>x</div><div tabindex=0>y</div><div>z</div>");
        var root = AccessibilityTree.Build(document);

        var generics = ImplicitRoleTests.FindAll(root, "generic");
        generics.Should().HaveCount(2);
        generics[0].Name.Should().Be("Named box");
        generics[1].Properties.Should().Contain(p => p.Name == AxPropertyName.Focusable);
    }

    [Test]
    public void HiddenSubtreesAreDroppedWholesale()
    {
        using var document = PageFixture.Parse(
            "<button>Visible</button>" +
            "<div hidden><button>A</button></div>" +
            "<div style='display:none'><button>B</button></div>" +
            "<div aria-hidden=true><button>C</button></div>" +
            "<div style='visibility:hidden'><button>D</button></div>");

        var root = AccessibilityTree.Build(document);

        ImplicitRoleTests.FindAll(root, "button").Select(static n => n.Name).Should().Equal("Visible");
    }

    [Test]
    public void AVisibleChildOfAVisibilityHiddenParentComesBack()
    {
        // `visibility` is the one CSS inherits, so the cascade — not an ancestor walk — is what answers.
        using var document = PageFixture.Parse(
            "<div style='visibility:hidden'><button style='visibility:visible'>Back</button><button>Gone</button></div>");

        var root = AccessibilityTree.Build(document);

        ImplicitRoleTests.FindAll(root, "button").Select(static n => n.Name).Should().Equal("Back");
    }

    [Test]
    public void ADisplayNoneFromAStyleSheetHidesTheSubtree()
    {
        using var document = PageFixture.Parse("<style>.gone{display:none}</style><div class=gone><button>A</button></div><button>B</button>");
        var root = AccessibilityTree.Build(document);

        ImplicitRoleTests.FindAll(root, "button").Select(static n => n.Name).Should().Equal("B");
    }

    [Test]
    public void WithoutTheCascadeTheInlineStyleAndTheHiddenAttributeStillAnswer()
    {
        // AngleSharp.Css's ComputeCurrentStyle throws rather than answering when the CSS service is absent.
        // The walk asks once, records that, and finishes on the inline-style path instead of failing.
        using var document = PageFixture.ParseWithoutCss(
            "<style>.gone{display:none}</style>" +
            "<div class=gone><button>Sheet</button></div>" +
            "<div style='display:none'><button>Inline</button></div>" +
            "<div hidden><button>Attribute</button></div>" +
            "<button>Visible</button>");

        var root = AccessibilityTree.Build(document);

        // The style sheet is beyond reach without the cascade, and this is what that costs.
        ImplicitRoleTests.FindAll(root, "button").Select(static n => n.Name).Should().Equal("Sheet", "Visible");
    }

    [Test]
    public void HeadScriptStyleAndTemplateContentsNeverReachTheTree()
    {
        using var document = PageFixture.Parse(
            "<head><title>T</title><style>button{color:red}</style></head>" +
            "<body><script>var x = 1;</script><template><button>In a template</button></template><button>Real</button></body>");

        var root = AccessibilityTree.Build(document, AccessibilityOptions.Full);
        var text = AccessibilitySnapshot.Render(root);

        text.Should().NotContain("var x").And.NotContain("color:red").And.NotContain("In a template");
        ImplicitRoleTests.FindAll(root, "button").Select(static n => n.Name).Should().Equal("Real");
    }

    [Test]
    public void IncludeIgnoredKeepsTheHiddenNodesAndSaysWhy()
    {
        using var document = PageFixture.Parse("<div hidden><button>A</button></div><div aria-hidden=true><span><button>B</button></span></div>");
        var root = AccessibilityTree.Build(document, AccessibilityOptions.Full);

        var buttons = ImplicitRoleTests.FindAll(root, "button");
        buttons.Should().HaveCount(2);
        buttons[0].IgnoredReason.Should().Be(AxIgnoredReason.Hidden);
        buttons[1].IgnoredReason.Should().Be(AxIgnoredReason.AriaHiddenSubtree);
        buttons.Should().AllSatisfy(static b => b.Ignored.Should().BeTrue());
    }

    [Test]
    public void AnImageWithAnEmptyAltIsIgnoredForBeingPresentational()
    {
        using var document = PageFixture.Parse("<img src=a.png alt=''><img src=b.png alt='A cat'>");
        var full = AccessibilityTree.Build(document, AccessibilityOptions.Full);

        ImplicitRoleTests.Find(full, "none")!.IgnoredReason.Should().Be(AxIgnoredReason.EmptyAlt);
        ImplicitRoleTests.Find(full, "image")!.Name.Should().Be("A cat");

        var pruned = AccessibilityTree.Build(document);
        ImplicitRoleTests.Find(pruned, "none").Should().BeNull();
    }

    [Test]
    public void TextNodesBecomeStaticTextOnlyWhenAsked()
    {
        using var document = PageFixture.Parse("<p>Hello there</p>");

        ImplicitRoleTests.Find(AccessibilityTree.Build(document), "StaticText").Should().BeNull();

        var full = AccessibilityTree.Build(document, AccessibilityOptions.Default with { IncludeText = true });
        ImplicitRoleTests.Find(full, "StaticText")!.Name.Should().Be("Hello there");
    }

    [Test]
    public void IdentifiersAreStableAcrossTwoBuildsOfTheSameDocument()
    {
        using var document = PageFixture.Parse("<button>A</button><a href='/x'>B</a><h1>C</h1>");

        var first = Identifiers(AccessibilityTree.Build(document));
        var second = Identifiers(AccessibilityTree.Build(document));

        second.Should().Equal(first);
        first.Should().OnlyHaveUniqueItems();

        static List<int> Identifiers(AxNode node)
        {
            var ids = new List<int> { node.Id };
            foreach (var child in node.Children)
            {
                ids.AddRange(Identifiers(child));
            }

            return ids;
        }
    }

    [Test]
    public void TwoDocumentsNumberTheirOwnNodes()
    {
        using var first = PageFixture.Parse("<button>A</button>");
        using var second = PageFixture.Parse("<button>A</button>");

        var a = ImplicitRoleTests.Find(AccessibilityTree.Build(first), "button")!;
        var b = ImplicitRoleTests.Find(AccessibilityTree.Build(second), "button")!;

        a.Id.Should().Be(b.Id, "each document counts from one, so identifiers are only unique within a document");
    }

    [Test]
    public void APartialTreeStartsAtTheElementAndInheritsTheAncestorsHiddenVerdict()
    {
        using var document = PageFixture.Parse("<div hidden><section id=s aria-label=S><button>A</button></section></div>");

        AccessibilityTree.Build(document.GetElementById("s")!).Should().BeNull();

        var included = AccessibilityTree.Build(document.GetElementById("s")!, AccessibilityOptions.Full);
        included!.Role.Should().Be("region");
        included.IgnoredReason.Should().Be(AxIgnoredReason.Hidden);
    }

    [Test]
    public void APartialTreeOfAPrunedElementAnswersItsSurvivingChild()
    {
        using var document = PageFixture.Parse("<div id=d><button>A</button></div>");
        var node = AccessibilityTree.Build(document.GetElementById("d")!);

        node!.Role.Should().Be("button");
    }

    private static IEnumerable<TestCaseData> Properties()
    {
        yield return Property("<input id=t type=checkbox checked>", AxPropertyName.Checked, "true");
        yield return Property("<input id=t type=checkbox>", AxPropertyName.Checked, "false");
        yield return Property("<div id=t role=checkbox aria-checked=mixed></div>", AxPropertyName.Checked, "mixed");
        yield return Property("<button id=t aria-pressed=true>x</button>", AxPropertyName.Pressed, "true");
        yield return Property("<details id=t open></details>", AxPropertyName.Expanded, "true");
        yield return Property("<details id=t></details>", AxPropertyName.Expanded, "false");
        yield return Property("<details open><summary id=t>x</summary></details>", AxPropertyName.Expanded, "true");
        yield return Property("<select><option id=t selected>x</option></select>", AxPropertyName.Selected, "true");
        yield return Property("<button id=t disabled>x</button>", AxPropertyName.Disabled, "true");
        yield return Property("<fieldset disabled><button id=t>x</button></fieldset>", AxPropertyName.Disabled, "true");
        yield return Property("<div id=t role=button aria-disabled=true>x</div>", AxPropertyName.Disabled, "true");
        yield return Property("<input id=t required>", AxPropertyName.Required, "true");
        yield return Property("<input id=t readonly>", AxPropertyName.Readonly, "true");
        yield return Property("<a id=t href='/x'>x</a>", AxPropertyName.Focusable, "true");
        yield return Property("<h3 id=t>x</h3>", AxPropertyName.Level, "3");
        yield return Property("<div id=t role=heading aria-level=5>x</div>", AxPropertyName.Level, "5");
        yield return Property("<textarea id=t></textarea>", AxPropertyName.Multiline, "true");
        yield return Property("<select id=t multiple></select>", AxPropertyName.Multiselectable, "true");
        yield return Property("<div id=t role=slider aria-orientation=vertical></div>", AxPropertyName.Orientation, "vertical");
        yield return Property("<input id=t aria-invalid=spelling>", AxPropertyName.Invalid, "spelling");
        yield return Property("<datalist id=d></datalist><input id=t list=d>", AxPropertyName.Autocomplete, "list");
        yield return Property("<div id=t role=button aria-haspopup=menu>x</div>", AxPropertyName.HasPopup, "menu");
        yield return Property("<div id=t role=status>x</div>", AxPropertyName.Live, "polite");
        yield return Property("<div id=t role=alert>x</div>", AxPropertyName.Live, "assertive");
        yield return Property("<div id=t role=status aria-live=off>x</div>", AxPropertyName.Live, "off");
        yield return Property("<input id=t type=range min=1 max=9>", AxPropertyName.Valuemin, "1");
        yield return Property("<input id=t type=range min=1 max=9>", AxPropertyName.Valuemax, "9");
        yield return Property("<progress id=t value=3 max=10></progress>", AxPropertyName.Valuemax, "10");
        yield return Property("<div id=t role=slider aria-valuetext='Large'></div>", AxPropertyName.Valuetext, "Large");
        yield return Property("<dialog id=t open aria-modal=true></dialog>", AxPropertyName.Modal, "true");
        yield return Property("<div id=t role=status aria-atomic=true aria-busy=true>x</div>", AxPropertyName.Busy, "true");
    }

    // The case data carries the protocol spelling rather than the enumeration member, because the
    // enumeration is internal and a public test method may not name it.
    private static TestCaseData Property(string html, AxPropertyName name, string value) =>
        new TestCaseData(html, new AxProperty(name, default).ProtocolName, value)
            .SetArgDisplayNames(html, name.ToString(), value);

    [TestCaseSource(nameof(Properties))]
    public void PublishesTheProperty(string html, string name, string expected)
    {
        using var document = PageFixture.Parse(html);
        var node = AccessibilityTree.Build(document.GetElementById("t")!, AccessibilityOptions.Full)!;

        var property = node.Properties.FirstOrDefault(p => string.Equals(p.ProtocolName, name, StringComparison.Ordinal));
        property.Should().NotBe(default(AxProperty), "the node should carry a {0} property", name);
        property.Value.ToDisplayString().Should().Be(expected);
    }

    [Test]
    public void PublishesTheUrlOfALinkAndAnImage()
    {
        using var document = PageFixture.Parse("<a id=a href='/page?q=1'>x</a><img id=i src='pic.png' alt=A>", "https://example.com/dir/index.html");

        Url(document, "a").Should().Be("https://example.com/page?q=1");
        Url(document, "i").Should().Be("https://example.com/dir/pic.png");

        string? Url(AngleSharp.Dom.IDocument doc, string id)
        {
            var node = AccessibilityTree.Build(doc.GetElementById(id)!, AccessibilityOptions.Full)!;
            return node.Properties.FirstOrDefault(p => p.Name == AxPropertyName.Url).Value.Text;
        }
    }

    [Test]
    public void PublishesAWidgetsValue()
    {
        using var document = PageFixture.Parse(
            "<input id=a value='typed'>" +
            "<textarea id=b>lines</textarea>" +
            "<select id=c><option>One</option><option selected>Two</option></select>" +
            "<progress id=d value=4 max=10></progress>");

        Value(document, "a").Should().Be("typed");
        Value(document, "b").Should().Be("lines");
        Value(document, "c").Should().Be("Two");
        Value(document, "d").Should().Be("4");

        string? Value(AngleSharp.Dom.IDocument doc, string id) =>
            AccessibilityTree.Build(doc.GetElementById(id)!, AccessibilityOptions.Full)!.Value;
    }

    [Test]
    public void ARoleThatIsPresentationalKeepsItsChildren()
    {
        using var document = PageFixture.Parse("<ul role=presentation><li><a href='/x'>Link</a></li></ul>");
        var root = AccessibilityTree.Build(document);

        ImplicitRoleTests.Find(root, "list").Should().BeNull();
        ImplicitRoleTests.Find(root, "link")!.Name.Should().Be("Link");
    }
}
