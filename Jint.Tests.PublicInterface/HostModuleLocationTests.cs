#nullable enable

using System.Collections.Generic;
using System.IO;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Modules;


namespace Jint.Tests.PublicInterface;

/// <summary>
/// The name a loaded module knows itself by — <see cref="ModuleRecord.Location"/>. These tests live in the
/// public-interface suite on purpose: the project has no <c>InternalsVisibleTo</c> grant, so every green
/// assertion here is proof that a third-party embedder can reach the same capability through
/// <see cref="ModuleLoader"/>, <see cref="ResolvedSpecifier"/>, <see cref="ModuleFactory"/> and
/// <see cref="Host.GetImportMetaProperties"/> alone.
/// </summary>
/// <remarks>
/// That name is load-bearing three ways over: it is the <c>referencingModuleLocation</c> handed back to
/// <see cref="ModuleLoader.Resolve"/> for the module's own imports, the module's identity in a stack trace
/// and to the debugger, and the only name a module has to report through <c>import.meta.url</c>.
/// </remarks>
public class HostModuleLocationTests
{
    /// <summary>
    /// Serves modules from a dictionary keyed by url, resolving a relative specifier against the importing
    /// module the way a browser does — which is only possible if that module knows its own url.
    /// </summary>
    /// <remarks>
    /// An absolute specifier becomes its key <em>verbatim</em>, so the key is deliberately not the
    /// canonicalized form <see cref="Uri.AbsoluteUri"/> would produce. That is what lets these tests tell the
    /// two candidate policies apart.
    /// </remarks>
    private sealed class UrlModuleLoader : ModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _modules;

