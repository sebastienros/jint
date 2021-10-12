using System;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop.Reflection
{
    /// <summary>
    /// Strategy to read and write CLR object properties and fields.
    /// </summary>
    internal abstract class ReflectionAccessor
    {
        private readonly Type _memberType;
        private readonly object _memberName;
        private readonly PropertyInfo _indexer;

        protected ReflectionAccessor(
            Type memberType,
            object memberName,
            PropertyInfo indexer = null)
        {
            _memberType = memberType;
            _memberName = memberName;
            _indexer = indexer;
        }

        public abstract bool Writable { get; }

        protected abstract object DoGetValue(object target);

        protected abstract void DoSetValue(object target, object value);

        public object GetValue(Engine engine, object target)
        {
            var constantValue = ConstantValue;
            if (constantValue is not null)
            {
                return constantValue;
            }

            // first check indexer so we don't confuse inherited properties etc
            var value = TryReadFromIndexer(target);

            if (value is null)
            {
                try
                {
                    value = DoGetValue(target);
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

                    ExceptionHelper.ThrowMeaningfulException(engine, tie);
                }
            }

            return value;
        }

        protected virtual JsValue ConstantValue => null;

        private object TryReadFromIndexer(object target)
        {
            var getter = _indexer?.GetGetMethod();
            if (getter is null)
            {
                return null;
            }

            try
            {
                object[] parameters = { _memberName };
                return getter.Invoke(target, parameters);
            }
            catch
            {
                return null;
            }
        }

        public void SetValue(Engine engine, object target, JsValue value)
        {
            object converted = null;
            if (_memberType == typeof(JsValue))
            {
                converted = value;
            }
            else if (_memberType == typeof(bool)
                     && (engine.Options.Interop.ValueCoercion & ValueCoercionType.Boolean) != 0)
            {
                converted = TypeConverter.ToBoolean(value);
            }
            else if (_memberType == typeof(string)
                     && !value.IsNullOrUndefined()
                     && (engine.Options.Interop.ValueCoercion & ValueCoercionType.String) != 0)
            {
                // we know how to print out correct string presentation for primitives
                // that are non-null and non-undefined
                converted = TypeConverter.ToString(value);
            }
            else if (_memberType.IsClrNumericCoercible() && (engine.Options.Interop.ValueCoercion & ValueCoercionType.Number) != 0)
            {
                // we know how to print out correct string presentation for primitives
                // that are non-null and non-undefined
                var number = TypeConverter.ToNumber(value);
                converted = number.AsNumberOfType(Type.GetTypeCode(_memberType));
            }

            if (converted is null)
            {
                // attempt to convert the JsValue to the target type
                converted = value.ToObject();
                if (converted != null && converted.GetType() != _memberType)
                {
                    converted = ConvertValueToSet(engine, converted);
                }
            }

            try
            {
                DoSetValue(target, converted);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionHelper.ThrowMeaningfulException(engine, exception);
            }
        }

        protected virtual object ConvertValueToSet(Engine engine, object value)
        {
            return engine.ClrTypeConverter.Convert(value, _memberType, CultureInfo.InvariantCulture);
        }

        public virtual PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target)
        {
            return new ReflectionDescriptor(engine, this, target);
        }
    }
}