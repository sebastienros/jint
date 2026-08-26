using Jint.Runtime.Modules;

#nullable enable

namespace Jint.Tests.Runtime.Modules;

public class NodeStyleModuleLoaderTests
{
    // ---------------------------------------------------------------------------------------------------
    // Relative and absolute specifiers: DefaultModuleLoader behaviour, base-path restriction included.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ResolvesARelativeSpecifierAgainstTheImportingModule()
    {
        using var tree = new PackageTree();
        tree.Add("app/main.js", "export const x = 1;")
            .Add("app/lib/util.js", "export const y = 2;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        var resolved = loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("./lib/util.js", []));

        resolved.Type.Should().Be(SpecifierType.RelativeOrAbsolute);
        resolved.Uri!.LocalPath.Should().Be(tree.PathOf("app/lib/util.js"));
    }

    [Test]
    public void ResolvesARelativeSpecifierAgainstTheBasePathWhenThereIsNoReferrer()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        var resolved = loader.Resolve(null, new ModuleRequest("./main.js", []));

        resolved.Uri!.LocalPath.Should().Be(tree.PathOf("main.js"));
    }

    [TestCase("../outside.js")]
    [TestCase("./../../outside.js")]
    public void RefusesAPathAboveTheBasePath(string specifier)
    {
        using var tree = new PackageTree();
        tree.Add("app/main.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.PathOf("app"));

        var exception = Invoking(() => loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest(specifier, [])))
            .Should().ThrowExactly<ModuleResolutionException>().Which;

        exception.ResolverAlgorithmError.Should().Be("Unauthorized Module Path");
    }

    [Test]
    public void RefusesAnAbsoluteUriThatIsNotAFile()
    {
        using var tree = new PackageTree();
        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("https://example.com/mod.js", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().Be("Unauthorized Module Path");
    }

    [Test]
    public void RefusesANodeBuiltinSpecifier()
    {
        using var tree = new PackageTree();
        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("node:fs", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Unsupported Module Scheme");
    }

    [TestCase("./lib%2F../../outside.js")]
    [TestCase("./lib%5Cutil.js")]
    public void RefusesAPercentEncodedSeparator(string specifier)
    {
        using var tree = new PackageTree();
        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest(specifier, [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Module Specifier");
    }

    [Test]
    public void RefusesAnImportsSpecifier()
    {
        using var tree = new PackageTree();
        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("#internal", [])))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("*PACKAGE_IMPORTS_RESOLVE*");
    }

    // ---------------------------------------------------------------------------------------------------
    // PACKAGE_RESOLVE: the node_modules walk, "main" and the index fallback.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ResolvesAPackageThroughItsMainField()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./lib/entry.js" }""")
            .Add("node_modules/pkg/lib/entry.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/lib/entry.js"));
    }

