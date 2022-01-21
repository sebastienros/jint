#nullable enable

using System;
using System.Collections.Generic;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

public sealed class ModuleBuilder
{
    private readonly Engine _engine;
    private readonly List<string> _sourceRaw = new();
    private readonly Dictionary<string, JsValue> _exports = new();

    public ModuleBuilder(Engine engine)
    {
        _engine = engine;
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

    internal Module Parse()
    {
        if (_sourceRaw.Count > 0)
        {
            return new JavaScriptParser(_sourceRaw.Count == 1 ? _sourceRaw[0] : string.Join(Environment.NewLine, _sourceRaw)).ParseModule();
        }
        else
        {
            return new Module(NodeList.Create(Array.Empty<Statement>()));
        }
    }

    internal void BindExportedValues(JsModule module)
    {
        foreach (var export in _exports)
        {
            module.BindExportedValue(export.Key, export.Value);
        }
    }
}