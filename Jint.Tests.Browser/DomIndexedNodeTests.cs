namespace Jint.Tests.Browser;

/// <summary>
/// The two nodes whose interface also supports indexed or named properties, and the union-typed operations
/// AngleSharp spells as two overloads.
/// </summary>
/// <remarks>
/// A node's wrapper is what the engine's tree-dispatch lane keys on and the wrapper cache keeps exactly one
/// per node, so a form cannot be an <c>ArrayLikeObject</c> without ceasing to be an <c>EventTarget</c> the
/// dispatcher can walk. <c>DomIndexedNodeObject</c> is the answer: a node wrapper with the interface's
/// generated projection on top, which is why the last test here is about dispatch rather than about
/// properties.
/// </remarks>
public sealed class DomIndexedNodeTests
{
    private const string Page = """
        <form id="f">
          <input name="username" id="u" value="ada">
          <input name="password" type="password">
        </form>
        <select id="s"><option value="a">A</option><option value="b">B</option></select>
        """;

    [Test]
    public void AFormAnswersItsControlsByIndexAndByName()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://html.spec.whatwg.org/multipage/forms.html#dom-form-item and #dom-form-nameditem
        fixture.Text("document.getElementById('f')[0].name").Should().Be("username");
        fixture.Text("document.getElementById('f')[1].name").Should().Be("password");
        fixture.Text("document.getElementById('f').username.value").Should().Be("ada");
        fixture.Text("document.getElementById('f').u.name").Should().Be("username", "an id is a supported name too");
        fixture.Evaluate("document.getElementById('f').nope").IsUndefined().Should().BeTrue();

