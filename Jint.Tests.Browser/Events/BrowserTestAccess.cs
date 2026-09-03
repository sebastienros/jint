using Jint.Browser;
using Jint.Browser.Events;

namespace Jint.Tests.Browser.Events;

/// <summary>
/// The internal state these tests assert on, read on the page loop and converted before it comes back.
/// </summary>
/// <remarks>
/// Nothing that belongs to an engine may cross out of a mailbox request — <c>Jint.Browser/AGENTS.md</c>'s
/// thread rule — so every helper here converts inside the request and hands back plain strings.
/// </remarks>
internal static class BrowserTestAccess
{
    /// <summary>
    /// What the default activation host recorded: the hyperlinks that were followed, the forms that were
    /// submitted and the file choosers that were opened, oldest first.
    /// </summary>
    internal static Task<string[]> PendingActivationsAsync(Page page)
        => page.RunOnLoopAsync(static engine => PendingActivations(engine));

    /// <summary>
    /// The same, for an engine with no page — where the recording host is still the one in place, because
    /// nothing installed a <c>PageActivationHost</c> over it.
    /// </summary>
    internal static string[] PendingActivations(Engine engine)
    {
        var activations = BrowserEventRealm.Of(engine).PendingActivations;
        var rendered = new string[activations.Count];

        for (var i = 0; i < activations.Count; i++)
        {
            rendered[i] = activations[i].ToString();
        }

        return rendered;
    }

    /// <summary>Runs the input dispatcher's key entry point, the one CDP's <c>Input</c> domain maps onto.</summary>
    internal static Task DispatchKeyAsync(Page page, string key, EventModifiers modifiers = EventModifiers.None)
        => page.RunOnLoopAsync(engine =>
        {
            InputDispatcher.DispatchKey(engine, KeyOptions.For(key) with { Modifiers = modifiers });
            return true;
        });

    /// <summary>
    /// Runs the input dispatcher's text entry point — what <c>Input.insertText</c> maps onto: an edit with no
    /// key events at all, which is the shape an IME commit and a paste arrive in.
    /// </summary>
    internal static Task InsertTextAsync(Page page, string text)
        => page.RunOnLoopAsync(engine =>
        {
            InputDispatcher.InsertText(engine, text);
            return true;
        });

    /// <summary>Types <paramref name="text"/> one key at a time, the way a protocol client would.</summary>
    internal static Task TypeAsync(Page page, string text)
        => page.RunOnLoopAsync(engine =>
        {
            foreach (var character in text)
            {
                InputDispatcher.DispatchKey(engine, KeyOptions.For(character.ToString()));
            }

            return true;
        });

    /// <summary>
    /// Runs the input dispatcher's click entry point at an element, which focuses it and then dispatches a
    /// trusted click — the other half of what CDP's <c>Input</c> domain maps onto.
    /// </summary>
    internal static Task DispatchClickAsync(Page page, string elementId)
        => page.RunOnLoopAsync(engine =>
        {
            var runtime = Jint.Browser.Runtime.PageRuntime.Find(engine);
            var element = runtime?.Document?.GetElementById(elementId);

            if (element is null || runtime is null)
            {
                return false;
            }

            InputDispatcher.DispatchClick(runtime.Dom.WrapNode(element), ClickOptions.At(0, 0));
            return true;
        });
}
