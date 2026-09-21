namespace Jint.Tests.Browser.Dom;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#dom-element-namespaceuri">DOM §4.9's <c>namespaceURI</c></a>: the
/// namespace an element was <b>created</b> with, which no insertion, adoption or import ever changes.
/// </summary>
/// <remarks>
/// <a href="https://github.com/sebastienros/jint/issues/3949">#3949</a> read as though adoption mutated the
/// element. It never did: AngleSharp's stored namespace is a <c>private readonly</c> field it exposes as
/// <c>GivenNamespaceUri</c>, and what moved was the <i>read</i> — <c>IElement.NamespaceUri</c> falls back to
/// an ancestor walk when nothing was stored. So every case here asserts the same element before and after it
/// is moved, and asserts that the query and <c>namespaceURI</c> give one answer rather than two.
/// </remarks>
public sealed class ElementNamespaceTests
{
    [Test]
    public void ANullNamespaceElementStaysInNoNamespaceWhenItIsAppended()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var detached = document.createElementNS(null, 'body');
            var empty = document.createElementNS('', 'body');
            """);

        fixture.Text("detached.namespaceURI").Should().BeNull();
        fixture.Text("empty.namespaceURI").Should().BeNull();

        fixture.Execute("root.appendChild(detached); root.appendChild(empty);");

        // The whole of #3949: appending to an HTML element used to make both of these read as XHTML.
        fixture.Text("detached.namespaceURI").Should().BeNull();
        fixture.Text("empty.namespaceURI").Should().BeNull();
    }

    [TestCase("null")]
    [TestCase("''")]
    public void ExplicitlyNullNamespacesIgnoreXmlnsOnSelfAndAncestors(string namespaceArgument)
    {
        using var fixture = DomTestFixture.Create("<main xmlns='urn:ancestor'></main>");
        fixture.Execute("var node = document.createElementNS(" + namespaceArgument + ", 'child');");
        fixture.Execute("""
            var root = document.querySelector('main');
            node.setAttribute('xmlns', 'urn:self');
            root.append(node);
            """);
        fixture.Text("node.namespaceURI").Should().BeNull();
        fixture.Number("root.getElementsByTagNameNS(null, 'child').length").Should().Be(1);
        fixture.Number("root.getElementsByTagNameNS('urn:self', 'child').length").Should().Be(0);
    }

    [TestCase("original.cloneNode(true)")]
    [TestCase("document.importNode(original, true)")]
    [TestCase("document.adoptNode(original)")]
    public void NullNamespaceProvenanceSurvivesSubtreeCopyAndAdoption(string operation)
    {
        using var fixture = DomTestFixture.Create("<main xmlns='urn:destination'></main>");
        fixture.Execute("""
            var other = new Document();
            var original = other.createElement('outer');
            original.appendChild(other.createElementNS(null, 'inner'));
            original.setAttribute('xmlns', 'urn:source');
            """);
        fixture.Execute("var moved = " + operation + "; document.querySelector('main').append(moved);");
        fixture.Text("moved.namespaceURI").Should().BeNull();
        fixture.Text("moved.firstChild.namespaceURI").Should().BeNull();
        fixture.Number("moved.getElementsByTagNameNS(null, '*').length").Should().Be(1);
        fixture.Bool("moved.ownerDocument === document && moved.firstChild.ownerDocument === document").Should().BeTrue();
        fixture.Bool("moved.isEqualNode(original)").Should().BeTrue();
    }

    [Test]
    public void TemplateContentClonesKeepExplicitlyNullNamespaces()
    {
        using var fixture = DomTestFixture.Create("<main xmlns='urn:destination'></main>");
        fixture.Execute("""
            var source = document.createElement('template');
            source.content.appendChild(document.createElementNS(null, 'child'));
            var copy = source.cloneNode(true);
            var child = document.querySelector('main').appendChild(copy.content.firstChild);
            """);
        fixture.Text("child.namespaceURI").Should().BeNull();
    }

    [Test]
    public void TheQueryAndNamespaceUriAgreeAboutANullNamespaceElement()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var none = root.appendChild(document.createElementNS('', 'body'));
            var html = root.appendChild(document.createElementNS('http://www.w3.org/1999/xhtml', 'body'));
            """);

        // getElementsByTagNameNS('', '*') is the assertion the corpus names "Empty string namespace".
        fixture.Number("root.getElementsByTagNameNS('', '*').length").Should().Be(1);
        fixture.Bool("root.getElementsByTagNameNS('', '*')[0] === none").Should().BeTrue();
        fixture.Number("root.getElementsByTagNameNS(null, 'body').length").Should().Be(1);
        fixture.Number("root.getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'body').length").Should().Be(1);
        fixture.Bool("root.getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'body')[0] === html").Should().BeTrue();

        // A qualified-name query folds case only for the HTML-namespace element, and tagName has to agree
        // with it or an element answers a name no list of that name contains.
        fixture.Text("none.tagName").Should().Be("body");
        fixture.Text("html.tagName").Should().Be("BODY");
        fixture.Number("root.getElementsByTagName('BODY').length").Should().Be(1);
        fixture.Bool("root.getElementsByTagName('BODY')[0] === html").Should().BeTrue();
    }

    [Test]
    public void AnHtmlNamespaceElementIsUnaffected()
    {
        using var fixture = DomTestFixture.Create("<main id='root'><p id='parsed'></p></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var created = root.appendChild(document.createElement('span'));
            var svg = root.appendChild(document.createElementNS('http://www.w3.org/2000/svg', 'circle'));
            """);

        fixture.Text("document.getElementById('parsed').namespaceURI").Should().Be("http://www.w3.org/1999/xhtml");
        fixture.Text("created.namespaceURI").Should().Be("http://www.w3.org/1999/xhtml");
        fixture.Text("svg.namespaceURI").Should().Be("http://www.w3.org/2000/svg");
        fixture.Text("document.documentElement.namespaceURI").Should().Be("http://www.w3.org/1999/xhtml");
        fixture.Text("created.tagName").Should().Be("SPAN");
        fixture.Text("svg.tagName").Should().Be("circle");
    }

    [Test]
    public void AdoptAndImportCarryTheCreationNamespace()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var other = document.implementation.createHTMLDocument('other');
            var made = other.createElementNS('', 'body');
            other.body.appendChild(made);
            var adopted = document.adoptNode(made);
            root.appendChild(adopted);
            var imported = document.importNode(other.createElementNS('', 'section'), true);
            root.appendChild(imported);
            """);

        fixture.Bool("adopted === made").Should().BeTrue();
        fixture.Text("adopted.namespaceURI").Should().BeNull();
        fixture.Text("imported.namespaceURI").Should().BeNull();
        fixture.Number("root.getElementsByTagNameNS('', '*').length").Should().Be(2);

        // Cloning copies the creation namespace rather than re-deriving one from the copy's new parent.
        fixture.Execute("var clone = root.appendChild(adopted.cloneNode(true));");
        fixture.Text("clone.namespaceURI").Should().BeNull();
        fixture.Number("root.getElementsByTagNameNS('', '*').length").Should().Be(3);
    }

    [Test]
    public void ANullNamespaceElementIsNotEqualToAnHtmlOneOfTheSameName()
    {
        using var fixture = DomTestFixture.Create("<main id='root'></main>");

        fixture.Execute("""
            var root = document.getElementById('root');
            var none = root.appendChild(document.createElementNS('', 'span'));
            var alsoNone = root.appendChild(document.createElementNS('', 'span'));
            var html = root.appendChild(document.createElementNS('http://www.w3.org/1999/xhtml', 'span'));
            """);

        // DOM §4.4 compares "A's namespace", which is the identity namespaceURI reports.
        fixture.Bool("none.isEqualNode(alsoNone)").Should().BeTrue();
        fixture.Bool("none.isEqualNode(html)").Should().BeFalse();
    }

    [Test]
    public async Task AnXmlnsAncestorStillGivesAParsedElementItsNamespace()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // AngleSharp's XML parser records no creation namespace at all, so an element parsed from XML has
        // only the xmlns declarations in scope to resolve against. Reading the stored namespace alone would
        // answer null here and lose a namespace the parse really did resolve.
        await page.SetContentAsync(
            """
            <script>
              const doc = new DOMParser().parseFromString(
                '<svg xmlns="http://www.w3.org/2000/svg"><g><circle/></g></svg>', 'text/xml');
              const root = doc.documentElement;
              window.log = [
                root.namespaceURI,
                root.getElementsByTagName('circle')[0].namespaceURI,
                doc.getElementsByTagNameNS('http://www.w3.org/2000/svg', '*').length,
                doc.getElementsByTagNameNS('', '*').length,
                document.importNode(root, true).namespaceURI,
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log"))
            .Should().Be("http://www.w3.org/2000/svg|http://www.w3.org/2000/svg|3|0|http://www.w3.org/2000/svg");
        page.Errors.Should().BeEmpty();
    }
    [TestCase("leaf.cloneNode(true)", false)]
    [TestCase("document.importNode(leaf, true)", false)]
    [TestCase("document.adoptNode(leaf)", true)]
    public async Task ParsedXmlNamespacesSurviveMoves(string operation, bool changeDeclarations)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<main xmlns='urn:destination'></main>");
        await page.EvaluateAsync<string>("""
            var xml = new DOMParser().parseFromString(
              '<root xmlns="urn:original" xmlns:p="urn:prefixed"><leaf><p:child/><none xmlns=""/></leaf></root>',
              'text/xml');
            'ready';
            """);
        if (changeDeclarations)
        {
            // Capture at document observation, before any descendant wrapper is requested.
            await page.EvaluateAsync<string>("""
                xml.documentElement.setAttribute('xmlns', 'urn:changed');
                xml.documentElement.setAttribute('xmlns:p', 'urn:changed-prefix');
                'changed';
                """);
        }
        await page.EvaluateAsync<string>("var leaf = xml.documentElement.firstChild; var moved = " + operation + "; document.querySelector('main').append(moved); 'done';");
        (await page.EvaluateAsync<string>("""
            [moved.namespaceURI, moved.firstChild.namespaceURI,
             moved.lastChild.namespaceURI === null,
             moved.getElementsByTagNameNS('urn:prefixed', 'child').length,
             moved.ownerDocument === document].join('|')
            """)).Should().Be("urn:original|urn:prefixed|true|1|true");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("cloneContents", "range.selectNode(root)", "root,child,leaf,sibling", "abcxyz")]
    [TestCase("cloneContents", "range.selectNodeContents(root)", "child,leaf,sibling", "abcxyz")]
    [TestCase("extractContents", "range.selectNodeContents(root)", "child,leaf,sibling", "abcxyz")]
    [TestCase("cloneContents", "range.setStart(leaf.firstChild, 1); range.setEnd(root, 1)", "child,leaf", "bc")]
    [TestCase("extractContents", "range.setStart(leaf.firstChild, 1); range.setEnd(root, 1)", "child,leaf", "bc")]
    [TestCase("cloneContents", "range.setStart(root, 0); range.setEnd(sibling.firstChild, 1)", "child,leaf,sibling", "abcx")]
    [TestCase("extractContents", "range.setStart(leaf.firstChild, 1); range.setEnd(sibling.firstChild, 1)", "child,leaf,sibling", "bcx")]
    [TestCase("cloneContents", "range.setStart(leaf.firstChild, 3); range.setEnd(root, 1)", "child,leaf", "")]
    [TestCase("extractContents", "range.setStart(root, 1); range.setEnd(sibling.firstChild, 0)", "sibling", "")]
    [TestCase("cloneContents", "range.selectNodeContents(leaf); range.collapse(true)", "", "")]
    [TestCase("extractContents", "range.setStart(leaf.firstChild, 1); range.setEnd(leaf.firstChild, 2)", "", "b")]
    public void RangeResultsCarryNamespacesWithoutChangingNativeSelection(
        string operation, string select, string names, string text)
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var root = document.createElementNS(null, 'root');
            var child = root.appendChild(document.createElementNS(null, 'child'));
            var leaf = child.appendChild(document.createElementNS(null, 'leaf'));
            leaf.textContent = 'abc';
            var sibling = root.appendChild(document.createElementNS(null, 'sibling'));
            sibling.textContent = 'xyz';
            for (var node of [root, child, leaf, sibling]) node.setAttribute('xmlns', 'urn:wrong');
            var range = document.createRange();
            """);
        if (select == "range.selectNode(root)")
        {
            fixture.Execute("document.querySelector('main').append(root);");
        }
        // The other cases exercise detached source trees as well as partial boundary containers.
        fixture.Execute(select + "; var result = range." + operation + "();");
        fixture.Text("Array.from(result.querySelectorAll('*'), e => e.localName).join(',')").Should().Be(names);
        fixture.Text("result.textContent").Should().Be(text);
        fixture.Bool("Array.from(result.querySelectorAll('*')).every(e => e.namespaceURI === null)").Should().BeTrue();
        if (operation == "extractContents" && select == "range.selectNodeContents(root)")
        {
            fixture.Bool("result.firstChild === child && result.lastChild === sibling").Should().BeTrue();
        }
    }

    [Test]
    public void RangeCloningCarriesTemplateContentAndSurroundMovesOriginalElements()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var root = document.querySelector('main');
            var template = root.appendChild(document.createElement('template'));
            var child = template.content.appendChild(document.createElementNS(null, 'child'));
            child.setAttribute('xmlns', 'urn:wrong');
            var range = document.createRange();
            range.selectNode(template);
            var result = range.cloneContents();
            var wrapper = document.createElementNS(null, 'wrapper');
            wrapper.setAttribute('xmlns', 'urn:wrong');
            range.surroundContents(wrapper);
            """);
        fixture.Text("result.firstChild.content.firstChild.namespaceURI").Should().BeNull();
        fixture.Bool("wrapper.firstChild === template && template.content.firstChild === child").Should().BeTrue();
        fixture.Text("child.namespaceURI").Should().BeNull();
    }

    [TestCase("cloneContents")]
    [TestCase("extractContents")]
    public void PartiallySelectedTemplatePreservesTheNativeShallowContentCopy(string operation)
    {
        // #4108 tracks the native shallow-template copying divergence. Provenance transfer must still
        // preserve that native result without throwing after extraction has already mutated the source.
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var root = document.querySelector('main');
            var template = root.appendChild(document.createElement('template'));
            var child = template.content.appendChild(document.createElementNS(null, 'child'));
            child.setAttribute('xmlns', 'urn:wrong');
            child.appendChild(document.createElementNS(null, 'leaf'));
            var range = document.createRange();
            range.setStart(template, 0);
            range.setEnd(root, 1);
            """);
        fixture.Execute("var result = range." + operation + "();");
        fixture.Text("result.firstChild.content.firstChild.namespaceURI").Should().BeNull();
        fixture.Number("result.firstChild.content.firstChild.childNodes.length").Should().Be(0);
    }

}
