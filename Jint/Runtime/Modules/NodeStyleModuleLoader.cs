using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Jint.Runtime.Modules;

/// <summary>
/// Options for <see cref="NodeStyleModuleLoader"/>. An instance carries no engine-affine state, so one may be
/// shared, but the loader takes a snapshot of it at construction: changing an option afterwards does not affect
/// a loader already built from it.
/// </summary>
public sealed class NodeModuleLoaderOptions
{
    /// <summary>
    /// The export conditions to match, as
    /// <see href="https://nodejs.org/api/packages.html#conditional-exports">conditional exports</see> defines
    /// them. The default is <c>["import", "default"]</c>: <c>"import"</c> because Jint only ever loads ES
    /// modules, and <c>"default"</c> because that is "the generic fallback that always matches".
    /// </summary>
    /// <remarks>
    /// This list decides <em>which</em> conditions match, never the order they are tried in - that is fixed by
    /// the order the conditions appear in the package's own <c>"exports"</c> object, where "earlier entries
    /// have higher priority and take precedence over later entries". Adding <c>"node"</c> here is the way to
    /// consume packages that ship a Node-specific entry point, and it is deliberately not a default: Jint is
    /// not Node, and a package's <c>"node"</c> branch is free to expect <c>process</c>, <c>Buffer</c> or
    /// <c>node:</c> builtins that no Jint engine has.
    /// </remarks>
    public string[] Conditions { get; set; } = ["import", "default"];

    /// <summary>
    /// Whether a module request may resolve to a <c>.json</c> file at all. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Independently of this flag the import attribute is enforced, exactly as Node enforces it: "the
    /// <c>with { type: 'json' }</c> syntax is mandatory", so a <c>.json</c> target reached without an explicit
    /// <c>type</c> attribute is refused rather than handed to the JavaScript parser. Setting this to
    /// <see langword="false"/> refuses every <c>.json</c> target, attribute or not, for a host that does not
    /// want script reaching data files through the module system.
    /// </remarks>
    public bool AllowJsonModules { get; set; } = true;

    /// <summary>
    /// Whether a specifier that names no existing file may be retried with an extension appended, or as a
    /// directory holding an index file. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// ES module resolution deliberately does no probing - "the resolver has the following properties: ...
    /// no extension searching" - and a specifier is a URL, so <c>./util</c> and <c>./util.js</c> are different
    /// modules. Turning this on trades that for the CommonJS-shaped convenience of <c>./util</c>,
    /// <c>./util/</c> and <c>./util/index.js</c> all naming one file, which is what a tree of packages
    /// published for CommonJS often needs. When on, the probe order is the exact path, then <c>.js</c>,
    /// <c>.mjs</c>, <c>.cjs</c> (then <c>.json</c> when a <c>type</c> attribute permits it), then the same
    /// list as <c>index.*</c> inside a directory of that name.
    /// </remarks>
    public bool ExtensionProbing { get; set; }
}

/// <summary>
/// A module loader that resolves bare specifiers - <c>import 'lodash'</c>, <c>import '@scope/pkg/feature.js'</c>
/// - the way Node.js resolves them for ES modules: by walking <c>node_modules</c> directories upwards from the
/// importing module and honouring the package's <c>package.json</c>, its <c>"exports"</c> map first and its
/// <c>"main"</c> field second.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm implemented is
/// <see href="https://nodejs.org/api/esm.html#resolution-algorithm-specification">ESM_RESOLVE</see> and the
/// functions it calls - PACKAGE_RESOLVE, PACKAGE_SELF_RESOLVE, PACKAGE_EXPORTS_RESOLVE,
/// PACKAGE_IMPORTS_EXPORTS_RESOLVE, PATTERN_KEY_COMPARE, PACKAGE_TARGET_RESOLVE, LOOKUP_PACKAGE_SCOPE and
/// READ_PACKAGE_JSON - with the deviations listed below. Its errors are that algorithm's own error names,
/// carried on <see cref="ModuleResolutionException.ResolverAlgorithmError"/>.
/// </para>
/// <para>
/// Relative and absolute specifiers behave as <see cref="DefaultModuleLoader"/> resolves them, base-path
/// restriction included - and here the restriction is not optional, because it is also what bounds the
/// <c>node_modules</c> walk. Nothing outside the base path is read and no path outside it appears in an error
/// message.
/// </para>
/// <para>
/// Deviations from the algorithm, each deliberate:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>A bare specifier that matches no package is handed back unresolved</b> rather than raising
/// <em>Module Not Found</em>. That is what lets a name registered with <c>Engine.Modules.Add</c> still be
/// imported by an engine using this loader, exactly as it can with <see cref="DefaultModuleLoader"/>. A package
/// that <em>is</em> found and then refuses - an unexported subpath, an invalid configuration, a missing target
/// file - fails there and then; only "no such package anywhere" defers.
/// </description></item>
/// <item><description>
/// <b>No module formats.</b> ESM_FILE_FORMAT and <c>"type"</c> are not implemented: Jint has no CommonJS
/// loader, so every resolved file is loaded as an ES module (or as JSON/text/bytes when an import attribute
/// says so). A package whose <c>"exports"</c> only offers a <c>"require"</c> branch is therefore unusable, as
/// it would be in any ESM-only host.
/// </description></item>
/// <item><description>
/// <b>No builtins.</b> PACKAGE_RESOLVE step 3 maps a builtin name to a <c>node:</c> url; Jint has none, so a
/// <c>node:</c> specifier is refused with a message saying so rather than resolving to something absent.
/// </description></item>
/// <item><description>
/// <b><c>#</c> imports are not supported</b>, the same posture <see cref="DefaultModuleLoader"/> takes:
/// PACKAGE_IMPORTS_RESOLVE raises <see cref="NotSupportedException"/>.
/// </description></item>
/// <item><description>
/// <b>Self-reference is supported</b> (PACKAGE_SELF_RESOLVE): a module inside a package may import the package
/// by its own <c>"name"</c>, and as in Node that works only when the package has an <c>"exports"</c> field and
/// only for what that field exposes. It costs one walk up to the nearest <c>package.json</c> per bare
/// specifier.
/// </description></item>
/// <item><description>
/// <b>No <c>realpath</c>.</b> A resolved path is normalized but not resolved through symlinks, so a symlinked
/// package keeps the identity it was reached by - and cannot be used to escape the base path either.
/// </description></item>
/// <item><description>
/// <b>Nothing is cached.</b> Every resolution reads the <c>package.json</c> files it needs from disk, so a
/// package edited between two imports is picked up. A host serving a large immutable tree that wants the reads
/// amortized can wrap this loader and memoize <see cref="Resolve"/> by (referrer, specifier).
/// </description></item>
/// </list>
/// <para>
/// The loader holds no mutable state and may be shared by several engines, including concurrent ones.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var engine = new Engine(options => options.UseModules(new NodeStyleModuleLoader(@"C:\app")));
/// var ns = engine.Modules.Import("./main.js"); // main.js may `import 'some-package'`
/// </code>
/// </example>
public sealed class NodeStyleModuleLoader : ModuleLoader
{
    private const string NodeModulesFolderName = "node_modules";
    private const string JsonExtension = ".json";

