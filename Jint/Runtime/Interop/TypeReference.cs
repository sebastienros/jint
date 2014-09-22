using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop
{
    public class TypeReference : FunctionInstance, IConstructor, IObjectWrapper
    {
        private TypeReference(Engine engine)
            : base(engine, null, null, false)
        {
        }

        public Type Type { get; set; }

        public static TypeReference CreateTypeReference(Engine engine, Type type)
        {
            var obj = new TypeReference(engine);
            obj.Extensible = false;
            obj.Type = type;

            // The value of the [[Prototype]] internal property of the TypeReference constructor is the Function prototype object 
            obj.Prototype = engine.Function.PrototypeObject;

            obj.FastAddProperty("length", 0, false, false, false);

            // The initial value of Boolean.prototype is the Boolean prototype object
            obj.FastAddProperty("prototype", engine.Object.PrototypeObject, false, false, false);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            // direct calls on a TypeReference constructor object is equivalent to the new operator 
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            var constructors = Type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            
            var methods = TypeConverter.FindBestMatch(Engine, constructors, arguments).ToList();

            foreach (var method in methods)
            {
                var parameters = new object[arguments.Length];
                try
                {
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        var parameterType =  method.GetParameters()[i].ParameterType;

                        if (parameterType == typeof(JsValue))
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

                    var constructor = (ConstructorInfo)method;
                    var result = TypeConverter.ToObject(Engine, JsValue.FromObject(Engine, constructor.Invoke(parameters.ToArray())));

                    // todo: cache method info

                    return result;
                }
                catch
                {
                    // ignore method
                }
            }

            throw new JavaScriptException(Engine.TypeError, "No public methods with the specified arguments were found.");
            
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            if (throwOnError)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't define a property of a TypeReference");
            }

            return false;
        }

        public override bool Delete(string propertyName, bool throwOnError)
        {
            if (throwOnError)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't delete a property of a TypeReference");
            }

            return false;
        }

        public override void Put(string propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc == null)
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError, "Unknown member: " + propertyName);
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

            if (Type.IsEnum)
            {
                Array enumValues = Enum.GetValues(Type);
                Array enumNames = Enum.GetNames(Type);

                for (int i = 0; i < enumValues.Length; i++)
                {
                    if (enumNames.GetValue(i) as string == propertyName)
                    {
                        return new PropertyDescriptor((int)enumValues.GetValue(i), false, false, false);
                    }
                }
                return PropertyDescriptor.Undefined;
            }

            var propertyInfo = Type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (propertyInfo != null)
            {
                return new PropertyInfoDescriptor(Engine, propertyInfo, Type);
            }

            var fieldInfo = Type.GetField(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (fieldInfo != null)
            {
                return new FieldInfoDescriptor(Engine, fieldInfo, Type);
            }

            var methodInfo = Type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(mi => mi.Name == propertyName)
                .ToArray();

            if (methodInfo.Length == 0)
            {
                return PropertyDescriptor.Undefined;
            }

            return new PropertyDescriptor(new MethodInfoFunctionInstance(Engine, methodInfo), false, false, false);
        }

        public object Target
        {
            get
            {
                return Type;
            } 
        }

        public override string Class
        {
            get { return "TypeReference"; }
        }
    }
}
