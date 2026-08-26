#nullable enable

using System;
using System.IO;
using Jint.NodeCompat;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The opt-in <c>node:</c> builtin modules from an embedder's side. These tests live in the public-interface
/// suite on purpose: the project has no <c>InternalsVisibleTo</c> grant, so every green assertion here is
/// proof that a third-party host can reach the capability through <see cref="NodeBuiltinModuleOptions"/>,
/// <c>options.UseNodeBuiltinModules</c>, <see cref="NodeStyleModuleLoader"/> and <c>Engine.Modules.Add</c>
/// alone.
/// </summary>
public class HostNodeBuiltinModuleTests
{
    /// <summary>
    /// The plain case, and the two options a host has any reason to set: which platform <c>node:path</c>
    /// follows, and what stands in for <c>process.cwd()</c>.
    /// </summary>
    [Test]
    public void AHostCanEnableAndConfigureTheBuiltins()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules(o =>
        {
            o.Platform = "linux";
            o.WorkingDirectory = "/srv/app";
        }));

        var path = engine.Modules.Import("node:path");

        engine.SetValue("path", path.Get("default"));

        engine.Evaluate("path.sep").AsString().Should().Be("/");
        engine.Evaluate("path.join('a', 'b', '..', 'c')").AsString().Should().Be("a/c");
        engine.Evaluate("path.resolve('x')").AsString().Should().Be("/srv/app/x");
    }

    /// <summary>
    /// Composing with a real <c>node_modules</c> tree, which is the arrangement the feature exists for: the
    /// package resolves through <see cref="NodeStyleModuleLoader"/> and imports a builtin of its own.
    /// </summary>
    [Test]
    public void APackageOnDiskCanImportABuiltin()
    {
        var root = Path.Combine(Path.GetTempPath(), "jint-node-builtins-public", Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "main.js", "import { titleOf } from 'tiny-titles'; export const title = titleOf('/docs/getting-started.md');");
            Write(root, "node_modules/tiny-titles/package.json", "{ \"name\": \"tiny-titles\", \"main\": \"index.js\" }");
            Write(root, "node_modules/tiny-titles/index.js", "import { basename, extname } from 'node:path'; export function titleOf(p) { return basename(p, extname(p)); }");

            var engine = new Engine(options => options
                .UseModules(new NodeStyleModuleLoader(root))
                .UseNodeBuiltinModules(o => o.Platform = "linux"));

            engine.Modules.Import("./main.js").Get("title").AsString().Should().Be("getting-started");
        }
        finally
        {
            Delete(root);
        }
    }

    /// <summary>
    /// A module the host registers itself wins over the builtin, which is also how it supplies one of the
    /// modules Jint deliberately does not provide.
    /// </summary>
    [Test]
    public void AHostRegistrationTakesPrecedence()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        engine.Modules.Add("node:path", "export const sep = '::';");
        engine.Modules.Add("node:fs", "export function readFileSync() { return 'host'; }");

        engine.Modules.Import("node:path").Get("sep").AsString().Should().Be("::");
        engine.SetValue("fs", engine.Modules.Import("node:fs").Get("readFileSync"));
        engine.Evaluate("fs()").AsString().Should().Be("host");
    }

    /// <summary>
    /// An unknown <c>node:</c> specifier fails with a message naming what is available, so a host reading the
    /// exception can tell the difference between "not implemented" and "typo".
    /// </summary>
    [Test]
    public void AnUnknownBuiltinReportsWhatIsAvailable()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        var exception = Assert.Throws<ModuleResolutionException>(() => engine.Modules.Import("node:child_process"))!;

        exception.ResolverAlgorithmError.Should().Contain("node:path");
        exception.ResolverAlgorithmError.Should().Contain("deliberately not provided");
    }

    /// <summary>
    /// An engine that did not ask is unchanged, which is what makes the feature safe to add to the engine at
    /// all.
    /// </summary>
    [Test]
    public void AnEngineThatDidNotOptInIsUnchanged()
    {
        var engine = new Engine();

        Assert.Catch<Exception>(() => engine.Modules.Import("node:path"));
    }

    private static void Write(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static void Delete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
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
