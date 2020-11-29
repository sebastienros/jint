using System;
using System.Globalization;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal abstract class ReflectionPropertyDescriptor : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly object _target;
        private readonly Type _targetType;

        private readonly PropertyInfo _indexerToTry;
        private readonly string _indexerKey;

        protected ReflectionPropertyDescriptor(
            Engine engine,
            Type targetType,
            object target, 
            bool writable,
            PropertyInfo indexerToTry = null,
            string indexerKey = null)
            : base(PropertyFlag.Enumerable | PropertyFlag.CustomJsValue)
        {
            _engine = engine;
            _targetType = targetType;
            _target = target;
            _indexerToTry = indexerToTry;
            _indexerKey = indexerKey;
            Writable = writable && engine.Options._IsClrWriteAllowed;
        }

        protected abstract object DoGetValue(object target);
        protected abstract void DoSetValue(object target, object value);
        
        protected internal sealed override JsValue CustomValue
        {
            get
            {
                // first check indexer so we don't confuse inherited properties etc
                object v = v = TryReadFromIndexer();

                if (v is null)
                {
                    try
                    {
                        v = DoGetValue(_target);
                    }
                    catch (TargetInvocationException tie)
                    {
                        switch (tie.InnerException)
                        {
                            case ArgumentOutOfRangeException _:
                            case IndexOutOfRangeException _:
                            case InvalidOperationException _:
                            case NotSupportedException _:
                                return JsValue.Undefined;
                        }

                        ExceptionHelper.ThrowMeaningfulException(_engine, tie);
                    }
                }

                return JsValue.FromObject(_engine, v);
            }
            set
            {
                object obj;
                if (_targetType == typeof(JsValue))
                {
                    obj = value;
                }
                else
                {
                    // attempt to convert the JsValue to the target type
                    obj = value.ToObject();
                    if (obj != null && obj.GetType() != _targetType)
                    {
                        obj = _engine.ClrTypeConverter.Convert(obj, _targetType, CultureInfo.InvariantCulture);
                    }
                }

                try
                {
                    DoSetValue(_target, obj);
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
            }
        }

        private object TryReadFromIndexer()
        {
            var getter = _indexerToTry?.GetGetMethod();
            if (getter is null)
            {
                return null;
            }

            try
            {
                object[] parameters = { _indexerKey };
                return getter.Invoke(_target, parameters);
            }
            catch
            {
                return null;
            }
        }
    }
}