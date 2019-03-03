using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop
{
    public sealed class TypeReference : FunctionInstance, IConstructor, IObjectWrapper
    {
        private TypeReference(Engine engine)
            : base(engine, "typereference", null, null, false, "TypeReference")
        {
        }

        public Type ReferenceType { get; set; }

        public static TypeReference CreateTypeReference(Engine engine, Type type)
        {
            var obj = new TypeReference(engine);
            obj.Extensible = false;
            obj.ReferenceType = type;

            // The value of the [[Prototype]] internal property of the TypeReference constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj._length = new PropertyDescriptor(0, PropertyFlag.AllForbidden);

            // The initial value of Boolean.prototype is the Boolean prototype object
            obj._prototype = new PropertyDescriptor(engine.Object.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            // direct calls on a TypeReference constructor object is equivalent to the new operator
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            if (arguments.Length == 0 && ReferenceType.IsValueType)
            {
                var instance = Activator.CreateInstance(ReferenceType);
                var result = TypeConverter.ToObject(Engine, JsValue.FromObject(Engine, instance));

                return result;
            }

            var constructors = ReferenceType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            foreach (var tuple in TypeConverter.FindBestMatch(_engine, constructors, (info, b) => arguments))
            {
                var method = tuple.Item1;

                var parameters = new object[arguments.Length];
                var methodParameters = method.GetParameters();
                try
                {
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        var parameterType = methodParameters[i].ParameterType;

                        if (typeof(JsValue).IsAssignableFrom(parameterType))
                        {
                            parameters[i] = arguments[i];
                        }
                        else
                        {
                            parameters[i] = Engine.ClrTypeConverter.Convert(
                                arguments[i].ToObject(),
                                parameterType,
                                CultureInfo.InvariantCulture);
                        }
                    }

                    var constructor = (ConstructorInfo) method;
                    var instance = constructor.Invoke(parameters);
                    var result = TypeConverter.ToObject(Engine, FromObject(Engine, instance));

                    // todo: cache method info

                    return result;
                }
                catch
                {
                    // ignore method
                }
            }

            ExceptionHelper.ThrowTypeError(_engine, "No public methods with the specified arguments were found.");
            return null;
        }

        public override bool HasInstance(JsValue v)
        {
            if (v.IsObject())
            {
                var wrapper = v.AsObject() as IObjectWrapper;
                if (wrapper != null)
                    return wrapper.Target.GetType() == ReferenceType;
            }

            return base.HasInstance(v);
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            if (throwOnError)
            {
                ExceptionHelper.ThrowTypeError(_engine, "Can't define a property of a TypeReference");
            }

            return false;
        }

        public override bool Delete(string propertyName, bool throwOnError)
        {
            if (throwOnError)
            {
                ExceptionHelper.ThrowTypeError(_engine, "Can't delete a property of a TypeReference");
            }

            return false;
        }

        public override void Put(string propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    ExceptionHelper.ThrowTypeError(Engine);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc == null)
            {
                if (throwOnError)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Unknown member: " + propertyName);
                }
                else
                {
                    return;
                }
            }

            ownDesc.Value = value;
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            // todo: cache members locally

            if (ReferenceType.IsEnum)
            {
                Array enumValues = Enum.GetValues(ReferenceType);
                Array enumNames = Enum.GetNames(ReferenceType);

                for (int i = 0; i < enumValues.Length; i++)
                {
                    if (enumNames.GetValue(i) as string == propertyName)
                    {
                        return new PropertyDescriptor((int) enumValues.GetValue(i), PropertyFlag.AllForbidden);
                    }
                }
                return PropertyDescriptor.Undefined;
            }

            var propertyInfo = ReferenceType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (propertyInfo != null)
            {
                return new PropertyInfoDescriptor(Engine, propertyInfo, Type);
            }

            var fieldInfo = ReferenceType.GetField(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (fieldInfo != null)
            {
                return new FieldInfoDescriptor(Engine, fieldInfo, Type);
            }

            List<MethodInfo> methodInfo = null;
            foreach (var mi in ReferenceType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (mi.Name == propertyName)
                {
                    methodInfo = methodInfo ?? new List<MethodInfo>();
                    methodInfo.Add(mi);
                }
            }

            if (methodInfo == null || methodInfo.Count == 0)
            {
                return PropertyDescriptor.Undefined;
            }

            return new PropertyDescriptor(new MethodInfoFunctionInstance(Engine, methodInfo.ToArray()), PropertyFlag.AllForbidden);
        }

        public object Target => ReferenceType;
    }
}
