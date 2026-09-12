namespace Jint.Tests.Browser.Dom;

/// <summary>
/// A supported property name that spells a canonical array index, on the two collections that have named
/// properties at all.
/// </summary>
/// <remarks>
/// <a href="https://webidl.spec.whatwg.org/#legacy-platform-object-getownproperty">WebIDL's
/// <c>[[GetOwnProperty]]</c></a> answers an array-index key from the indexed getter and <b>stops</b>, so a
/// supported name spelling one is unreachable as a property — and <c>[[OwnPropertyKeys]]</c> leaves it out
/// for exactly that reason. <c>namedItem</c> has no such rule in front of it and still finds the element.
/// <para>
/// The engine says the same thing from its side: <c>ArrayLikeObject</c> reserves canonical indices and
/// <c>length</c> from the named projection, and with host-contract verification on it <i>fails</i> a
/// projection that advertises one — which is what <c>&lt;div id="0"&gt;</c> did to
/// <c>Object.getOwnPropertyNames(el.children)</c> before this rule was applied on the binding's side.
/// </para>
/// </remarks>
public sealed class HtmlCollectionIndexLikeNameTests
{
    private const string Markup = """
        <!doctype html><html><body><div id="0"></div><div id="4294967294"></div>
        <div id="4294967295"></div><div id="043"></div><div id="x"></div></body></html>
        """;

    [TestCase("document.body.children")]
    [TestCase("document.all")]
    public void AnIdSpellingAnArrayIndexIsNotAKeyButIsStillANamedItem(string collection)
    {
        using var fixture = DomTestFixture.Create(Markup);

        // "0" is a key of the collection because it is an *index*, and it must appear exactly once — the
        // projection advertising it as a name too is what put a duplicate into [[OwnPropertyKeys]].
        fixture.Number($"Object.getOwnPropertyNames({collection}).filter(n => n === '0').length").Should().Be(1);

        // "4294967294" is a canonical array index far past the end, so it is not a key at all.
        fixture.Bool($"Object.getOwnPropertyNames({collection}).includes('4294967294')").Should().BeFalse();

        // ... and the three that are not are present, so the rule is the array-index one and not a filter on
        // digits: 2^32-1 is one past the last array index, and "043" is not canonical.
        fixture.Bool($"Object.getOwnPropertyNames({collection}).includes('4294967295')").Should().BeTrue();
        fixture.Bool($"Object.getOwnPropertyNames({collection}).includes('043')").Should().BeTrue();
        fixture.Bool($"Object.getOwnPropertyNames({collection}).includes('x')").Should().BeTrue();

        // The property read goes to the indexed half, and namedItem to the named one.
        fixture.Bool($"{collection}['0'] === {collection}.item(0)").Should().BeTrue();
        fixture.Bool($"{collection}['4294967294'] === undefined").Should().BeTrue();
        fixture.Text($"{collection}.namedItem('0').id").Should().Be("0");
        fixture.Text($"{collection}.namedItem('4294967294').id").Should().Be("4294967294");
    }
}
