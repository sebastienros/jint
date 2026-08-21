using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Modules;

public sealed class ModuleBuilder
{
    private readonly Engine _engine;
    private readonly string _specifier;
    private Prepared<AstModule>? _module;
    private readonly List<string> _sourceRaw = new();
    private readonly Dictionary<string, JsValue> _exports = new(StringComparer.Ordinal);
    private readonly ParserOptions _defaultParserOptions;
    private ModuleParsingOptions _parsingOptions;

    internal ModuleBuilder(Engine engine, string specifier)
    {
        _engine = engine;
        _specifier = specifier;
        _parsingOptions = ModuleParsingOptions.Default;
        _defaultParserOptions = _engine.DefaultModuleParserOptions;
    }

    public ModuleBuilder AddSource(string code)
    {
        if (_module != null)
        {
            throw new InvalidOperationException("Cannot have both source text and pre-compiled.");
        }
        _sourceRaw.Add(code);
        return this;
    }

    /// <summary>
    /// Uses a module the host prepared with <see cref="Engine.PrepareModule"/> instead of source text.
    /// </summary>
    /// <remarks>
    /// A prepared module keeps the name it was given at prepare time: the AST is the host's, already parsed,
    /// so loading it under a registration cannot rename it the way a module built from source is named by its
    /// resolved key. Its own relative imports therefore resolve against that prepare-time name - a module with
    /// relative imports should be prepared with <c>PrepareModule</c>'s <c>source</c> argument set to the key
    /// the loader resolves the registration to.
    /// </remarks>
    public ModuleBuilder AddModule(in Prepared<AstModule> preparedModule)
    {
        if (!preparedModule.IsValid)
        {
            Throw.InvalidPreparedModuleArgumentException(nameof(preparedModule));
        }

        if (_sourceRaw.Count > 0)
        {
            throw new InvalidOperationException("Cannot have both source text and pre-compiled.");
        }

        if (_module != null)
        {
            throw new InvalidOperationException("pre-compiled module already exists.");
        }
        _module = preparedModule;
        return this;
    }

    public ModuleBuilder ExportValue(string name, JsValue value)
    {
        _exports.Add(name, value);
        return this;
    }

    public ModuleBuilder ExportObject(string name, object value)
    {
        _exports.Add(name, JsValue.FromObject(_engine, value));
        return this;
    }

    public ModuleBuilder ExportType<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>()
    {
        ExportType<T>(typeof(T).Name);
        return this;
    }

    public ModuleBuilder ExportType<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name)
    {
        _exports.Add(name, TypeReference.CreateTypeReference<T>(_engine));
        return this;
    }

    public ModuleBuilder ExportType([DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        ExportType(type.Name, type);
        return this;
    }

    public ModuleBuilder ExportType(string name, [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        _exports.Add(name, TypeReference.CreateTypeReference(_engine, type));
        return this;
    }

    public ModuleBuilder ExportFunction(string name, Func<JsValue[], JsValue> fn)
    {
        _exports.Add(name, new ClrFunction(_engine, name, (_, args) => fn(args)));
        return this;
    }

    public ModuleBuilder ExportFunction(string name, Func<JsValue> fn)
    {
        _exports.Add(name, new ClrFunction(_engine, name, (_, _) => fn()));
        return this;
    }

    public ModuleBuilder ExportFunction(string name, Action<JsValue[]> fn)
    {
        _exports.Add(name, new ClrFunction(_engine, name, (_, args) =>
        {
            fn(args);
            return JsValue.Undefined;
        }));
        return this;
    }

    public ModuleBuilder ExportFunction(string name, Action fn)
    {
        _exports.Add(name, new ClrFunction(_engine, name, (_, _) =>
        {
            fn();
            return JsValue.Undefined;
        }));
        return this;
    }

    public ModuleBuilder WithOptions(Func<ModuleParsingOptions, ModuleParsingOptions> configure)
    {
        _parsingOptions = configure(_parsingOptions);
        return this;
    }

    /// <summary>
    /// Returns the UTF-8 byte count of the accumulated source, or 0 for exports-only/prepared modules.
    /// </summary>
    internal long GetSourceByteCount()
    {
        if (_module is not null)
        {
            return 0; // Prepared module — original source size unknowable.
        }

        if (_sourceRaw.Count == 0)
        {
            return 0; // Exports-only — no source.
        }

        long bytes = 0;
        foreach (var s in _sourceRaw)
        {
            bytes += System.Text.Encoding.UTF8.GetByteCount(s);
        }

        bytes += (long) (_sourceRaw.Count - 1) * System.Text.Encoding.UTF8.GetByteCount(Environment.NewLine);
        return bytes;
    }

    /// <summary>
    /// Parses the accumulated source, naming the module <paramref name="location"/>. That is the key the loader
    /// resolved the registration to rather than the name it was registered under, since it is what the module's
    /// own relative imports are resolved against; the two are the same string unless the loader canonicalized.
    /// A module supplied pre-compiled through <see cref="AddModule"/> is the exception - it keeps the name it
    /// was prepared with, as its doc explains.
    /// </summary>
    internal Prepared<AstModule> Parse(string location)
    {
        if (_module != null) return _module.Value;

        var parserOptions = ReferenceEquals(_parsingOptions, ModuleParsingOptions.Default)
            ? _defaultParserOptions
            : _parsingOptions.GetParserOptions(_engine.Options);

        if (_sourceRaw.Count <= 0)
        {
            // No source means nothing to parse and no relative imports to resolve, but the module is still
            // named so that an exports-only registration identifies itself the way every other module does.
            var exportsOnly = new AstModule(NodeList.From(Array.Empty<Statement>()))
            {
                Location = default(SourceLocation).WithSourceFile(location),
            };
            return new Prepared<AstModule>(
                exportsOnly,
                parserOptions,
                parsingConstraints: _engine.CombineParsingConstraints(ParsingConstraints.From(_parsingOptions)));
        }

        var parser = _engine.CreateModuleParser(parserOptions, _parsingOptions);
        try
        {
            if (_sourceRaw.Count > 1)
            {
                long sourceLength = (long) Environment.NewLine.Length * (_sourceRaw.Count - 1);
                for (var i = 0; i < _sourceRaw.Count; i++)
                {
                    sourceLength += _sourceRaw[i].Length;
                }

                _engine.CheckParsingSourceLength(sourceLength, _parsingOptions);
            }

            var source = _sourceRaw.Count == 1 ? _sourceRaw[0] : string.Join(Environment.NewLine, _sourceRaw);
            return new Prepared<AstModule>(
                parser.ParseModule(source, location),
                parserOptions,
                parsingConstraints: parser.Constraints);
        }
        catch (ParseErrorException ex)
        {
            // The diagnostic names the module the way the host knows it, which is the registration name.
            var errorLocation = SourceLocation.From(Position.From(ex.LineNumber, ex.Column), Position.From(ex.LineNumber, ex.Column), _specifier);
            Throw.SyntaxError(_engine.Realm, $"Error while loading module: error in module '{_specifier}': {ex.Error}", in errorLocation);
            return default;
        }
    }

    internal void BindExportedValues(BuilderModule module)
    {
        foreach (var export in _exports)
        {
            module.BindExportedValue(export.Key, export.Value);
        }
    }
}
