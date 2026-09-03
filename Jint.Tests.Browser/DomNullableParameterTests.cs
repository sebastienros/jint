namespace Jint.Tests.Browser;

/// <summary>
/// Web IDL's <c>DOMString?</c> in an argument position: <c>null</c> and <c>undefined</c> stay null, where a
/// plain <c>DOMString</c> turns them into the strings <c>"null"</c> and <c>"undefined"</c>.
/// </summary>
/// <remarks>
/// <para>
/// Which arguments those are is read from AngleSharp's own nullable-reference metadata, narrowed by
/// <c>overrides.json</c>'s <c>nonNullableParameters</c> where the annotation is wider than the standard.
/// Every namespaced member is in the first group and it is the group a page notices: <c>getAttributeNS(null,
/// name)</c> is how a library reads an attribute in no namespace, and with the argument converted as a plain
/// <c>DOMString</c> it looked for a namespace literally spelled <c>"null"</c> and answered <c>null</c> for
/// every attribute that exists.
/// </para>
/// <para>
/// The second group is here as well, because a list that only ever widens would be half a decision: the
/// tests below pin the members where the standard is <em>narrower</em> than AngleSharp's annotation, so
/// removing a row of that list fails rather than silently changing what a page reads back.
/// </para>
/// </remarks>
public sealed class DomNullableParameterTests
{
    private const string Page = "<div id='a' title='t'>x</div>";

    [Test]
    public void ANullNamespaceIsNoNamespaceRatherThanTheStringNull()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://dom.spec.whatwg.org/#dom-element-getattributens — `DOMString? namespace`.
        fixture.Text("document.getElementById('a').getAttributeNS(null, 'title')").Should().Be("t");
        fixture.Bool("document.getElementById('a').hasAttributeNS(null, 'title')").Should().BeTrue();

        fixture.Execute("document.getElementById('a').setAttributeNS(null, 'data-x', 'v');");
        fixture.Text("document.getElementById('a').getAttribute('data-x')").Should().Be("v");
        fixture.Text("document.getElementById('a').getAttributeNS(null, 'data-x')").Should().Be("v");

        fixture.Execute("document.getElementById('a').removeAttributeNS(null, 'data-x');");
        fixture.Bool("document.getElementById('a').hasAttribute('data-x')").Should().BeFalse();

        // A null namespace is "no namespace", and an HTML element is in the XHTML one, so the null lookup
        // finding nothing is what a browser answers too. What the argument must not be is the string "null":
        // that is a namespace, and one no document has.
        fixture.Number("document.getElementsByTagNameNS(null, 'div').length").Should().Be(0);
        fixture.Number("document.getElementsByTagNameNS('http://www.w3.org/1999/xhtml', 'div').length").Should().Be(1);
    }

    [Test]
    public void ANullNamespaceReachesTheMembersThatCreateAndLookUp()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://dom.spec.whatwg.org/#dom-document-createelementns: a null namespace is *no* namespace, and
        // not the namespace spelled "null" — which is what the argument used to become, and what kept every
        // custom element created through this member from ever matching a definition. The name keeps the case
        // the script wrote, because the lower-casing belongs to createElement rather than to this member.
        fixture.Text("document.createElementNS(null, 'thing').localName").Should().Be("thing");
        fixture.Text("document.createElementNS(null, 'Thing').localName").Should().Be("Thing");
        fixture.Evaluate("document.createElementNS(null, 'thing').namespaceURI").IsNull().Should().BeTrue();
        fixture.Text("document.createElement('Thing').localName").Should().Be("thing");

        fixture.Text("document.createAttributeNS(null, 'thing').name").Should().Be("thing");
        fixture.Evaluate("document.createAttributeNS(null, 'thing').namespaceURI").IsNull().Should().BeTrue();

        // https://dom.spec.whatwg.org/#dom-node-lookupprefix — `DOMString? lookupPrefix(DOMString? namespace)`.
        fixture.Evaluate("document.getElementById('a').lookupPrefix(null)").IsNull().Should().BeTrue();
    }

    [Test]
    public void AnUndefinedNamespaceIsTheSameAsANullOne()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://webidl.spec.whatwg.org/#es-nullable-type — a nullable type takes undefined as null too,
        // which is what makes `getAttributeNS(undefined, name)` and a one-argument call agree.
        fixture.Text("document.getElementById('a').getAttributeNS(undefined, 'title')").Should().Be("t");
    }

    [Test]
    public void AnArgumentTheStandardDeclaresNonNullableStillTakesTheStringNull()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://dom.spec.whatwg.org/#dom-element-setattributens — only the namespace is nullable; the value
        // is a plain DOMString, so null converts to the four characters. AngleSharp annotates the parameter
        // nullable because its own setter reads null as removing the attribute, and overrides.json's
        // nonNullableParameters is what stops that annotation reaching the binding.
        fixture.Execute("document.getElementById('a').setAttributeNS(null, 'data-y', null);");
        fixture.Text("document.getElementById('a').getAttribute('data-y')").Should().Be("null");
        fixture.Bool("document.getElementById('a').hasAttribute('data-y')").Should().BeTrue();
    }

    [Test]
    public void AReflectedContentAttributeTakesTheStringNullToo()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The other half of the same decision, and the reason the annotation is read for an operation's
        // argument only: AngleSharp annotates a hundred and fifty reflected content attributes String?, and
        // Web IDL declares every one of them a non-nullable DOMString.
        fixture.Execute("document.getElementById('a').id = null;");
        fixture.Text("document.getElementById('null').id").Should().Be("null");

        fixture.Execute("document.getElementById('null').className = null;");
        fixture.Text("document.getElementById('null').getAttribute('class')").Should().Be("null");
    }
}
