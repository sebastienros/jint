using System;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class FieldInfoDescriptor : PropertyDescriptor, ISharedDescriptor
    {
        private readonly Engine _engine;
        private readonly FieldProxy _fieldProxy;
        private object _item;

        public FieldInfoDescriptor(Engine engine, FieldProxy fieldProxy, object item = null)
        {
            _engine = engine;
            _fieldProxy = fieldProxy;
            _item = item;

            Writable = _fieldProxy.CanWrite;
        }

        public override JsValue Value
        {
            get
            {
                return _fieldProxy.GetValue(_engine, _item);
            }

            set
            {
                var currentValue = value;
                object obj;
                if (_fieldProxy.FieldType == typeof (JsValue))
                {
                    obj = currentValue;
                }
                else
                {
                    // attempt to convert the JsValue to the target type
                    obj = currentValue.ToObject();
                    if (obj.GetType() != _fieldProxy.FieldType)
                    {
                        obj = _engine.ClrTypeConverter.Convert(obj, _fieldProxy.FieldType, CultureInfo.InvariantCulture);
                    }
                }
                
                _fieldProxy.SetValue(_engine, _item, obj);
            }
        }

        public void SetTarget(object target)
        {
            _item = target;
        }
    }
}
