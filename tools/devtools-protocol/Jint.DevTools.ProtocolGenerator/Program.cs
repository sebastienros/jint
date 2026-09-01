using Jint.DevTools.ProtocolGenerator;

// The regeneration command tools/devtools-protocol/README.md documents:
//
//   dotnet run --project tools/devtools-protocol/Jint.DevTools.ProtocolGenerator -c Release -- \
//       --protocol tools/devtools-protocol \
//       --manifest tools/devtools-protocol/manifest.json \
//       --output Jint.DevTools/Protocol/Generated
//
// It writes the whole directory, deleting any *.g.cs the manifest no longer produces, so that a domain
// removed from generatedDomains cannot leave a stale file compiling behind it.

string? protocolDirectory = null;
string? manifestPath = null;
string? outputDirectory = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--protocol":
            protocolDirectory = Next(args, ref i);
            break;
        case "--manifest":
            manifestPath = Next(args, ref i);
            break;
        case "--output":
            outputDirectory = Next(args, ref i);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
            return 2;
    }
}

if (protocolDirectory is null || manifestPath is null || outputDirectory is null)
{
    Console.Error.WriteLine("usage: --protocol <directory> --manifest <file> --output <directory>");
    return 2;
}

try
{
    var files = ProtocolEmitter.Emit(protocolDirectory, manifestPath);

    Directory.CreateDirectory(outputDirectory);

    foreach (var stale in Directory.GetFiles(outputDirectory, "*.g.cs"))
    {
        if (!files.ContainsKey(Path.GetFileName(stale)))
        {
            File.Delete(stale);
            Console.WriteLine($"deleted {Path.GetFileName(stale)}");
        }
    }

    foreach (var (name, content) in files.OrderBy(file => file.Key, StringComparer.Ordinal))
    {
        var path = Path.Combine(outputDirectory, name);
        File.WriteAllText(path, content);
        Console.WriteLine($"wrote {name} ({content.Length:N0} characters)");
    }

    return 0;
}
catch (ProtocolGeneratorException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static string Next(string[] arguments, ref int index)
{
    index++;
    if (index >= arguments.Length)
    {
        throw new ProtocolGeneratorException($"'{arguments[index - 1]}' needs a value.");
    }

    return arguments[index];
}
