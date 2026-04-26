using System.Collections.Immutable;
using System.Reflection;
using Jint.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Jint.Tests.SourceGenerators;

internal static class VerifyHelper
{
    private static readonly ImmutableArray<MetadataReference> _references = BuildReferences();

    public static Task VerifyGenerator(string source, [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            assemblyName: "JintSourceGenTests",
            syntaxTrees: [syntaxTree],
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var driver = CSharpGeneratorDriver
            .Create(new ObjectGenerator())
            .RunGenerators(compilation);

        return Verifier
            .Verify(driver, sourceFile: sourceFile)
            .UseDirectory("Snapshots");
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var refs = ImmutableArray.CreateBuilder<MetadataReference>();
        // The Jint runtime types the generated code references.
        refs.Add(MetadataReference.CreateFromFile(typeof(Jint.Engine).Assembly.Location));
        // Pull in everything the runtime assembly references (System.Runtime, System.Private.CoreLib, etc.)
        // — keeps the test compilation self-contained without depending on runtime probing.
        var trustedAssemblies = ((string?) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in trustedAssemblies)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name is "System.Runtime"
                or "System.Private.CoreLib"
                or "System.Collections"
                or "System.Linq"
                or "netstandard"
                or "System.Memory"
                or "System.Runtime.CompilerServices.Unsafe")
            {
                refs.Add(MetadataReference.CreateFromFile(path));
            }
        }
        return refs.ToImmutable();
    }
}
