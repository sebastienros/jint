namespace Jint.Tests.Browser;

/// <summary>
/// What the node wrappers get from the engine's tree-aware event dispatcher for free, now that
/// <c>DomNodeObject</c> answers <c>IsNode</c> and <c>GetParent</c>.
/// </summary>
/// <remarks>
/// Nothing in this package dispatches an event: every one of these is Jint's own DOM §2.9 dispatch walking a
/// path built from the AngleSharp tree. That is the point — the seam the engine grew is enough, and
/// AngleSharp's own event bus is neither observed nor driven.
/// </remarks>
public sealed class DomEventTests
{
    private const string Page = "<div id='outer'><div id='inner'><span id='leaf'>x</span></div></div>";

    [Test]
    public void AnEventDispatchedOnALeafBubblesToTheDocument()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var seen = [];
            document.addEventListener('ping', e => seen.push('document:' + e.currentTarget.nodeName));
            document.querySelector('#outer').addEventListener('ping', e => seen.push('outer'));
            document.querySelector('#inner').addEventListener('ping', e => seen.push('inner'));
            document.querySelector('#leaf').addEventListener('ping', e => seen.push('leaf'));

            document.querySelector('#leaf').dispatchEvent(new Event('ping', { bubbles: true }));
            seen.join(',');
            """).Should().Be("leaf,inner,outer,document:#document");
    }

    [Test]
    public void CaptureRunsFromTheDocumentDown()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var seen = [];
            document.addEventListener('ping', () => seen.push('document-capture'), true);
            document.querySelector('#outer').addEventListener('ping', () => seen.push('outer-capture'), true);
            document.querySelector('#leaf').addEventListener('ping', () => seen.push('leaf'));
            document.querySelector('#outer').addEventListener('ping', () => seen.push('outer-bubble'));

            document.querySelector('#leaf').dispatchEvent(new Event('ping', { bubbles: true }));
            seen.join(',');
            """).Should().Be("document-capture,outer-capture,leaf,outer-bubble");
    }

    [Test]
    public void TargetAndCurrentTargetAreWhatTheSpecificationSays()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var result;
            document.querySelector('#outer').addEventListener('ping', function (e) {
                result = [e.target.id, e.currentTarget.id, this.id, e.eventPhase].join('|');
            });

            document.querySelector('#leaf').dispatchEvent(new Event('ping', { bubbles: true }));
            result;
            """).Should().Be("leaf|outer|outer|3");
    }

    [Test]
    public void ComposedPathIsTheWholeTreePathUpToTheDocument()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var path;
            document.querySelector('#leaf').addEventListener('ping', e => {
                path = e.composedPath().map(t => t.nodeName || String(t)).join(',');
            });

            document.querySelector('#leaf').dispatchEvent(new Event('ping', { bubbles: true }));
            path;
            """).Should().Be("SPAN,DIV,DIV,BODY,HTML,#document");
    }

    [Test]
    public void StopPropagationEndsTheWalk()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var seen = [];
            document.addEventListener('ping', () => seen.push('document'));
            document.querySelector('#inner').addEventListener('ping', e => { seen.push('inner'); e.stopPropagation(); });
            document.querySelector('#leaf').addEventListener('ping', () => seen.push('leaf'));

            document.querySelector('#leaf').dispatchEvent(new Event('ping', { bubbles: true }));
            seen.join(',');
            """).Should().Be("leaf,inner");
    }

    [Test]
    public void ANonBubblingEventReachesOnlyItsTarget()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var seen = [];
            document.querySelector('#outer').addEventListener('ping', () => seen.push('outer'));
            document.querySelector('#leaf').addEventListener('ping', () => seen.push('leaf'));

            document.querySelector('#leaf').dispatchEvent(new Event('ping'));
            seen.join(',');
            """).Should().Be("leaf");
    }

    [Test]
    public void ADetachedNodeDispatchesToItselfAlone()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("""
            var seen = [];
            var orphan = document.createElement('div');
            document.addEventListener('ping', () => seen.push('document'));
            orphan.addEventListener('ping', () => seen.push('orphan'));

            orphan.dispatchEvent(new Event('ping', { bubbles: true }));
            seen.join(',');
            """).Should().Be("orphan");
    }

    [Test]
    public void AnEventFromAnOpenShadowTreeRetargetsAtTheHost()
    {
        using var fixture = DomTestFixture.Create(Page);

        // The three shadow seams DomNodeObject answers — IsShadowRoot, IsClosedShadowRoot and ShadowHost —
        // are read by nothing in this package; the engine's dispatcher is their only consumer. This is what
        // proves they are wired: a listener outside the shadow tree sees the host as the target, and one
        // inside sees the real node.
        fixture.Text("""
            var host = document.querySelector('#inner');
            var root = host.attachShadow({ mode: 'open' });
            var inner = document.createElement('span');
            root.appendChild(inner);

            var seen = [];
            document.querySelector('#outer').addEventListener('ping', e => seen.push('outer:' + e.target.id));
            inner.addEventListener('ping', e => seen.push('inner:' + e.target.nodeName));

            inner.dispatchEvent(new Event('ping', { bubbles: true, composed: true }));
            seen.join(',');
            """).Should().Be("inner:SPAN,outer:inner");
    }

    [Test]
    public void AClosedShadowTreeIsHiddenFromAComposedPathOutsideIt()
    {
        using var fixture = DomTestFixture.Create(Page);

        // IsClosedShadowRoot is the only thing that separates this from the test above, and what it decides is
        // exactly what composedPath() shows an outside listener.
        fixture.Text("""
            var host = document.querySelector('#inner');
            var root = host.attachShadow({ mode: 'closed' });
            var inner = document.createElement('span');
            root.appendChild(inner);

            var outside;
            document.querySelector('#outer').addEventListener('ping', e => {
                outside = e.composedPath().map(t => t.nodeName || String(t)).join(',');
            });

            inner.dispatchEvent(new Event('ping', { bubbles: true, composed: true }));
            outside;
            """).Should().Be("DIV,DIV,BODY,HTML,#document");
    }

    [Test]
    public void ANonComposedEventDoesNotLeaveAShadowTree()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://dom.spec.whatwg.org/#get-the-parent — a shadow root's parent is null when the event's
        // composed flag is unset, which is what DomNodeObject.GetParent's shadow clause implements.
        fixture.Text("""
            var host = document.querySelector('#inner');
            var root = host.attachShadow({ mode: 'open' });
            var inner = document.createElement('span');
            root.appendChild(inner);

            var seen = [];
            document.querySelector('#outer').addEventListener('ping', () => seen.push('outer'));
            inner.addEventListener('ping', () => seen.push('inner'));

            inner.dispatchEvent(new Event('ping', { bubbles: true }));
            seen.join(',');
            """).Should().Be("inner");
    }
}