    [Test]
    public void FallsBackToIndexJsWhenThereIsNoMain()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg" }""")
            .Add("node_modules/pkg/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/index.js"));
    }

    [Test]
    public void FallsBackToIndexJsWhenMainNamesNothingThatExists()
    {
        // Node's legacyMainResolve falls through to the index candidates rather than failing on a stale "main".
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./dist/gone.js" }""")
            .Add("node_modules/pkg/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/index.js"));
    }

    [Test]
    public void ResolvesAPackageDirectoryThatCarriesNoPackageJsonAtAll()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/index.js"));
    }

    [Test]
    public void ReportsAPackageThatResolvesToNothing()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./dist/gone.js" }""");

        var loader = new NodeStyleModuleLoader(tree.Root);

        var exception = Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>().Which;

        exception.ResolverAlgorithmError.Should().StartWith("Module Not Found");
        exception.ResolverAlgorithmError.Should().Contain("index.js");
    }

    [Test]
    public void ResolvesASubpathOfAPackageWithoutExports()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg" }""")
            .Add("node_modules/pkg/extra/feature.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg/extra/feature.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/extra/feature.js"));
    }

    [Test]
    public void WalksUpwardsFromTheImportingModule()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/dep/package.json", """{ "name": "dep", "main": "./index.js" }""")
            .Add("node_modules/dep/index.js", "export const value = 'outer';")
            .Add("app/deep/consumer.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(tree.PathOf("app/deep/consumer.js"), new ModuleRequest("dep", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/dep/index.js"));
    }

    [Test]
    public void ANestedNodeModulesShadowsTheOuterPackage()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/dep/package.json", """{ "name": "dep", "main": "./index.js" }""")
            .Add("node_modules/dep/index.js", "export const value = 'outer';")
            .Add("node_modules/host/package.json", """{ "name": "host", "main": "./index.js" }""")
            .Add("node_modules/host/index.js", "export const value = 'host';")
            .Add("node_modules/host/node_modules/dep/package.json", """{ "name": "dep", "main": "./index.js" }""")
            .Add("node_modules/host/node_modules/dep/index.js", "export const value = 'inner';");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(tree.PathOf("node_modules/host/index.js"), new ModuleRequest("dep", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/host/node_modules/dep/index.js"));
    }

    [Test]
    public void TheWalkNeverLeavesTheBasePath()
    {
        using var tree = new PackageTree();
        // The package sits above the base path, where a Node resolver would still find it.
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./index.js" }""")
            .Add("node_modules/pkg/index.js", "export const value = 1;")
            .Add("app/main.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.PathOf("app"));

        loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("pkg", [])).Type
            .Should().Be(SpecifierType.Bare);
    }

    [Test]
    public void ResolvesAScopedPackage()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/@org/pkg/package.json", """{ "name": "@org/pkg", "exports": { "./feature.js": "./src/feature.js" } }""")
            .Add("node_modules/@org/pkg/src/feature.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("@org/pkg/feature.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/@org/pkg/src/feature.js"));
    }

    [Test]
    public void RefusesAScopeWithoutAPackageName()
    {
        using var tree = new PackageTree();
        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("@org", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Module Specifier");
    }

    [Test]
    public void ABareSpecifierThatMatchesNoPackageIsHandedBackUnresolved()
    {
        using var tree = new PackageTree();
        var loader = new NodeStyleModuleLoader(tree.Root);

        var resolved = loader.Resolve(null, new ModuleRequest("nowhere", []));

        resolved.Type.Should().Be(SpecifierType.Bare);
        resolved.Key.Should().Be("nowhere");
        resolved.Uri.Should().BeNull();
    }

    [Test]
    public void AModuleRegisteredWithModulesAddIsStillImportable()
    {
        using var tree = new PackageTree();
        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));
        engine.Modules.Add("registered", "export const value = 'from the registry';");

        engine.Modules.Import("registered").Get("value").AsString().Should().Be("from the registry");
    }

    [Test]
    public void AnImportOfANameThatIsNeitherAPackageNorRegisteredSaysSo()
    {
        using var tree = new PackageTree();
        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));

        Invoking(() => engine.Modules.Import("nowhere"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*node_modules*");
    }

    // ---------------------------------------------------------------------------------------------------
    // PACKAGE_EXPORTS_RESOLVE.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ExportsTakesPrecedenceOverMain()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./main.js", "exports": "./exported.js" }""")
            .Add("node_modules/pkg/main.js", "export const value = 'main';")
            .Add("node_modules/pkg/exported.js", "export const value = 'exports';");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/exported.js"));
    }

    [Test]
    public void ResolvesTheDotEntryOfAnExportsMap()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": "./entry.js", "./extra.js": "./src/extra.js" } }""")
            .Add("node_modules/pkg/entry.js", "export const value = 1;")
            .Add("node_modules/pkg/src/extra.js", "export const value = 2;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/entry.js"));
        loader.Resolve(null, new ModuleRequest("pkg/extra.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/src/extra.js"));
    }

    [Test]
    public void ASubpathTheExportsMapDoesNotNameIsRefused()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": "./entry.js" } }""")
            .Add("node_modules/pkg/entry.js", "export const value = 1;")
            .Add("node_modules/pkg/internal.js", "export const secret = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        var exception = Invoking(() => loader.Resolve(null, new ModuleRequest("pkg/internal.js", [])))
            .Should().ThrowExactly<ModuleResolutionException>().Which;

        exception.ResolverAlgorithmError.Should().StartWith("Package Path Not Exported");
        exception.ResolverAlgorithmError.Should().Contain("./internal.js");
    }

    [Test]
    public void AnExplicitNullTargetBlocksASubpath()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./*.js": "./src/*.js", "./private.js": null } }""")
            .Add("node_modules/pkg/src/private.js", "export const secret = 1;")
            .Add("node_modules/pkg/src/public.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg/public.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/src/public.js"));

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg/private.js", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Package Path Not Exported");
    }

    [Test]
    public void AnExplicitNullTargetStopsTheConditionLoopRatherThanFallingThrough()
    {
        // The one place null and "no match" differ: PACKAGE_TARGET_RESOLVE step 2.2.3 continues only for
        // undefined, so a null under a matching condition blocks the subpath instead of letting "default"
        // answer for it. Turning "import" off, on the other hand, leaves the condition unmatched and
        // "default" does answer.
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": { "import": null, "default": "./fallback.js" } } }""")
            .Add("node_modules/pkg/fallback.js", "export const value = 1;");

        Invoking(() => new NodeStyleModuleLoader(tree.Root).Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Package Path Not Exported");

        new NodeStyleModuleLoader(tree.Root, new NodeModuleLoaderOptions { Conditions = ["default"] })
            .Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/fallback.js"));
    }

    [Test]
    public void ExportsNullFallsThroughToMain()
    {
        // "exports": null is "not null or undefined" being false, so the package is not encapsulated at all.
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./main.js", "exports": null }""")
            .Add("node_modules/pkg/main.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/main.js"));
    }

    [Test]
    public void ATargetThatLeavesThePackageIsAnInvalidPackageTarget()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": "./../escaped.js" }""")
            .Add("node_modules/escaped.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Package Target");
    }

    [Test]
    public void ATargetThatDoesNotStartWithDotSlashIsAnInvalidPackageTarget()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": "entry.js" }""")
            .Add("node_modules/pkg/entry.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().Contain("must start with './'");
    }

    [Test]
    public void MixingSubpathKeysWithConditionKeysIsAnInvalidPackageConfiguration()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": "./a.js", "import": "./b.js" } }""")
            .Add("node_modules/pkg/a.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Package Configuration");
    }

    [Test]
    public void AMalformedPackageJsonIsAnInvalidPackageConfiguration()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", }""")
            .Add("node_modules/pkg/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Package Configuration");
    }

    [Test]
    public void ADirectoryNamedByAnExportsTargetIsAnUnsupportedDirectoryImport()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": "./src" }""")
            .Add("node_modules/pkg/src/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Unsupported Directory Import");
    }

    // ---------------------------------------------------------------------------------------------------
    // Conditional exports: order-sensitive, and decided by the package's own key order.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ConditionsAreTriedInThePackagesOwnKeyOrder()
    {
        // Both objects hold the same two conditions, and both of them match. Only the order differs, and the
        // "earlier entries have higher priority" rule is what decides.
        using var tree = new PackageTree();
        tree.Add("node_modules/import-first/package.json", """{ "name": "import-first", "exports": { "import": "./esm.js", "default": "./fallback.js" } }""")
            .Add("node_modules/import-first/esm.js", "export const value = 1;")
            .Add("node_modules/import-first/fallback.js", "export const value = 2;")
            .Add("node_modules/default-first/package.json", """{ "name": "default-first", "exports": { "default": "./fallback.js", "import": "./esm.js" } }""")
            .Add("node_modules/default-first/esm.js", "export const value = 1;")
            .Add("node_modules/default-first/fallback.js", "export const value = 2;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("import-first", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/import-first/esm.js"));
        loader.Resolve(null, new ModuleRequest("default-first", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/default-first/fallback.js"));
    }

    [Test]
    public void AConditionThatIsNotConfiguredIsSkipped()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "node": "./node.js", "default": "./default.js" } }""")
            .Add("node_modules/pkg/node.js", "export const value = 1;")
            .Add("node_modules/pkg/default.js", "export const value = 2;");

        new NodeStyleModuleLoader(tree.Root)
            .Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/default.js"));

        new NodeStyleModuleLoader(tree.Root, new NodeModuleLoaderOptions { Conditions = ["node", "import", "default"] })
            .Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/node.js"));
    }

    [Test]
    public void APackageOfferingOnlyRequireIsRefused()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "require": "./cjs.js" } }""")
            .Add("node_modules/pkg/cjs.js", "module.exports = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Package Path Not Exported");
    }

    [Test]
    public void ConditionsNestNestedObjects()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": { "import": { "default": "./esm.js" } } } }""")
            .Add("node_modules/pkg/esm.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/esm.js"));
    }

    [Test]
    public void AFallbackArrayTriesEachEntryInTurn()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": ["not-a-relative-target", "./entry.js"] }""")
            .Add("node_modules/pkg/entry.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/entry.js"));
    }

    // ---------------------------------------------------------------------------------------------------
    // Subpath patterns.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void APatternExportExpandsEveryStar()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./features/*.js": "./src/features/*.js" } }""")
            .Add("node_modules/pkg/src/features/nested/one.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        // "* maps expose nested subpaths as it is a string replacement syntax only".
        loader.Resolve(null, new ModuleRequest("pkg/features/nested/one.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/src/features/nested/one.js"));
    }

    [Test]
    public void TheMostSpecificPatternWins()
    {
        // PATTERN_KEY_COMPARE orders by prefix length first: "./a/b/*" has to beat "./a/*" for "./a/b/x.js"
        // however the two are written in the file.
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./a/*": "./general/*", "./a/b/*": "./specific/*" } }""")
            .Add("node_modules/pkg/general/b/x.js", "export const value = 'general';")
            .Add("node_modules/pkg/specific/x.js", "export const value = 'specific';");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg/a/b/x.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/specific/x.js"));
    }

    [Test]
    public void AnExactKeyBeatsAPatternThatWouldAlsoMatch()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./*": "./src/*", "./special.js": "./special-impl.js" } }""")
            .Add("node_modules/pkg/src/special.js", "export const value = 'pattern';")
            .Add("node_modules/pkg/special-impl.js", "export const value = 'exact';");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(null, new ModuleRequest("pkg/special.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/special-impl.js"));
    }

    [Test]
    public void APatternMatchThatTraversesIsRefused()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./*": "./src/*" } }""")
            .Add("node_modules/pkg/secret.js", "export const secret = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg/../secret.js", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Module Specifier");
    }

    [Test]
    public void ASubpathEndingInASlashIsRefused()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./*": "./src/*" } }""")
            .Add("node_modules/pkg/src/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg/src/", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Invalid Module Specifier");
    }

    // ---------------------------------------------------------------------------------------------------
    // PACKAGE_SELF_RESOLVE.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void APackageCanImportItselfByNameThroughItsExports()
    {
        using var tree = new PackageTree();
        tree.Add("package.json", """{ "name": "a-package", "exports": { ".": "./index.js", "./foo.js": "./foo.js" } }""")
            .Add("index.js", "export const value = 1;")
            .Add("foo.js", "export const something = 2;")
            .Add("a-module.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(tree.PathOf("a-module.js"), new ModuleRequest("a-package/foo.js", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("foo.js"));
    }

    [Test]
    public void ASelfReferenceOnlyReachesWhatExportsAllows()
    {
        using var tree = new PackageTree();
        tree.Add("package.json", """{ "name": "a-package", "exports": { ".": "./index.mjs", "./foo.js": "./foo.js" } }""")
            .Add("index.mjs", "export const value = 1;")
            .Add("foo.js", "export const something = 2;")
            .Add("m.mjs", "export const another = 3;")
            .Add("another-module.mjs", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(tree.PathOf("another-module.mjs"), new ModuleRequest("a-package/m.mjs", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Package Path Not Exported");
    }

    [Test]
    public void ASelfReferenceNeedsAnExportsField()
    {
        using var tree = new PackageTree();
        tree.Add("package.json", """{ "name": "a-package", "main": "./index.js" }""")
            .Add("index.js", "export const value = 1;")
            .Add("a-module.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        // No "exports" means no self-reference: the name falls through to the node_modules walk, finds nothing
        // and is handed back unresolved.
        loader.Resolve(tree.PathOf("a-module.js"), new ModuleRequest("a-package", [])).Type
            .Should().Be(SpecifierType.Bare);
    }

    [Test]
    public void ThePackageScopeStopsAtNodeModules()
    {
        // LOOKUP_PACKAGE_SCOPE returns null once it reaches a node_modules segment, so a package inside
        // node_modules cannot self-reference the containing application's name.
        using var tree = new PackageTree();
        tree.Add("package.json", """{ "name": "app", "exports": { "./secret.js": "./secret.js" } }""")
            .Add("secret.js", "export const secret = 1;")
            .Add("node_modules/pkg/index.js", "export const value = 1;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        loader.Resolve(tree.PathOf("node_modules/pkg/index.js"), new ModuleRequest("app/secret.js", [])).Type
            .Should().Be(SpecifierType.Bare);
    }

    // ---------------------------------------------------------------------------------------------------
    // JSON modules and extension probing.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void AJsonTargetNeedsTheTypeImportAttribute()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./data.json": "./data.json" } }""")
            .Add("node_modules/pkg/data.json", """{ "value": 1 }""");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(null, new ModuleRequest("pkg/data.json", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Missing Import Attribute");

        loader.Resolve(null, new ModuleRequest("pkg/data.json", [new ModuleImportAttribute("type", "json")])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/data.json"));
    }

    [Test]
    public void JsonModulesCanBeTurnedOffAltogether()
    {
        using var tree = new PackageTree();
        tree.Add("data.json", """{ "value": 1 }""");

        var loader = new NodeStyleModuleLoader(tree.Root, new NodeModuleLoaderOptions { AllowJsonModules = false });

        Invoking(() => loader.Resolve(null, new ModuleRequest("./data.json", [new ModuleImportAttribute("type", "json")])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Unsupported JSON Module");
    }

    [Test]
    public void ExtensionProbingIsOffByDefault()
    {
        using var tree = new PackageTree();
        tree.Add("app/main.js", "export const x = 1;")
            .Add("app/lib/util.js", "export const y = 2;")
            .Add("app/dir/index.js", "export const z = 3;");

        var loader = new NodeStyleModuleLoader(tree.Root);

        Invoking(() => loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("./lib/util", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().Be("Unsupported Directory Import");

        Invoking(() => loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("./dir", [])))
            .Should().ThrowExactly<ModuleResolutionException>();
    }

    [Test]
    public void ExtensionProbingResolvesAnExtensionlessSpecifierAndADirectoryIndex()
    {
        using var tree = new PackageTree();
        tree.Add("app/main.js", "export const x = 1;")
            .Add("app/lib/util.js", "export const y = 2;")
            .Add("app/dir/index.js", "export const z = 3;");

        var loader = new NodeStyleModuleLoader(tree.Root, new NodeModuleLoaderOptions { ExtensionProbing = true });

        loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("./lib/util", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("app/lib/util.js"));
        loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("./dir", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("app/dir/index.js"));
    }

    [Test]
    public void ExtensionProbingReachesAPackageMainWithoutAnExtension()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "main": "./lib/entry" }""")
            .Add("node_modules/pkg/lib/entry.js", "export const value = 1;");

        new NodeStyleModuleLoader(tree.Root, new NodeModuleLoaderOptions { ExtensionProbing = true })
            .Resolve(null, new ModuleRequest("pkg", [])).Uri!.LocalPath
            .Should().Be(tree.PathOf("node_modules/pkg/lib/entry.js"));

        // Without probing the same package has no entry point at all: "main" is a URL, not a stem.
        Invoking(() => new NodeStyleModuleLoader(tree.Root).Resolve(null, new ModuleRequest("pkg", [])))
            .Should().ThrowExactly<ModuleResolutionException>()
            .Which.ResolverAlgorithmError.Should().StartWith("Module Not Found");
    }

    // ---------------------------------------------------------------------------------------------------
    // Error messages, and what they are allowed to name.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ErrorMessagesNamePathsRelativeToTheBasePath()
    {
        using var tree = new PackageTree();
        tree.Add("app/node_modules/pkg/package.json", """{ "name": "pkg", "exports": { ".": "./entry.js" } }""")
            .Add("app/node_modules/pkg/entry.js", "export const value = 1;")
            .Add("app/main.js", "export const x = 1;");

        var loader = new NodeStyleModuleLoader(tree.PathOf("app"));

        var exception = Invoking(() => loader.Resolve(tree.PathOf("app/main.js"), new ModuleRequest("pkg/internal.js", [])))
            .Should().ThrowExactly<ModuleResolutionException>().Which;

        exception.ResolverAlgorithmError.Should().Contain("node_modules/pkg");
        exception.ResolverAlgorithmError.Should().NotContain(tree.Root);
        exception.FilePath.Should().Be("node_modules/pkg");
    }

    // ---------------------------------------------------------------------------------------------------
    // End to end.
    // ---------------------------------------------------------------------------------------------------

    [Test]
    public void ATwoPackageGraphExecutes()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "import { greet } from 'greeter'; export const message = greet('world');")
            .Add("node_modules/greeter/package.json", """{ "name": "greeter", "exports": { ".": { "import": "./esm/index.js", "default": "./cjs/index.js" } } }""")
            .Add("node_modules/greeter/esm/index.js", "import { upper } from 'upper'; export function greet(name) { return 'hello ' + upper(name); }")
            .Add("node_modules/greeter/cjs/index.js", "throw new Error('the require branch must not be taken');")
            .Add("node_modules/upper/package.json", """{ "name": "upper", "main": "lib/upper.js" }""")
            .Add("node_modules/upper/lib/upper.js", "export function upper(text) { return text.toUpperCase(); }");

        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));

        var ns = engine.Modules.Import("./main.js");

        ns.Get("message").AsString().Should().Be("hello WORLD");
    }

    [Test]
    public void APackageImportedByNameExecutes()
    {
        using var tree = new PackageTree();
        tree.Add("node_modules/pkg/package.json", """{ "name": "pkg", "exports": { "./data.json": "./data.json", ".": "./index.js" } }""")
            .Add("node_modules/pkg/index.js", "import data from './data.json' with { type: 'json' }; export const value = data.answer;")
            .Add("node_modules/pkg/data.json", """{ "answer": 42 }""");

        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));

        engine.Modules.Import("pkg").Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void APackageIsLoadedOnceHoweverManyModulesImportIt()
    {
        using var tree = new PackageTree();
        tree.Add("main.js", "import { value as a } from './one.js'; import { value as b } from './two.js'; export const total = a + b;")
            .Add("one.js", "import { counter } from 'shared'; export const value = counter.next();")
            .Add("two.js", "import { counter } from 'shared'; export const value = counter.next();")
            .Add("node_modules/shared/package.json", """{ "name": "shared", "exports": "./index.js" }""")
            .Add("node_modules/shared/index.js", "let n = 0; export const counter = { next() { return ++n; } };");

        var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(tree.Root)));

        // 1 + 2 rather than 1 + 1: both importers share one module record, which they only do if both
        // resolutions produced the same key.
        engine.Modules.Import("./main.js").Get("total").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// A throwaway <c>node_modules</c> tree on disk. Every path is written relative to the root with forward
    /// slashes, whatever the platform separator is.
    /// </summary>
    private sealed class PackageTree : IDisposable
    {
        public PackageTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "jint-node-resolution", Guid.NewGuid().ToString("N"));
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
