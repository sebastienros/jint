using Jint.Browser.Events;
using Jint.Browser.Runtime;

namespace Jint.Browser;

/// <summary>The rectangle an element occupies in the viewport, in CSS pixels.</summary>
/// <param name="X">The left edge, relative to the viewport.</param>
/// <param name="Y">The top edge, relative to the viewport.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <remarks>
/// It is the synthetic layout's box, the one <c>getBoundingClientRect()</c> answers and the one a click, a tap
/// and the coordinate mouse hit-test against — not a rendered rectangle.
/// </remarks>
public readonly record struct ElementBox(double X, double Y, double Width, double Height);

/// <summary>Which mouse button a coordinate mouse event presses or releases.</summary>
public enum MouseButton
{
    /// <summary>The primary button.</summary>
    Left = 0,

    /// <summary>The auxiliary button, usually the wheel.</summary>
    Middle = 1,

    /// <summary>The secondary button.</summary>
    Right = 2,
}

public sealed partial class Page
{
    // The pointer a coordinate mouse moves: where it is and which buttons it holds. Host state rather than page
    // state, written and read only on the page's loop, so it survives a navigation as a real pointer does.
    private double _mouseX;
    private double _mouseY;
    private int _mouseButtons;

    /// <summary>Reads the box of what <paramref name="target"/> names, scrolled as it currently is.</summary>
    /// <param name="target">A target: a CSS selector, a <c>ref=</c> from an accessibility snapshot, or a <c>role=</c>, <c>label=</c> or <c>text=</c> locator, optionally chained with <c>&gt;&gt;</c>.</param>
    /// <param name="index">The zero-based match, or a negative offset from the last match.</param>
    /// <returns>The box, or <see langword="null"/> when nothing matched or the match has no box.</returns>
    /// <remarks>
    /// An element that is not being rendered — <c>display: none</c> on it or an ancestor — has no box, as it
    /// has none for a click. The answer is the synthetic layout's, so it is a coordinate to hand back to
    /// <see cref="MouseMoveAsync"/>, not a measurement of a rendering.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<ElementBox?> BoundingBoxAsync(string target, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(ElementBox? (engine) =>
        {
            if (PageRuntime.Find(engine) is not { } runtime
                || ElementLocator.Find(runtime.Dom, runtime.Document, target, index) is not { } element
                || runtime.Layout.Current().ClientBoxOf(element) is not { } box)
            {
                return null;
            }

            return new ElementBox(box.X, box.Y, box.Width, box.Height);
        });
    }

    /// <summary>Moves the pointer to <paramref name="x"/>, <paramref name="y"/> in the viewport.</summary>
    /// <param name="x">The viewport <c>x</c> coordinate, in CSS pixels.</param>
    /// <param name="y">The viewport <c>y</c> coordinate, in CSS pixels.</param>
    /// <param name="steps">
    /// How many moves the glide is made of, evenly spaced between where the pointer was and the point; one by
    /// default, which jumps.
    /// </param>
    /// <remarks>
    /// <para>
    /// Each step is one <c>pointermove</c> and one <c>mousemove</c> at the element the step's point hits, with
    /// whatever buttons <see cref="MouseDownAsync"/> left held in <c>buttons</c> — which is what a drag
    /// library listening for movement with a button down sees from a user. The pointer starts at the origin
    /// and stays where it was left across navigations.
    /// </para>
    /// <para>
    /// The steps run as separate page turns, so a listener's timers, promises and the reorders a sortable
    /// list makes between two moves have run by the time the next move is hit-tested. Boundary events are not
    /// fired, as <see cref="HoverAsync(string)"/> describes.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is less than one.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task MouseMoveAsync(double x, double y, int steps = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        ObjectDisposedException.ThrowIf(_closed, this);

        var (fromX, fromY) = await _loop.PostAsync(_ => (_mouseX, _mouseY)).ConfigureAwait(false);
        for (var step = 1; step <= steps; step++)
        {
            var stepX = fromX + ((x - fromX) * step / steps);
            var stepY = fromY + ((y - fromY) * step / steps);
            await _loop.PostAsync(engine =>
            {
                _mouseX = stepX;
                _mouseY = stepY;
                if (PageRuntime.Find(engine) is { } runtime)
                {
                    InputDispatcher.DispatchMouse(
                        runtime,
                        new MouseInput(MouseInputKind.Moved, stepX, stepY, 0, _mouseButtons, 0, EventModifiers.None, 0, 0));
                }

                return true;
            }).ConfigureAwait(false);
        }
    }

    /// <summary>Presses <paramref name="button"/> where the pointer is.</summary>
    /// <param name="button">The button to press; the primary one by default.</param>
    /// <remarks>
    /// One <c>pointerdown</c> and one <c>mousedown</c> at the element under the pointer, which takes focus when
    /// the press is not cancelled. The button stays held — and every later <see cref="MouseMoveAsync"/> says
    /// so — until <see cref="MouseUpAsync"/>.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task MouseDownAsync(MouseButton button = MouseButton.Left)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        return _loop.PostAsync(engine =>
        {
            _mouseButtons |= ButtonsBit(button);
            if (PageRuntime.Find(engine) is { } runtime)
            {
                InputDispatcher.DispatchMouse(
                    runtime,
                    new MouseInput(MouseInputKind.Pressed, _mouseX, _mouseY, (int) button, _mouseButtons, 1, EventModifiers.None, 0, 0));
            }

            return true;
        });
    }

    /// <summary>Releases <paramref name="button"/> where the pointer is, and waits for any navigation it causes.</summary>
    /// <param name="button">The button to release; the primary one by default.</param>
    /// <param name="options">How far to wait for a navigation the release causes; the defaults when omitted.</param>
    /// <remarks>
    /// One <c>pointerup</c> and one <c>mouseup</c> at the element under the pointer, then the <c>click</c> the
    /// press and the release share when both hit the same element — so a down, no move and an up is a click,
    /// and a navigation that click starts is awaited, as <see cref="ClickAsync(string, NavigationOptions)"/>
    /// awaits one.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task MouseUpAsync(MouseButton button = MouseButton.Left, NavigationOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        var captured = await _loop.PostAsync(NavigationRequest? (engine) =>
        {
            _mouseButtons &= ~ButtonsBit(button);
            if (PageRuntime.Find(engine) is not { } runtime)
            {
                return null;
            }

            _capturingNavigation = true;
            _capturedNavigation = null;
            try
            {
                InputDispatcher.DispatchMouse(
                    runtime,
                    new MouseInput(MouseInputKind.Released, _mouseX, _mouseY, (int) button, _mouseButtons, 1, EventModifiers.None, 0, 0));
                return _capturedNavigation;
            }
            finally
            {
                _capturingNavigation = false;
                _capturedNavigation = null;
            }
        }).ConfigureAwait(false);

        if (captured is { } navigation)
        {
            await NavigateCoreAsync(navigation with { Options = options ?? NavigationOptions.Default }).ConfigureAwait(false);
        }
    }

    // https://w3c.github.io/uievents/#dom-mouseevent-buttons — primary 1, secondary 2, auxiliary 4.
    private static int ButtonsBit(MouseButton button) => button switch
    {
        MouseButton.Left => 1,
        MouseButton.Right => 2,
        MouseButton.Middle => 4,
        _ => 0,
    };
}
