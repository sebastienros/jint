using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop
{
    public sealed class TypeReference : FunctionInstance, IConstructor, IObjectWrapper
    {
        private static readonly JsString _name = new JsString("typereference");
        private static readonly ConcurrentDictionary<Type, MethodDescriptor[]> _constructorCache = new();
        private static readonly ConcurrentDictionary<Tuple<Type, string>, ReflectionAccessor> _memberAccessors = new();

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

            var constructors = _constructorCache.GetOrAdd(
                ReferenceType,
                t => MethodDescriptor.Build(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));

            foreach (var tuple in TypeConverter.FindBestMatch(_engine, constructors, _ => arguments))
            {
                var method = tuple.Item1;

                var parameters = new object[arguments.Length];
                var methodParameters = method.Parameters;
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

                    var constructor = (ConstructorInfo) method.Method;
                    var instance = constructor.Invoke(parameters);
                    var result = TypeConverter.ToObject(Engine, FromObject(Engine, instance));

                    // todo: cache method info

                    return result;
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
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
            if (property is not JsString jsString)
            {
                return PropertyDescriptor.Undefined;
            }
            
            var key = jsString._value;
            var descriptor = PropertyDescriptor.Undefined;

            if (_properties?.TryGetValue(key, out descriptor) != true)
            {
                descriptor = CreatePropertyDescriptor(key);
                _properties ??= new PropertyDictionary();
                _properties[key] = descriptor;
            }

            return descriptor;
        }

        private PropertyDescriptor CreatePropertyDescriptor(string name)
        {
            var accessor = _memberAccessors.GetOrAdd(
                new Tuple<Type, string>(ReferenceType, name),
                key => ResolveMemberAccessor(key.Item1, key.Item2)
            );
            return accessor.CreatePropertyDescriptor(_engine, ReferenceType);
        }

        private static ReflectionAccessor ResolveMemberAccessor(Type type, string name)
        {
            if (type.IsEnum)
            {
                var enumValues = Enum.GetValues(type);
                var enumNames = Enum.GetNames(type);

                for (var i = 0; i < enumValues.Length; i++)
                {
                    if (enumNames.GetValue(i) as string == name)
                    {
                        var value = enumValues.GetValue(i);
                        return new ConstantValueAccessor(JsNumber.Create(value));
                    }
                }

                return ConstantValueAccessor.NullAccessor;
            }

            var propertyInfo = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (propertyInfo != null)
            {
                return new PropertyAccessor(name, propertyInfo);
            }

            var fieldInfo = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (fieldInfo != null)
            {
                return new FieldAccessor(fieldInfo, name);
            }

            List<MethodInfo> methods = null;
            foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (mi.Name != name)
                {
                    continue;
                }

                methods ??= new List<MethodInfo>();
                methods.Add(mi);
            }

            if (methods == null || methods.Count == 0)
            {
                return ConstantValueAccessor.NullAccessor;
            }

            return new MethodAccessor(MethodDescriptor.Build(methods));
        }

        public object Target => ReferenceType;
    }
}
