using Jint.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Jint.Runtime.Interop
{
    public class PropertyProxy
    {
        private PropertyInfo _propertyInfo;
        private MethodInfo _getter;
        private MethodInfo _setter;
        private Func<object, object> _getterProxy;
        private Action<object, object> _setterProxy;

        public PropertyProxy(PropertyInfo propertyInfo, MethodInfo getter, MethodInfo setter)
        {
            _propertyInfo = propertyInfo;
            _getter = getter;
            _setter = setter;
        }

        public string Name
        {
            get
            {
                return _propertyInfo.Name;
            }
        }

        public Type PropertyType
        {
            get
            {
                return _propertyInfo.PropertyType;
            }
        }

        public bool CanWrite
        {
            get
            {
                return _propertyInfo.CanWrite;
            }
        }

        public bool HasIndexParameters
        {
            get
            {
                return _propertyInfo.GetIndexParameters().Length != 0;
            }
        }

        public JsValue GetValue(Engine engine, object item)
        {
#if __IOS__
            // Avoid slower, "interpreted Expression", path on iOS/Xamarin.
            return JsValue.FromObject(engine, _propertyInfo.GetValue(item, null));
#else
            if (_getterProxy == null)
            {
                var selfArgument = Expression.Parameter(typeof(object), "self");

                if (_getter.IsStatic)
                {
                    _getterProxy = Expression.Lambda<Func<object, object>>(
                        Expression.Convert(
                            Expression.Call(
                                _getter
                            ),
                        typeof(object)), selfArgument).Compile();
                }
                else
                {
                    _getterProxy = Expression.Lambda<Func<object, object>>(
                        Expression.Convert(
                            Expression.Call(
                                Expression.Convert(selfArgument, _propertyInfo.DeclaringType),
                                _getter
                            ),
                        typeof(object)), selfArgument).Compile();
                }
            }

            return JsValue.FromObject(engine, _getterProxy(item));
#endif
        }

        public void SetValue(Engine engine, object item, object value)
        {
#if __IOS__
            _propertyInfo.SetValue(item, value, null);
#else
            if (_setterProxy == null)
            {
                var selfArgument = Expression.Parameter(typeof(object), "self");
                var valueArgument = Expression.Parameter(typeof(object), "value");

                if (_setter.IsStatic)
                {
                    _setterProxy = Expression.Lambda<Action<object, object>>(
                        Expression.Call(
                            _setter,
                            Expression.Convert(valueArgument, _propertyInfo.PropertyType)
                        ), selfArgument, valueArgument).Compile();
                }
                else
                {
                    _setterProxy = Expression.Lambda<Action<object, object>>(
                        Expression.Call(
                            Expression.Convert(selfArgument, _propertyInfo.DeclaringType),
                            _setter,
                            Expression.Convert(valueArgument, _propertyInfo.PropertyType)
                        ), selfArgument, valueArgument).Compile();                    
                }
            }

            _setterProxy(item, value);
#endif
        }
    }
}
