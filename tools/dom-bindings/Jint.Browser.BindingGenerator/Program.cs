using Jint.Browser.BindingGenerator;

var arguments = ParseArguments(args);

if (!arguments.TryGetValue("contract", out var contract)
    || !arguments.TryGetValue("output", out var output)
    || arguments.Keys.Any(key => key is not ("contract" or "output" or "report")))
{
    Console.Error.WriteLine("""
        usage: dotnet run --project tools/dom-bindings/Jint.Browser.BindingGenerator -- \
            --contract tools/dom-bindings/contract.json --output Jint.Browser/Dom/Generated [--report <file>]

        See tools/dom-bindings/README.md; the regeneration path most people want is the test:
            JINT_DOM_BINDINGS=update dotnet test -c Release --project Jint.Tests.Browser/Jint.Tests.Browser.csproj \
                --filter FullyQualifiedName~DomBindingsStalenessTests
        """);
    return 1;
}

var result = BindingGenerator.Run(new BindingGeneratorOptions
{
    ContractPath = contract,
});

var files = result.Files;
var report = result.Report;

Directory.CreateDirectory(output);

foreach (var existing in Directory.GetFiles(output, "*.g.cs"))
{
    if (!files.ContainsKey(Path.GetFileName(existing)))
    {
        File.Delete(existing);
    }
}

foreach (var (name, content) in files)
{
    File.WriteAllText(Path.Combine(output, name), content);
}

if (arguments.TryGetValue("report", out var reportPath))
{
    File.WriteAllText(reportPath, report);
}

Console.WriteLine(report);
Console.WriteLine("Wrote " + files.Count + " files to " + output);
return 0;

static Dictionary<string, string> ParseArguments(string[] args)
{
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            parsed[args[i][2..]] = args[i + 1];
        }
    }

    return parsed;
}
