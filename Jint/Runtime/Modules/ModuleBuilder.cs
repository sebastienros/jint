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

    public ModuleBuilder AddModule(in Prepared<AstModule> preparedModule)
    {
        if (!preparedModule.IsValid)
        {
            ExceptionHelper.ThrowInvalidPreparedModuleArgumentException(nameof(preparedModule));
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

    internal Prepared<AstModule> Parse()
    {
        if (_module != null) return _module.Value;

        var parserOptions = ReferenceEquals(_parsingOptions, ModuleParsingOptions.Default)
            ? _defaultParserOptions
            : _parsingOptions.GetParserOptions(_engine.Options);

        if (_sourceRaw.Count <= 0)
        {
            return new Prepared<AstModule>(new AstModule(NodeList.From(Array.Empty<Statement>())), parserOptions);
        }

        var parser = new Parser(parserOptions);
        try
        {
            var source = _sourceRaw.Count == 1 ? _sourceRaw[0] : string.Join(Environment.NewLine, _sourceRaw);
            return new Prepared<AstModule>(parser.ParseModule(source, _specifier), parserOptions);
        }
        catch (ParseErrorException ex)
        {
            var location = SourceLocation.From(Position.From(ex.LineNumber, ex.Column), Position.From(ex.LineNumber, ex.Column), _specifier);
            ExceptionHelper.ThrowSyntaxError(_engine.Realm, $"Error while loading module: error in module '{_specifier}': {ex.Error}", location);
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