    /// <summary>
    /// The prefix every refusal that PACKAGE_TARGET_RESOLVE's array case is allowed to swallow starts with.
    /// The message carries detail after the name, so the test is a prefix test.
    /// </summary>
    private const string InvalidPackageTarget = "Invalid Package Target";

    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    /// <summary>
    /// Windows compares paths case-insensitively and every other platform .NET runs on compares them
    /// case-sensitively. Guessing the other way round on either would be a security bug rather than a nuisance:
    /// a case-insensitive comparison on a case-sensitive filesystem lets a path out of the base directory.
    /// </summary>
    private static readonly StringComparison PathComparison =
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly string[] ExtensionCandidates = [".js", ".mjs", ".cjs"];
    private static readonly string[] IndexCandidates = ["index.js", "index.mjs", "index.cjs"];

    /// <summary>Always ends with <see cref="Path.DirectorySeparatorChar"/>.</summary>
    private readonly string _basePath;

    /// <summary>The same directory spelled the way <see cref="Path.GetDirectoryName(string)"/> spells one, so
    /// that walking upwards from it visits each directory once.</summary>
    private readonly string _baseDirectory;

    private readonly string[] _conditions;
    private readonly bool _allowJsonModules;
    private readonly bool _extensionProbing;

    /// <param name="basePath">
    /// The directory every module is served from, as a rooted file system path or a <c>file:</c> uri. It bounds
    /// resolution in both directions: nothing above it can be imported, and the <c>node_modules</c> walk stops
    /// there.
    /// </param>
    /// <param name="options">Resolution options, or null for the defaults.</param>
    public NodeStyleModuleLoader(string basePath, NodeModuleLoaderOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            Throw.ArgumentException("Value cannot be null or whitespace.", nameof(basePath));
        }

        string fullPath;
        if (Uri.TryCreate(basePath, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                Throw.ArgumentException("Node-style resolution reads packages from disk, so the base path must be a file system path or a file: uri.", nameof(basePath));
            }

            fullPath = Path.GetFullPath(uri.LocalPath);
        }
        else
        {
            if (!Path.IsPathRooted(basePath))
            {
                Throw.ArgumentException("Path must be rooted", nameof(basePath));
            }

            fullPath = Path.GetFullPath(basePath);
        }

        _basePath = fullPath[fullPath.Length - 1] == Path.DirectorySeparatorChar
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;

        // Path.GetDirectoryName of a file inside the base path is the base path as that method spells a
        // directory: no trailing separator, except at a filesystem root, where there has to be one.
        _baseDirectory = Path.GetDirectoryName(Path.Combine(_basePath, "_")) ?? _basePath;

        options ??= new NodeModuleLoaderOptions();

        // Snapshot, so that a host mutating the options object afterwards - or sharing it with another loader -
        // cannot change how an engine already resolving against this loader behaves.
        var conditions = options.Conditions ?? [];
        _conditions = new string[conditions.Length];
        Array.Copy(conditions, _conditions, conditions.Length);

