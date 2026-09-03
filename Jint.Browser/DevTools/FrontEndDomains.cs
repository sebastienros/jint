using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Security</c> domain, accepted and with no certificate state to report.
/// </summary>
/// <remarks>
/// <para>
/// A DevTools front end enables it while attaching to a page and reads its state to draw the padlock. There
/// is no padlock here and, more to the point, no certificate decision this package makes: every request goes
/// out over the <c>HttpClient</c> the browser context was given, and whether a certificate is trusted is
/// that client's handler's business and therefore the host's. So the domain answers, reports nothing, and
/// <c>securityStateChanged</c> is never sent — a state of <c>unknown</c> repeated forever would be noise.
/// </para>
/// <para>
/// <b><c>setIgnoreCertificateErrors</c> is accepted and ignores nothing</b>, which is the one refusal that
/// would have been worse than a no-op: a client sends it to relax the connection, and answering
/// <c>-32601</c> makes a test suite fail at connect time instead of at the request it was about. A host that
/// wants a lax certificate policy sets one on the <c>HttpClient</c> it hands the context — the same place it
/// sets every other network decision — and this package deliberately does not let a protocol client loosen
/// a host's transport security from outside.
/// </para>
/// <para>
/// <c>handleCertificateError</c> and <c>setOverrideCertificateErrors</c> stay unimplemented: both are the
/// interactive half of a decision nothing here ever asks about, so a client enabling the override would wait
/// forever for a <c>certificateError</c> event.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Security/"/>.
/// </para>
/// </remarks>
internal sealed class SecurityDomain : SecurityDomainBase
{
    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc cref="SecurityDomain"/>
    protected override ValueTask<EmptyResult> SetIgnoreCertificateErrorsAsync(
        Jint.DevTools.Protocol.Security.SetIgnoreCertificateErrorsRequest parameters,
        CommandContext context)
        => new(EmptyResult.Instance);
}

/// <summary>
/// The <c>Overlay</c> domain, accepted and drawing nothing.
/// </summary>
/// <remarks>
/// <para>
/// Every command of this domain asks for something to be painted over the page — a node highlight, a grid,
/// a ruler, the "Paused in debugger" banner — and this browser renders no pixels, so there is no surface for
/// any of it. The four that are answered are the four a front end sends while merely <i>attaching</i>:
/// enabling the domain, disabling it, hiding a highlight it never showed, and setting the banner text it
/// would have drawn during a pause. Answering them is what lets a front end attach at all.
/// </para>
/// <para>
/// <b>Every highlighting command is honestly <c>-32601</c>.</b> <c>highlightNode</c> answering success would
/// tell a user hovering the elements panel that the element is being shown on a page they cannot see; the
/// front end copes with the method being absent, and a client feature-detecting the domain gets the truth.
/// <c>setInspectMode</c> is absent for the sharper version of the same reason: it promises an
/// <c>inspectNodeRequested</c> event that can only come from a click on a rendering.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Overlay/"/>.
/// </para>
/// </remarks>
internal sealed class OverlayDomain : OverlayDomainBase
{
    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>Accepted, and there was never a highlight to hide.</summary>
    protected override ValueTask<EmptyResult> HideHighlightAsync(EmptyParameters parameters, CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and the message is drawn nowhere.</summary>
    /// <remarks>
    /// The front end sets it as it enables the debugger, before anything has paused, so refusing it is a
    /// refusal at attach time for a banner.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetPausedInDebuggerMessageAsync(
        Jint.DevTools.Protocol.Overlay.SetPausedInDebuggerMessageRequest parameters,
        CommandContext context)
        => new(EmptyResult.Instance);

    /// <summary>Accepted, and no size is shown because the viewport is never resized by a user.</summary>
    /// <remarks>
    /// A viewport here only changes when <c>Emulation.setDeviceMetricsOverride</c> says so, which is the
    /// client's own doing — so the overlay this would enable has nothing to announce.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetShowViewportSizeOnResizeAsync(
        Jint.DevTools.Protocol.Overlay.SetShowViewportSizeOnResizeRequest parameters,
        CommandContext context)
        => new(EmptyResult.Instance);
}
