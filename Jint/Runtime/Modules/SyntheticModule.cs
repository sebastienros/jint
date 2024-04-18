using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Modules;

internal sealed class SyntheticModule : Module
{
    private readonly JsValue _obj;
    private readonly ParserOptions? _parserOptions;
    private readonly List<string> _exportNames = ["default"];

    internal SyntheticModule(Engine engine, Realm realm, JsValue obj, string? location, ParserOptions? parserOptions = null)
        : base(engine, realm, location)
    {
        _obj = obj;

        var env = JintEnvironment.NewModuleEnvironment(_engine, realm.GlobalEnv);
        _environment = env;
        _parserOptions = parserOptions;

    }

    public override List<string> GetExportedNames(List<CyclicModule>? exportStarSet = null) => _exportNames;

    internal override ResolvedBinding? ResolveExport(string exportName, List<ExportResolveSetItem>? resolveSet = null)
    {
        if (!_exportNames.Contains(exportName))
        {
            return null;
        }

        return new ResolvedBinding(this, exportName);
    }

    public override void Link()
    {
        InnerModuleLinking(null!, 0);
    }

    public override JsValue Evaluate()
    {
        var moduleContext = new ExecutionContext(
            function: null,
            realm: _realm,
            scriptOrModule: this,
            variableEnvironment: _environment,
            lexicalEnvironment: _environment,
            privateEnvironment: null,
            generator: null,
            parserOptions: _parserOptions);

        // 7.Suspend the currently running execution context.
        _engine.EnterExecutionContext(moduleContext);

        _environment.SetMutableBinding(KnownKeys.Default, _obj, strict: true);

        _engine.LeaveExecutionContext();

        var pc = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        pc.Resolve.Call(Undefined, Array.Empty<JsValue>());
        return pc.PromiseInstance;
    }

    protected internal override int InnerModuleLinking(Stack<CyclicModule> stack, int index)
    {
        foreach (var exportName in _exportNames)
        {
            _environment.CreateMutableBinding(exportName, canBeDeleted: false);
            _environment.InitializeBinding(exportName, Undefined);
        }
        return index;
    }

    protected internal override Completion InnerModuleEvaluation(Stack<CyclicModule> stack, int index, ref int asyncEvalOrder)
    {
        _environment.SetMutableBinding(KnownKeys.Default, _obj, strict: true);
        return new Completion(CompletionType.Normal, index, new Identifier(""));
    }
}
