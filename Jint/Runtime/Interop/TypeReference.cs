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

        private TypeReference(Engine engine, Type type)
            : base(engine, engine.Realm, _name, FunctionThisMode.Global, ObjectClass.TypeReference)
        {
            ReferenceType = type;

            _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;
            _prototypeDescriptor = new PropertyDescriptor(engine.Realm.Intrinsics.Object.PrototypeObject, PropertyFlag.AllForbidden);

            PreventExtensions();
        }

        public Type ReferenceType { get; }

        public static TypeReference CreateTypeReference(Engine engine, Type type)
        {
            return new TypeReference(engine, type);
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

            foreach (var tuple in TypeConverter.FindBestMatch(constructors, _ => arguments))
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
                var typeResolverMemberNameCreator = typeResolver.MemberNameCreator;

                var enumValues = Enum.GetValues(type);
                var enumNames = Enum.GetNames(type);

                for (var i = 0; i < enumValues.Length; i++)
                {
                    var enumOriginalName = enumNames.GetValue(i).ToString();
                    var member = type.GetMember(enumOriginalName)[0];
                    foreach (var exposedName in typeResolverMemberNameCreator(member))
                    {
                        if (memberNameComparer.Equals(name, exposedName))
                        {
                            var value = enumValues.GetValue(i);
                            return new ConstantValueAccessor(JsNumber.Create(value));
                        }
                    }
                }

                return ConstantValueAccessor.NullAccessor;
            }

            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static;
            return typeResolver.TryFindMemberAccessor(_engine, type, name, bindingFlags, indexerToTry: null, out var accessor)
                ? accessor
                : ConstantValueAccessor.NullAccessor;
        }

        public object Target => ReferenceType;
    }
}
