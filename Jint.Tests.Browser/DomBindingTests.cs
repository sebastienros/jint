namespace Jint.Tests.Browser;

/// <summary>
/// What a script can do with the generated bindings: the DOM's own surface, exercised the way a page would.
/// </summary>
public sealed class DomBindingTests
{
    private const string Page = """
        <!doctype html>
        <html><body>
          <div id="a" class="one two" data-foo="bar" style="color: red">hello <b>world</b></div>
          <ul id="list"><li>one</li><li>two</li><li>three</li></ul>
          <a href="https://example.com/x" id="link">link</a>
          <form id="f"><input id="i" name="username" type="text" maxlength="7" tabindex="3" disabled></form>
        </body></html>
        """;

    [Test]
    public void QuerySelectorReadsTheTree()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("document.querySelector('#a').textContent").Should().Be("hello world");
        fixture.Text("document.querySelector('#a').tagName").Should().Be("DIV");
        fixture.Text("document.querySelector('nope')").Should().BeNull();
    }

    [Test]
    public void QuerySelectorAllIsAnIndexableIterableCollection()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.querySelectorAll('li').length").Should().Be(3);
        fixture.Text("document.querySelectorAll('li')[1].textContent").Should().Be("two");
        fixture.Text("[...document.querySelectorAll('li')].map(e => e.textContent).join(',')").Should().Be("one,two,three");

        fixture.Text("""
            var out = [];
            for (const li of document.querySelectorAll('li')) { out.push(li.textContent); }
            out.join('|');
            """).Should().Be("one|two|three");
    }

    [Test]
    public void AttributesAreReadWrittenAndRemoved()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("document.querySelector('#a').hasAttribute('data-foo')").Should().BeTrue();
        fixture.Text("document.querySelector('#a').getAttribute('data-foo')").Should().Be("bar");
        fixture.Text("document.querySelector('#a').getAttribute('nope')").Should().BeNull();

        fixture.Evaluate("document.querySelector('#a').setAttribute('data-foo', 'baz')");
        fixture.Text("document.querySelector('#a').getAttribute('data-foo')").Should().Be("baz");

        fixture.Evaluate("document.querySelector('#a').removeAttribute('data-foo')");
        fixture.Bool("document.querySelector('#a').hasAttribute('data-foo')").Should().BeFalse();
    }

    [Test]
    public void ClassListIsALiveTokenList()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("document.querySelector('#a').classList.contains('one')").Should().BeTrue();
        fixture.Number("document.querySelector('#a').classList.length").Should().Be(2);
        fixture.Text("document.querySelector('#a').classList[0]").Should().Be("one");

        fixture.Evaluate("document.querySelector('#a').classList.add('three')");
        fixture.Bool("document.querySelector('#a').classList.contains('three')").Should().BeTrue();

        fixture.Evaluate("document.querySelector('#a').classList.toggle('one')");
        fixture.Bool("document.querySelector('#a').classList.contains('one')").Should().BeFalse();

        fixture.Text("document.querySelector('#a').className").Should().Be("two three");
    }

    [Test]
    public void TheTreeCanBeWalkedInBothDirections()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.querySelector('#list').children.length").Should().Be(3);
        fixture.Text("document.querySelector('#list').firstElementChild.textContent").Should().Be("one");
        fixture.Text("document.querySelector('#list').lastElementChild.textContent").Should().Be("three");
        fixture.Text("document.querySelector('li').parentNode.id").Should().Be("list");
        fixture.Text("document.querySelector('li').nextSibling.textContent").Should().Be("two");
        fixture.Number("document.querySelector('#list').childNodes.length").Should().Be(3);
        fixture.Number("document.querySelector('#list').nodeType").Should().Be(1);
        fixture.Number("Node.ELEMENT_NODE").Should().Be(1);
    }

    [Test]
    public void DatasetProjectsDataAttributes()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("document.querySelector('#a').dataset.foo").Should().Be("bar");

        fixture.Evaluate("document.querySelector('#a').dataset.baz = 'qux'");
        fixture.Text("document.querySelector('#a').getAttribute('data-baz')").Should().Be("qux");
        fixture.Text("Object.keys(document.querySelector('#a').dataset).sort().join(',')").Should().Be("baz,foo");

        // The JavaScript-visible half of the deleter. The content attribute itself survives, because
        // AngleSharp's StringMap.Remove sets its value to null instead of removing it — reported upstream and
        // recorded in Jint.Browser/AGENTS.md rather than worked around here.
        fixture.Evaluate("delete document.querySelector('#a').dataset.foo");
        fixture.Evaluate("document.querySelector('#a').dataset.foo").IsUndefined().Should().BeTrue();
        fixture.Bool("'foo' in document.querySelector('#a').dataset").Should().BeFalse();
    }

    [Test]
    public void StyleIsReadAndWrittenByCssPropertyName()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("document.querySelector('#a').style.color").Should().Be("rgba(255, 0, 0, 1)");


        // AngleSharp.Css serializes every color as rgba(...) where CSSOM specifies rgb(...) when the alpha
        // is 1; reported upstream. What the binding owns is the round trip, and that holds.
        fixture.Evaluate("document.querySelector('#a').style.color = 'blue'");
        fixture.Text("document.querySelector('#a').style.color").Should().Be("rgba(0, 0, 255, 1)");

        fixture.Evaluate("document.querySelector('#a').style.setProperty('font-weight', 'bold')");
        fixture.Text("document.querySelector('#a').style.getPropertyValue('font-weight')").Should().Be("bold");
    }

    [Test]
    public void InnerHtmlIsReadDirectlyAndWrittenThroughTheHook()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("document.querySelector('#a').innerHTML").Should().Be("hello <b>world</b>");

        fixture.Evaluate("document.querySelector('#a').innerHTML = '<i>x</i>'");
        fixture.Text("document.querySelector('#a').firstElementChild.tagName").Should().Be("I");
    }

    [Test]
    public void NodesAreCreatedAppendedAndRemoved()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            var li = document.createElement('li');
            li.textContent = 'four';
            document.querySelector('#list').appendChild(li);
            """);

        fixture.Number("document.querySelectorAll('#list li').length").Should().Be(4);
        fixture.Text("document.querySelector('#list').lastElementChild.textContent").Should().Be("four");

        fixture.Evaluate("document.querySelector('#list').lastElementChild.remove()");
        fixture.Number("document.querySelectorAll('#list li').length").Should().Be(3);

        fixture.Evaluate("document.querySelector('#list').removeChild(document.querySelector('#list').firstElementChild)");
        fixture.Text("document.querySelector('#list').firstElementChild.textContent").Should().Be("two");
    }

    /// <summary>
    /// <c>insertBefore(node, null)</c> appends, because WebIDL's second parameter is <c>Node?</c>.
    /// </summary>
    /// <remarks>
    /// Not an edge case: it is how a virtual DOM adds its last row, and the obstacle course's Vue and Preact
    /// fixtures both died on it with
    /// <c>TypeError: Failed to execute 'Node.insertBefore': parameter 2 is not of the expected type</c>
    /// before <c>overrides.json</c>'s <c>nullableParameters</c> table existed. <c>Node.contains(null)</c> and
    /// <c>Node.isEqualNode(null)</c> are the same rule and are <i>not</i> fixed here: AngleSharp annotates
    /// both parameters non-nullable and does not implement the null arm, so they are a row in the divergence
    /// table rather than a null this binding forwards.
    /// </remarks>
    [Test]
    public void ANullNodeIsLegalWhereWebIdlSaysItIs()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Evaluate("""
            var li = document.createElement('li');
            li.textContent = 'four';
            document.querySelector('#list').insertBefore(li, null);
            """);

        fixture.Text("document.querySelector('#list').lastElementChild.textContent").Should().Be("four");
        fixture.Number("document.querySelectorAll('#list li').length").Should().Be(4);

        // And the required half is still required: `appendChild(null)` is a TypeError, not an append.
        var refused = () => fixture.Evaluate("document.querySelector('#list').appendChild(null)");
        refused.Should().Throw<Jint.Runtime.JavaScriptException>().WithMessage("*not of the expected type*");
    }

    [Test]
    public void ReflectedAttributesRoundTripThroughTheirIdlTypes()
    {
        using var fixture = DomTestFixture.Create(Page);

        // A numeric reflected attribute.
        fixture.Number("document.querySelector('#i').maxLength").Should().Be(7);
        fixture.Number("document.querySelector('#i').tabIndex").Should().Be(3);
        fixture.Evaluate("document.querySelector('#i').tabIndex = 9");
        fixture.Text("document.querySelector('#i').getAttribute('tabindex')").Should().Be("9");

        // A boolean reflected attribute.
        fixture.Bool("document.querySelector('#i').disabled").Should().BeTrue();
        fixture.Evaluate("document.querySelector('#i').disabled = false");
        fixture.Bool("document.querySelector('#i').hasAttribute('disabled')").Should().BeFalse();

        // An enumerated one, whose IDL type is DOMString with a limited value set.
        fixture.Text("document.querySelector('#i').type").Should().Be("text");
        fixture.Evaluate("document.querySelector('#i').type = 'checkbox'");
        fixture.Text("document.querySelector('#i').getAttribute('type')").Should().Be("checkbox");

        // And a URL-valued one, which reflects as an absolute URL.
        fixture.Text("document.querySelector('#link').href").Should().Be("https://example.com/x");
    }

    [Test]
    public void AnAbsentReflectedStringAttributeReadsAsTheEmptyString()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        // DOMString, not DOMString?: the specified value is "" when the content attribute is absent, which is
        // what the conversion table's default produces. getAttribute is the nullable one.
        fixture.Text("document.querySelector('#a').className").Should().BeEmpty();
        fixture.Text("document.querySelector('#a').getAttribute('class')").Should().BeNull();
    }
}
