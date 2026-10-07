namespace Jint.Tests.Browser.Dom;

/// <summary>
/// DOM's HTMLCollection and HTML's HTMLAllCollection use content attributes (null namespace,
/// local name), rather than Element.getAttribute's qualified-name lookup.
/// https://dom.spec.whatwg.org/#interface-htmlcollection
/// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#htmlallcollection
/// </summary>
public sealed class CollectionContentAttributeTests
{
    [TestCase("name", "name")]
    [TestCase("name", "p:name")]
    [TestCase("id", "id")]
    [TestCase("id", "p:id")]
    public void NamespacedAttributesNeitherMatchNorHideContentAttributes(string localName, string qualifiedName)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><div id=root></div>");
        fixture.Engine.SetValue("local", localName);
        fixture.Engine.SetValue("qualified", qualifiedName);
        fixture.Bool(
            """
            var root = document.getElementById('root');
            var image = document.createElement('img');
            image.setAttributeNS('urn:foreign', qualified, 'foreign');
            root.appendChild(image);
            var collections = [root.children, root.getElementsByTagName('img'), document.all];
            function absent(c, key) {
                return c.namedItem(key) === null && c[key] === undefined && !(key in c)
                    && Object.getOwnPropertyDescriptor(c, key) === undefined
                    && !Object.getOwnPropertyNames(c).includes(key);
            }
            function present(c, key) {
                return c.namedItem(key) === image && c[key] === image && key in c
                    && Object.getOwnPropertyDescriptor(c, key).value === image
                    && Object.getOwnPropertyNames(c).includes(key);
            }
            var ok = collections.every(c => absent(c, 'foreign'));
            // The foreign attribute precedes a same-local-name content attribute in native storage.
            image.setAttributeNS(null, local, 'content');
            ok &&= collections.every(c => absent(c, 'foreign') && present(c, 'content'));
            image.removeAttributeNS(null, local);
            ok &&= collections.every(c => absent(c, 'content') && absent(c, 'foreign'));
            // Reverse insertion order must give the same answer.
            image.removeAttributeNS('urn:foreign', local);
            image.setAttributeNS(null, local, 'content');
            image.setAttributeNS('urn:foreign', qualified, 'foreign');
            ok && collections.every(c => absent(c, 'foreign') && present(c, 'content'));
            """).Should().BeTrue();
    }

    [TestCase("name")]
    [TestCase("p:name")]
    public void AllNamedSubcollectionsStayLiveAndExcludeNamespacedNames(string qualifiedName)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><body><div id=root></div>");
        fixture.Engine.SetValue("qualified", qualifiedName);
        fixture.Bool(
            """
            var root = document.getElementById('root');
            var foreign = document.createElement('img');
            foreign.setAttributeNS('urn:foreign', qualified, 'target');
            var first = document.createElement('img');
            first.setAttribute('name', 'target');
            var second = document.createElement('img');
            second.setAttribute('name', 'target');
            root.append(foreign, first, second);
            var property = document.all.target;
            var named = document.all.namedItem('target');
            var item = document.all.item('target');
            var called = document.all('target');
            var lists = [property, named, item, called];
            var ok = lists.every(c => c.length === 2 && c[0] === first && c[1] === second);
            first.removeAttribute('name');
            ok &&= lists.every(c => c.length === 1 && c[0] === second);
            ok &&= document.all.target === second;
            second.removeAttribute('name');
            ok &&= lists.every(c => c.length === 0);
            ok && document.all.namedItem('target') === null && document.all.target === undefined
                && !Object.getOwnPropertyNames(document.all).includes('target');
            """).Should().BeTrue();
    }
}
