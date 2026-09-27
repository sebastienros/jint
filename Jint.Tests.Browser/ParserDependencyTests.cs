using System.Xml.Linq;
using Jint.Browser;
using Jint.Browser.BindingGenerator;
using Jint.HtmlParser;

namespace Jint.Tests.Browser;

public sealed class ParserDependencyTests
{
    [Test]
    public void UpstreamPackagesAreOnlyReferencedByNativeParserBenchmarks()
    {
        var references = new List<string>();
        foreach (var path in Directory.EnumerateFiles(RepositoryPaths.Root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(path) is not (".csproj" or ".props" or ".targets"))
                continue;
            var relative = Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "artifacts" or "bin" or "obj" or ".git"))
                continue;

            foreach (var element in XDocument.Load(path).Descendants())
            {
                if (element.Name.LocalName is not ("PackageReference" or "PackageVersion" or "PackageDownload"))
                    continue;
                var package = (string?) element.Attribute("Include") ?? (string?) element.Attribute("Update");
                if (package is null || !package.StartsWith("AngleSharp", StringComparison.Ordinal))
                    continue;

                references.Add(relative + ":" + element.Name.LocalName + ":" + package);
            }
        }

        references.Should().BeEquivalentTo(
            "Directory.Packages.props:PackageVersion:AngleSharp",
            "Directory.Packages.props:PackageVersion:AngleSharp.Css",
            "Directory.Packages.props:PackageVersion:AngleSharp.Xml",
            "Jint.Benchmark/Jint.Benchmark.csproj:PackageReference:AngleSharp",
            "Jint.Benchmark/Jint.Benchmark.csproj:PackageReference:AngleSharp.Css",
            "Jint.Benchmark/Jint.Benchmark.csproj:PackageReference:AngleSharp.Xml");
    }

    [Test]
    public void NativeParserBrowserAndEmitterHaveNoUpstreamAssemblyReferences()
    {
        foreach (var assembly in new[] { typeof(MarkupParser).Assembly, typeof(Page).Assembly, typeof(BindingGenerator).Assembly })
        {
            assembly.GetReferencedAssemblies().Should().NotContain(
                reference => reference.Name != null && reference.Name.StartsWith("AngleSharp", StringComparison.Ordinal));
        }
    }
}
