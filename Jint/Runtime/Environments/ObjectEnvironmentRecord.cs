using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents an object environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2
    /// </summary>
    public sealed class ObjectEnvironmentRecord : EnvironmentRecord
    {
        private readonly Engine _engine;
        private readonly ObjectInstance _bindingObject;
        private readonly bool _provideThis;

        public ObjectEnvironmentRecord(Engine engine, ObjectInstance bindingObject, bool provideThis) : base(engine)
        {
            _engine = engine;
            _bindingObject = bindingObject;
            _provideThis = provideThis;
        }

        public override bool HasBinding(string name)
        {
            return _bindingObject.HasProperty(name);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2.2
        /// </summary>
        /// <param name="name"></param>
        /// <param name="configurable"></param>
        public override void CreateMutableBinding(string name, bool configurable = true)
        {
            _bindingObject.DefineOwnProperty(name, new PropertyDescriptor(Undefined.Instance, true, true, configurable), true);
        }

        public override void SetMutableBinding(string name, JsValue value, bool strict)
        {
            _bindingObject.Put(name, value, strict);
        }

        public override JsValue GetBindingValue(string name, bool strict)
        {
            // todo: can be optimized

            if (!_bindingObject.HasProperty(name))
            {
                if(!strict)
                {
                    return Undefined.Instance;
                }

                throw new JavaScriptException(_engine.ReferenceError);
            }

            return _bindingObject.Get(name);
        }

        public override bool DeleteBinding(string name)
        {
            return _bindingObject.Delete(name, false);
        }

        public override JsValue ImplicitThisValue()
        {
            if (_provideThis)
            {
                return new JsValue(_bindingObject);
            }

            return Undefined.Instance;
        }

        public override string[] GetAllBindingNames()
        {
            if (_bindingObject != null && _bindingObject.Properties != null)
            {
                return _bindingObject.Properties.Keys.ToArray();
            }
            return new string[0];
        }
    }
}
