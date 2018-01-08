using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class ClrAccessDescriptor : IPropertyDescriptor
    {
        private readonly EnvironmentRecord _env;
        private readonly Engine _engine;
        private readonly string _name;

        private GetterFunctionInstance _get;
        private SetterFunctionInstance _set;

        public ClrAccessDescriptor(
            EnvironmentRecord env,
            Engine engine,
            string name)
        {
            _env = env;
            _engine = engine;
            _name = name;
        }

        public JsValue Get => _get = _get ?? new GetterFunctionInstance(_engine, DoGet);
        public JsValue Set => _set = _set ?? new SetterFunctionInstance(_engine, DoSet);

        public bool? Enumerable => null;
        public bool? Writable => null;
        public bool? Configurable => true;

        public JsValue Value { get; set; }

        private JsValue DoGet(JsValue n)
        {
            return _env.GetBindingValue(_name, false);
        }

        private void DoSet(JsValue n, JsValue o)
        {
            _env.SetMutableBinding(_name, o, true);
        }
    }
}
