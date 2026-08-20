#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Modules;

namespace Jint.Tests.Runtime.NodeCompat;

/// <summary>
/// How the opt-in <c>node:</c> builtin modules are reached: what an engine that did not ask for them sees,
/// how they compose with a configured module loader and with <c>Engine.Modules.Add</c>, and what an unknown
/// <c>node:</c> specifier reports.
/// </summary>
public class BuiltinModuleTests
{
    // -------------------------------------------------------------------------------------------------
    // Opting in, and what an engine that did not.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The default engine is the engine it always was: a <c>node:</c> specifier is refused exactly as it was
    /// before these modules existed.
    /// </summary>
    [Fact]
    public void ADefaultEngineCannotImportABuiltin()
    {
        var engine = new Engine();

        Assert.ThrowsAny<Exception>(() => engine.Modules.Import("node:path"));
    }

    /// <summary>
    /// And so is one with a real loader that never asked for the builtins - <see cref="NodeStyleModuleLoader"/>
    /// keeps refusing a <c>node:</c> scheme with its own message.
    /// </summary>
    [Fact]
    public void ANodeStyleLoaderThatDidNotOptInStillRefusesTheNodeScheme()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "export const x = 1;");

        var engine = new Engine(options => options.EnableModules(new NodeStyleModuleLoader(tree.Root)));

        var exception = Assert.Throws<ModuleResolutionException>(() => engine.Modules.Import("node:path"));

        exception.ResolverAlgorithmError.Should().StartWith("Unsupported Module Scheme");
    }

    /// <summary>
    /// The builtins need no file-based loader at all: an engine that enabled nothing else still gets them, and
    /// everything else keeps failing the way it did.
    /// </summary>
    [Fact]
    public void TheBuiltinsWorkWithoutAModuleLoader()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.Platform = "linux"));

        engine.Modules.Import("node:path").Get("sep").AsString().Should().Be("/");

        Assert.ThrowsAny<Exception>(() => engine.Modules.Import("./main.js"));
    }

    /// <summary>
    /// Every builtin the running build provides is importable, and each carries a default export beside its
    /// named ones.
    /// </summary>
    [Theory]
    [InlineData("node:path")]
    [InlineData("node:path/posix")]
    [InlineData("node:path/win32")]
#if NET8_0_OR_GREATER
    [InlineData("node:querystring")]
    [InlineData("node:url")]
#endif
    public void EveryProvidedBuiltinIsImportable(string specifier)
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        engine.Modules.Import(specifier).Get("default").IsObject().Should().BeTrue();
    }

    /// <summary>
    /// A dynamic <c>import()</c> reaches them too, which is the shape a lazily loaded package uses.
    /// </summary>
    [Fact]
    public void ADynamicImportReachesABuiltin()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.Platform = "linux"));

        engine.Execute("var result; import('node:path').then(m => { result = m.join('a', 'b'); });");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("result").AsString().Should().Be("a/b");
    }

    // -------------------------------------------------------------------------------------------------
    // Unknown node: specifiers.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// An unknown <c>node:</c> specifier fails with a message that names what <em>is</em> available, and says
    /// that the modules needing platform resources are absent on purpose.
    /// </summary>
    [Theory]
    [InlineData("node:fs")]
    [InlineData("node:buffer")]
    [InlineData("node:crypto")]
    [InlineData("node:os")]
    public void AnUnknownBuiltinNamesWhatIsAvailable(string specifier)
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        var exception = Assert.Throws<ModuleResolutionException>(() => engine.Modules.Import(specifier));

        exception.ResolverAlgorithmError.Should().StartWith("Unknown Node Builtin Module");
        exception.ResolverAlgorithmError.Should().Contain("node:path");
        exception.ResolverAlgorithmError.Should().Contain("node:path/posix");
        exception.ResolverAlgorithmError.Should().Contain("node:path/win32");
        exception.ResolverAlgorithmError.Should().Contain("deliberately not provided");
#if NET8_0_OR_GREATER
        exception.ResolverAlgorithmError.Should().Contain("node:querystring");
        exception.ResolverAlgorithmError.Should().Contain("node:url");
#endif
    }

    /// <summary>
    /// The un-prefixed spelling of an absent module is <em>not</em> claimed: <c>import 'fs'</c> is an ordinary
    /// bare specifier that a package in <c>node_modules</c> - or a host registration - may answer.
    /// </summary>
    [Fact]
    public void AnUnprefixedNameThatIsNotABuiltinIsLeftToTheLoader()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());
        engine.Modules.Add("fs", "export const mine = true;");

        engine.Modules.Import("fs").Get("mine").AsBoolean().Should().BeTrue();
    }

    // -------------------------------------------------------------------------------------------------
    // The two spellings.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// PACKAGE_RESOLVE step 3 of ESM_RESOLVE: "if specifier is a Node.js builtin module name, return the
    /// string 'node:' concatenated with specifier". So both spellings work, and both name one module record.
    /// </summary>
    [Fact]
    public void BothSpellingsNameOneModule()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.Platform = "linux"));

        engine.SetValue("prefixed", engine.Modules.Import("node:path").Get("default"));
        engine.SetValue("bare", engine.Modules.Import("path").Get("default"));

        engine.Evaluate("prefixed === bare").AsBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("path")]
    [InlineData("path/posix")]
    [InlineData("path/win32")]