        _allowJsonModules = options.AllowJsonModules;
        _extensionProbing = options.ExtensionProbing;
    }

    /// <inheritdoc />
    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var specifier = moduleRequest.Specifier;
        if (string.IsNullOrEmpty(specifier))
        {
            Throw.ModuleResolutionException("Invalid Module Specifier: the specifier is empty", specifier, referencingModuleLocation);
        }

        var context = new ResolutionContext(moduleRequest, referencingModuleLocation);

        // ESM_RESOLVE step 2: a specifier that parses as a URL is taken as one.
        if (Uri.TryCreate(specifier, UriKind.Absolute, out var absolute))
        {
            if (string.Equals(absolute.Scheme, "node", StringComparison.Ordinal))
            {
                Throw.ModuleResolutionException(
                    "Unsupported Module Scheme: Node.js builtin modules have no counterpart in Jint, so a 'node:' specifier cannot resolve",
                    specifier,
                    referencingModuleLocation);
            }

            if (!absolute.IsFile)
            {
                Throw.ModuleResolutionException("Unauthorized Module Path", specifier, referencingModuleLocation);
            }

            RejectEncodedSeparators(in context);
            return ResolveFileSpecifier(absolute.LocalPath, in context);
        }

        // ESM_RESOLVE step 3.
        if (IsRelative(specifier))
        {
            RejectEncodedSeparators(in context);
            RejectInvalidPathCharacters(specifier, in context);

            var directory = GetReferencingDirectory(referencingModuleLocation);
            return ResolveFileSpecifier(Path.Combine(directory, specifier), in context);
        }

        // ESM_RESOLVE step 4.
        if (specifier[0] == '#')
        {
            Throw.NotSupportedException($"PACKAGE_IMPORTS_RESOLVE is not supported: '{specifier}'");
        }

        // ESM_RESOLVE step 5.
        return PackageResolve(specifier, in context);
    }

    /// <inheritdoc />
    protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
    {
        var fileName = GetLoadableFileName(resolved);
        return File.ReadAllText(fileName);
    }

    /// <inheritdoc />
    protected override byte[] LoadModuleContentsAsBytes(Engine engine, ResolvedSpecifier resolved)
    {
        var fileName = GetLoadableFileName(resolved);
        return File.ReadAllBytes(fileName);
    }

    private static string GetLoadableFileName(ResolvedSpecifier resolved)
    {
        var specifier = resolved.ModuleRequest.Specifier;
        if (resolved.Type != SpecifierType.RelativeOrAbsolute)
        {
            // The bare specifier matched no package on disk, and no module was registered under it either.
            // NotSupportedException rather than a load failure, because this is API guidance: the name has to
            // come from somewhere, and the two places it can come from are a node_modules tree and
            // Engine.Modules.Add.
            Throw.NotSupportedException($"No package named '{specifier}' was found in any node_modules directory below the base path, and no module is registered under that name with {nameof(Engine)}.{nameof(Engine.Modules.Add)}().");
        }

        if (resolved.Uri is null)
        {
            Throw.InvalidOperationException($"Module '{specifier}' of type '{resolved.Type}' has no resolved URI.");
        }

        var fileName = resolved.Uri.LocalPath;
        if (!File.Exists(fileName))
        {
            Throw.ModuleResolutionException("Module Not Found", specifier, parent: null, fileName);
        }

        return fileName;
    }

    private static bool IsRelative(string specifier)
    {
        // ESM_RESOLVE step 3 exactly: "/", "./" or "../". Anything else starting with "." is a bare specifier,
        // which PACKAGE_RESOLVE step 6 then refuses by name.
        return specifier[0] == '/'
               || specifier.StartsWith("./", StringComparison.Ordinal)
               || specifier.StartsWith("../", StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves a specifier that already names a file, applying the base-path restriction and - only when
    /// <see cref="NodeModuleLoaderOptions.ExtensionProbing"/> is on - the extension and index probes.
    /// </summary>
    private ResolvedSpecifier ResolveFileSpecifier(string path, in ResolutionContext context)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsWithinBasePath(fullPath))
        {
            Throw.ModuleResolutionException("Unauthorized Module Path", context.Specifier, context.ReferencingModuleLocation);
        }

        if (_extensionProbing)
        {
            if (TryProbe(fullPath, in context, out var probed))
            {
                fullPath = probed;
            }
            else if (Directory.Exists(fullPath))
            {
                Throw.ModuleResolutionException("Unsupported Directory Import", context.Specifier, context.ReferencingModuleLocation);
            }
        }
        else if (!Path.HasExtension(fullPath))
        {
            // What DefaultModuleLoader does, kept so that turning probing off leaves the two loaders answering
            // an extensionless specifier the same way.
            Throw.ModuleResolutionException("Unsupported Directory Import", context.Specifier, context.ReferencingModuleLocation);
        }

        return CreateResolvedFile(fullPath, in context);
    }

    /// <summary>
    /// PACKAGE_RESOLVE, minus the builtin case and with step 11 replaced by handing the specifier back
    /// unresolved.
    /// </summary>
    private ResolvedSpecifier PackageResolve(string specifier, in ResolutionContext context)
    {
        // Steps 4 and 5: the package name is the first segment, or the first two for a scoped package.
        string packageName;
        if (specifier[0] != '@')
        {
            var separator = specifier.IndexOf('/');
            packageName = separator < 0 ? specifier : specifier.Substring(0, separator);
        }
        else
        {
            var first = specifier.IndexOf('/');
            if (first < 0)
            {
                Throw.ModuleResolutionException("Invalid Module Specifier: a scoped package name needs a '/' after the scope", specifier, context.ReferencingModuleLocation);
            }

            var second = specifier.IndexOf('/', first + 1);
            packageName = second < 0 ? specifier : specifier.Substring(0, second);
        }

        // Step 6.
        if (packageName.Length == 0 || packageName[0] == '.' || packageName.Contains('\\') || packageName.Contains('%'))
        {
            Throw.ModuleResolutionException("Invalid Module Specifier: a package name must not be empty, start with '.', or contain '\\' or '%'", specifier, context.ReferencingModuleLocation);
        }

        RejectInvalidPathCharacters(specifier, in context);

        // Step 7: "." for the package itself, "./x" for a subpath of it.
        var remainder = specifier.Substring(packageName.Length);
        var packageSubpath = remainder.Length == 0 ? "." : "." + remainder;

        var referencingDirectory = GetReferencingDirectory(context.ReferencingModuleLocation);

        // Steps 8 and 9: PACKAGE_SELF_RESOLVE.
        var scope = LookupPackageScope(referencingDirectory);
        if (scope is not null)
        {
            var scopePackage = ReadPackageJson(scope, in context);
            if (scopePackage?.Exports is not null && string.Equals(scopePackage.Name, packageName, StringComparison.Ordinal))
            {
                return ResolveExports(scope, packageSubpath, scopePackage.Exports, in context);
            }
        }

        // Step 10, bounded by the base path rather than by the file system root.
        var directory = IsWithinBasePath(referencingDirectory) ? referencingDirectory : _baseDirectory;
        var packageRelativePath = packageName.Replace('/', Path.DirectorySeparatorChar);
        while (directory is not null && IsWithinBasePath(directory))
        {
            var packageDirectory = Path.Combine(directory, NodeModulesFolderName, packageRelativePath);
            if (Directory.Exists(packageDirectory))
            {
                return ResolveWithinPackage(packageDirectory, packageSubpath, in context);
            }

            directory = Path.GetDirectoryName(directory);
        }

        // Step 11 would be a Module Not Found error. Handing the specifier back unresolved instead is what
        // keeps Engine.Modules.Add working, and it is the only outcome deferred this way - a package that was
        // found and then refused has already thrown by now. LoadModuleContents reports the miss if nothing is
        // registered under the name either.
        return new ResolvedSpecifier(context.Request, specifier, Uri: null, SpecifierType.Bare);
    }

    /// <summary>PACKAGE_RESOLVE steps 10.4 to 10.7, for a package directory that exists.</summary>
    private ResolvedSpecifier ResolveWithinPackage(string packageDirectory, string subpath, in ResolutionContext context)
    {
        var package = ReadPackageJson(packageDirectory, in context);

        // Step 10.5: "exports" takes precedence over "main" and encapsulates everything it does not name.
        if (package?.Exports is not null)
        {
            return ResolveExports(packageDirectory, subpath, package.Exports, in context);
        }

        // Step 10.6.
        if (string.Equals(subpath, ".", StringComparison.Ordinal))
        {
            return ResolveLegacyMain(packageDirectory, package, in context);
        }

        // Step 10.7.
        var target = Path.GetFullPath(Path.Combine(packageDirectory, subpath.Substring(2)));
        if (!IsWithinDirectory(packageDirectory, target) && !string.Equals(target, packageDirectory, PathComparison))
        {
            Throw.ModuleResolutionException($"Invalid Module Specifier: '{subpath}' leaves package '{Display(packageDirectory)}'", context.Specifier, context.ReferencingModuleLocation);
        }

        return ResolvePackageFile(target, $"'{subpath}' of package '{Display(packageDirectory)}'", in context);
    }

    /// <summary>
    /// The entry point of a package with no <c>"exports"</c>: its <c>"main"</c>, then <c>index.js</c>. The
    /// pseudo-code has only the <c>"main"</c> half; the <c>index.js</c> fallback is what Node's own
    /// <c>legacyMainResolve</c> does, and it is what makes a plain CommonJS-era package importable at all.
    /// </summary>
    private ResolvedSpecifier ResolveLegacyMain(string packageDirectory, PackageJson? package, in ResolutionContext context)
    {
        var main = package?.Main;
        if (main is not null && main.Length > 0)
        {
            RejectInvalidPathCharacters(main, in context);
            var mainCandidate = Path.GetFullPath(Path.Combine(packageDirectory, main));
            if (IsWithinDirectory(packageDirectory, mainCandidate))
            {
                if (File.Exists(mainCandidate))
                {
                    return CreateResolvedFile(mainCandidate, in context);
                }

                if (_extensionProbing && TryProbe(mainCandidate, in context, out var probedMain))
                {
                    return CreateResolvedFile(probedMain, in context);
                }
            }

            // Node falls through to the index candidates when "main" names nothing that exists.
        }

        // Without probing, index.js is the one default a package gets; index.mjs and index.cjs are probe
        // candidates like any other.
        var index = Path.Combine(packageDirectory, IndexCandidates[0]);
        if (File.Exists(index))
        {
            return CreateResolvedFile(index, in context);
        }

        if (_extensionProbing && TryProbe(packageDirectory, in context, out var probedIndex))
        {
            return CreateResolvedFile(probedIndex, in context);
        }

        var described = main is null
            ? $"package '{Display(packageDirectory)}' has no \"exports\", no \"main\" and no index.js"
            : $"package '{Display(packageDirectory)}' has no \"exports\", and neither its \"main\" ('{main}') nor index.js names an existing file";

        Throw.ModuleResolutionException($"Module Not Found: {described}", context.Specifier, context.ReferencingModuleLocation, Display(packageDirectory));
        return default!;
    }

    /// <summary>PACKAGE_EXPORTS_RESOLVE, followed by the file checks ESM_RESOLVE step 7 makes.</summary>
    private ResolvedSpecifier ResolveExports(string packageDirectory, string subpath, PackageJsonValue exports, in ResolutionContext context)
    {
        var hasDotKey = false;
        var hasNonDotKey = false;
        if (exports.Kind == PackageJsonValueKind.Object)
        {
            var members = exports.Members;
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i].Key.StartsWith('.'))
                {
                    hasDotKey = true;
                }
                else
                {
                    hasNonDotKey = true;
                }
            }

            // Step 1.
            if (hasDotKey && hasNonDotKey)
            {
                Throw.ModuleResolutionException(
                    $"Invalid Package Configuration: the \"exports\" of '{Display(packageDirectory)}' mixes subpath keys (starting with '.') with condition keys",
                    context.Specifier,
                    context.ReferencingModuleLocation,
                    Display(packageDirectory));
            }
        }

        var resolution = TargetResolution.Undefined;

        // Step 2.
        if (string.Equals(subpath, ".", StringComparison.Ordinal))
        {
            PackageJsonValue? mainExport = null;
            if (exports.Kind is PackageJsonValueKind.String or PackageJsonValueKind.Array
                || (exports.Kind == PackageJsonValueKind.Object && !hasDotKey))
            {
                mainExport = exports;
            }
            else if (exports.Kind == PackageJsonValueKind.Object && exports.TryGetMember(".", out var dotExport))
            {
                mainExport = dotExport;
            }

            if (mainExport is not null)
            {
                resolution = ResolveTarget(packageDirectory, mainExport, patternMatch: null, in context);
            }
        }
        else if (exports.Kind == PackageJsonValueKind.Object && !hasNonDotKey)
        {
            // Step 3.
            resolution = ResolveMatchKey(subpath, exports, packageDirectory, in context);
        }

        // Step 4.
        if (resolution.Kind != TargetResolutionKind.Resolved)
        {
            Throw.ModuleResolutionException(
                $"Package Path Not Exported: '{subpath}' is not exported by the \"exports\" of '{Display(packageDirectory)}', which encapsulates every subpath it does not name",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(packageDirectory));
        }

        return ResolvePackageFile(resolution.ResolvedPath!, $"the \"exports\" entry for '{subpath}' of '{Display(packageDirectory)}'", in context);
    }

    /// <summary>PACKAGE_IMPORTS_EXPORTS_RESOLVE, in its exports half (<c>isImports</c> is always false here).</summary>
    private TargetResolution ResolveMatchKey(string matchKey, PackageJsonValue matchObject, string packageDirectory, in ResolutionContext context)
    {
        // Step 1.
        if (matchKey.EndsWith('/'))
        {
            Throw.ModuleResolutionException(
                $"Invalid Module Specifier: the subpath '{matchKey}' ends with '/', which \"exports\" no longer matches - name the file, or use a '*' pattern",
                context.Specifier,
                context.ReferencingModuleLocation);
        }

        var members = matchObject.Members;

        // Step 2.
        for (var i = 0; i < members.Count; i++)
        {
            var key = members[i].Key;
            if (string.Equals(key, matchKey, StringComparison.Ordinal) && !key.Contains('*'))
            {
                return ResolveTarget(packageDirectory, members[i].Value, patternMatch: null, in context);
            }
        }

        // Step 3: the keys carrying exactly one '*', most specific first.
        List<string>? expansionKeys = null;
        for (var i = 0; i < members.Count; i++)
        {
            if (CountStars(members[i].Key) == 1)
            {
                (expansionKeys ??= new List<string>()).Add(members[i].Key);
            }
        }

        if (expansionKeys is null)
        {
            return TargetResolution.NullTarget;
        }

        SortByPatternSpecificity(expansionKeys);

        // Step 4.
        for (var i = 0; i < expansionKeys.Count; i++)
        {
            var expansionKey = expansionKeys[i];
            var starIndex = expansionKey.IndexOf('*');
            var patternBase = expansionKey.Substring(0, starIndex);
            if (!matchKey.StartsWith(patternBase, StringComparison.Ordinal) || string.Equals(matchKey, patternBase, StringComparison.Ordinal))
            {
                continue;
            }

            var patternTrailer = expansionKey.Substring(starIndex + 1);
            if (patternTrailer.Length != 0
                && !(matchKey.EndsWith(patternTrailer, StringComparison.Ordinal) && matchKey.Length >= expansionKey.Length))
            {
                continue;
            }

            matchObject.TryGetMember(expansionKey, out var target);
            var patternMatch = matchKey.Substring(patternBase.Length, matchKey.Length - patternBase.Length - patternTrailer.Length);
            return ResolveTarget(packageDirectory, target, patternMatch, in context);
        }

        // Step 5.
        return TargetResolution.NullTarget;
    }

    /// <summary>PACKAGE_TARGET_RESOLVE, in its exports half (<c>isImports</c> is always false here).</summary>
    private TargetResolution ResolveTarget(string packageDirectory, PackageJsonValue target, string? patternMatch, in ResolutionContext context)
    {
        switch (target.Kind)
        {
            case PackageJsonValueKind.String:
                return ResolveStringTarget(packageDirectory, target.StringValue!, patternMatch, in context);

            case PackageJsonValueKind.Object:
                return ResolveConditions(packageDirectory, target, patternMatch, in context);

            case PackageJsonValueKind.Array:
                return ResolveFallbackArray(packageDirectory, target, patternMatch, in context);

            case PackageJsonValueKind.Null:
                // Step 4: an explicit null blocks the subpath, and unlike "no match" it stops the surrounding
                // condition loop rather than letting a later condition answer.
                return TargetResolution.NullTarget;

            default:
                Throw.ModuleResolutionException(
                    $"{InvalidPackageTarget}: an \"exports\" target of '{Display(packageDirectory)}' is neither a string, an object, an array nor null",
                    context.Specifier,
                    context.ReferencingModuleLocation,
                    Display(packageDirectory));
                return default;
        }
    }

    private TargetResolution ResolveStringTarget(string packageDirectory, string target, string? patternMatch, in ResolutionContext context)
    {
        // Step 1.1: with isImports false every string target must be a package-relative path.
        if (!target.StartsWith("./", StringComparison.Ordinal))
        {
            Throw.ModuleResolutionException(
                $"{InvalidPackageTarget}: the \"exports\" target '{target}' of '{Display(packageDirectory)}' must start with './'",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(packageDirectory));
        }

        // Step 1.2.
        if (HasForbiddenSegment(target, skipLeadingDotSegment: true))
        {
            Throw.ModuleResolutionException(
                $"{InvalidPackageTarget}: the \"exports\" target '{target}' of '{Display(packageDirectory)}' contains an empty, '.', '..' or 'node_modules' segment",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(packageDirectory));
        }

        var relative = target;
        if (patternMatch is not null)
        {
            // Step 1.6, which guards step 1.7's substitution: a '*' expands to whatever the importer wrote, so
            // it is the importer - not the package - that is refused for a traversal segment.
            if (HasForbiddenSegment(patternMatch, skipLeadingDotSegment: false))
            {
                Throw.ModuleResolutionException(
                    $"Invalid Module Specifier: the part matched by '*' contains an empty, '.', '..' or 'node_modules' segment",
                    context.Specifier,
                    context.ReferencingModuleLocation);
            }

            // Step 1.7: "all instances of * on the right hand side will then be replaced with this value".
            relative = target.Replace("*", patternMatch);
        }

        RejectInvalidPathCharacters(relative, in context);

        var resolved = Path.GetFullPath(Path.Combine(packageDirectory, relative.Substring(2)));

        // Step 1.4 asserts the package contains the target. The segment checks above make that true, so a
        // failure here is a bug rather than a configuration error - but it is the last line before a path is
        // handed out, so it is checked rather than assumed.
        if (!IsWithinDirectory(packageDirectory, resolved))
        {
            Throw.ModuleResolutionException(
                $"{InvalidPackageTarget}: the \"exports\" target '{target}' of '{Display(packageDirectory)}' leaves the package",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(packageDirectory));
        }

        return TargetResolution.Resolved(resolved);
    }

    /// <summary>PACKAGE_TARGET_RESOLVE step 2: a conditions object, tried in the order the package wrote it.</summary>
    private TargetResolution ResolveConditions(string packageDirectory, PackageJsonValue target, string? patternMatch, in ResolutionContext context)
    {
        var members = target.Members;

        // Step 2.1.
        for (var i = 0; i < members.Count; i++)
        {
            if (IsArrayIndexKey(members[i].Key))
            {
                Throw.ModuleResolutionException(
                    $"Invalid Package Configuration: a conditions object of '{Display(packageDirectory)}' has the array-index key '{members[i].Key}'",
                    context.Specifier,
                    context.ReferencingModuleLocation,
                    Display(packageDirectory));
            }
        }

        // Step 2.2. Source order decides, not the order of the configured conditions: "earlier entries have
        // higher priority and take precedence over later entries".
        for (var i = 0; i < members.Count; i++)
        {
            var condition = members[i].Key;
            if (!string.Equals(condition, "default", StringComparison.Ordinal) && !MatchesCondition(condition))
            {
                continue;
            }

            var resolved = ResolveTarget(packageDirectory, members[i].Value, patternMatch, in context);
            if (resolved.Kind == TargetResolutionKind.Undefined)
            {
                continue;
            }

            return resolved;
        }

        // Step 2.3.
        return TargetResolution.Undefined;
    }

    /// <summary>PACKAGE_TARGET_RESOLVE step 3: a fallback array.</summary>
    private TargetResolution ResolveFallbackArray(string packageDirectory, PackageJsonValue target, string? patternMatch, in ResolutionContext context)
    {
        var items = target.Items;

        // Step 3.1.
        if (items.Count == 0)
        {
            return TargetResolution.NullTarget;
        }

        ModuleResolutionException? lastError = null;
        var lastWasNull = false;

        // Step 3.2: an Invalid Package Target is not fatal inside a fallback array - the next entry gets its
        // turn, which is the whole point of writing one.
        for (var i = 0; i < items.Count; i++)
        {
            TargetResolution resolved;
            try
            {
                resolved = ResolveTarget(packageDirectory, items[i], patternMatch, in context);
            }
            catch (ModuleResolutionException ex) when (ex.ResolverAlgorithmError.StartsWith(InvalidPackageTarget, StringComparison.Ordinal))
            {
                lastError = ex;
                lastWasNull = false;
                continue;
            }

            if (resolved.Kind == TargetResolutionKind.Undefined)
            {
                continue;
            }

            if (resolved.Kind == TargetResolutionKind.Null)
            {
                lastError = null;
                lastWasNull = true;
                continue;
            }

            return resolved;
        }

        // Step 3.3: "return or throw the last fallback resolution null return or error".
        if (lastError is not null)
        {
            throw lastError;
        }

        return lastWasNull ? TargetResolution.NullTarget : TargetResolution.Undefined;
    }

    /// <summary>
    /// LOOKUP_PACKAGE_SCOPE: the nearest directory at or above <paramref name="directory"/> that holds a
    /// <c>package.json</c>, stopping at the base path and at any directory named <c>node_modules</c>.
    /// </summary>
    private string? LookupPackageScope(string directory)
    {
        var scope = directory;
        while (scope is not null && IsWithinBasePath(scope))
        {
            if (string.Equals(Path.GetFileName(scope), NodeModulesFolderName, PathComparison))
            {
                return null;
            }

            if (File.Exists(Path.Combine(scope, PackageJson.FileName)))
            {
                return scope;
            }

            scope = Path.GetDirectoryName(scope);
        }

        return null;
    }

    private PackageJson? ReadPackageJson(string packageDirectory, in ResolutionContext context)
    {
        if (!PackageJson.TryRead(packageDirectory, out var package))
        {
            Throw.ModuleResolutionException(
                $"Invalid Package Configuration: '{Display(packageDirectory)}/{PackageJson.FileName}' is not valid JSON",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(packageDirectory));
        }

        return package;
    }

    /// <summary>
    /// ESM_RESOLVE step 7 for a path a package resolved to: it has to exist, and it has to be a file.
    /// </summary>
    private ResolvedSpecifier ResolvePackageFile(string candidate, string described, in ResolutionContext context)
    {
        if (File.Exists(candidate))
        {
            return CreateResolvedFile(candidate, in context);
        }

        if (_extensionProbing && TryProbe(candidate, in context, out var probed))
        {
            return CreateResolvedFile(probed, in context);
        }

        if (Directory.Exists(candidate))
        {
            Throw.ModuleResolutionException(
                $"Unsupported Directory Import: {described} names a directory",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(candidate));
        }

        Throw.ModuleResolutionException(
            $"Module Not Found: {described} resolves to '{Display(candidate)}', which does not exist",
            context.Specifier,
            context.ReferencingModuleLocation,
            Display(candidate));
        return default!;
    }

    private ResolvedSpecifier CreateResolvedFile(string fullPath, in ResolutionContext context)
    {
        ValidateJsonTarget(fullPath, in context);

        var uri = new Uri(fullPath);
        return new ResolvedSpecifier(context.Request, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
    }

    /// <summary>
    /// Node makes the <c>type</c> import attribute mandatory for a JSON module - "the
    /// <c>with { type: 'json' }</c> syntax is mandatory" - and without that rule a <c>.json</c> file would be
    /// handed to the JavaScript parser and fail as a syntax error somewhere far from its cause.
    /// </summary>
    private void ValidateJsonTarget(string fullPath, in ResolutionContext context)
    {
        if (!fullPath.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_allowJsonModules)
        {
            Throw.ModuleResolutionException(
                $"Unsupported JSON Module: '{Display(fullPath)}' is a JSON file and {nameof(NodeModuleLoaderOptions)}.{nameof(NodeModuleLoaderOptions.AllowJsonModules)} is disabled",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(fullPath));
        }

        if (!HasTypeAttribute(context.Request))
        {
            Throw.ModuleResolutionException(
                $"Missing Import Attribute: '{Display(fullPath)}' is a JSON file, so the import needs `with {{ type: 'json' }}`",
                context.Specifier,
                context.ReferencingModuleLocation,
                Display(fullPath));
        }
    }

    private static bool HasTypeAttribute(ModuleRequest request)
        => request.IsJsonModule() || request.IsTextModule() || request.IsBytesModule();

    private bool TryProbe(string candidate, in ResolutionContext context, out string result)
    {
        if (File.Exists(candidate))
        {
            result = candidate;
            return true;
        }

        var json = _allowJsonModules && HasTypeAttribute(context.Request);

        foreach (var extension in ExtensionCandidates)
        {
            var probed = candidate + extension;
            if (File.Exists(probed))
            {
                result = probed;
                return true;
            }
        }

        if (json && File.Exists(candidate + JsonExtension))
        {
            result = candidate + JsonExtension;
            return true;
        }

        if (Directory.Exists(candidate))
        {
            foreach (var index in IndexCandidates)
            {
                var probed = Path.Combine(candidate, index);
                if (File.Exists(probed))
                {
                    result = probed;
                    return true;
                }
            }

            if (json)
            {
                var probed = Path.Combine(candidate, "index" + JsonExtension);
                if (File.Exists(probed))
                {
                    result = probed;
                    return true;
                }
            }
        }

        result = "";
        return false;
    }

    /// <summary>
    /// The directory a module's own imports resolve against. <c>referencingModuleLocation</c> is
    /// <see cref="ModuleRecord.Location"/>, which for anything this loader produced is a file system path - but a
    /// module registered with <c>Engine.Modules.Add</c> knows itself by its registration name, and that name is
    /// resolved against the base path, exactly as <see cref="DefaultModuleLoader"/> resolves it.
    /// </summary>
    private string GetReferencingDirectory(string? referencingModuleLocation)
    {
        if (string.IsNullOrEmpty(referencingModuleLocation))
        {
            return _baseDirectory;
        }

        var location = referencingModuleLocation!;

        string path;
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            path = uri.LocalPath;
        }
        else if (location.IndexOfAny(InvalidPathChars) >= 0)
        {
            // A location that cannot be a path at all - a loader-chosen name, most likely - resolves against
            // the base path, which is what a name with no location of its own gets.
            return _baseDirectory;
        }
        else if (Path.IsPathRooted(location))
        {
            path = location;
        }
        else
        {
            path = Path.Combine(_basePath, location);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return directory ?? _baseDirectory;
    }

    private bool IsWithinBasePath(string path)
    {
        if (path.StartsWith(_basePath, PathComparison))
        {
            return true;
        }

        // The base directory itself, spelled without the trailing separator the field carries.
        return path.Length == _basePath.Length - 1 && _basePath.StartsWith(path, PathComparison);
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var prefix = directory[directory.Length - 1] == Path.DirectorySeparatorChar
            ? directory
            : directory + Path.DirectorySeparatorChar;

        return path.StartsWith(prefix, PathComparison);
    }

    /// <summary>
    /// A path to name in an error message, relative to the base path and with forward slashes so it reads the
    /// same on every platform. Nothing above the base path can be named: resolution refuses such a path before
    /// anything gets to describe it, and this is the second line of that defence.
    /// </summary>
    private string Display(string fullPath)
    {
        if (!fullPath.StartsWith(_basePath, PathComparison))
        {
            return ".";
        }

        var relative = fullPath.Substring(_basePath.Length).Replace('\\', '/');
        return relative.Length == 0 ? "." : relative;
    }

    private bool MatchesCondition(string condition)
    {
        foreach (var candidate in _conditions)
        {
            if (string.Equals(candidate, condition, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void RejectEncodedSeparators(in ResolutionContext context)
    {
        // ESM_RESOLVE step 7.1: a percent-encoded separator is refused rather than decoded, so that a specifier
        // cannot smuggle a path segment past the checks that read it.
        var specifier = context.Specifier;
#pragma warning disable CA2249 // string.Contains(string, StringComparison) is netstandard2.1+, and net472 is a target
        if (specifier.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0
            || specifier.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0)
#pragma warning restore CA2249
        {
            Throw.ModuleResolutionException(
                "Invalid Module Specifier: a percent-encoded '/' or '\\' is not allowed in a module specifier",
                specifier,
                context.ReferencingModuleLocation);
        }
    }

    private static void RejectInvalidPathCharacters(string value, in ResolutionContext context)
    {
        // Path.Combine throws ArgumentException for these on .NET Framework, and an ArgumentException escaping
        // Resolve would reach the host as something other than a resolution refusal.
        if (value.IndexOfAny(InvalidPathChars) >= 0)
        {
            Throw.ModuleResolutionException(
                "Invalid Module Specifier: it contains a character that cannot appear in a file system path",
                context.Specifier,
                context.ReferencingModuleLocation);
        }
    }

    /// <summary>
    /// PACKAGE_TARGET_RESOLVE steps 1.2 and 1.6: no empty, <c>.</c>, <c>..</c> or <c>node_modules</c> segment,
    /// case-insensitively and including the percent-encoded spellings of a dot.
    /// </summary>
    private static bool HasForbiddenSegment(string value, bool skipLeadingDotSegment)
    {
        var segments = value.Split('/', '\\');
        for (var i = 0; i < segments.Length; i++)
        {
            if (i == 0 && skipLeadingDotSegment)
            {
                // The leading "." of a "./..." target is the one segment the rule exempts.
                continue;
            }

            var segment = DecodeEncodedDots(segments[i]);
            if (segment.Length == 0
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal)
                || string.Equals(segment, NodeModulesFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string DecodeEncodedDots(string segment)
    {
        if (segment.IndexOf('%') < 0)
        {
            return segment;
        }

        // Only the dot is decoded: it is the one character whose encoded spelling changes what a segment means
        // to the checks above, and decoding the rest would turn this into a URL decoder it does not need to be.
        var builder = new StringBuilder(segment.Length);
        var i = 0;
        while (i < segment.Length)
        {
            if (segment[i] == '%'
                && i + 2 < segment.Length
                && segment[i + 1] == '2'
                && (segment[i + 2] == 'e' || segment[i + 2] == 'E'))
            {
                builder.Append('.');
                i += 3;
                continue;
            }

            builder.Append(segment[i]);
            i++;
        }

        return builder.ToString();
    }

    private static bool IsArrayIndexKey(string key)
    {
        if (key.Length == 0 || (key.Length > 1 && key[0] == '0'))
        {
            return false;
        }

        for (var i = 0; i < key.Length; i++)
        {
            if (!char.IsAsciiDigit(key[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountStars(string value)
    {
        var count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '*')
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// PATTERN_KEY_COMPARE: longer prefix before shorter, then longer key before shorter. The sort is an
    /// insertion sort because it has to be <em>stable</em> - the comparison answers 0 for two keys of equal
    /// shape, and Node's own sort leaves those in source order.
    /// </summary>
    private static void SortByPatternSpecificity(List<string> keys)
    {
        for (var i = 1; i < keys.Count; i++)
        {
            var key = keys[i];
            var j = i - 1;
            while (j >= 0 && ComparePatternKeys(keys[j], key) > 0)
            {
                keys[j + 1] = keys[j];
                j--;
            }

            keys[j + 1] = key;
        }
    }

    private static int ComparePatternKeys(string keyA, string keyB)
    {
        var baseLengthA = keyA.IndexOf('*');
        var baseLengthB = keyB.IndexOf('*');
        if (baseLengthA > baseLengthB)
        {
            return -1;
        }

        if (baseLengthB > baseLengthA)
        {
            return 1;
        }

        if (keyA.Length > keyB.Length)
        {
            return -1;
        }

        if (keyB.Length > keyA.Length)
        {
            return 1;
        }

        return 0;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ResolutionContext(ModuleRequest Request, string? ReferencingModuleLocation)
    {
        public string Specifier => Request.Specifier;
    }

    private enum TargetResolutionKind
    {
        /// <summary>No condition matched; the caller keeps looking.</summary>
        Undefined,

        /// <summary>The target is explicitly blocked; the caller stops looking.</summary>
        Null,

        Resolved,
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TargetResolution(TargetResolutionKind Kind, string? ResolvedPath)
    {
        public static readonly TargetResolution Undefined = new(TargetResolutionKind.Undefined, null);
        public static readonly TargetResolution NullTarget = new(TargetResolutionKind.Null, null);

        public static TargetResolution Resolved(string path) => new(TargetResolutionKind.Resolved, path);
    }
}
