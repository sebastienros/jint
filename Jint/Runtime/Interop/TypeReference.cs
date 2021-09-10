using System;
using System.Collections.Concurrent;
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

        private TypeReference(
            Engine engine,
            Realm realm)
            : base(engine, realm, _name, FunctionThisMode.Global, ObjectClass.TypeReference)
        {
        }

        public Type ReferenceType { get; private set; }

        public static TypeReference CreateTypeReference(Engine engine, Type type)
        {
            var obj = new TypeReference(engine, engine.Realm);
            obj.PreventExtensions();
            obj.ReferenceType = type;

            // The value of the [[Prototype]] internal property of the TypeReference constructor is the Function prototype object
            obj._prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
            obj._length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;

            // The initial value of Boolean.prototype is the Boolean prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(engine.Realm.Intrinsics.Object.PrototypeObject, PropertyFlag.AllForbidden);

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
                var result = TypeConverter.ToObject(_realm, FromObject(Engine, instance));

                return result;
            }

            var constructors = _constructorCache.GetOrAdd(
                ReferenceType,
                t => MethodDescriptor.Build(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));

            foreach (var tuple in TypeConverter.FindBestMatch(_engine, constructors, _ => arguments))
            {
                var method = tuple.Item1;
                var retVal = method.Call(Engine, null, arguments);
                var result = TypeConverter.ToObject(_realm, retVal);

                // todo: cache method info

                return result;
            }

            ExceptionHelper.ThrowTypeError(_engine.Realm, "No public methods with the specified arguments were found.");
            return null;
        }

        internal override bool OrdinaryHasInstance(JsValue v)
        {
            if (v is IObjectWrapper wrapper)
            {
                return wrapper.Target.GetType() == ReferenceType;
            }

            return base.OrdinaryHasInstance(v);
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
            var key = new Tuple<Type, string>(ReferenceType, name);
            var accessor = _memberAccessors.GetOrAdd(key, x => ResolveMemberAccessor(x.Item1, x.Item2));
            return accessor.CreatePropertyDescriptor(_engine, ReferenceType);
        }

        private ReflectionAccessor ResolveMemberAccessor(Type type, string name)
        {
            var typeResolver = _engine.Options.Interop.TypeResolver;

            if (type.IsEnum)
            {
                var memberNameComparer = typeResolver.MemberNameComparer;

                var enumValues = Enum.GetValues(type);
                var enumNames = Enum.GetNames(type);

                for (var i = 0; i < enumValues.Length; i++)
                {
                    if (memberNameComparer.Equals(enumNames.GetValue(i), name))
                    {
                        var value = enumValues.GetValue(i);
                        return new ConstantValueAccessor(JsNumber.Create(value));
                    }
                }

                return ConstantValueAccessor.NullAccessor;
            }

            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static;
            return typeResolver.TryFindMemberAccessor(type, name, bindingFlags, indexerToTry: null, out var accessor)
                ? accessor
                : ConstantValueAccessor.NullAccessor;
        }

        public object Target => ReferenceType;
    }
}
