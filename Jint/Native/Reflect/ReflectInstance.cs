using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Reflect
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/index.html#sec-reflect-object
    /// </summary>
    public sealed class ReflectInstance : ObjectInstance
    {
        private readonly Realm _realm;

        internal ReflectInstance(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype) : base(engine, ObjectClass.Reflect)
        {
            _realm = realm;
            _prototype = objectPrototype;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(14, checkExistingKeys: false)
            {
                ["apply"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "apply", Apply, 3, PropertyFlag.Configurable), true, false, true),
                ["construct"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "construct", Construct, 2, PropertyFlag.Configurable), true, false, true),
                ["defineProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "defineProperty", DefineProperty, 3, PropertyFlag.Configurable), true, false, true),
                ["deleteProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "deleteProperty", DeleteProperty, 2, PropertyFlag.Configurable), true, false, true),
                ["get"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "get", Get, 2, PropertyFlag.Configurable), true, false, true),
                ["getOwnPropertyDescriptor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getOwnPropertyDescriptor", GetOwnPropertyDescriptor, 2, PropertyFlag.Configurable), true, false, true),
                ["getPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getPrototypeOf", GetPrototypeOf, 1, PropertyFlag.Configurable), true, false, true),
                ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 2, PropertyFlag.Configurable), true, false, true),
                ["isExtensible"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isExtensible", IsExtensible, 1, PropertyFlag.Configurable), true, false, true),
                ["ownKeys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "ownKeys", OwnKeys, 1, PropertyFlag.Configurable), true, false, true),
                ["preventExtensions"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "preventExtensions", PreventExtensions, 1, PropertyFlag.Configurable), true, false, true),
                ["set"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "set", Set, 3, PropertyFlag.Configurable), true, false, true),
                ["setPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setPrototypeOf", SetPrototypeOf, 2, PropertyFlag.Configurable), true, false, true),
            };
            SetProperties(properties);
        }

        private JsValue Apply(JsValue thisObject, JsValue[] arguments)
        {
            return _realm.Intrinsics.Function.PrototypeObject.Apply(arguments.At(0), new[]
            {
                arguments.At(1),
                arguments.At(2)
            });
        }

        private JsValue Construct(JsValue thisObject, JsValue[] arguments)
        {
            var targetArgument = arguments.At(0);
            var target = AssertConstructor(_engine, targetArgument);

            var newTargetArgument = arguments.At(2, arguments[0]);
            AssertConstructor(_engine, newTargetArgument);

            var args = _realm.Intrinsics.Function.PrototypeObject.CreateListFromArrayLike(arguments.At(1));

            return target.Construct(args, newTargetArgument);
        }

        private JsValue DefineProperty(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.defineProperty called on non-object");
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToPropertyKey(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(_realm, attributes);

            return o.DefineOwnProperty(name, desc);
        }

        private JsValue DeleteProperty(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.deleteProperty called on non-object");
            }

            var property = TypeConverter.ToPropertyKey(arguments.At(1));
            return o.Delete(property) ? JsBoolean.True : JsBoolean.False;
        }

        private JsValue Has(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.has called on non-object");
            }

            var property = TypeConverter.ToPropertyKey(arguments.At(1));
            return o.HasProperty(property) ? JsBoolean.True : JsBoolean.False;
        }

        private JsValue Set(JsValue thisObject, JsValue[] arguments)
        {
            var target = arguments.At(0);
            var property = TypeConverter.ToPropertyKey(arguments.At(1));
            var value = arguments.At(2);
            var receiver = arguments.At(3, target);

            var o = target as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.set called on non-object");
            }

            return o.Set(property, value, receiver);
        }

        private JsValue Get(JsValue thisObject, JsValue[] arguments)
        {
            var target = arguments.At(0);
            var o = target as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.get called on non-object");
            }

            var receiver = arguments.At(2, target);
            var property = TypeConverter.ToPropertyKey(arguments.At(1));
            return o.Get(property, receiver);
        }

        private JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue[] arguments)
        {
            if (!arguments.At(0).IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.getOwnPropertyDescriptor called on non-object");
            }
            return _realm.Intrinsics.Object.GetOwnPropertyDescriptor(Undefined, arguments);
        }

        private JsValue OwnKeys(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.get called on non-object");
            }

            var keys = o.GetOwnPropertyKeys();
            return _realm.Intrinsics.Array.CreateArrayFromList(keys);
        }

        private JsValue IsExtensible(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.isExtensible called on non-object");
            }

            return o.Extensible;
        }

        private JsValue PreventExtensions(JsValue thisObject, JsValue[] arguments)
        {
            var o = arguments.At(0) as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.preventExtensions called on non-object");
            }

            return o.PreventExtensions();
        }

        private JsValue GetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var target = arguments.At(0);

            if (!target.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.getPrototypeOf called on non-object");
            }

            return _realm.Intrinsics.Object.GetPrototypeOf(Undefined, arguments);
        }

        private JsValue SetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var target = arguments.At(0);

            var o = target as ObjectInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Reflect.setPrototypeOf called on non-object");
            }

            var prototype = arguments.At(1);
            if (!prototype.IsObject() && !prototype.IsNull())
            {
                ExceptionHelper.ThrowTypeError(_realm, $"Object prototype may only be an Object or null: {prototype}");
            }

            return o.SetPrototypeOf(prototype);
        }
    }
}