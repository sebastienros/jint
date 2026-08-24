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
        => Run(new ObjectGenerator(), [source], sourceFile);

    /// <summary>
    /// Runs the <c>[JsAccessible]</c> generator. No <c>RootNamespace</c> is supplied, so the registration
    /// entry point lands in the compilation's assembly name — which is what a project without an explicit
    /// root namespace gets too.
    /// </summary>
    public static Task VerifyJsAccessibleGenerator(string source, [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
        => Run(new Jint.SourceGenerators.Interop.JsAccessibleGenerator(), [source], sourceFile);

    /// <inheritdoc cref="VerifyJsAccessibleGenerator(string, string)" />
    /// <remarks>Several sources, for the cases where two declarations in one compilation are the point.</remarks>
    public static Task VerifyJsAccessibleGenerator(string[] sources, [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
        => Run(new Jint.SourceGenerators.Interop.JsAccessibleGenerator(), sources, sourceFile);

    private static Task Run(IIncrementalGenerator generator, string[] sources, string sourceFile)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = new SyntaxTree[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            syntaxTrees[i] = CSharpSyntaxTree.ParseText(sources[i], parseOptions);
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "JintSourceGenTests",
            syntaxTrees: syntaxTrees,
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var driver = CSharpGeneratorDriver
            .Create(generator)
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
