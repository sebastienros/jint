using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class ClrAccessDescriptor : PropertyDescriptor
    {
        private readonly EnvironmentRecord _env;
        private readonly Engine _engine;
        private readonly Key _name;

        private GetterFunctionInstance _get;
        private SetterFunctionInstance _set;

        public ClrAccessDescriptor(
            EnvironmentRecord env,
            Engine engine,
            string name)
            : base(value: null, PropertyFlag.Configurable)
        {
            _env = env;
            _engine = engine;
            _name = new Key(name);
        }

        public override JsValue Get => _get = _get ?? new GetterFunctionInstance(_engine, DoGet);
        public override JsValue Set => _set = _set ?? new SetterFunctionInstance(_engine, DoSet);

        private JsValue DoGet(JsValue n)
        {
            return _env.TryGetBinding(_name, false, out var binding, out _)
                ? binding.Value
                : JsValue.Undefined;
        }

        private void DoSet(JsValue n, JsValue o)
        {
            _env.SetMutableBinding(_name, o, true);
        }
    }
}
