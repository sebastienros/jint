namespace Jint.Tests.Browser;

/// <summary>
/// A spot-check of HTML's <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes">reflection</a>
/// rules — the semantics web-platform-tests' <c>html/dom/reflection-*</c> files check exhaustively, five
/// attributes at a time, until the browser lane (campaign item R8) can run those files themselves.
/// </summary>
/// <remarks>
/// Five kinds, one attribute each: <c>DOMString</c>, URL, <c>boolean</c>, <c>long</c>, and enumerated with an
/// invalid-value default. They are here because they are the five conversion-table rows a reflected attribute
/// can take, and because getting one of them wrong is invisible until a real page reads it.
/// </remarks>
public sealed class DomReflectionTests
{
    [Test]
    public void ADomStringAttributeReflectsTheContentAttributeAndDefaultsToEmpty()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Text("document.querySelector('#a').title").Should().BeEmpty();

        fixture.Evaluate("document.querySelector('#a').title = 'hello'");
        fixture.Text("document.querySelector('#a').getAttribute('title')").Should().Be("hello");
        fixture.Text("document.querySelector('#a').title").Should().Be("hello");

        // A number assigned to a DOMString attribute is stringified, per WebIDL's ToString.
        fixture.Evaluate("document.querySelector('#a').title = 42");
        fixture.Text("document.querySelector('#a').getAttribute('title')").Should().Be("42");
    }

    [Test]
    public void AUrlAttributeReflectsAnAbsoluteUrl()
    {
        using var fixture = DomTestFixture.Create("<a id='a' href='/relative'>x</a>");

        // The document has no base URL of its own here (it was opened from a string), so what matters is that
        // the getter runs the URL parser rather than handing back the content attribute.
        fixture.Text("document.querySelector('#a').getAttribute('href')").Should().Be("/relative");

        fixture.Evaluate("document.querySelector('#a').href = 'https://example.com/x?y#z'");
        fixture.Text("document.querySelector('#a').href").Should().Be("https://example.com/x?y#z");
        fixture.Text("document.querySelector('#a').getAttribute('href')").Should().Be("https://example.com/x?y#z");
    }

    [Test]
    public void ABooleanAttributeReflectsPresence()
    {
        using var fixture = DomTestFixture.Create("<input id='i'>");

        fixture.Bool("document.querySelector('#i').disabled").Should().BeFalse();

        fixture.Evaluate("document.querySelector('#i').disabled = true");
        fixture.Bool("document.querySelector('#i').hasAttribute('disabled')").Should().BeTrue();
        fixture.Text("document.querySelector('#i').getAttribute('disabled')").Should().BeEmpty();

        // Presence, not value: `disabled="false"` is still disabled.
        fixture.Evaluate("document.querySelector('#i').setAttribute('disabled', 'false')");
        fixture.Bool("document.querySelector('#i').disabled").Should().BeTrue();

        fixture.Evaluate("document.querySelector('#i').disabled = false");
        fixture.Bool("document.querySelector('#i').hasAttribute('disabled')").Should().BeFalse();
    }

    [Test]
    public void ALongAttributeConvertsThroughToInt32()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div>");

        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(0);

        fixture.Evaluate("document.querySelector('#a').tabIndex = 5");
        fixture.Text("document.querySelector('#a').getAttribute('tabindex')").Should().Be("5");

        // WebIDL's `long` is ToInt32, so a string, a float and a boolean all have defined answers.
        fixture.Evaluate("document.querySelector('#a').tabIndex = '7'");
        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(7);

        fixture.Evaluate("document.querySelector('#a').tabIndex = 3.9");
        fixture.Number("document.querySelector('#a').tabIndex").Should().Be(3);
    }

    [Test]
    public void AnEnumeratedAttributeFallsBackToItsInvalidValueDefault()
    {
        using var fixture = DomTestFixture.Create("<input id='i' type='email'>");

        fixture.Text("document.querySelector('#i').type").Should().Be("email");

        // https://html.spec.whatwg.org/multipage/input.html#attr-input-type — the invalid value default and
        // the missing value default are both "text".
        fixture.Evaluate("document.querySelector('#i').setAttribute('type', 'not-a-type')");
        fixture.Text("document.querySelector('#i').type").Should().Be("text");

        fixture.Evaluate("document.querySelector('#i').removeAttribute('type')");
        fixture.Text("document.querySelector('#i').type").Should().Be("text");

        // And the reflected value is ASCII-lowercased.
        fixture.Evaluate("document.querySelector('#i').setAttribute('type', 'CHECKBOX')");
        fixture.Text("document.querySelector('#i').type").Should().Be("checkbox");
    }
}
