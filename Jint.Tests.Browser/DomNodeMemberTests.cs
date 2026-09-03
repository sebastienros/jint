namespace Jint.Tests.Browser;

/// <summary>
/// The <c>Node</c> and <c>Element</c> members the DOM standard requires and AngleSharp has no
/// <c>[DomName]</c> for — <c>Dom/DomElementMembers</c> — plus the two whose only fault was their name.
/// </summary>
public sealed class DomNodeMemberTests
{
    private const string Page =
        """
        <!doctype html>
        <html><body>
          <div id="host" class="c" data-x="1"><b id="b">one</b><i id="i">two</i></div>
          <p id="p">text</p>
        </body></html>
        """;

    [Test]
    public void ReplaceWithSwapsTheChildForItsArguments()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.2.7 spells it `replaceWith`; AngleSharp's IChildNode carries [DomName("replace")], so the
        // mixin projected a name the standard does not have.
        fixture.Text(
            """
            var host = document.getElementById('host');
            var b = document.getElementById('b');
            b.replaceWith(document.createElement('u'), document.createElement('s'));
            [host.innerHTML, typeof b.replace, typeof document.getElementById('p').replaceWith].join('|');
            """)
            .Should().Be("<u></u><s></s><i id=\"i\">two</i>|undefined|function");
    }

    [Test]
    public void HasAttributesAndGetAttributeNamesReadTheAttributeList()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var host = document.getElementById('host');
            [host.hasAttributes(), document.createElement('div').hasAttributes(),
             host.getAttributeNames().join(','), Array.isArray(host.getAttributeNames())].join('|');
            """)
            .Should().Be("true|false|id,class,data-x|true");
    }

    [Test]
    public void ToggleAttributeTellsAForceThatWasGivenFromOneThatWasNot()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var e = document.createElement('div');
            var log = [];
            log.push(e.toggleAttribute('hidden'), e.getAttribute('hidden'));
            log.push(e.toggleAttribute('hidden'), e.hasAttribute('hidden'));
            log.push(e.toggleAttribute('hidden', false), e.hasAttribute('hidden'));
            log.push(e.toggleAttribute('hidden', true), e.hasAttribute('hidden'));
            log.push(e.toggleAttribute('hidden', true), e.hasAttribute('hidden'));
            log.join('|');
            """)
            // The new attribute's value is the empty string, and `force` decides in both directions.
            .Should().Be("true||false|false|false|false|true|true|true|true");
    }

    [Test]
    public void ToggleAttributeLowerCasesTheNameOfAnHtmlElementsAttribute()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var e = document.createElement('div');
            e.toggleAttribute('FOO');
            [e.hasAttribute('foo'), e.getAttributeNames().join(',')].join('|');
            """)
            .Should().Be("true|foo");
    }

    [Test]
    public void TheAttrNodeFamilyReadsWritesAndRemoves()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var host = document.getElementById('host');
            var id = host.getAttributeNode('id');
            var log = [id.name, id.value, id.ownerElement === host];

            var made = document.createAttribute('data-y');
            made.value = '2';
            log.push(host.setAttributeNode(made), host.getAttribute('data-y'));

            var removed = host.removeAttributeNode(made);
            log.push(removed === made, host.hasAttribute('data-y'));
            log.join('|');
            """)
            .Should().Be("id|host|true||2|true|false");
    }

    [Test]
    public void TheNamespacedAttrNodeMembersUseTheNamespace()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var e = document.createElement('div');
            var attr = document.createAttributeNS('http://example.com/ns', 'p:a');
            attr.value = 'v';
            e.setAttributeNodeNS(attr);
            [e.getAttributeNodeNS('http://example.com/ns', 'a').value,
             e.getAttributeNodeNS(null, 'a'),
             e.getAttributeNS('http://example.com/ns', 'a')].join('|');
            """)
            .Should().Be("v||v");
    }

    [Test]
    public void RemovingAnAttributeNodeTheElementDoesNotHoldIsANotFoundError()
    {
        using var fixture = DomTestFixture.Create(Page);

        // DOM §4.9 removes an attribute *node*, so one that belongs elsewhere is a refusal rather than a
        // silent miss on a name that happens to match.
        fixture.Text(
            """
            (() => {
              var e = document.createElement('div');
              var other = document.createElement('div');
              other.setAttribute('a', '1');
              try { e.removeAttributeNode(other.getAttributeNode('a')) } catch (err) { return err.name }
              return 'no throw';
            })()
            """)
            .Should().Be("NotFoundError");
    }

    [Test]
    public void InsertAdjacentPutsANodeAtEachOfTheFourPositions()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var host = document.getElementById('host');
            var b = document.getElementById('b');
            var made = document.createElement('u');
            var back = b.insertAdjacentElement('beforebegin', made);
            b.insertAdjacentElement('afterbegin', document.createElement('s'));
            b.insertAdjacentText('beforeend', 'end');
            b.insertAdjacentText('afterend', 'after');
            [host.innerHTML, back === made].join('|');
            """)
            .Should().Be("<u></u><b id=\"b\"><s></s>oneend</b>after<i id=\"i\">two</i>|true");
    }

    [Test]
    public void InsertAdjacentRefusesAnUnknownPositionAndAnswersNullWithNoParent()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            (() => {
              var e = document.createElement('div');
              try { e.insertAdjacentElement('somewhere', document.createElement('u')) } catch (err) { return err.name }
              return 'no throw';
            })()
            """)
            .Should().Be("SyntaxError");

        // Case-insensitive, which is what a page written against the original IE spelling relies on.
        fixture.Text(
            """
            var e = document.createElement('div');
            [e.insertAdjacentElement('BeforeBegin', document.createElement('u')),
             e.insertAdjacentElement('AfterEnd', document.createElement('s'))].join('|');
            """)
            .Should().Be("|", "an element with no parent has nowhere to put a sibling, so both answer null");
    }

    [Test]
    public void IsSameNodeComparesIdentityAndNotEquality()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var b = document.getElementById('b');
            var twin = b.cloneNode(true);
            [b.isSameNode(b), b.isSameNode(twin), b.isEqualNode(twin), b.isSameNode(null)].join('|');
            """)
            .Should().Be("true|false|true|false");
    }

    [Test]
    public void WebkitMatchesSelectorIsTheAliasTheStandardRequires()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var host = document.getElementById('host');
            [host.webkitMatchesSelector('.c'), host.webkitMatchesSelector('p'),
             host.webkitMatchesSelector === host.matches].join('|');
            """)
            // The alias is a member of its own, not the same function object: WebIDL declares two operations.
            .Should().Be("true|false|false");

        fixture.Text(
            "(() => { try { document.body.webkitMatchesSelector('[') } catch (e) { return e.name } return 'no throw' })()")
            .Should().Be("SyntaxError", "the alias refuses a bad selector the way matches does");
    }

    [Test]
    public void ANodeListCarriesWebIdlsValueIterator()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text(
            """
            var nodes = document.getElementById('host').childNodes;
            [nodes.keys === Array.prototype.keys,
             nodes.values === Array.prototype.values,
             nodes.entries === Array.prototype.entries,
             nodes.forEach === Array.prototype.forEach,
             [...nodes.keys()].join(','),
             [...nodes.values()].map(n => n.nodeName).join(',')].join('|');
            """)
            .Should().Be("true|true|true|true|0,1|B,I");
    }
}
