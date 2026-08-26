#nullable enable

using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The name a programmatically registered module is found under — <c>Engine.Modules.Add</c> versus the key the
/// <see cref="ModuleLoader"/> resolves an import to. A registration has to have exactly one identity, because a
/// second one either hides it (the loader canonicalized and the engine looks under the canonical name only) or
/// over-exposes it (the raw import text matches, which for a relative specifier names a different file in every
/// importing directory).
/// </summary>
public class HostModuleBuilderIdentityTests
{
    /// <summary>
    /// Resolves every specifier through <see cref="Uri"/>, the way a loader serving urls has to, and refuses to
    /// load anything at all — so a test that reaches the loader fails loudly instead of silently getting the
    /// wrong source.
    /// </summary>
    private sealed class CanonicalizingModuleLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var uri = Uri.TryCreate(referencingModuleLocation, UriKind.Absolute, out var referrer)
                ? new Uri(referrer, moduleRequest.Specifier)
                : new Uri(moduleRequest.Specifier, UriKind.Absolute);

            return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("the loader was asked to load " + resolved.Key);
    }

    [Test]
    public void ARegisteredModuleIsFoundWhenTheLoaderCanonicalizesItsName()
    {
        // `new Uri("http://localhost").AbsoluteUri` is "http://localhost/", so the registration name and the
        // resolved key differ by the path Uri supplies. The loader throws if it is reached, which is what the
        // mismatch used to cause.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost", "export const value = 'from the builder';");

        engine.Modules.Import("http://localhost").Get("value").AsString().Should().Be("from the builder");
    }

    [Test]
    public void ARegisteredModuleIsFoundUnderTheCanonicalSpellingToo()
    {
        // The direction a raw-specifier fallback cannot reach: the import is written the way an import map or a
        // generated specifier writes it, so it already equals the resolved key and never needs the fallback -
        // but the registration is still filed under the shorter name.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost", "export const value = 'from the builder';");

        engine.Modules.Import("http://localhost/").Get("value").AsString().Should().Be("from the builder");
    }

    [Test]
    public void EitherSpellingReachesTheSameModuleInstance()
    {
        // Both spellings resolve to one key, so they are one module - not two, and not one plus a failure once
        // the registration has been consumed.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost", "export const value = {};");

        var first = engine.Modules.Import("http://localhost");
        var second = engine.Modules.Import("http://localhost/");

        second.Should().BeSameAs(first);
    }

    [Test]
    public void ARegisteredRelativeNameIsNotHandedToAnUnrelatedImportSpellingTheSameText()
    {
        // The negative fact, and the reason the raw ModuleRequest.Specifier cannot be the match key: `./dep.js`
        // written inside `sub/entry.js` means `sub/dep.js`, which is not what `Add("./dep.js")` registered.
        // Matching the text would hand this module the host's registration and never load the real sibling.
        var path = Path.Combine(Path.GetTempPath(), "jint-builder-identity-" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(path, "sub");

        try
        {
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "entry.js"), "export { origin } from './dep.js';");
            File.WriteAllText(Path.Combine(sub, "dep.js"), "export const origin = 'the real sibling';");

            var engine = new Engine(options => options.UseModules(path));
            engine.Modules.Add("./dep.js", "export const origin = 'the registration';");

            engine.Modules.Import("./sub/entry.js").Get("origin").AsString().Should().Be("the real sibling");

            // ...and the registration is still there for the name it actually named.
            engine.Modules.Import("./dep.js").Get("origin").AsString().Should().Be("the registration");
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Test]
    public void ARegistrationIsConsumedUnderTheNameItWasFiledUnder()
    {
        // Pins the other half: LoadFromBuilder removes the registration, and it has to remove the entry that is
        // actually in the dictionary rather than the resolved key. It caches into the module map before
        // removing, so passing the wrong name leaves a builder entry alive for the engine's lifetime and no
        // import ever notices. Re-registering the same specifier is what notices.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost", "export const value = 'first';");
        engine.Modules.Import("http://localhost").Get("value").AsString().Should().Be("first");

        Invoking(() => engine.Modules.Add("http://localhost", "export const value = 'second';"))
            .Should().NotThrow("the registration was consumed by the load, so the name is free again");

        // The already-cached module keeps answering imports of the name - a load consumes the builder, it
        // does not evict the module - so the re-registration is never consulted for this key.
        engine.Modules.Import("http://localhost").Get("value").AsString().Should().Be("first");
    }

    [Test]
    public void ARegisteredModuleResolvesItsOwnRelativeImportAgainstItsResolvedKey()
    {
        // A builder module's location is the key it resolved to, not the name it was registered under, because
        // the location is the referrer that the module's *own* imports resolve against. With a loader mapping
        // the bare name `lib` into a virtual tree, a module registered as `lib` and named `lib` would resolve
        // its nested `./util.js` against a name that has no directory at all.
        var engine = new Engine(options => options.UseModules(new VirtualTreeLoader(new Dictionary<string, string>
        {
            ["/vfs/util.js"] = "export const value = 'from the virtual sibling';",
        })));

        engine.Modules.Add("lib", "export { value } from './util.js';");

        engine.Modules.Import("lib").Get("value").AsString().Should().Be("from the virtual sibling");
    }

    /// <summary>
    /// Maps a bare name into a virtual tree — the shape of a loader serving modules from something other than a
    /// filesystem — and resolves a relative specifier against the referring module's directory. A registration
    /// name and the key it resolves to therefore have nothing in common, which is what makes the module's own
    /// location observable.
    /// </summary>
    private sealed class VirtualTreeLoader : ModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _modules;

        public VirtualTreeLoader(IReadOnlyDictionary<string, string> modules)
        {
            _modules = modules;
        }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var specifier = moduleRequest.Specifier;
            string key;

            if (specifier.StartsWith("./", StringComparison.Ordinal))
            {
                var referrer = referencingModuleLocation ?? "";
                var lastSlash = referrer.LastIndexOf('/');
                key = referrer.Substring(0, lastSlash + 1) + specifier.Substring(2);
            }
            else
            {
                key = "/vfs/" + specifier + ".js";
            }

            return new ResolvedSpecifier(moduleRequest, key, Uri: null, SpecifierType.Bare);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => _modules.TryGetValue(resolved.Key, out var code)
                ? code
                : throw new InvalidOperationException("no module at " + resolved.Key);
    }

    [Test]
    public void ARegistrationTheLoaderRefusesToResolveDoesNotBreakAnUnrelatedImport()
    {
        // Resolution is lazy and per-registration, so a specifier DefaultModuleLoader rejects - a directory
        // import here - must not surface on the import that happens to trigger the indexing pass.
        var path = Path.Combine(Path.GetTempPath(), "jint-builder-identity-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "entry.js"), "export const value = 'from disk';");

            var engine = new Engine(options => options.UseModules(path));
            engine.Modules.Add("./not-a-module", "export const value = 'unreachable';");

            engine.Modules.Import("./entry.js").Get("value").AsString().Should().Be("from disk");
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }

    [Test]
    public void RegisteringAModuleDoesNotRequireModulesToBeEnabled()
    {
        // Add stays independent of the loader, which is why the resolution is lazy: with modules disabled the
        // loader throws from LoadModule, and registration must not reach it.
        var engine = new Engine();

        Invoking(() => engine.Modules.Add("lib", "export const value = 1;")).Should().NotThrow();
    }

    [Test]
    public void ALaterRegistrationIsIndexedToo()
    {
        // The index is built lazily, so a registration added after an earlier import already triggered it has
        // to be picked up rather than left behind the first pass.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost", "export const value = 'first';");
        engine.Modules.Import("http://localhost").Get("value").AsString().Should().Be("first");

        engine.Modules.Add("http://elsewhere", "export const value = 'second';");
        engine.Modules.Import("http://elsewhere").Get("value").AsString().Should().Be("second");
    }

    [Test]
    public void TwoRegistrationsResolvingToTheSameKeyFailLoudly()
    {
        // Both spellings canonicalize to http://localhost/, so whichever registration lost would be silently
        // unreachable under every name - its own name resolves to the shared key too, which now belongs to the
        // winner. That is host-supplied source being discarded, the failure this identity scheme exists to
        // eliminate, so the import that discovers the pair throws instead of picking a winner.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost", "export const value = 'first';");
        engine.Modules.Add("HTTP://localhost", "export const value = 'second';");

        Invoking(() => engine.Modules.Import("http://localhost"))
            .Should().Throw<InvalidOperationException>().WithMessage("*resolve to the same specifier*");
    }

    [Test]
    public void ARegistrationCollidingWithOneFiledUnderTheCanonicalNameFailsLoudlyToo()
    {
        // The other collision shape: the earlier registration already carries the canonical spelling, so it is
        // matched without ever being indexed, and only the later registration's resolution reveals the clash.
        var engine = new Engine(options => options.UseModules(new CanonicalizingModuleLoader()));

        engine.Modules.Add("http://localhost/", "export const value = 'canonical';");
        engine.Modules.Add("http://localhost", "export const value = 'short';");

        Invoking(() => engine.Modules.Import("http://elsewhere"))
            .Should().Throw<InvalidOperationException>().WithMessage("*resolve to the same specifier*");
    }

    [Test]
    public void CancellationInsideTheIndexingPassIsNotSwallowed()
    {
        // A refusal is the loader rejecting one name; cancellation is the host calling off the whole
        // operation, and it must reach whoever cancelled rather than be treated as "leave that registration
        // unindexed" while the triggering import carries on loading and evaluating.
        var engine = new Engine(options => options.UseModules(new CancellingResolveLoader()));

        engine.Modules.Add("cancelled", "export const value = 1;");
        engine.Modules.Add("http://localhost", "export const value = 2;");

        Invoking(() => engine.Modules.Import("http://localhost"))
            .Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void CancellationInsideTheIndexingPassCanBeRetried()
    {
        var engine = new Engine(options => options.UseModules(new CancelOnceResolveLoader()));

        engine.Modules.Add("cancelled", "export const value = 1;");

        Invoking(() => engine.Modules.Import("cancelled"))
            .Should().Throw<OperationCanceledException>();

        engine.Modules.Import("cancelled").Get("value").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// Canonicalizes like <see cref="CanonicalizingModuleLoader"/>, except that one specifier makes it throw
    /// <see cref="OperationCanceledException"/> — the shape of a loader honoring a <c>CancellationToken</c>
    /// mid-resolve.
    /// </summary>
    private sealed class CancellingResolveLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (string.Equals(moduleRequest.Specifier, "cancelled", StringComparison.Ordinal))
            {
                throw new OperationCanceledException();
            }

            var uri = new Uri(moduleRequest.Specifier, UriKind.Absolute);
            return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("the loader was asked to load " + resolved.Key);
    }

    private sealed class CancelOnceResolveLoader : ModuleLoader
    {
        private bool _cancel = true;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (_cancel)
            {
                _cancel = false;
                throw new OperationCanceledException();
            }

            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, null, SpecifierType.Bare);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The registered module should satisfy the load.");
    }

    [Test]
    public void ALoaderHandingBackANullKeyDoesNotBreakAnUnrelatedImport()
    {
        // A loader compiled without nullable annotations can hand back a null key instead of throwing. That is
        // a refusal in a different costume, and like the thrown kind it must not surface on the import that
        // merely triggered the indexing pass.
        var engine = new Engine(options => options.UseModules(new NullKeyResolveLoader()));

        engine.Modules.Add("broken", "export const value = 'unreachable';");
        engine.Modules.Add("http://localhost", "export const value = 'fine';");

        engine.Modules.Import("http://localhost").Get("value").AsString().Should().Be("fine");
    }

    /// <summary>
    /// Canonicalizes like <see cref="CanonicalizingModuleLoader"/>, except that one specifier resolves to a
    /// <c>null</c> key — which the annotations forbid, but a loader compiled without them can produce.
    /// </summary>
    private sealed class NullKeyResolveLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (string.Equals(moduleRequest.Specifier, "broken", StringComparison.Ordinal))
            {
                return new ResolvedSpecifier(moduleRequest, Key: null!, Uri: null, SpecifierType.Bare);
            }

            var uri = new Uri(moduleRequest.Specifier, UriKind.Absolute);
            return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("the loader was asked to load " + resolved.Key);
    }

    [Test]
    public void APreCompiledModuleResolvesItsRelativeImportsAgainstItsPrepareTimeName()
    {
        // A pre-compiled module cannot be renamed by the registration - the AST is the host's - so its
        // relative imports resolve against the name it was prepared with. Preparing it under the key the
        // loader resolves the registration to, as the AddModule doc says, is what makes the two agree.
        var engine = new Engine(options => options.UseModules(new VirtualTreeLoader(new Dictionary<string, string>
        {
            ["/vfs/util.js"] = "export const value = 'from the virtual sibling';",
        })));

        var prepared = Engine.PrepareModule("export { value } from './util.js';", "/vfs/lib.js");
        engine.Modules.Add("lib", builder => builder.AddModule(prepared));

        engine.Modules.Import("lib").Get("value").AsString().Should().Be("from the virtual sibling");
    }
}
