namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-nonelementparentnode">DOM §4.2.4</a>'s
/// <c>getElementById</c> on both of the nodes that have it, and DOM §4.9's definition of an element's ID.
/// </summary>
/// <remarks>
/// An element's ID is <i>unset</i> while its <c>id</c> content attribute is absent or empty, so the empty
/// string can never be any element's ID. AngleSharp compares the attribute value, which made the empty string
/// match the first element with no <c>id</c> at all — the document element of an ordinary page.
/// </remarks>
public sealed class NonElementParentNodeTests
{
    [Test]
    public void AnEmptyIdMatchesNothingInADocument()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body><div id=''></div></body></html>");

        fixture.Evaluate("document.getElementById('')").IsNull().Should().BeTrue();
    }

    [Test]
    public void AnEmptyIdMatchesNothingInAFragment()
    {
        using var fixture = DomTestFixture.Create("<body></body>");

        fixture.Evaluate(
            """
            var fragment = document.createDocumentFragment();
            var child = document.createElement('div');
            child.setAttribute('id', '');
            fragment.appendChild(child);
            fragment.getElementById('');
            """).IsNull().Should().BeTrue();
    }

    [Test]
    public void ANonEmptyIdStillMatches()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body><div id='here'></div></body></html>");

        fixture.Text("document.getElementById('here').tagName").Should().Be("DIV");
        fixture.Evaluate("document.getElementById('nowhere')").IsNull().Should().BeTrue();
    }
}
