using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Modules;

public sealed class ModuleBuilder
{
    private readonly Engine _engine;
    private readonly string _specifier;
    private Prepared<Esprima.Ast.Module>? _module;
    private readonly List<string> _sourceRaw = new();
    private readonly Dictionary<string, JsValue> _exports = new(StringComparer.Ordinal);
    private readonly ParserOptions _defaultParserOptions;
    private readonly Parser _defaultParser;
    private ModuleParseOptions _parseOptions;

    internal ModuleBuilder(Engine engine, string specifier)
    {
        _engine = engine;
        _specifier = specifier;
        _defaultParserOptions = ModuleParseOptions.Default.GetParserOptions(engine.Options);
        _defaultParser = new Parser(_defaultParserOptions);
        _parseOptions = ModuleParseOptions.Default;
    }

    private Parser GetParserFor(ModuleParseOptions parseOptions, out ParserOptions parserOptions)
    {
        if (ReferenceEquals(parseOptions, ModuleParseOptions.Default))
        {
            parserOptions = _defaultParserOptions;
            return _defaultParser;
        }
        else
        {
            parserOptions = parseOptions.GetParserOptions(_engine.Options);
            return new Parser(parserOptions);
        }
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

    public ModuleBuilder AddModule(in Prepared<Esprima.Ast.Module> preparedModule)
    {
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

    public ModuleBuilder WithOptions(Func<ModuleParseOptions, ModuleParseOptions> configure)
    {
        _parseOptions = configure(_parseOptions);
        return this;
    }

    internal Prepared<Esprima.Ast.Module> Parse()
    {
        if (_module != null) return _module.Value;

        ParserOptions parserOptions;
        if (_sourceRaw.Count <= 0)
        {
            parserOptions = _parseOptions.GetParserOptions();
            return new Prepared<Esprima.Ast.Module>(new Esprima.Ast.Module(NodeList.Create(Array.Empty<Statement>())), parserOptions);
        }

        var parser = GetParserFor(_parseOptions, out parserOptions);
        try
        {
            var source = _sourceRaw.Count == 1 ? _sourceRaw[0] : string.Join(Environment.NewLine, _sourceRaw);
            return new Prepared<Esprima.Ast.Module>(parser.ParseModule(source, _specifier), parserOptions);
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
