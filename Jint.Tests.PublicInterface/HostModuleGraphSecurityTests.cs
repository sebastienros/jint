#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Modules;


namespace Jint.Tests.PublicInterface;

/// <summary>
/// Host-configurable module graph security limits: module count, total source bytes, graph depth,
/// resolution hops, and load policy. Tests live here because the public-interface project has no
/// <c>InternalsVisibleTo</c> grant, so every green assertion proves an embedder can reach the surface.
/// </summary>
public sealed class HostModuleGraphSecurityTests
{
    // Helpers

    /// <summary>Simple in-memory module loader returning fixed source per specifier.</summary>
    private sealed class DictLoader : ModuleLoader
    {
        private readonly Dictionary<string, string> _modules;
        private readonly string _basePath;

        public DictLoader(Dictionary<string, string> modules, string? basePath = null)
        {
            _modules = modules;
            _basePath = basePath ?? "/base";
        }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var key = moduleRequest.Specifier;
            if (key.StartsWith("./", StringComparison.Ordinal))
            {
                key = key.Substring(2);
            }

            var uri = new Uri($"file://{_basePath}/{key}");
            return new ResolvedSpecifier(moduleRequest, key, uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
        {
            if (_modules.TryGetValue(resolved.Key!, out var source))
            {
                return source;
            }

            throw new ModuleResolutionException($"Module not found: {resolved.Key}", resolved.ModuleRequest.Specifier, null, null);
        }
    }

    /// <summary>Simple loader that uses bare specifiers (no URI).</summary>
    private sealed class BareLoader : ModuleLoader
    {
        private readonly Dictionary<string, string> _modules;

        public BareLoader(Dictionary<string, string> modules) => _modules = modules;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
        {
            if (_modules.TryGetValue(resolved.Key!, out var source))
            {
                return source;
            }

            throw new ModuleResolutionException($"Not found: {resolved.Key}", resolved.ModuleRequest.Specifier, null, null);
        }
    }

    private sealed class DeferredLoader : IAsyncModuleLoader
    {
        private readonly List<ModuleLoadCompletion> _pending = [];
        private readonly Dictionary<string, int> _loads = new(StringComparer.Ordinal);

        public int LoadsFor(string specifier) => _loads.TryGetValue(specifier, out var count) ? count : 0;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The asynchronous path should be used.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            _loads.TryGetValue(resolved.Key, out var count);
            _loads[resolved.Key] = count + 1;
            _pending.Add(completion);
        }

        public void Deliver(string specifier, string source)
        {
            var completion = _pending.First(x => x.Resolved.Key == specifier);
            _pending.Remove(completion);
            completion.SetSource(source);
        }

        public void Deliver(string specifier, byte[] source)
        {
            var completion = _pending.First(x => x.Resolved.Key == specifier);
            _pending.Remove(completion);
            completion.SetSource(source);
        }
    }

