using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Replicates the exact pattern from JavaScriptEngineSwitcher's JsExecutionHeavyBenchmark:
/// a Handlebars template rendering library is loaded on 4 separate engine instances,
/// each calling renderTemplate() with different content items.
/// See: https://github.com/Taritsyn/JavaScriptEngineSwitcher/blob/master/test/JavaScriptEngineSwitcher.Benchmarks/JsExecutionHeavyBenchmark.cs
/// </summary>
[MemoryDiagnoser]
public class PreparedScriptBenchmark
{
    private string _libraryCode = null!;
    private ContentItem[] _contentItems = null!;

    [GlobalSetup]
    public void Setup()
    {
        var handlebars = File.ReadAllText("Scripts/template-rendering/lib/handlebars.js");
        var helpers = File.ReadAllText("Scripts/template-rendering/lib/helpers.js");
        _libraryCode = handlebars + "\n" + helpers;

        var contentDir = "Scripts/template-rendering/content";
        string[] names = ["hello-world", "contacts", "js-engines", "web-browser-family-tree"];
        _contentItems = names.Select(name => new ContentItem(
            File.ReadAllText(Path.Combine(contentDir, name, "template.handlebars")),
            File.ReadAllText(Path.Combine(contentDir, name, "data.json"))
        )).ToArray();
    }

    [Benchmark(Baseline = true)]
    public void ExecuteStringOnMultipleEngines()
    {
        RenderTemplates(prepareScript: false, compileRegex: false);
    }

    [Benchmark]
    public void ExecutePreparedWithoutRegexCompilationOnMultipleEngines()
    {
        RenderTemplates(prepareScript: true, compileRegex: false);
    }

    [Benchmark]
    public void ExecutePreparedWithRegexCompilationOnMultipleEngines()
    {
        RenderTemplates(prepareScript: true, compileRegex: true);
    }

    private void RenderTemplates(bool prepareScript, bool compileRegex)
    {
        // First engine: load library and render first item
        Prepared<Script> preparedLibrary = default;
        var engine = new Engine();
        if (prepareScript)
        {
            var preparationOptions = new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { CompileRegex = compileRegex }
            };
            preparedLibrary = Engine.PrepareScript(_libraryCode, options: preparationOptions);
            engine.Execute(preparedLibrary);
        }
        else
            engine.Execute(_libraryCode);

        engine.Invoke("renderTemplate", _contentItems[0].Template, _contentItems[0].Data);

        // Remaining engines: each loads the same library and renders one item
        for (int i = 1; i < _contentItems.Length; i++)
        {
            engine = new Engine();
            if (prepareScript)
                engine.Execute(preparedLibrary);
            else
                engine.Execute(_libraryCode);

            engine.Invoke("renderTemplate", _contentItems[i].Template, _contentItems[i].Data);
        }
    }

    private sealed record ContentItem(string Template, string Data);
}
