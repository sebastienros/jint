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
        private static readonly JsString _name = new("typereference");
        private static readonly ConcurrentDictionary<Type, MethodDescriptor[]> _constructorCache = new();
        private static readonly ConcurrentDictionary<MemberAccessorKey, ReflectionAccessor> _memberAccessors = new();

        private readonly record struct MemberAccessorKey(Type Type, string PropertyName);

        private TypeReference(Engine engine, Type type)
            : base(engine, engine.Realm, _name, FunctionThisMode.Global)
        {
            ReferenceType = type;

            _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
            _length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;

            var proto = new ObjectInstance(engine);
            _prototypeDescriptor = new PropertyDescriptor(proto, PropertyFlag.AllForbidden);

            PreventExtensions();
        }

        public Type ReferenceType { get; }

        public static TypeReference CreateTypeReference<T>(Engine engine)
        {
            return CreateTypeReference(engine, typeof(T));
        }

        public static TypeReference CreateTypeReference(Engine engine, Type type)
        {
            return new TypeReference(engine, type);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            // direct calls on a TypeReference constructor object is equivalent to the new operator
            return Construct(arguments, Undefined);
        }

        ObjectInstance IConstructor.Construct(JsValue[] arguments, JsValue newTarget) => Construct(arguments, newTarget);

        private ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            static ObjectInstance ObjectCreator(Engine engine, Realm realm, ObjectCreateState state)
            {
                var arguments = state.Arguments;
                var referenceType = state.TypeReference.ReferenceType;

                ObjectInstance? result = null;
                if (arguments.Length == 0 && referenceType.IsValueType)
                {
                    var instance = Activator.CreateInstance(referenceType);
                    result = TypeConverter.ToObject(realm, FromObject(engine, instance));
                }
                else
                {
                    var constructors = _constructorCache.GetOrAdd(
                        referenceType,
                        t => MethodDescriptor.Build(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));

                    foreach (var (method, _, _) in TypeConverter.FindBestMatch(engine, constructors, _ => arguments))
                    {
                        var retVal = method.Call(engine, null, arguments);
                        result = TypeConverter.ToObject(realm, retVal);

                        // todo: cache method info
                        break;
                    }
                }

                if (result is null)
                {
                    ExceptionHelper.ThrowTypeError(realm, "No public methods with the specified arguments were found.");
                }

                result.SetPrototypeOf(state.TypeReference);

                return result;
            }

            // TODO should inject prototype that reflects TypeReference's target's layout
            var thisArgument = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Object.PrototypeObject,
                ObjectCreator,
                new ObjectCreateState(this, arguments));


            return thisArgument;
        }

        private readonly record struct ObjectCreateState(TypeReference TypeReference, JsValue[] Arguments);

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
                if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
                {
                    _properties ??= new PropertyDictionary();
                    _properties[key] = descriptor;
                    return descriptor;
                }
            }

            return base.GetOwnProperty(property);
        }

        private PropertyDescriptor CreatePropertyDescriptor(string name)
        {
            var key = new MemberAccessorKey(ReferenceType, name);
            var accessor = _memberAccessors.GetOrAdd(key, x => ResolveMemberAccessor(_engine, x.Type, x.PropertyName));
            return accessor.CreatePropertyDescriptor(_engine, ReferenceType, enumerable: true);
        }

        private static ReflectionAccessor ResolveMemberAccessor(Engine engine, Type type, string name)
        {
            var typeResolver = engine.Options.Interop.TypeResolver;

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

            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            return typeResolver.TryFindMemberAccessor(engine, type, name, bindingFlags, indexerToTry: null, out var accessor)
                ? accessor
                : ConstantValueAccessor.NullAccessor;
        }

        public object Target => ReferenceType;
    }
}
