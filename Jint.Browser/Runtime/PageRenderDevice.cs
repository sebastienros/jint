using AngleSharp.Css;

namespace Jint.Browser.Runtime;

/// <summary>
/// The device AngleSharp.Css resolves a relative length and a style sheet's <c>@media</c> against: this
/// page's own viewport, media type and scripting state, read live.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registering one is what stops the cascade raising.</b> <c>ComputeCurrentStyle()</c> resolves every
/// length to pixels, and a percentage, a <c>vw</c>, a <c>vh</c> or a <c>calc()</c> over one needs an extent
/// to resolve against — so with no <c>IRenderDevice</c> on the browsing context AngleSharp.Css falls back to
/// a <c>DefaultRenderDevice</c> reporting 0 × 0 and raises <c>ArgumentException</c> instead of answering.
/// <c>width: 100%</c> is the commonest declaration on the web, and it made <c>getComputedStyle</c> and every
/// box query a CLR exception out of AngleSharp
/// (<a href="https://github.com/sebastienros/jint/issues/3730">#3730</a>).
/// </para>
/// <para>
/// <b>It holds no numbers of its own, and that is what makes emulation effective.</b> Every member is read
/// from <see cref="PageRuntime.Media"/> at the moment the cascade asks, so
/// <c>Emulation.setDeviceMetricsOverride</c>, <c>setVisibleSize</c> and <c>setEmulatedMedia</c> reach the
/// next cascade with nothing to re-register — the device is a service on the browsing context, which lives
/// as long as the document while the viewport does not.
/// </para>
/// <para>
/// <b>It mirrors <see cref="MediaQuery"/> number for number</b>, because the two answer the same question
/// through different code: a page reading <c>matchMedia('(min-width: 600px)')</c> and a style sheet whose
/// <c>@media (min-width: 600px)</c> the cascade evaluates must agree
/// (<a href="https://github.com/sebastienros/jint/issues/3721">#3721</a>). Which features AngleSharp.Css can
/// evaluate at all — and the four it evaluates wrongly — is the table in
/// <c>Jint.Browser/AGENTS.md</c>; the preference features are not among them, so
/// <c>@media (prefers-color-scheme: dark)</c> still never matches and <see cref="PageMediaEnvironment"/>
/// answers that half itself.
/// </para>
/// <para>
/// It is read on the page loop, and during a parse on the parser's thread — one holder at a time, which the
/// baton keeps. Each read is one reference read of an immutable value, so a resize that lands between two
/// declarations of one cascade is stale rather than torn.
/// </para>
/// </remarks>
internal sealed class PageRenderDevice : IRenderDevice
{
    private readonly PageRuntime _runtime;

    internal PageRenderDevice(PageRuntime runtime) => _runtime = runtime;

    /// <inheritdoc />
    /// <remarks>
    /// Clamped to at least one pixel, in both axes: a zero extent is exactly what makes AngleSharp.Css raise
    /// rather than answer, and <c>Emulation.setDeviceMetricsOverride</c> takes a width and a height from a
    /// client that may send zeros.
    /// </remarks>
    public int ViewPortWidth => Math.Max(1, _runtime.Viewport.Width);

    /// <inheritdoc />
    public int ViewPortHeight => Math.Max(1, _runtime.Viewport.Height);

    /// <inheritdoc />
    /// <remarks>The window <em>is</em> the screen here, which is what <c>screen.width</c> answers too.</remarks>
    public int DeviceWidth => ViewPortWidth;

    /// <inheritdoc />
    public int DeviceHeight => ViewPortHeight;

    /// <inheritdoc />
    public double RenderWidth => ViewPortWidth;

    /// <inheritdoc />
    public double RenderHeight => ViewPortHeight;

    /// <inheritdoc />
    /// <remarks>
    /// CSS's initial <c>font-size</c>, which is what an <c>em</c> resolves against with no font metrics to
    /// ask — the same number <see cref="MediaQuery.PixelsPerEm"/> uses for an <c>em</c> in a query.
    /// </remarks>
    public double FontSize => MediaQuery.PixelsPerEm;

    /// <inheritdoc />
    /// <remarks>Dots per inch, which is the device pixel ratio against CSS's 96 dpi reference.</remarks>
    public int Resolution => (int) Math.Round(_runtime.Viewport.DeviceScaleFactor * 96);

    /// <inheritdoc />
    /// <remarks>What <c>Emulation.setEmulatedMedia</c> said the page is being shown as.</remarks>
    public DeviceCategory Category => _runtime.Media.MediaType switch
    {
        "print" => DeviceCategory.Printer,
        "speech" => DeviceCategory.Speech,
        "screen" => DeviceCategory.Screen,
        _ => DeviceCategory.Other,
    };

    /// <inheritdoc />
    /// <remarks><c>Emulation.setScriptExecutionDisabled</c> decides it, for this document.</remarks>
    public bool IsScripting => _runtime.ScriptingEnabled;

    /// <inheritdoc />
    /// <remarks>Nothing is scanned, so <c>(scan: interlace)</c> is false.</remarks>
    public bool IsInterlaced => false;

    /// <inheritdoc />
    /// <remarks>Nothing is a terminal, so <c>(grid: 0)</c>.</remarks>
    public bool IsGrid => false;

    /// <inheritdoc />
    /// <remarks>Bits per colour component, which is what a browser reports for <c>(color)</c>.</remarks>
    public int ColorBits => 8;

    /// <inheritdoc />
    /// <remarks>Zero, because the device has colour: <c>(monochrome: 0)</c>, as a browser answers.</remarks>
    public int MonochromeBits => 0;

    /// <inheritdoc />
    /// <remarks>No CSS media feature reads it; it is AngleSharp's own member.</remarks>
    public int Frequency => 60;
}