    private sealed class UriLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var uri = new Uri(moduleRequest.Specifier, UriKind.Absolute);
            return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => "export default 1;";
    }

    private sealed class PrefixingLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, "/" + moduleRequest.Specifier.TrimStart('/'), null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("A registered module should satisfy the load.");
    }

    private sealed class PreparedLoader : IModuleLoader
    {
        private readonly Prepared<Module> _prepared = Engine.PrepareModule(
            "export default 1;",
            "prepared");

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, in _prepared);
    }

    private sealed class BytesLoader : IModuleLoader
    {
        private readonly byte[] _bytes;

        public BytesLoader(byte[] bytes) => _bytes = bytes;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildBytesModule(engine, resolved, _bytes);
    }

    private sealed class TextAndJsonLoader : IModuleLoader
    {
        internal const string Json = """{"value":1}""";
        internal const string Text = "plain text";

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => resolved.Key == "json"
                ? ModuleFactory.BuildJsonModule(engine, resolved, Json)
                : ModuleFactory.BuildTextModule(engine, resolved, Text);
    }

    private sealed class ThrowingModuleLoader : ModuleLoader
    {
        private readonly Exception _exception;

        public ThrowingModuleLoader(Exception exception) => _exception = exception;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw _exception;
    }

    private sealed class ThrowingResolveLoader : ModuleLoader
    {
        private readonly Exception _exception;

        public ThrowingResolveLoader(Exception exception) => _exception = exception;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => throw _exception;

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("Resolution should have failed.");
    }

    private sealed class FaultingAsyncModuleLoader : AsyncModuleLoader
    {
        private readonly Exception _exception;

        public FaultingAsyncModuleLoader(Exception exception) => _exception = exception;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromException<string>(_exception);
    }

    private sealed class TimingOutAsyncModuleLoader : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
        {
            try
            {
                engine.Execute("while (true);");
                return Task.FromResult("");
            }
            catch (Exception ex)
            {
                return Task.FromException<string>(ex);
            }
        }
    }

    private sealed class CancelingAsyncModuleLoader : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromCanceled<string>(cancellationToken);
    }

    private sealed class TimingOutEngineModuleLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
        {
            engine.Execute("while (true);");
            return "";
        }
    }

    private sealed class RegexTimingOutEngineModuleLoader : ModuleLoader
    {
        private const string Pattern = "^(https?:\\/\\/)?([\\da-z\\.-]+)\\.([a-z\\.]{2,6})([\\/\\w\\.-]*)*\\/?$";
        private const string Input = "https://archiverbx.blob.core.windows.net/static/C:/Users/USR/Documents/Projects/PROJ/static/images/full/1234567890.jpg";

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
        {
            engine.Execute($"'{Input}'.match(/{Pattern}/)");
            return "";
        }
    }

    /// <summary>Policy that denies everything.</summary>
    private sealed class DenyAllPolicy : IModuleLoadPolicy
    {
        public bool AllowLoad(string? referrerLocation, ModuleRequest request, ResolvedSpecifier resolved) => false;
    }

    /// <summary>Policy that allows everything.</summary>
    private sealed class AllowAllPolicy : IModuleLoadPolicy
    {
        public bool AllowLoad(string? referrerLocation, ModuleRequest request, ResolvedSpecifier resolved) => true;
    }

    // Module count

    [Fact]
    public void ModuleCount_ExactBoundary_Succeeds()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export const x = 1;",
            ["b.js"] = "export const y = 2;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 2);

        engine.Modules.Import("a.js");
        engine.Modules.Import("b.js");
    }

    [Fact]
    public void ModuleCount_OverLimit_ThrowsModuleGraphLimitException()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export const x = 1;",
            ["b.js"] = "export const y = 2;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 1);

        engine.Modules.Import("a.js");
        Invoking(() => engine.Modules.Import("b.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void ModuleCount_DuplicateImportCountsOnce()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export const x = 1;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 1);

        engine.Modules.Import("a.js");
        // Re-importing the same module should not count again.
        engine.Modules.Import("a.js");
    }

    [Fact]
    public void ModuleCount_ProgrammaticModuleParticipates()
    {
        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(new Dictionary<string, string>
            {
                ["b.js"] = "import { v } from 'lib'; export const x = v;",
            }));
            o.Modules.MaxModuleCount = 2;
        });

        engine.Modules.Add("lib", builder => builder.ExportValue("v", 42));
        // 'lib' counts as 1, 'b.js' as 2: at the limit.
        engine.Modules.Import("b.js");
    }

    [Fact]
    public void ModuleCount_DiamondCountsOnce()
    {
        // a -> b, a -> c, b -> d, c -> d: diamond, d should count once.
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; import './c.js'; export default 1;",
            ["b.js"] = "import './d.js'; export default 2;",
            ["c.js"] = "import './d.js'; export default 3;",
            ["d.js"] = "export default 4;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 4);

        engine.Modules.Import("a.js");
    }

    [Fact]
    public void ModuleCount_CycleCountsOnce()
    {
        // a -> b -> a cycle: each module is registered once.
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; export const x = 1;",
            ["b.js"] = "import './a.js'; export const y = 2;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 2);

        engine.Modules.Import("a.js");
    }

    // Total source bytes

    [Fact]
    public void SourceBytes_ExactBoundary_Succeeds()
    {
        var source = "export const x = 1;";
        var byteCount = Encoding.UTF8.GetByteCount(source);
        var modules = new Dictionary<string, string> { ["a.js"] = source };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxTotalModuleSourceBytes = byteCount);

        engine.Modules.Import("a.js");
    }

    [Fact]
    public void SourceBytes_OverLimit_ThrowsModuleGraphLimitException()
    {
        var source = "export const x = 1;";
        var byteCount = Encoding.UTF8.GetByteCount(source);
        var modules = new Dictionary<string, string> { ["a.js"] = source };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxTotalModuleSourceBytes = byteCount - 1);

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void SourceBytes_ProgrammaticExportsOnlyChargesZero()
    {
        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(new Dictionary<string, string>()));
            o.Modules.MaxTotalModuleSourceBytes = 1; // only 1 byte allowed
            o.Modules.MaxModuleCount = 1;
        });

        // Exports-only modules charge 0 bytes and do not exceed the 1-byte limit.
        engine.Modules.Add("lib", builder => builder.ExportValue("v", 42));
        engine.Modules.Import("lib");
    }

    [Fact]
    public void SourceBytes_PreparedModuleChargesZero()
    {
        var engine = new Engine(o =>
        {
            o.UseModules(new PreparedLoader());
            o.Modules.MaxTotalModuleSourceBytes = 1;
        });

        engine.Modules.Import("prepared");
    }

    [Fact]
    public void SourceBytes_RawBytesUseExactLength()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var engine = new Engine(o =>
        {
            o.UseModules(new BytesLoader(bytes));
            o.Modules.MaxTotalModuleSourceBytes = bytes.Length - 1;
        });

        Invoking(() => engine.Modules.Import("bytes"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void SourceBytes_JsonAndTextModulesUseUtf8Length()
    {
        var limit = Encoding.UTF8.GetByteCount(TextAndJsonLoader.Json)
            + Encoding.UTF8.GetByteCount(TextAndJsonLoader.Text);
        var engine = new Engine(o =>
        {
            o.UseModules(new TextAndJsonLoader());
            o.Modules.MaxTotalModuleSourceBytes = limit;
        });

        engine.Modules.Import("json");
        engine.Modules.Import("text");
    }

    [Fact]
    public void SourceBytes_ProgrammaticSourcesIncludeInsertedLineBreaks()
    {
        const string first = "export const a = 1;";
        const string second = "export const b = 2;";
        var sourceBytesOnly = Encoding.UTF8.GetByteCount(first) + Encoding.UTF8.GetByteCount(second);
        var engine = new Engine(o => o.Modules.MaxTotalModuleSourceBytes = sourceBytesOnly);
        engine.Modules.Add("joined", builder => builder.AddSource(first).AddSource(second));

        Invoking(() => engine.Modules.Import("joined"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void SourceBytes_AsynchronousBytesAreRejectedByTheEnginePump()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(o =>
        {
            o.UseModules(loader);
            o.Modules.MaxTotalModuleSourceBytes = 3;
        });
        var operation = engine.Modules.StartImport("bytes");

        loader.Deliver("bytes", new byte[] { 1, 2, 3, 4 });

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().Throw<ModuleGraphLimitException>();
        operation.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void SourceBytes_AsynchronousStringLoadsAreCumulative()
    {
        const string first = "export const first = 1;";
        const string second = "export const second = 2;";
        var loader = new DeferredLoader();
        var engine = new Engine(o =>
        {
            o.UseModules(loader);
            o.Modules.MaxTotalModuleSourceBytes =
                Encoding.UTF8.GetByteCount(first) + Encoding.UTF8.GetByteCount(second) - 1;
        });

        var firstImport = engine.Modules.StartImport("first.js");
        loader.Deliver("first.js", first);
        engine.Tasks.ProcessTasks();
        firstImport.IsCompleted.Should().BeTrue();

        var secondImport = engine.Modules.StartImport("second.js");
        loader.Deliver("second.js", second);

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().Throw<ModuleGraphLimitException>();
        secondImport.IsCompleted.Should().BeFalse();
    }

    // Graph depth

    [Fact]
    public void GraphDepth_ExactBoundary_Succeeds()
    {
        // Chain: a -> b -> c, depth = 3.
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; export default 1;",
            ["b.js"] = "import './c.js'; export default 2;",
            ["c.js"] = "export default 3;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleGraphDepth = 3);

        engine.Modules.Import("a.js");
    }

    [Fact]
    public void GraphDepth_OverLimit_ThrowsModuleGraphLimitException()
    {
        // Chain: a -> b -> c, depth = 3, limit = 2.
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; export default 1;",
            ["b.js"] = "import './c.js'; export default 2;",
            ["c.js"] = "export default 3;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleGraphDepth = 2);

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void GraphDepth_CycleContributesEachDistinctModule()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; export const a = 1;",
            ["b.js"] = "import './a.js'; export const b = 1;",
        };
        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleGraphDepth = 1);

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Theory]
    [InlineData("import './shared.js'; import './one.js';")]
    [InlineData("import './one.js'; import './shared.js';")]
    public void GraphDepth_SharedSubtreeUsesLongestPathRegardlessOfSiblingOrder(string imports)
    {
        var modules = new Dictionary<string, string>
        {
            ["root.js"] = imports,
            ["one.js"] = "import './two.js';",
            ["two.js"] = "import './shared.js';",
            ["shared.js"] = "import './leaf.js';",
            ["leaf.js"] = "export default 1;",
        };
        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleGraphDepth = 4);

        Invoking(() => engine.Modules.Import("root.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    private const int LongChainLength = 1_000;

    /// <summary>A chain of <paramref name="count"/> modules, each importing the next.</summary>
    private static Dictionary<string, string> ImportChain(int count)
    {
        var modules = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (var i = 0; i < count - 1; i++)
        {
            modules[$"{i}.js"] = $"import './{i + 1}.js';";
        }

        modules[$"{count - 1}.js"] = "export default 1;";
        return modules;
    }

    private static ModuleRecord ChainRoot(Dictionary<string, string> modules, int maximumGraphDepth)
    {
        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(modules));
            o.Modules.MaxModuleCount = modules.Count + 1;
            o.Modules.MaxModuleGraphDepth = maximumGraphDepth;
        });

        var request = new ModuleRequest("0.js", []);
        var resolved = new ResolvedSpecifier(request, "0.js", new Uri("file:///base/0.js"), SpecifierType.RelativeOrAbsolute);
        return ModuleFactory.BuildSourceTextModule(engine, resolved, modules["0.js"]);
    }

    /// <summary>
    /// The load phase walks a thousand-deep import chain <b>iteratively</b>, so how deep a graph a host can
    /// load is a question about <see cref="Options.ModuleOptions.MaxModuleGraphDepth"/> and not about how
    /// much stack the calling thread happens to have.
    /// </summary>
    /// <remarks>
    /// The load phase, and only the load phase: <c>LoadRequestedModules</c> is what this drives, rather than
    /// a whole <c>Import</c>. Linking and evaluation still recurse once per module, so an import of this
    /// same graph is bounded by the stack — that is
    /// <see href="https://github.com/sebastienros/jint/issues/3308">#3308</see>, where a thousand nested
    /// <c>InnerModuleLinking</c> frames overflowed the stack and ended the test process on macOS under
    /// <c>net8.0</c> while passing everywhere else. Asserting the import here asserted that gap did not
    /// exist on whichever runtime and operating system ran the suite, which is not a property of Jint.
    /// <see cref="GraphDepth_LongSynchronousChainImportsOnAStackSizedForTheRecursivePhases"/> keeps the
    /// import covered, on a stack chosen for it.
    /// </remarks>
    [Fact]
    public void GraphDepth_LongSynchronousChainLoadsIteratively()
    {
        var modules = ImportChain(LongChainLength);

        ChainRoot(modules, LongChainLength).LoadRequestedModules();

        // ...and it reached the bottom rather than stopping short: the chain is exactly this deep, so one
        // less is the limit that has to fail. Without this the test above would still pass against a loader
        // that quietly gave up part-way.
        Invoking(() => ChainRoot(modules, LongChainLength - 1).LoadRequestedModules())
            .Should().Throw<ModuleGraphLimitException>();
    }

    /// <summary>
    /// The same graph, imported end to end. On a stack sized for it, because linking and evaluation each
    /// recurse once per module — see the remarks on
    /// <see cref="GraphDepth_LongSynchronousChainLoadsIteratively"/>.
    /// </summary>
    [Fact]
    public void GraphDepth_LongSynchronousChainImportsOnAStackSizedForTheRecursivePhases()
    {
        var modules = ImportChain(LongChainLength);

        DedicatedThread.Run(() =>
        {
            var engine = new Engine(o =>
            {
                o.UseModules(new DictLoader(modules));
                o.Modules.MaxModuleCount = LongChainLength;
                o.Modules.MaxModuleGraphDepth = LongChainLength;
            });

            engine.Modules.Import("0.js");
        });
    }

    // Resolution hops

    [Fact]
    public void ResolutionHops_ExactBoundary_Succeeds()
    {
        // a -> b -> c = 3 hops (one per resolve: a, b, c).
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; export default 1;",
            ["b.js"] = "import './c.js'; export default 2;",
            ["c.js"] = "export default 3;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleResolutionHops = 3);

        engine.Modules.Import("a.js");
    }

    [Fact]
    public void ResolutionHops_OverLimit_ThrowsModuleGraphLimitException()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "import './b.js'; export default 1;",
            ["b.js"] = "import './c.js'; export default 2;",
            ["c.js"] = "export default 3;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleResolutionHops = 2);

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void ResolutionHops_PerOperation_NotCumulative()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export default 1;",
            ["b.js"] = "export default 2;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleResolutionHops = 1);

        // Each import is its own operation with its own budget.
        engine.Modules.Import("a.js");
        engine.Modules.Import("b.js");
    }

    [Fact]
    public void ResolutionHops_AsyncGraphKeepsOneBudgetAfterRootSettles()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(o => o.UseModules(loader)
            .Modules.MaxModuleResolutionHops = 2);

        engine.Modules.StartImport("a.js");
        loader.Deliver("a.js", "import './b.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./b.js", "import './c.js';");

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void ResolutionHops_RegistrationIndexingDoesNotConsumeTheBudget()
    {
        var engine = new Engine(o => o.UseModules(new PrefixingLoader())
            .Modules.MaxModuleResolutionHops = 1);
        for (var i = 0; i < 20; i++)
        {
            engine.Modules.Add($"module-{i}", $"export default {i};");
        }

        engine.Modules.Import("module-19");
    }

    [Fact]
    public void ResolutionHops_InFlightDuplicateRequestsEachConsumeAHop()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(o => o.UseModules(loader)
            .Modules.MaxModuleResolutionHops = 2);

        engine.Modules.StartImport("root.js");
        loader.Deliver(
            "root.js",
            "import defer * as deferred from './dep.js'; import './dep.js';");

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().Throw<ModuleGraphLimitException>();
        loader.LoadsFor("./dep.js").Should().Be(1);
    }

    [Fact]
    public void GraphDepth_AsyncGraphFailsFromThePump()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(o => o.UseModules(loader)
            .Modules.MaxModuleGraphDepth = 2);

        engine.Modules.StartImport("a.js");
        loader.Deliver("a.js", "import './b.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./b.js", "import './c.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./c.js", "export default 1;");

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void ModuleCount_AsyncDiamondCoalescesSharedLoad()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(o => o.UseModules(loader)
            .Modules.MaxModuleCount = 4);

        var operation = engine.Modules.StartImport("root.js");
        loader.Deliver("root.js", "import './left.js'; import './right.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./left.js", "import './shared.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./right.js", "import './shared.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./shared.js", "export default 1;");
        engine.Tasks.ProcessTasks();

        operation.IsCompleted.Should().BeTrue();
        loader.LoadsFor("./shared.js").Should().Be(1);
    }

    // Limit exception propagation

    [Fact]
    public void LimitException_IsNotJavaScriptException()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export const x = 1;",
            ["b.js"] = "export const y = 2;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 1);

        engine.Modules.Import("a.js");

        try
        {
            engine.Modules.Import("b.js");
            throw new Exception("Should have thrown");
        }
        catch (ModuleGraphLimitException)
        {
            // Expected: not a JavaScriptException, propagates like a constraint.
        }
    }

    [Fact]
    public void LimitException_NotCatchableByScript()
    {
        // Dynamic import that would exceed the limit should not be catchable as a rejection.
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export default 1;",
            ["b.js"] = "export default 2;",
        };

        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 1);

        engine.Modules.Import("a.js");

        // StartImport should throw the limit exception directly.
        Invoking(() => engine.Modules.StartImport("b.js"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void LimitException_FromDynamicImportIsNotARejection()
    {
        var modules = new Dictionary<string, string>
        {
            ["a.js"] = "export default 1;",
            ["b.js"] = "export default 2;",
        };
        var engine = new Engine(o => o.UseModules(new DictLoader(modules))
            .Modules.MaxModuleCount = 1);
        engine.Modules.Import("a.js");

        Invoking(() => engine.Execute("import('b.js').catch(() => globalThis.caught = true);"))
            .Should().Throw<ModuleGraphLimitException>();
    }

    [Fact]
    public void OrdinaryCancellationFromSynchronousLoaderIsALoadFailure()
    {
        var engine = new Engine(o => o.UseModules(
            new ThrowingModuleLoader(new OperationCanceledException("transport canceled"))));

        Invoking(() => engine.Modules.Import("cancel"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Could not load module.");
    }

    [Fact]
    public void OrdinaryTimeoutFromSynchronousLoaderIsALoadFailure()
    {
        var engine = new Engine(o => o.UseModules(
            new ThrowingModuleLoader(new TimeoutException("transport timed out"))));

        Invoking(() => engine.Modules.Import("timeout"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Could not load module.");
    }

    [Fact]
    public void OrdinaryTimeoutFromStartImportResolveIsALoadFailure()
    {
        var engine = new Engine(o => o.UseModules(
            new ThrowingResolveLoader(new TimeoutException("transport timed out"))));

        var operation = engine.Modules.StartImport("timeout");
        engine.Tasks.ProcessTasks();

        operation.IsCompleted.Should().BeTrue();
        Invoking(() => operation.GetResult()).Should().Throw<PromiseRejectedException>();
    }

    [Fact]
    public void OrdinaryTimeoutFromAsyncLoaderIsALoadFailure()
    {
        var engine = new Engine(o => o.UseModules(
            new FaultingAsyncModuleLoader(new TimeoutException("transport timed out"))));

        var operation = engine.Modules.StartImport("timeout");
        engine.Tasks.ProcessTasks();

        operation.IsCompleted.Should().BeTrue();
        Invoking(() => operation.GetResult()).Should().Throw<PromiseRejectedException>();
    }

    [Fact]
    public void RegisteredCancellationFromSynchronousLoaderPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new Engine(o =>
        {
            o.ObserveCancellation(cancellation.Token);
            o.UseModules(new ThrowingModuleLoader(
                new OperationCanceledException(cancellation.Token)));
        });

        Invoking(() => engine.Modules.Import("cancel"))
            .Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void EngineTimeoutFromSynchronousLoaderPropagates()
    {
        var engine = new Engine(o =>
        {
            o.LimitExecutionTime(TimeSpan.FromMilliseconds(1));
            o.UseModules(new TimingOutEngineModuleLoader());
        });

        Invoking(() => engine.Modules.Import("timeout"))
            .Should().ThrowExactly<TimeoutException>();
    }

    [Fact]
    public void EngineTimeoutFromAsyncLoaderPropagates()
    {
        var engine = new Engine(o =>
        {
            o.LimitExecutionTime(TimeSpan.FromMilliseconds(1));
            o.UseModules(new TimingOutAsyncModuleLoader());
        });

        Invoking(() => engine.Modules.StartImport("timeout"))
            .Should().ThrowExactly<TimeoutException>();
    }

    [Fact]
    public void EngineRegexTimeoutFromSynchronousLoaderPropagates()
    {
        var engine = new Engine(o =>
        {
            o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(1);
            o.UseModules(new RegexTimingOutEngineModuleLoader());
        });

        Invoking(() => engine.Modules.Import("timeout"))
            .Should().ThrowExactly<RegexMatchTimeoutException>();
    }

    [Fact]
    public void RegisteredCancellationFromAsyncLoaderPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new Engine(o =>
        {
            o.ObserveCancellation(cancellation.Token);
            o.UseModules(new CancelingAsyncModuleLoader());
        });

        Invoking(() => engine.Modules.StartImport("cancel"))
            .Should().Throw<OperationCanceledException>();
    }

    // Custom policy

    [Fact]
    public void CustomPolicy_DenyAll_ThrowsModuleResolutionException()
    {
        var modules = new Dictionary<string, string> { ["a.js"] = "export default 1;" };

        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(modules));
            o.Modules.LoadPolicy = new DenyAllPolicy();
        });

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void CustomPolicy_AllowAll_Succeeds()
    {
        var modules = new Dictionary<string, string> { ["a.js"] = "export default 1;" };

        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(modules));
            o.Modules.LoadPolicy = new AllowAllPolicy();
        });

        engine.Modules.Import("a.js");
    }

    // Built-in allowlist policy

    [Fact]
    public void AllowlistPolicy_AllowedScheme_Succeeds()
    {
        var modules = new Dictionary<string, string> { ["a.js"] = "export default 1;" };

        var policy = new ModuleAllowlistPolicy { AllowedSchemes = { "file" } };

        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(modules));
            o.Modules.LoadPolicy = policy;
        });

        engine.Modules.Import("a.js");
    }

    [Fact]
    public void AllowlistPolicy_DisallowedScheme_Denied()
    {
        var modules = new Dictionary<string, string> { ["a.js"] = "export default 1;" };

        var policy = new ModuleAllowlistPolicy { AllowedSchemes = { "https" } };

        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(modules));
            o.Modules.LoadPolicy = policy;
        });

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void AllowlistPolicy_BareSpecifier_DeniedByDefault()
    {
        var modules = new Dictionary<string, string> { ["a.js"] = "export default 1;" };

        // Any dimension configured triggers bare check.
        var policy = new ModuleAllowlistPolicy { AllowedSchemes = { "file" } };

        var engine = new Engine(o =>
        {
            o.UseModules(new BareLoader(modules));
            o.Modules.LoadPolicy = policy;
        });

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void AllowlistPolicy_BareSpecifier_AllowedWhenExplicit()
    {
        var modules = new Dictionary<string, string> { ["a.js"] = "export default 1;" };

        var policy = new ModuleAllowlistPolicy
        {
            AllowedSchemes = { "file" },
            AllowBareSpecifiers = true,
        };

        var engine = new Engine(o =>
        {
            o.UseModules(new BareLoader(modules));
            o.Modules.LoadPolicy = policy;
        });

        engine.Modules.Import("a.js");
    }

    [Fact]
    public void AllowlistPolicy_RequiresEveryConfiguredUriDimension()
    {
        var policy = new ModuleAllowlistPolicy
        {
            AllowedSchemes = { "https" },
            AllowedHosts = { "cdn.example.com" },
            AllowedOrigins = { "https://cdn.example.com" },
        };
        var engine = new Engine(o =>
        {
            o.UseModules(new UriLoader());
            o.Modules.LoadPolicy = policy;
        });

        engine.Modules.Import("https://cdn.example.com/a.js");
        Invoking(() => engine.Modules.Import("https://cdn.example.com:444/b.js"))
            .Should().Throw<ModuleResolutionException>();
        Invoking(() => engine.Modules.Import("https://other.example.com/c.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void AllowlistPolicy_FileRootUsesASeparatorBoundary()
    {
        var policy = new ModuleAllowlistPolicy { AllowedFileRoots = { "/allowed" } };
        var allowed = new Engine(o =>
        {
            o.UseModules(new DictLoader(
                new Dictionary<string, string> { ["a.js"] = "export default 1;" },
                "/allowed"));
            o.Modules.LoadPolicy = policy;
        });
        var sibling = new Engine(o =>
        {
            o.UseModules(new DictLoader(
                new Dictionary<string, string> { ["a.js"] = "export default 1;" },
                "/allowed-sibling"));
            o.Modules.LoadPolicy = policy;
        });

        allowed.Modules.Import("a.js");
        Invoking(() => sibling.Modules.Import("a.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void AllowlistPolicy_FileRootsDenyNonFileUris()
    {
        var engine = new Engine(o =>
        {
            o.UseModules(new UriLoader());
            o.Modules.LoadPolicy = new ModuleAllowlistPolicy
            {
                AllowedFileRoots = { "/allowed" },
            };
        });

        Invoking(() => engine.Modules.Import("https://cdn.example.com/a.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void AllowlistPolicy_HostsDenyFileUris()
    {
        var engine = new Engine(o =>
        {
            o.UseModules(new DictLoader(
                new Dictionary<string, string> { ["a.js"] = "export default 1;" },
                "/allowed"));
            o.Modules.LoadPolicy = new ModuleAllowlistPolicy
            {
                AllowedHosts = { "cdn.example.com" },
            };
        });

        Invoking(() => engine.Modules.Import("a.js"))
            .Should().Throw<ModuleResolutionException>();
    }

    [Fact]
    public void DefaultModuleLoader_RemainsRestrictedToItsBasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "jint-module-policy-" + Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "child");
        try
        {
            Directory.CreateDirectory(child);
            File.WriteAllText(Path.Combine(root, "outside.js"), "export default 1;");
            var engine = new Engine(o => o.UseModules(child));

            Invoking(() => engine.Modules.Import("../outside.js"))
                .Should().Throw<ModuleResolutionException>();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // Invalid limits at construction

    [Fact]
    public void InvalidLimits_Zero_ThrowsArgumentException()
    {
        Invoking(() => new Engine(o =>
        {
            o.UseModules(new DictLoader(new Dictionary<string, string>()));
            o.Modules.MaxModuleCount = 0;
        })).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InvalidLimits_Negative_ThrowsArgumentException()
    {
        Invoking(() => new Engine(o =>
        {
            o.UseModules(new DictLoader(new Dictionary<string, string>()));
            o.Modules.MaxModuleResolutionHops = -1;
        })).Should().Throw<ArgumentException>();
    }

    // Default (unlimited) behavior unchanged

    [Fact]
    public void DefaultLimits_NoRestriction()
    {
        var modules = new Dictionary<string, string>();
        for (var i = 0; i < 50; i++)
        {
            modules[$"m{i}.js"] = "export default " + i + ";";
        }

        var engine = new Engine(o => o.UseModules(new DictLoader(modules)));

        for (var i = 0; i < 50; i++)
        {
            engine.Modules.Import($"m{i}.js");
        }
    }

    // Modules disabled unchanged

    [Fact]
    public void ModulesDisabled_UnchangedBehavior()
    {
        var engine = new Engine();
        Invoking(() => engine.Modules.Import("anything"))
            .Should().Throw<InvalidOperationException>();
    }

}
