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

    /// <summary>
    /// The <c>[JsAccessible]</c> generator's diagnostics for one source, beside the compilation they were
    /// reported against — which is the pair the tree-location invariant is about.
    /// </summary>
    public static (IReadOnlyList<Diagnostic> Diagnostics, Compilation Compilation) RunJsAccessibleGeneratorFor(string source)
    {
        var compilation = Compile([source], LanguageVersion.Latest);
        var result = CSharpGeneratorDriver
            .Create(new Jint.SourceGenerators.Interop.JsAccessibleGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        return (result.Diagnostics, compilation);
    }

    /// <summary>
    /// Compiles one source together with everything the <c>[JsAccessible]</c> generator emits for it, at a
    /// given language version, and returns the errors. Generated trees inherit the compilation's parse
    /// options, so this is the question a consumer's build asks — and it is not the question
    /// <c>SyntaxTree.GetDiagnostics()</c> answers: <c>#nullable enable</c> under C# 7.3 <em>parses</em>
    /// fine and fails in binding, as CS8370.
    /// </summary>
    public static IReadOnlyList<Diagnostic> CompileWithJsAccessibleGenerator(string source, LanguageVersion languageVersion)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);

        CSharpGeneratorDriver
            .Create(
                generators: [new Jint.SourceGenerators.Interop.JsAccessibleGenerator().AsSourceGenerator()],
                additionalTexts: null,
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(Compile([source], languageVersion), out var output, out _);

        return [.. output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error)];
    }

    private static Task Run(IIncrementalGenerator generator, string[] sources, string sourceFile)
        => Verifier
            .Verify(
                CSharpGeneratorDriver.Create(generator).RunGenerators(Compile(sources, LanguageVersion.Latest)),
                sourceFile: sourceFile)
            .UseDirectory("Snapshots");

    private static CSharpCompilation Compile(string[] sources, LanguageVersion languageVersion)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var syntaxTrees = new SyntaxTree[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            syntaxTrees[i] = CSharpSyntaxTree.ParseText(sources[i], parseOptions);
        }

        return CSharpCompilation.Create(
            assemblyName: "JintSourceGenTests",
            syntaxTrees: syntaxTrees,
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
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
