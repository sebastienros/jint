using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Descriptors.Specialized;

internal sealed class ClrAccessDescriptor : PropertyDescriptor
{
    private readonly DeclarativeEnvironment _env;
    private readonly Engine _engine;
    private readonly Environment.BindingName _name;

    private GetterFunction? _get;
    private SetterFunction? _set;

    public ClrAccessDescriptor(
        DeclarativeEnvironment env,
        Engine engine,
        string name)
        : base(value: null, PropertyFlag.Configurable)
    {
        _flags |= PropertyFlag.NonData;
        _env = env;
        _engine = engine;
        _name = new Environment.BindingName(name);
    }

    public override JsValue Get => _get ??= new GetterFunction(_engine, DoGet);
    public override JsValue Set => _set ??= new SetterFunction(_engine, DoSet);

    private JsValue DoGet(JsValue n)
    {
        return _env.TryGetBinding(_name, strict: true, out var value)
            ? value
            : JsValue.Undefined;
    }

    private void DoSet(JsValue n, JsValue o)
    {
        _env.SetMutableBinding(_name.Key, o, strict: true);
    }
}
