using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed class ClrFunctionInstance : FunctionInstance
    {
        private const string PropertyNameName = "name";
        private const int PropertyNameNameLength = 4;
        
        private readonly Func<JsValue, JsValue[], JsValue> _func;
        private PropertyDescriptor _name;

        public ClrFunctionInstance(
            Engine engine,
            string name,
            Func<JsValue, JsValue[], JsValue> func,
            int length) : base(engine, null, null, false)
        {
            _func = func;
            _name = new PropertyDescriptor(name, PropertyFlag.Configurable);

            Prototype = engine.Function.PrototypeObject;
            Extensible = true;

            _length = new PropertyDescriptor(length, PropertyFlag.Configurable);
        }

        public ClrFunctionInstance(
            Engine engine,
            string name,
            Func<JsValue, JsValue[], JsValue> func)
            : this(engine, name, func, 0)
        {
        }

        public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            if (_name != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(PropertyNameName, _name);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                return _name ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(propertyName);
        }

        protected internal override void SetOwnProperty(string propertyName, PropertyDescriptor desc)
        {
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                _name = desc;
            }
            else
            {
                base.SetOwnProperty(propertyName, desc);
            }
        }

        public override bool HasOwnProperty(string propertyName)
        {
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                return _name != null;
            }

            return base.HasOwnProperty(propertyName);
        }

        public override void RemoveOwnProperty(string propertyName)
        {
            if (propertyName.Length == PropertyNameNameLength && propertyName == PropertyNameName)
            {
                _name = null;
            }

            base.RemoveOwnProperty(propertyName);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            try
            {
                var result = _func(thisObject, arguments);
                return result;
            }
            catch (InvalidCastException)
            {
                ExceptionHelper.ThrowTypeError(Engine);
                return null;
            }
        }
    }
}
