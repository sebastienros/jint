using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Modules;

/// <summary>
/// This is a custom ModuleRecord implementation for dynamically built modules using <see cref="ModuleBuilder"/>
/// </summary>
internal sealed class BuilderModuleRecord : SourceTextModuleRecord
{
    private List<KeyValuePair<string, JsValue>> _exportBuilderDeclarations = new();

    internal BuilderModuleRecord(Engine engine, Realm realm, Module source, string? location, bool async)
        : base(engine, realm, source, location, async)
    {
    }

    internal void BindExportedValue(string name, JsValue value)
    {
        if (_environment is not null)
        {
            ExceptionHelper.ThrowInvalidOperationException("Cannot bind exported values after the environment has been initialized");
        }

        _exportBuilderDeclarations ??= new();
        _exportBuilderDeclarations.Add(new KeyValuePair<string, JsValue>(name, value));
    }

    protected override void InitializeEnvironment()
    {
        base.InitializeEnvironment();

        if (_exportBuilderDeclarations != null)
        {
            for (var i = 0; i < _exportBuilderDeclarations.Count; i++)
            {
                var d = _exportBuilderDeclarations[i];
                _environment.CreateImmutableBindingAndInitialize(d.Key, true, d.Value);
                _localExportEntries.Add(new ExportEntry(d.Key, null, null, d.Key));
            }

            _exportBuilderDeclarations.Clear();
        }
    }
}
