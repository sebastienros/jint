namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#dom-domimplementation-hasfeature">DOM §4.5's <c>hasFeature</c></a>,
/// which is one step: return true.
/// </summary>
/// <remarks>
/// It is a legacy member kept for one reason, which the standard states itself: old feature-detection code
/// gates on it, and a browser answering true is what makes that code take the modern path. AngleSharp
/// answered true for three of the 136 pairs <c>DOMImplementation-hasFeature.html</c> asks about and false for
/// the rest, so a page asking <c>hasFeature('Core', '3.0')</c> took its fallback.
/// </remarks>
public sealed class DomImplementationTests
{
    private const string Page = "<!doctype html><html><body></body></html>";

    [TestCase("document.implementation.hasFeature()")]
    [TestCase("document.implementation.hasFeature('Core', '3.0')")]
    [TestCase("document.implementation.hasFeature('XML', '1.0')")]
    [TestCase("document.implementation.hasFeature('nonsense')")]
    [TestCase("document.implementation.hasFeature('', '')")]
    [TestCase("document.implementation.hasFeature(null, null)")]
    [TestCase("document.implementation.hasFeature(undefined, undefined)")]
    public void HasFeatureIsUnconditionallyTrue(string source)
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool(source).Should().BeTrue();
    }

    /// <summary>
    /// Its IDL takes no arguments at all, so the member's <c>length</c> is 0 — which is the half that made
    /// the no-argument call a <c>TypeError</c> rather than a wrong answer.
    /// </summary>
    [Test]
    public void ItsArityIsZero()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Number("document.implementation.hasFeature.length").Should().Be(0);
    }
}
