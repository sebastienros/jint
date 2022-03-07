#nullable enable

using System;
using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

public sealed class ModuleBuilder
{
    private readonly Engine _engine;
    private readonly string _specifier;
    private readonly List<string> _sourceRaw = new();
    private readonly Dictionary<string, JsValue> _exports = new();
    private readonly ParserOptions _options;

    internal ModuleBuilder(Engine engine, string specifier)
    {
        _engine = engine;
        _specifier = specifier;
        _options = new ParserOptions(specifier);
    }

    public ModuleBuilder AddSource(string code)
    {
        _sourceRaw.Add(code);
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

    public ModuleBuilder ExportType<T>()
    {
        ExportType<T>(typeof(T).Name);
        return this;
    }

    public ModuleBuilder ExportType<T>(string name)
    {
        _exports.Add(name, TypeReference.CreateTypeReference<T>(_engine));
        return this;
    }

    public ModuleBuilder ExportType(Type type)
    {
        ExportType(type.Name, type);
        return this;
    }

    public ModuleBuilder ExportType(string name, Type type)
    {
        _exports.Add(name, TypeReference.CreateTypeReference(_engine, type));
        return this;
    }

    public ModuleBuilder ExportFunction(string name, Func<JsValue[], JsValue> fn)
    {
        _exports.Add(name, new ClrFunctionInstance(_engine, name, (@this, args) => fn(args)));
        return this;
    }

    public ModuleBuilder WithOptions(Action<ParserOptions> configure)
    {
        configure(_options);
        return this;
    }

    internal Module Parse()
    {
        if (_sourceRaw.Count <= 0) return new Module(NodeList.Create(Array.Empty<Statement>()));

        var javaScriptParser = new JavaScriptParser(_sourceRaw.Count == 1 ? _sourceRaw[0] : string.Join(Environment.NewLine, _sourceRaw), _options);

        try
        {
            return javaScriptParser.ParseModule();
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(_engine.Realm, $"Error while loading module: error in module '{_specifier}': {ex.Error}");
            return null!;
        }
        catch (Exception)
        {
            ExceptionHelper.ThrowJavaScriptException(_engine, $"Could not load module {_specifier}", Completion.Empty());
            return null!;
        }
    }

    internal void BindExportedValues(CyclicModuleRecord module)
    {
        foreach (var export in _exports)
        {
            module.BindExportedValue(export.Key, export.Value);
        }
    }
}
