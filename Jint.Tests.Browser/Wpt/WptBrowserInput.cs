using System.Text.Json;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.WebApi.Events;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// What <c>testdriver-vendor.js</c> posts, turned into the input the <c>Input</c> domain would have run.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a decoder and nothing else.</b> Every message becomes one call to
/// <c>Jint.Browser</c>'s <see cref="InputDispatcher"/> — the same flat hit test
/// <c>Input.dispatchMouseEvent</c> reaches and the same key dispatch <c>Input.dispatchKeyEvent</c> reaches.
/// If a wpt document and a Puppeteer client disagreed about what a click does, one of the two would be
/// testing something no client can reach; there is one implementation so that they cannot.
/// </para>
/// <para>
/// <b>It runs on the page's own loop thread</b>, inside the turn the overlay called from, which is what makes
/// it allowed to touch the page's DOM at all. Nothing of the engine's crosses back: the delegate takes one
/// JSON string and answers nothing.
/// </para>
/// </remarks>
internal static class WptBrowserInput
{
    /// <summary>Runs one input message against the page the calling engine belongs to.</summary>
    internal static void Dispatch(Engine engine, string json)
    {
        if (PageRuntime.Find(engine) is not { } runtime)
        {
            return;
        }

        using var document = JsonDocument.Parse(json);
        var message = document.RootElement;

        switch (message.GetProperty("kind").GetString())
        {
            case "mouse":
                InputDispatcher.DispatchMouse(runtime, new MouseInput(
                    MouseKind(Text(message, "type")),
                    Number(message, "x"),
                    Number(message, "y"),
                    Number(message, "button"),
                    Number(message, "buttons"),
                    (int) Number(message, "clickCount"),
                    EventModifiers.None,
                    Number(message, "deltaX"),
                    Number(message, "deltaY")));
                return;

            case "key":
                InputDispatcher.DispatchKey(
                    runtime,
                    new KeyOptions(
                        Text(message, "key"),
                        Text(message, "code"),
                        Text(message, "text"),
                        Location: 0,
                        Repeat: false,
                        EventModifiers.None,
                        Commands: null),
                    KeyKind(Text(message, "type")));
                return;

            default:
                throw new InvalidOperationException("testdriver-vendor.js posted an unknown message: " + json);
        }
    }

    private static MouseInputKind MouseKind(string type) => type switch
    {
        "moved" => MouseInputKind.Moved,
        "pressed" => MouseInputKind.Pressed,
        "released" => MouseInputKind.Released,
        "wheel" => MouseInputKind.Wheel,
        _ => throw new InvalidOperationException("testdriver-vendor.js posted an unknown mouse type: " + type),
    };

    private static KeyInputKind KeyKind(string type) => type switch
    {
        "keyDown" => KeyInputKind.KeyDown,
        "rawKeyDown" => KeyInputKind.RawKeyDown,
        "keyUp" => KeyInputKind.KeyUp,
        "char" => KeyInputKind.Char,
        _ => throw new InvalidOperationException("testdriver-vendor.js posted an unknown key type: " + type),
    };

    private static string Text(in JsonElement message, string name)
        => message.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

    private static double Number(in JsonElement message, string name)
        => message.TryGetProperty(name, out var value) ? value.GetDouble() : 0;
}
