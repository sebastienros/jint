using Jint.Runtime.Modules;

#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The npm-shaped module loader as a third party reaches it: constructed from outside the assembly, handed to
/// <see cref="OptionsExtensions.UseModules(Options,IModuleLoader)"/>, and asked questions through nothing but
/// public API.
/// </summary>
public class NodeStyleModuleLoaderTests
{
    [Fact]
    public void AHostCanBuildTheLoaderAndImportAPackageByName()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/greeter/package.json", """{ "name": "greeter", "exports": { ".": { "import": "./esm/index.js", "default": "./cjs/index.js" } } }""")
            .Add("node_modules/greeter/esm/index.js", "export function greet(name) { return 'hello ' + name; }")
            .Add("node_modules/greeter/cjs/index.js", "throw new Error('the require branch must not be taken');")
            .Add("main.js", "import { greet } from 'greeter'; export const message = greet('world');");

        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));

        engine.Modules.Import("./main.js").Get("message").AsString().Should().Be("hello world");
    }

    [Fact]
    public void TheLoaderIsAModuleLoaderAndAnIModuleLoader()
    {
        using var tree = new PackageTree();

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Should().BeAssignableTo<ModuleLoader>().And.BeAssignableTo<IModuleLoader>();
    }

    [Fact]
    public void ResolutionIsReachableWithoutAnEngine()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./lib/entry.js" }""")
            .Add("node_modules/pkg/lib/entry.js", "export const value = 1;");

        var resolved = new NodeStyleModuleLoader(tree.Root).Resolve(null, new ModuleRequest("pkg", []));

        resolved.Type.Should().Be(SpecifierType.RelativeOrAbsolute);
        resolved.Uri!.LocalPath.Should().Be(tree.PathOf("node_modules/pkg/lib/entry.js"));

        // The name the engine would give the module built from this, which a host preparing modules itself has
        // to reproduce exactly.
        ModuleFactory.LocationOf(resolved).Should().Be(tree.PathOf("node_modules/pkg/lib/entry.js"));
    }

    [Fact]
    public void ARefusalNamesTheRuleThatRefusedIt()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": "./entry.js" } }""")
            .Add("node_modules/pkg/entry.js", "export const value = 1;")
            .Add("node_modules/pkg/internal.js", "export const secret = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        var exception = Invoking(() => loader.Resolve(null, new ModuleRequest("pkg/internal.js", [])))
            .Should().ThrowExactly<ModuleResolutionException>().Which;

        exception.ResolverAlgorithmError.Should().StartWith("Package Path Not Exported");
        exception.Specifier.Should().Be("pkg/internal.js");
        exception.FilePath.Should().Be("node_modules/pkg");

        // Nothing above the base path is named, whatever the message says about the package.
        exception.Message.Should().NotContain(tree.Root);
    }

    [Fact]
    public void OneLoaderServesSeveralEngines()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": "./index.js" }""")
            .Add("node_modules/pkg/index.js", "export const value = 'shared';");

        var loader = new NodeStyleModuleLoader(tree.Root);

        foreach (var _ in Enumerable.Range(0, 3))
        {
            var engine = new Engine(options => options.UseModules(loader));
            engine.Modules.Import("pkg").Get("value").AsString().Should().Be("shared");
        }
    }

    [Fact]
    public void OptionsAreSnapshotWhenTheLoaderIsBuilt()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "node": "./node.js", "default": "./default.js" } }""")
            .Add("node_modules/pkg/node.js", "export const value = 1;")
            .Add("node_modules/pkg/default.js", "export const value = 2;");

        var options = new NodeModuleLoaderOptions();
        var loader = new NodeStyleModuleLoader(tree.Root, options);

        // Neither replacing the array nor writing through the one that was passed in reaches the loader.
        options.Conditions[0] = "node";
        options.Conditions = ["node"];
        options.AllowJsonModules = false;
        options.ExtensionProbing = true;

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/default.js"));
    }

    [Fact]
    public void ANameWithNoPackageOnDiskStillReachesTheModuleRegistry()
    {
        // The one documented deviation from PACKAGE_RESOLVE step 11, pinned from the embedder's side because
        // it is what keeps Engine.Modules.Add usable together with this loader.
        using var tree = new PackageTree();
        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));
        engine.Modules.Add("lib", builder => builder.ExportValue("version", 15));

        engine.Modules.Import("lib").Get("version").AsNumber().Should().Be(15);
    }

    [Fact]
    public void ADefaultEngineStillHasNoModuleLoader()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": "./index.js" }""")
            .Add("node_modules/pkg/index.js", "export const value = 1;");

        // Building a loader configures nothing by itself.
        _ = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => new Engine().Modules.Import("pkg"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*disabled*");
    }

    [Fact]
    public void AJsonModuleNeedsItsImportAttribute()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/config/package.json", """{ "name": "config", "exports": { "./data.json": "./data.json" } }""")
            .Add("node_modules/config/data.json", """{ "answer": 42 }""")
            .Add("with-attribute.js", "import data from 'config/data.json' with { type: 'json' }; export const answer = data.answer;")
            .Add("without-attribute.js", "import data from 'config/data.json'; export const answer = data.answer;");

        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));

        engine.Modules.Import("./with-attribute.js").Get("answer").AsNumber().Should().Be(42);

        Invoking(() => engine.Modules.Import("./without-attribute.js"))
            .Should().Throw<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Missing Import Attribute");
    }

    [Fact]
    public void ABasePathThatIsNotAFileSystemLocationIsRejected()
    {
        Invoking(() => new NodeStyleModuleLoader("https://example.com/modules/"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*file*");

        Invoking(() => new NodeStyleModuleLoader("relative/path"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*rooted*");
    }

    /// <summary>A throwaway <c>node_modules</c> tree on disk, written with forward slashes on every platform.</summary>
    private sealed class PackageTree : IDisposable
    {
        public PackageTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "jint-node-resolution-public", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string PathOf(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public PackageTree Add(string relativePath, string contents)
        {
            var path = PathOf(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return this;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A file left open by a failing test must not replace that test's own failure.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
