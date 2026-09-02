using Jint.Browser.Dom;
using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>
/// Builds the engine one navigation runs in, with the web APIs a page needs, the page's sinks, and the DOM.
/// </summary>
/// <remarks>
/// <para>
/// One engine per top-level navigation, so "per document" and "per engine" coincide and no <c>WindowProxy</c>
/// is needed: the previous engine is disposed on the page loop and the new one starts with a clean realm.
/// </para>
/// <para>
/// The feature set is the web-API default plus <c>XMLHttpRequest</c> and <c>Storage</c>. It carries no
/// <c>Fetch</c> grant of its own — a page reaches no network in this version — though the
/// <c>XMLHttpRequest</c> grant's own closure brings the fetch machinery it is built on, which is what makes
/// an <c>XMLHttpRequest</c> object exist and answer without ever being able to send.
/// </para>
/// <para>
/// The host's own configuration runs last, so it can change anything set here, including the feature set.
/// </para>
/// </remarks>
internal static class BrowserEngineFactory
{
    internal static Engine Create(Page page, BrowserOptions options, PageRecorder recorder, string url)
    {
        var engine = new Engine(o =>
        {
            o.UseWebApis(WebApiFeatures.Default | WebApiFeatures.XmlHttpRequest | WebApiFeatures.Storage);

            if (options.RecordErrors)
            {
                // Installing a sink is not only a recording: it is what makes an exception escaping a timer,
                // a listener or a microtask a report instead of an eruption out of the pump.
                o.WebApi.Diagnostics.Sink = new PageRecorder.Diagnostics(recorder);
            }

            if (options.RecordConsoleMessages)
            {
                o.WebApi.Console.Sink = new PageRecorder.Console(recorder);
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var baseUrl))
            {
                o.WebApi.Fetch.BaseUrl = baseUrl;
            }

            // So that a stack trace in a recorded error names the function it came from, and so that the
            // protocol can hand a client the source of a page's own script later.
            o.RetainFunctionSourceText = true;

            foreach (var configure in options.EngineConfiguration)
            {
                configure(o);
            }
        });

        var runtime = PageRuntime.Attach(engine, page, options, recorder);

        DomBindings.Install(engine);
        WindowInstaller.Install(runtime);

        return engine;
    }
}
