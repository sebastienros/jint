using System.Diagnostics.CodeAnalysis;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Modules;

public sealed class ModuleBuilder
{
    private readonly Engine _engine;
    private readonly string _specifier;
    private global::Esprima.Ast.Module? _module;
    private readonly List<string> _sourceRaw = new();
    private readonly Dictionary<string, JsValue> _exports = new(StringComparer.Ordinal);
    private readonly ParserOptions _options;

    internal ModuleBuilder(Engine engine, string specifier)
    {
        _engine = engine;
        _specifier = specifier;
        _options = new ParserOptions
        {
            RegexTimeout = engine.Options.Constraints.RegexTimeout
        };
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

    public ModuleBuilder AddModule(global::Esprima.Ast.Module module)
    {
        if (_sourceRaw.Count > 0)
        {
            throw new InvalidOperationException("Cannot have both source text and pre-compiled.");
        }

        if (_module != null)
        {
            throw new InvalidOperationException("pre-compiled module already exists.");
        }
        _module = module;
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

    public ModuleBuilder WithOptions(Action<ParserOptions> configure)
    {
        configure(_options);
        return this;
    }

    internal global::Esprima.Ast.Module Parse()
    {
        if (_module != null) return _module;
        if (_sourceRaw.Count <= 0)
        {
            return new global::Esprima.Ast.Module(NodeList.Create(Array.Empty<Statement>()));
        }

        var javaScriptParser = new JavaScriptParser(_options);
        try
        {
            var source = _sourceRaw.Count == 1 ? _sourceRaw[0] : string.Join(Environment.NewLine, _sourceRaw);
            return javaScriptParser.ParseModule(source, _specifier);
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(_engine.Realm, $"Error while loading module: error in module '{_specifier}': {ex.Error}", Location.From(Position.From(ex.LineNumber, ex.Column), Position.From(ex.LineNumber, ex.Column), _specifier));
            return null!;
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