#if NET8_0_OR_GREATER
    [InlineData("querystring")]
    [InlineData("url")]
#endif
    public void TheUnprefixedSpellingsResolveToTheBuiltins(string specifier)
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        engine.Modules.Import(specifier).Get("default").IsObject().Should().BeTrue();
    }

    /// <summary>
    /// Turning the alias off leaves the un-prefixed names to the loader, which is what a tree that really does
    /// depend on an npm package called <c>path</c> needs.
    /// </summary>
    [Fact]
    public void TheUnprefixedSpellingCanBeTurnedOff()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o => o.AllowUnprefixedSpecifiers = false));
        engine.Modules.Add("path", "export const mine = true;");

        engine.Modules.Import("path").Get("mine").AsBoolean().Should().BeTrue();
        engine.Modules.Import("node:path").Get("sep").IsString().Should().BeTrue();
    }

    // -------------------------------------------------------------------------------------------------
    // Composition with Engine.Modules.Add.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// A module the host registered under the builtin's own name replaces it - the non-clobbering posture the
    /// <c>process</c> shim and the web APIs take, arrived at here by leaving the precedence the module system
    /// already has alone.
    /// </summary>
    [Fact]
    public void AHostRegistrationUnderThePrefixedNameWins()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());
        engine.Modules.Add("node:path", "export const sep = '@'; export default { sep: '@' };");

        engine.Modules.Import("node:path").Get("sep").AsString().Should().Be("@");
    }

    /// <summary>
    /// And a registration under the un-prefixed name claims both spellings, because both resolve to the one
    /// <c>node:</c> key.
    /// </summary>
    [Fact]
    public void AHostRegistrationUnderTheUnprefixedNameClaimsBothSpellings()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());
        engine.Modules.Add("path", "export const sep = '@';");

        engine.Modules.Import("path").Get("sep").AsString().Should().Be("@");
        engine.Modules.Import("node:path").Get("sep").AsString().Should().Be("@");
    }

    /// <summary>
    /// The same door is how a host supplies one of the modules Jint deliberately does not provide.
    /// </summary>
    [Fact]
    public void AHostCanSupplyAModuleJintDoesNotProvide()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());
        engine.Modules.Add("node:fs", "export function readFileSync() { return 'from the host'; }");

        engine.Modules.Add("main", "import { readFileSync } from 'node:fs'; export const contents = readFileSync();");

        engine.Modules.Import("main").Get("contents").AsString().Should().Be("from the host");
    }

    // -------------------------------------------------------------------------------------------------
    // Composition with a module loader.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The configured loader keeps answering everything that is not a builtin, and reading
    /// <c>options.Modules.ModuleLoader</c> back keeps giving the host what it set: the decorator is applied
    /// when the engine is built, not to the options object.
    /// </summary>
    [Fact]
    public void TheConfiguredLoaderIsUnchangedAndStillAnswersEverythingElse()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "export const x = 41;");

        var loader = new NodeStyleModuleLoader(tree.Root);
        var options = new Options().EnableModules(loader).UseNodeBuiltinModules();

        options.Modules.ModuleLoader.Should().BeSameAs(loader);

        var engine = new Engine(options);

        engine.Modules.Import("./main.js").Get("x").AsNumber().Should().Be(41);
        engine.Modules.Import("node:path").Get("sep").IsString().Should().BeTrue();
    }

    /// <summary>
    /// And the order of the two calls does not matter, which is the whole reason the wrap happens at engine
    /// build rather than inside <c>UseNodeBuiltinModules</c>.
    /// </summary>
    [Fact]
    public void TheOrderOfEnableModulesAndUseNodeBuiltinModulesDoesNotMatter()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "export const x = 1;");

        var first = new Engine(options => options.UseNodeBuiltinModules().EnableModules(new NodeStyleModuleLoader(tree.Root)));
        var second = new Engine(options => options.EnableModules(new NodeStyleModuleLoader(tree.Root)).UseNodeBuiltinModules());

        foreach (var engine in new[] { first, second })
        {
            engine.Modules.Import("node:path").Get("sep").IsString().Should().BeTrue();
            engine.Modules.Import("./main.js").Get("x").AsNumber().Should().Be(1);
        }
    }

    /// <summary>
    /// A package in <c>node_modules</c> reached through <see cref="NodeStyleModuleLoader"/>, importing
    /// <c>node:path</c> across two files of its own - the shape a real published package has, and the thing
    /// the whole slice exists to make work.
    /// </summary>
    [Fact]
    public void APackageInNodeModulesCanImportTheBuiltins()
    {
        using var tree = new PackageTree();
        tree
            .Add("package.json", "{ \"name\": \"app\", \"type\": \"module\" }")
            .Add("app/main.js", """
                import { pathOf, extensionOf } from 'tiny-router';
                export const route = pathOf('posts', '2026', 'hello.md');
                export const extension = extensionOf(route);
                """)
            .Add("node_modules/tiny-router/package.json", """
                { "name": "tiny-router", "version": "1.0.0", "exports": { ".": "./index.js", "./naming": "./lib/naming.js" } }
                """)
            .Add("node_modules/tiny-router/index.js", """
                import path from 'node:path';
                import { slugify } from './lib/naming.js';
                export function pathOf(...segments) {
                    return path.posix.join('/', ...segments.map(slugify));
                }
                export { extensionOf } from 'tiny-router/naming';
                """)
            .Add("node_modules/tiny-router/lib/naming.js", """
                import { extname } from 'node:path/posix';
                export function slugify(segment) {
                    return segment.toLowerCase().replace(/\s+/g, '-');
                }
                export function extensionOf(candidate) {
                    return extname(candidate);
                }
                """);

        var engine = new Engine(options => options
            .EnableModules(new NodeStyleModuleLoader(tree.Root))
            .UseNodeBuiltinModules(o => o.Platform = "linux"));

        var namespaceObject = engine.Modules.Import("./app/main.js");

        namespaceObject.Get("route").AsString().Should().Be("/posts/2026/hello.md");
        namespaceObject.Get("extension").AsString().Should().Be(".md");
    }

    /// <summary>
    /// A package that imports the un-prefixed spelling - which is still the commoner one in published code -
    /// gets the builtin rather than whatever <c>node_modules</c> happens to hold under that name, exactly as
    /// Node resolves it.
    /// </summary>
    [Fact]
    public void ABuiltinOutranksAPackageOfTheSameName()
    {
        using var tree = new PackageTree();
        tree
            .Add("main.js", "import { sep } from 'path'; export const separator = sep;")
            .Add("node_modules/path/package.json", "{ \"name\": \"path\", \"main\": \"index.js\" }")
            .Add("node_modules/path/index.js", "export const sep = 'from the polyfill';");

        var withBuiltins = new Engine(options => options
            .EnableModules(new NodeStyleModuleLoader(tree.Root))
            .UseNodeBuiltinModules(o => o.Platform = "linux"));

        withBuiltins.Modules.Import("./main.js").Get("separator").AsString().Should().Be("/");

        // And with the alias turned off the package is what the same tree resolves to.
        var withoutAlias = new Engine(options => options
            .EnableModules(new NodeStyleModuleLoader(tree.Root))
            .UseNodeBuiltinModules(o => o.AllowUnprefixedSpecifiers = false));

        withoutAlias.Modules.Import("./main.js").Get("separator").AsString().Should().Be("from the polyfill");
    }

    /// <summary>
    /// An asynchronous loader stays asynchronous through the decorator - the engine keys its whole load path
    /// on that interface - and a builtin settles on the stack it was asked on, so importing one costs the
    /// event loop nothing.
    /// </summary>
    [Fact]
    public async Task ComposesWithAnAsynchronousLoader()
    {
        var loader = new RecordingAsyncLoader();
        var engine = new Engine(options => options
            .EnableModules(loader)
            .UseNodeBuiltinModules(o => o.Platform = "linux"));

        engine.Modules.ModuleLoader.Should().BeAssignableTo<IAsyncModuleLoader>();

        var builtin = await engine.Modules.ImportAsync("node:path");
        builtin.Get("sep").AsString().Should().Be("/");
        loader.Requested.Should().BeEmpty();

        var hosted = await engine.Modules.ImportAsync("./main.js");
        hosted.Get("value").AsNumber().Should().Be(7);
        loader.Requested.Should().ContainSingle();
    }

    /// <summary>
    /// A loader that only loads synchronously must not be turned into an asynchronous one, because that would
    /// change which failures become promise rejections rather than exceptions on the caller's thread.
    /// </summary>
    [Fact]
    public void ASynchronousLoaderStaysSynchronous()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "export const x = 1;");

        var engine = new Engine(options => options
            .EnableModules(new NodeStyleModuleLoader(tree.Root))
            .UseNodeBuiltinModules());

        engine.Modules.ModuleLoader.Should().NotBeAssignableTo<IAsyncModuleLoader>();
    }

    /// <summary>
    /// A loader that fetches over I/O, recording what it was asked for so that a builtin can be shown never to
    /// reach it.
    /// </summary>
    private sealed class RecordingAsyncLoader : AsyncModuleLoader
    {
        public List<string> Requested { get; } = new();

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            Requested.Add(resolved.Key);
            await Task.Yield();
            return "export const value = 7;";
        }
    }

    /// <summary>
    /// A throwaway <c>node_modules</c> tree on disk. Every path is written relative to the root with forward
    /// slashes, whatever the platform separator is.
    /// </summary>
    private sealed class PackageTree : IDisposable
    {
        public PackageTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "jint-node-builtins", Guid.NewGuid().ToString("N"));
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
