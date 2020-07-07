using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop
{
    public sealed class TypeReference : FunctionInstance, IConstructor, IObjectWrapper
    {
        private static readonly JsString _name = new JsString("typereference");

        private TypeReference(Engine engine)
            : base(engine, _name, FunctionThisMode.Global, ObjectClass.TypeReference)
        {
        }

        public Type ReferenceType { get; set; }

        public static TypeReference CreateTypeReference(Engine engine, Type type)
        {
            var obj = new TypeReference(engine);
            obj.PreventExtensions();
            obj.ReferenceType = type;

            // The value of the [[Prototype]] internal property of the TypeReference constructor is the Function prototype object
            obj._prototype = engine.Function.PrototypeObject;
            obj._length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;

            // The initial value of Boolean.prototype is the Boolean prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(engine.Object.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            // direct calls on a TypeReference constructor object is equivalent to the new operator
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (arguments.Length == 0 && ReferenceType.IsValueType)
            {
                var instance = Activator.CreateInstance(ReferenceType);
                var result = TypeConverter.ToObject(Engine, FromObject(Engine, instance));

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

            return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine, "No public methods with the specified arguments were found.");
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

        public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            return false;
        }

        public override bool Delete(JsValue property)
        {
            return false;
        }

        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            if (!CanPut(property))
            {
                return false;
            }

            var ownDesc = GetOwnProperty(property);

            if (ownDesc == null)
            {
                return false;
            }

            ownDesc.Value = value;
            return true;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            // todo: cache members locally
            var name = property.ToString();
            if (ReferenceType.IsEnum)
            {
                Array enumValues = Enum.GetValues(ReferenceType);
                Array enumNames = Enum.GetNames(ReferenceType);

                for (int i = 0; i < enumValues.Length; i++)
                {
                    if (enumNames.GetValue(i) as string == name)
                    {
                        return new PropertyDescriptor((int) enumValues.GetValue(i), PropertyFlag.AllForbidden);
                    }
                }
                return PropertyDescriptor.Undefined;
            }

            var propertyInfo = ReferenceType.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (propertyInfo != null)
            {
                return new PropertyInfoDescriptor(Engine, propertyInfo, Type);
            }

            var fieldInfo = ReferenceType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (fieldInfo != null)
            {
                return new FieldInfoDescriptor(Engine, fieldInfo, Type);
            }

            List<MethodInfo> methodInfo = null;
            foreach (var mi in ReferenceType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (mi.Name == name)
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

        public override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments) => Task.FromResult(Call(thisObject, arguments));
    }
}
