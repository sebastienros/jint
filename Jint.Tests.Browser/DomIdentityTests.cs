namespace Jint.Tests.Browser;

/// <summary>
/// Wrapper identity: one JavaScript object per DOM object, for as long as the DOM object lives.
/// </summary>
public sealed class DomIdentityTests
{
    private const string Page = "<div id='a' class='c'>x</div><div id='b'>y</div>";

    [Test]
    public void TheSameNodeIsTheSameObjectEveryTimeItIsReached()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("document.querySelector('#a') === document.querySelector('#a')").Should().BeTrue();
        fixture.Bool("document.querySelector('#a') === document.getElementById('a')").Should().BeTrue();
        fixture.Bool("document.querySelector('#a') === document.body.firstElementChild").Should().BeTrue();
        fixture.Bool("document.querySelector('#a') === document.querySelector('#b')").Should().BeFalse();
    }

    [Test]
    public void AnExpandoSurvivesARoundTripThroughTheDom()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The reason wrapper identity matters at all: React and Vue hang state off a node and expect to find
        // it again when they next reach the node from the tree.
        fixture.Evaluate("document.querySelector('#a').__state = { count: 1 };");
        fixture.Number("document.querySelector('#a').__state.count").Should().Be(1);

        fixture.Evaluate("document.body.firstElementChild.__state.count++;");
        fixture.Number("document.querySelector('#a').__state.count").Should().Be(2);
    }

    [Test]
    public void AShortLivedViewKeepsOneWrapperForAsLongAsAngleSharpKeepsOneObject()
    {
        using var fixture = DomTestFixture.Create(Page);

        // AngleSharp hands back the same DOMTokenList and the same DOMStringMap for one element, so the
        // wrapper cache gives them one identity too.
        fixture.Bool("document.querySelector('#a').classList === document.querySelector('#a').classList").Should().BeTrue();
        fixture.Bool("document.querySelector('#a').dataset === document.querySelector('#a').dataset").Should().BeTrue();

        // It does NOT for a query result, because AngleSharp builds a fresh collection per call. A browser
        // answers true here; the divergence is AngleSharp's and is recorded in Jint.Browser/AGENTS.md.
        fixture.Bool("document.querySelectorAll('div') === document.querySelectorAll('div')").Should().BeFalse();
    }

    [Test]
    public void TheDocumentItselfIsAWrapperLikeAnyOther()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("document.body.ownerDocument === document").Should().BeTrue();
        fixture.Bool("document instanceof Document").Should().BeTrue();
        fixture.Bool("document instanceof Node").Should().BeTrue();
        fixture.Bool("document instanceof EventTarget").Should().BeTrue();
    }
}
