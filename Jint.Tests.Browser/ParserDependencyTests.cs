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
        foreach (var path in EnumerateDependencyFiles(RepositoryPaths.Root))
        {
            var relative = Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');

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
    public void DependencyScanSkipsNestedCheckoutsButKeepsOrdinarySourceDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "jint-dependencies-" + Guid.NewGuid().ToString("N"));
        string[] included =
        [
            "Directory.Packages.props",
            "Production/Production.csproj",
            "Production/Build/Dependencies.targets",
            ".worktrees/OrdinarySource/Dependencies.props",
            "Production/obj-like/Dependencies.props"
        ];
        string[] excluded =
        [
            ".worktrees/checkout/Production/Production.csproj",
            "CustomManagedPath/checkout/Directory.Packages.props",
            "NestedRepository/Production/Dependencies.props",
            "Production/bin/Dependencies.props",
            "Production/obj/Dependencies.props",
            "artifacts/Dependencies.targets",
            ".git/Dependencies.props"
        ];
        try
        {
            foreach (var relative in included.Concat(excluded))
            {
                var path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "<Project />");
            }
            File.WriteAllText(Path.Combine(root, ".worktrees/checkout/.git"), "gitdir: elsewhere");
            File.WriteAllText(Path.Combine(root, "CustomManagedPath/checkout/.git"), "gitdir: elsewhere");
            Directory.CreateDirectory(Path.Combine(root, "NestedRepository/.git"));

            EnumerateDependencyFiles(root)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Should().BeEquivalentTo(included);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IEnumerable<string> EnumerateDependencyFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            var directory = pending.Pop();
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (Path.GetExtension(path) is ".csproj" or ".props" or ".targets")
                    yield return path;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (Path.GetFileName(child) is "artifacts" or "bin" or "obj" or ".git")
                    continue;

                // A nested checkout owns its own dependencies. Worktrees use a .git file,
                // while standalone repositories use a directory; neither belongs to this scan.
                var git = Path.Combine(child, ".git");
                if (File.Exists(git) || Directory.Exists(git))
                    continue;

                pending.Push(child);
            }
        }
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
