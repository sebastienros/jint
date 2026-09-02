using Jint.Browser.Runtime;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Emulation;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Emulation</c> domain: what a client asks the page to pretend it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>One of these is real and the rest are stored.</b> <c>setDeviceMetricsOverride</c> changes the page's
/// <see cref="Viewport"/>, which is what <c>window.innerWidth</c>, <c>window.innerHeight</c>,
/// <c>devicePixelRatio</c>, <c>screen</c> and every <c>matchMedia</c> query are answered from — so a page
/// that branches on viewport size really does branch. Touch, focus, media and the user agent are kept on the
/// target and answered as the successes they are; each says which campaign item makes it effective, and none
/// of them is refused, because every recorded client sends at least one while opening a page and reads a
/// refusal as a broken target.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Emulation/"/>.
/// </para>
/// </remarks>
internal sealed class EmulationDomain : EmulationDomainBase
{
    private readonly PageTarget _target;

    internal EmulationDomain(PageTarget target)
    {
        _target = target;
    }

    /// <summary>Sets what the page believes its window to be, for real.</summary>
    /// <remarks>
    /// <c>mobile</c>, <c>scale</c>, the screen dimensions and the orientation are accepted and not acted on:
    /// there is no renderer for a scale to mean anything to, and no layout for an orientation to change.
    /// The width, the height and the device scale factor are the three a page can read, and they are the
    /// three that take effect.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetDeviceMetricsOverrideAsync(SetDeviceMetricsOverrideRequest parameters, CommandContext context)
    {
        var scale = parameters.DeviceScaleFactor > 0 ? parameters.DeviceScaleFactor : 1;
        SetViewport(new Viewport(parameters.Width, parameters.Height, scale));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Puts the page's viewport back to what the browser opened it with.</summary>
    protected override ValueTask<EmptyResult> ClearDeviceMetricsOverrideAsync(EmptyParameters parameters, CommandContext context)
    {
        SetViewport(_target.Emulation.DefaultViewport);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Stores what the client asked for; the input model (campaign item C5) makes it effective.</summary>
    protected override ValueTask<EmptyResult> SetTouchEmulationEnabledAsync(SetTouchEmulationEnabledRequest parameters, CommandContext context)
    {
        _target.Emulation.TouchEnabled = parameters.Enabled;
        _target.Emulation.MaxTouchPoints = parameters.MaxTouchPoints ?? 1;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc cref="SetTouchEmulationEnabledAsync"/>
    protected override ValueTask<EmptyResult> SetFocusEmulationEnabledAsync(SetFocusEmulationEnabledRequest parameters, CommandContext context)
    {
        _target.Emulation.FocusEmulationEnabled = parameters.Enabled;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Stores the media type; the CSSOM work is what would make a query answer differently.</summary>
    /// <remarks>
    /// <c>features</c> is accepted and dropped: a feature override is a value the cascade would have to be
    /// asked with, and <c>Runtime/MediaQuery</c> answers from the viewport rather than from the cascade.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetEmulatedMediaAsync(SetEmulatedMediaRequest parameters, CommandContext context)
    {
        _target.Emulation.EmulatedMedia = parameters.Media;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Stores the user agent; the network layer (campaign item C3) is what sends it.</summary>
    protected override ValueTask<EmptyResult> SetUserAgentOverrideAsync(SetUserAgentOverrideRequest parameters, CommandContext context)
    {
        _target.Emulation.UserAgent = parameters.UserAgent;
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Writes the viewport straight onto the page runtime, which is safe because this is the loop thread.
    /// </summary>
    /// <remarks>
    /// Every command of a page target is answered on the page's own loop — the target's mailbox is the
    /// engine's, and the engine is the loop's — so the viewport is set where a media query would read it,
    /// with the <c>change</c> events that follow dispatched on the same turn.
    /// </remarks>
    private void SetViewport(Viewport viewport)
    {
        _target.Emulation.Viewport = viewport;
        PageRuntime.Find(_target.Runtime.Engine)?.SetViewport(viewport);
    }
}
