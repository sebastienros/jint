using Jint.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Jint.Runtime.Interop
{
    public class FieldProxy
    {
        private FieldInfo _fieldInfo;
        private Func<object, object> _getterProxy;
        private Action<object, object> _setterProxy;

        public FieldProxy(FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
        }

        public string Name
        {
            get
            {
                return _fieldInfo.Name;
            }
        }

        public Type FieldType
        {
            get
            {
                return _fieldInfo.FieldType;
            }
        }

        public bool CanWrite
        {
            get
            {
                // don't write to fields marked as readonly
                return !_fieldInfo.Attributes.HasFlag(FieldAttributes.InitOnly);
            }
        }

        public JsValue GetValue(Engine engine, object item)
        {
#if __IOS__
            // Avoid slower, "interpreted Expression", path on iOS/Xamarin.
            return JsValue.FromObject(engine, _fieldInfo.GetValue(item));
#else
            if (_getterProxy == null)
            {
                var selfArgument = Expression.Parameter(typeof(object), "self");

                if (_fieldInfo.IsStatic)
                {
                    _getterProxy = Expression.Lambda<Func<object, object>>(
                        Expression.Convert(
                            Expression.Field(
                                null, _fieldInfo),
                        typeof(object)), selfArgument).Compile();
                }
                else
                {
                    _getterProxy = Expression.Lambda<Func<object, object>>(
                        Expression.Convert(
                            Expression.Field(
                                Expression.Convert(selfArgument, _fieldInfo.DeclaringType), _fieldInfo),
                        typeof(object)), selfArgument).Compile();
                }
            }

            return JsValue.FromObject(engine, _getterProxy(item));
#endif
        }

        public void SetValue(Engine engine, object item, object value)
        {
#if __IOS__
            _fieldInfo.SetValue(item, value);
#else
            if (_setterProxy == null)
            {
                var selfArgument = Expression.Parameter(typeof(object), "self");
                var valueArgument = Expression.Parameter(typeof(object), "value");

                if (_fieldInfo.IsStatic)
                {
                    _setterProxy = Expression.Lambda<Action<object, object>>(
                        Expression.Assign(
                            Expression.Field(
                                null, _fieldInfo),
                            Expression.Convert(valueArgument, _fieldInfo.FieldType)
                        ), selfArgument, valueArgument).Compile();
                }
                else
                {
                    _setterProxy = Expression.Lambda<Action<object, object>>(
                        Expression.Assign(
                            Expression.Field(
                                Expression.Convert(selfArgument, _fieldInfo.DeclaringType), _fieldInfo),
                            Expression.Convert(valueArgument, _fieldInfo.FieldType)
                        ), selfArgument, valueArgument).Compile();
                }
            }

            _setterProxy(item, value);
#endif
        }
    }
}
