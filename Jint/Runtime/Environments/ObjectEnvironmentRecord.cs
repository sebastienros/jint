using Jint.Native;
using Jint.Native.Errors;
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
        private readonly ObjectInstance _bindingObject;
        private readonly bool _provideThis;

        public ObjectEnvironmentRecord(ObjectInstance bindingObject, bool provideThis = false)
        {
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
            var property = new DataDescriptor(Undefined.Instance)
                {
                    Writable = true,
                    Enumerable = true,
                    Configurable = configurable
                };

            _bindingObject.DefineOwnProperty(name, property, true);
        }

        public override void SetMutableBinding(string name, object value, bool strict)
        {
            _bindingObject.Put(name, value, strict);
        }

        public override object GetBindingValue(string name, bool strict)
        {
            // todo: can be optimized

            if (!_bindingObject.HasProperty(name))
            {
                if(!strict)
                {
                    return Undefined.Instance;
                }

                throw new ReferenceError();
            }

            return _bindingObject.Get(name);
        }

        public override bool DeleteBinding(string name)
        {
            return _bindingObject.Delete(name, false);
        }

        public override object ImplicitThisValue()
        {
            if (_provideThis)
            {
                return _bindingObject;
            }

            return Undefined.Instance;
        }
    }
}
