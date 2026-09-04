using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Runtime;

/// <summary>
/// A page's <c>navigator</c>: the members an embedded interpreter has no business publishing, and the one
/// member the engine already had.
/// </summary>
/// <remarks>
/// <para>
/// The engine's <c>Navigator</c> carries <c>userAgent</c> alone, because WinterTC's Minimum Common API
/// requires it and everything else on HTML's interface describes a user agent with a user, a document and a
/// network stack. A page <i>is</i> that, so the rest arrive here — and the one they share has to answer the
/// page's string rather than the engine's, whichever way a script reads it.
/// </para>
/// <para>
/// <c>Emulation.setUserAgentOverride</c> reading back through the same member is
/// <c>DevTools/EmulationDomainTests</c>; this file is what a page sees with no client attached.
/// </para>
/// </remarks>
public sealed class NavigatorTests
{
    private const string Ua = "Mozilla/5.0 (compatible; Probe/1.0)";

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-useragent, read the two ways a
    /// page can read it. Before <see href="https://github.com/sebastienros/jint/issues/3655">#3655</see> the
    /// page shadowed <c>userAgent</c> with an own property, so the accessor WebIDL puts on
    /// <c>Navigator.prototype</c> still answered the engine's <c>Jint/&lt;version&gt;</c> — two answers to
    /// one IDL attribute, and the second is the one a script that hardens against tampering reaches for.
    /// </summary>
    [Test]
    public async Task OneUserAgentAnswersWhicheverWayAPageReadsIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureBrowser: options => options.UserAgent = Ua);

        await fixture.Page.SetContentAsync("<p>ok</p>");

        (await fixture.Page.EvaluateAsync<string>("navigator.userAgent")).Should().Be(Ua);
        (await fixture.Page.EvaluateAsync<string>(
                "Object.getOwnPropertyDescriptor(Navigator.prototype, 'userAgent').get.call(navigator)"))
            .Should().Be(Ua, "the accessor the standard declares is the one that has to answer");
    }

    /// <summary>
    /// And it is the prototype's, so the instance has no own <c>userAgent</c> at all — which is what a
    /// browser answers and what idlharness asserts.
    /// </summary>
    [Test]
    public async Task TheUserAgentIsNotAnOwnPropertyOfTheNavigator()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureBrowser: options => options.UserAgent = Ua);

        await fixture.Page.SetContentAsync("<p>ok</p>");

        (await fixture.Page.EvaluateAsync<bool>("Object.getOwnPropertyNames(navigator).includes('userAgent')"))
            .Should().BeFalse("WebIDL puts a readonly attribute on the interface prototype object");

        // The members the engine's Navigator does not have are still the page's own, and still hidden from
        // Object.keys the way an inherited member would be.
        (await fixture.Page.EvaluateAsync<bool>("Object.getOwnPropertyNames(navigator).includes('platform')"))
            .Should().BeTrue();
        (await fixture.Page.EvaluateAsync<double>("Object.keys(navigator).length")).Should().Be(0);
    }

    /// <summary>
    /// The brand check survives the move: the getter is the interface's, so extracting it and calling it on
    /// something that is not a <c>Navigator</c> is a <c>TypeError</c> exactly as a browser answers.
    /// </summary>
    [Test]
    public async Task TheUserAgentGetterStillBrandChecksItsReceiver()
    {
        await using var fixture = await LoopbackPage.CreateAsync(configureBrowser: options => options.UserAgent = Ua);

        await fixture.Page.SetContentAsync("<p>ok</p>");

        (await fixture.Page.EvaluateAsync<string>("""
            (() => {
              const get = Object.getOwnPropertyDescriptor(Navigator.prototype, 'userAgent').get;
              try { get.call({}); return 'no throw'; } catch (e) { return e.constructor.name; }
            })()
            """)).Should().Be("TypeError");
    }
}