        public UrlModuleLoader(IReadOnlyDictionary<string, string> modules)
        {
            _modules = modules;
        }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (Uri.TryCreate(moduleRequest.Specifier, UriKind.Absolute, out var absolute))
            {
                return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, absolute, SpecifierType.RelativeOrAbsolute);
            }

            // The invariant under test: a module's location has to be a url for its own relative imports to
            // resolve against. Saying so here keeps a regression from surfacing as an opaque UriFormatException
            // out of Resolve, which ModuleLoader.LoadModule does not wrap and which names nothing.
            if (!Uri.TryCreate(referencingModuleLocation, UriKind.Absolute, out var referrer))
            {
                throw new InvalidOperationException(
                    $"cannot resolve '{moduleRequest.Specifier}': the referring module's location is not an absolute url, but '{referencingModuleLocation}'");
            }

            var uri = new Uri(referrer, moduleRequest.Specifier);
            return new ResolvedSpecifier(moduleRequest, uri.ToString(), uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => _modules.TryGetValue(resolved.Key, out var code)
                ? code
                : throw new InvalidOperationException("no module at " + resolved.Key);
    }

    /// <summary>
    /// Reports each module's location through <c>import.meta.url</c>, which is the only way a module's own
    /// name is observable from script — Jint leaves <c>import.meta</c> to the host.
    /// </summary>
    private sealed class ImportMetaUrlHost : Host
    {
        public override List<KeyValuePair<JsValue, JsValue>> GetImportMetaProperties(ModuleRecord moduleRecord)
        {
            var properties = base.GetImportMetaProperties(moduleRecord);
            properties.Add(new KeyValuePair<JsValue, JsValue>("url", moduleRecord.Location));
            return properties;
        }
    }

    private static Engine EngineWith(ModuleLoader loader) => new Engine(options =>
    {
        options.UseModules(loader);
        options.UseHostFactory(_ => new ImportMetaUrlHost());
    });

    [Test]
    public void LoadedModuleKnowsItselfByTheNameItsLoaderChose()
    {
        // Both keys differ from what Uri.AbsoluteUri would produce for the same uri — it strips a default
        // port and percent-encodes — so this is the assertion that states the choice between the resolved
        // key and the canonicalized url. The key is also what the module map is keyed on, so reporting
        // anything else would let a module's own name and the name it is cached under drift apart.
        var engine = EngineWith(new UrlModuleLoader(new Dictionary<string, string>
        {
            ["http://localhost:80/lib/entry.js"] = "export const url = import.meta.url;",
            ["http://localhost/lib/my report.js"] = "export const url = import.meta.url;",
        }));

        engine.Modules.Import("http://localhost:80/lib/entry.js").Get("url").AsString()
            .Should().Be("http://localhost:80/lib/entry.js");
        engine.Modules.Import("http://localhost/lib/my report.js").Get("url").AsString()
            .Should().Be("http://localhost/lib/my report.js");
    }

    [Test]
    public void LoadedModuleResolvesARelativeImportAgainstItsOwnUrl()
    {
        // The referrer a module's own imports are resolved against is its location, so reducing that to a
        // path leaves './dep.js' nothing to resolve against.
        var engine = new Engine(options => options.UseModules(new UrlModuleLoader(new Dictionary<string, string>
        {
            ["http://localhost/lib/entry.js"] = "export { value } from './dep.js';",
            ["http://localhost/lib/dep.js"] = "export const value = 'from the sibling';",
        })));

        var ns = engine.Modules.Import("http://localhost/lib/entry.js");

        ns.Get("value").AsString().Should().Be("from the sibling");
    }

    [Test]
    public void LoadedModuleNamesItselfByItsKeyInAStackTrace()
    {
        // The same string is the module's identity in error.stack, so a host parsing frames sees the url.
        var engine = new Engine(options => options.UseModules(new UrlModuleLoader(new Dictionary<string, string>
        {
            ["http://localhost/lib/entry.js"] = "export function boom() { throw new Error('bang'); }\nboom();",
        })));

        var exception = Invoking(() => engine.Modules.Import("http://localhost/lib/entry.js"))
            .Should().Throw<JavaScriptException>().Which;

        exception.JavaScriptStackTrace.Should().Contain("http://localhost/lib/entry.js");
    }

    [Test]
    public void ADebuggerCanBreakInAModuleLoadedFromAUrl()
    {
        // BreakPointCollection keys on BreakLocation.Source, which is this same string. Nothing else covers a
        // non-file: scheme under the debugger, so without this a change to the naming rule would silently
        // stop every such breakpoint from hitting and the debugger would just look dead.
        var engine = new Engine(options =>
        {
            options.UseModules(new UrlModuleLoader(new Dictionary<string, string>
            {
                ["http://localhost/lib/entry.js"] = "const a = 1;\nexport const value = a + 1;",
            }));
            options.Debugger.Enabled = true;
        });

        var hits = new List<string?>();
        engine.Debugger.BreakPoints.Set(new BreakPoint("http://localhost/lib/entry.js", 2, 0));
        engine.Debugger.Break += (_, info) =>
        {
            hits.Add(info.Location.SourceFile);
            return StepMode.None;
        };

        engine.Modules.Import("http://localhost/lib/entry.js");

        hits.Should().ContainSingle().Which.Should().Be("http://localhost/lib/entry.js");
    }

    [Test]
    public void LoadedFileModuleKnowsItselfByPath()
    {
        // A file: url is still reported as a filesystem path, which is what every host on DefaultModuleLoader
        // already sees, and what code doing Path.GetDirectoryName on a module's location depends on.
        // The directory name carries a guid because the finally below recursively deletes it: a machine-wide
        // name would let one run delete a concurrent run's directory, or a directory that was already there.
        var path = Path.Combine(Path.GetTempPath(), "jint-module-location-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(path, "entry.js");

        try
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(file, "export const url = import.meta.url;");

            var engine = new Engine(options =>
            {
                options.UseModules(path);
                options.UseHostFactory(_ => new ImportMetaUrlHost());
            });
            var url = engine.Modules.Import("./entry.js").Get("url").AsString();

            url.Should().NotStartWith("file:");
            url.Should().Be(new Uri(file).LocalPath);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    private static ResolvedSpecifier Resolved(string key, Uri? uri)
        => new ResolvedSpecifier(new ModuleRequest(key, []), key, uri, SpecifierType.Bare);

    public static TestCases<string, Uri?> UnusableUris => new TestCases<string, Uri?>
    {
        { "no uri at all", null },
        { "a relative uri", new Uri("lib/dep.js", UriKind.Relative) },
    };

    /// <summary>
    /// Every factory overload taking a <see cref="ResolvedSpecifier"/> names the module by its key when the
    /// uri cannot answer the one question left for it — is this a file? A relative uri is the case that used
    /// to throw <see cref="InvalidOperationException"/> out of <see cref="Uri.LocalPath"/>; a null uri is the
    /// case that used to produce a null location for three of the four.
    /// </summary>
    [TestCaseSource(nameof(UnusableUris))]
    public void AModuleWhoseUriCannotNameItKnowsItselfByItsKey(string because, Uri? uri)
    {
        because.Should().NotBeEmpty();
        var engine = new Engine();

        ModuleFactory.BuildSourceTextModule(engine, Resolved("entry.js", uri), "export const value = 1;")
            .Location.Should().Be("entry.js");
        ModuleFactory.BuildJsonModule(engine, Resolved("config.json", uri), "{ \"value\": 1 }")
            .Location.Should().Be("config.json");
        ModuleFactory.BuildTextModule(engine, Resolved("readme.txt", uri), "hello")
            .Location.Should().Be("readme.txt");
        ModuleFactory.BuildBytesModule(engine, Resolved("blob.bin", uri), [1, 2, 3])
            .Location.Should().Be("blob.bin");
    }

    [Test]
    public void AModuleLoadedFromAFileUriKnowsItselfByPathThroughEveryFactory()
    {
        // The file: exception is not confined to source text: the same rule has to hold for the synthetic
        // module types, or a json module next to a script would be named differently from it.
        var engine = new Engine();
        var uri = new Uri("file:///lib/config.json");

        ModuleFactory.BuildJsonModule(engine, Resolved("file:///lib/config.json", uri), "{ \"value\": 1 }")
            .Location.Should().Be(uri.LocalPath);
        ModuleFactory.BuildTextModule(engine, Resolved("file:///lib/config.json", uri), "hello")
            .Location.Should().Be(uri.LocalPath);
        ModuleFactory.BuildBytesModule(engine, Resolved("file:///lib/config.json", uri), [1, 2, 3])
            .Location.Should().Be(uri.LocalPath);
    }
}