        // The same values the collection beside it carries, which is what a page used to have to reach for.
        fixture.Bool("document.getElementById('f')[0] === document.getElementById('f').elements[0]").Should().BeTrue();
        fixture.Bool("document.getElementById('f').username === document.getElementById('f').elements.namedItem('username')").Should().BeTrue();
    }

    [Test]
    public void ASelectAnswersItsOptionsByIndex()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://html.spec.whatwg.org/multipage/form-elements.html#dom-select-item
        fixture.Text("document.getElementById('s')[1].value").Should().Be("b");
        fixture.Evaluate("document.getElementById('s')[9]").IsUndefined().Should().BeTrue();

        // An indexed getter is all HTML gives a select, so a name is nothing special on it.
        fixture.Evaluate("document.getElementById('s').a").IsUndefined().Should().BeTrue();

        // The Array.prototype generics reach it through the projection, index by index.
        fixture.Text("Array.prototype.map.call(document.getElementById('s'), o => o.value).join(',')").Should().Be("a,b");
    }

    [Test]
    public void TheProjectedPropertiesAreCoherentWithEveryWayOfAskingAboutThem()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The obligation Jint/Native/Object/AGENTS.md states: a name GetOwnProperty answers has to be a name
        // GetOwnPropertyKeys lists, or hasOwnProperty and getOwnPropertyNames disagree about one object.
        fixture.Text("JSON.stringify(Object.getOwnPropertyNames(document.getElementById('f')))")
            .Should().Be("[\"0\",\"1\",\"username\",\"u\",\"password\"]");
        fixture.Bool("document.getElementById('f').hasOwnProperty('username')").Should().BeTrue();
        fixture.Bool("'username' in document.getElementById('f')").Should().BeTrue();
        fixture.Bool("'nope' in document.getElementById('f')").Should().BeFalse();

        // https://webidl.spec.whatwg.org/#legacy-platform-object-getownproperty — a supported index with no
        // setter is read-only, so an assignment is refused rather than shadowing the projection.
        fixture.Bool("Object.getOwnPropertyDescriptor(document.getElementById('f'), '0').writable").Should().BeFalse();
        fixture.Bool("Object.getOwnPropertyDescriptor(document.getElementById('f'), '0').configurable").Should().BeTrue();
        fixture.Execute("document.getElementById('f')[0] = 5;");
        fixture.Text("document.getElementById('f')[0].name").Should().Be("username");

        // An expando is still an ordinary own property, and it enumerates after the projection.
        fixture.Execute("document.getElementById('f').expando = 1;");
        fixture.Text("Object.keys(document.getElementById('f')).join(',')").Should().Be("0,1,username,u,password,expando");
    }

    [Test]
    public void AProjectedNodeIsStillANodeAndAnEventTarget()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The whole reason the projection rides on the node wrapper rather than replacing it.
        fixture.Bool("document.getElementById('f') instanceof HTMLFormElement").Should().BeTrue();
        fixture.Bool("document.getElementById('f') instanceof EventTarget").Should().BeTrue();
        fixture.Text("document.getElementById('f').nodeName").Should().Be("FORM");
        fixture.Bool("document.getElementById('f') === document.forms[0]").Should().BeTrue("a node keeps one wrapper");

        fixture.Number("""
            (function () {
              let hits = 0;
              const form = document.getElementById('f');
              form.addEventListener('probe', () => hits++);
              form.querySelector('input').dispatchEvent(new Event('probe', { bubbles: true }));
              return hits;
            })()
            """).Should().Be(1, "the event path still walks up through the form");
    }

    [Test]
    public void AUnionTypedOperationTakesBothArmsOfItsUnion()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://html.spec.whatwg.org/multipage/form-elements.html#dom-select-add —
        // `(HTMLOptionElement or HTMLOptGroupElement)`, which AngleSharp models as two overloads sharing one
        // [DomName]. The generator binds one member per name, so the optgroup arm used to be a TypeError.
        fixture.Execute("const g = document.createElement('optgroup'); g.label = 'G'; document.getElementById('s').add(g);");
        fixture.Text("document.getElementById('s').lastElementChild.tagName").Should().Be("OPTGROUP");

        fixture.Execute("const o = document.createElement('option'); o.value = 'c'; document.getElementById('s').add(o);");
        fixture.Number("document.getElementById('s').options.length").Should().Be(3);

        // The same union on HTMLOptionsCollection.
        fixture.Execute("document.getElementById('s').options.add(document.createElement('optgroup'));");
        fixture.Number("document.getElementById('s').querySelectorAll('optgroup').length").Should().Be(2);

        // An argument that is neither arm is still WebIDL's conversion failure.
        fixture.Text("(() => { try { document.getElementById('s').add(document.createElement('div')); return 'accepted'; } catch (e) { return e.name; } })()")
            .Should().Be("TypeError");
    }

    [Test]
    public void AVariadicNodeParameterTakesStringsAsTextNodes()
    {
        using var fixture = DomTestFixture.Create("<div id='a'></div><p id='b'>x</p>");

        // https://dom.spec.whatwg.org/#converting-nodes-into-a-node — `(Node or DOMString)...`, where the
        // string half becomes a Text node in the receiver's node document. AngleSharp's signature is INode[],
        // so a string argument used to be a TypeError where a browser inserts text.
        fixture.Execute("document.getElementById('a').append('one', document.createElement('span'), 2);");
        fixture.Text("document.getElementById('a').innerHTML").Should().Be("one<span></span>2");
        fixture.Number("document.getElementById('a').childNodes.length").Should().Be(3);
        fixture.Number("document.getElementById('a').firstChild.nodeType").Should().Be(3, "a text node");

        fixture.Execute("document.getElementById('b').before('lead');");
        fixture.Text("document.getElementById('b').previousSibling.data").Should().Be("lead");

        fixture.Execute("document.getElementById('a').prepend('first');");
        fixture.Text("document.getElementById('a').firstChild.data").Should().Be("first");

        // A DocumentFragment is a node document's too, so the same conversion reaches it.
        fixture.Text("(() => { const f = new DocumentFragment(); f.append('in a fragment'); return f.textContent; })()")
            .Should().Be("in a fragment");
    }
}
