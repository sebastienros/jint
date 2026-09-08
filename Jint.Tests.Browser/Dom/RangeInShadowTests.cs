namespace Jint.Tests.Browser.Dom;

/// <summary>
/// DOM <a href="https://dom.spec.whatwg.org/#concept-node-remove">§4.2.3's removing steps</a> adjust a live
/// range's boundary points only for the node tree the removal happened in, so a range inside a shadow tree
/// is untouched when the host — or an ancestor of it — leaves the document.
/// </summary>
/// <remarks>
/// <b>This is here because the browser wpt lane cannot ask the question.</b>
/// <c>dom/ranges/Range-in-shadow-after-the-shadow-removed.html</c> takes its mode from
/// <c>&lt;meta name="variant"&gt;</c>, which upstream's runner appends to the URL as a query string and this
/// lane does not — so the document runs with <c>location.search</c> empty, calls
/// <c>attachShadow({mode: null})</c> and fails on the enum conversion before it reaches its subject. Its two
/// rows stay excluded for exactly that reason and this pins what they were meant to measure.
/// </remarks>
public sealed class RangeInShadowTests
{
    [TestCase("open")]
    [TestCase("closed")]
    public void ARangeInAShadowTreeSurvivesTheHostBeingRemoved(string mode)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body></body></html>");

        fixture.Text(Probe(mode, remove: "host")).Should().Be("true:1");
    }

    [TestCase("open")]
    [TestCase("closed")]
    public void ARangeInAShadowTreeSurvivesTheHostsParentBeingRemoved(string mode)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><html><body></body></html>");

        fixture.Text(Probe(mode, remove: "wrapper")).Should().Be("true:1");
    }

    private static string Probe(string mode, string remove)
        => """
           (function () {
             const wrapper = document.createElement("div");
             const host = document.createElement("div");
             const root = host.attachShadow({mode: "MODE"});
             root.innerHTML = '<div id="in-shadow">ABC</div>';
             wrapper.appendChild(host);
             document.body.appendChild(wrapper);
             const range = document.createRange();
             range.setStart(root.firstChild, 1);
             REMOVE.remove();
             return (range.startContainer === root.firstChild) + ':' + range.startOffset;
           })()
           """
            .Replace("MODE", mode, System.StringComparison.Ordinal)
            .Replace("REMOVE", remove, System.StringComparison.Ordinal);
}
