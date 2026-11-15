using Jint.Browser.Runtime;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>The viewport the page currently believes it has.</summary>
    /// <remarks>
    /// <para>
    /// It starts as <see cref="BrowserContextOptions.Viewport"/>, or <see cref="BrowserOptions.Viewport"/> when
    /// the context named none, and moves with <see cref="SetViewportAsync(Viewport)"/>,
    /// <see cref="SetViewportSizeAsync"/> and a protocol client's <c>Emulation.setDeviceMetricsOverride</c>,
    /// whichever wrote last.
    /// </para>
    /// <para>
    /// Readable from any thread without a round trip to the page loop: it is the value the last write recorded,
    /// which is what the document showing reports as <c>innerWidth</c>, <c>innerHeight</c> and
    /// <c>devicePixelRatio</c> once that write has completed.
    /// </para>
    /// </remarks>
    public Viewport Viewport => Emulation.Viewport;

    /// <summary>Resizes the page's viewport, keeping its device scale factor.</summary>
    /// <param name="width">The new viewport width in CSS pixels; at least 1.</param>
    /// <param name="height">The new viewport height in CSS pixels; at least 1.</param>
    /// <returns>A task that completes once the document showing has been moved to the new size.</returns>
    /// <remarks>
    /// Playwright's <c>page.setViewportSize</c>; <see cref="SetViewportAsync(Viewport)"/> says what happens.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than 1.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task SetViewportSizeAsync(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            Emulation.SetViewport(Emulation.Viewport with { Width = width, Height = height }, PageRuntime.Find(engine));
            return true;
        });
    }

    /// <summary>Changes the size and device scale factor the page believes its window to have.</summary>
    /// <param name="viewport">The new viewport; its width and height must be at least 1 and its device scale
    /// factor positive and finite.</param>
    /// <returns>A task that completes once the document showing has heard the change.</returns>
    /// <remarks>
    /// <para>
    /// <b>The document that is loaded moves, now.</b> <c>innerWidth</c>, <c>innerHeight</c>,
    /// <c>devicePixelRatio</c>, <c>screen</c>, <c>visualViewport</c> and every client rectangle answer the new
    /// numbers; when the width or height changed, <c>resize</c> fires at the window and then at
    /// <c>visualViewport</c>; every <c>MediaQueryList</c> whose answer moved fires <c>change</c>; stylesheet
    /// <c>@media</c> rules are matched against the new size on the next style read; and an <c>&lt;img&gt;</c>
    /// whose <c>srcset</c> or <c>&lt;picture&gt;</c> now selects a different candidate fetches it. All of it
    /// has happened, listeners included, when the task completes.
    /// </para>
    /// <para>
    /// It is the same write as <c>Emulation.setDeviceMetricsOverride</c>, so a host in this process and a
    /// protocol client attached to the page see one viewport, and the last writer wins. It belongs to the page
    /// rather than to the document, so every later navigation opens at this size too.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is less than 1, or the device scale factor is
    /// not a positive finite number.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task SetViewportAsync(Viewport viewport)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(viewport.Width, 1, nameof(viewport));
        ArgumentOutOfRangeException.ThrowIfLessThan(viewport.Height, 1, nameof(viewport));
        if (!(viewport.DeviceScaleFactor > 0) || double.IsInfinity(viewport.DeviceScaleFactor))
        {
            throw new ArgumentOutOfRangeException(nameof(viewport), viewport.DeviceScaleFactor,
                "The device scale factor must be a positive, finite number.");
        }

        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            Emulation.SetViewport(viewport, PageRuntime.Find(engine));
            return true;
        });
    }
}
