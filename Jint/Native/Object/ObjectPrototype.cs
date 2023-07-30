using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Proxy;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectPrototype : Prototype
    {
        private readonly ObjectConstructor _constructor;
        internal ObjectChangeFlags _objectChangeFlags;

        internal ObjectPrototype(
            Engine engine,
            Realm realm,
            ObjectConstructor constructor) : base(engine, realm)
        {
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(12, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, propertyFlags),
                ["__defineGetter__"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "__defineGetter__", DefineGetter, 2, lengthFlags), propertyFlags),
                ["__defineSetter__"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "__defineSetter__", DefineSetter, 2, lengthFlags), propertyFlags),
                ["__lookupGetter__"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "__lookupGetter__", LookupGetter, 1, lengthFlags), propertyFlags),
                ["__lookupSetter__"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "__lookupSetter__", LookupSetter, 1, lengthFlags), propertyFlags),
                ["__proto__"] = new GetSetPropertyDescriptor(
                    new ClrFunctionInstance(Engine, "get __proto__", (thisObject, _) => TypeConverter.ToObject(_realm, thisObject).GetPrototypeOf() ?? Null, 0, lengthFlags),
                    new ClrFunctionInstance(Engine, "set __proto__", (thisObject, arguments) =>
                    {
                        TypeConverter.CheckObjectCoercible(_engine, thisObject);

                        var proto = arguments.At(0);
                        if (!proto.IsObject() && !proto.IsNull() || thisObject is not ObjectInstance objectInstance)
                        {
                            return Undefined;
                        }

                        if (!objectInstance.SetPrototypeOf(proto))
                        {
                            ExceptionHelper.ThrowTypeError(_realm, "Invalid prototype");
                        }

                        return Undefined;
                    }, 0, lengthFlags),
                    enumerable: false, configurable: true),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToObjectString, 0, lengthFlags), propertyFlags),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, lengthFlags), propertyFlags),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, lengthFlags), propertyFlags),
                ["hasOwnProperty"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "hasOwnProperty", HasOwnProperty, 1, lengthFlags), propertyFlags),
                ["isPrototypeOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "isPrototypeOf", IsPrototypeOf, 1, lengthFlags), propertyFlags),
                ["propertyIsEnumerable"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "propertyIsEnumerable", PropertyIsEnumerable, 1, lengthFlags), propertyFlags)
            };
            SetProperties(properties);
        }

        public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            TrackChanges(property);
            return base.DefineOwnProperty(property, desc);
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            TrackChanges(property);
            base.SetOwnProperty(property, desc);
        }

        private void TrackChanges(JsValue property)
        {
            EnsureInitialized();
            if (ArrayInstance.IsArrayIndex(property, out _))
            {
                _objectChangeFlags |= ObjectChangeFlags.ArrayIndex;
            }
            else
            {
                _objectChangeFlags |= property.IsSymbol() ? ObjectChangeFlags.Symbol : ObjectChangeFlags.Property;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.__defineGetter__
        /// </summary>
        private JsValue DefineGetter(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            var p = arguments.At(0);
            var getter = arguments.At(1);

            if (!getter.IsCallable)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Target is not callable");
            }

            var desc = new GetSetPropertyDescriptor(getter, null, enumerable: true, configurable: true);
            var key = TypeConverter.ToPropertyKey(p);
            o.DefinePropertyOrThrow(key, desc);

            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.__defineSetter__
        /// </summary>
        private JsValue DefineSetter(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            var p = arguments.At(0);
            var setter = arguments.At(1);

            if (!setter.IsCallable)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Target is not callable");
            }

            var desc = new GetSetPropertyDescriptor(null, setter, enumerable: true, configurable: true);
            var key = TypeConverter.ToPropertyKey(p);
            o.DefinePropertyOrThrow(key, desc);

            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.__lookupGetter__
        /// </summary>
        private JsValue LookupGetter(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            var key = TypeConverter.ToPropertyKey(arguments.At(0));
            while (true)
            {
                var desc = o.GetOwnProperty(key);
                if (!ReferenceEquals(desc, PropertyDescriptor.Undefined))
                {
                    if (desc.IsAccessorDescriptor())
                    {
                        return desc.Get ?? Undefined;
                    }

                    return Undefined;
                }

                o = o.GetPrototypeOf();
                if (o is null)
                {
                    return Undefined;
                }
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.__lookupSetter__
        /// </summary>
        private JsValue LookupSetter(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            var key = TypeConverter.ToPropertyKey(arguments.At(0));
            while (true)
            {
                var desc = o.GetOwnProperty(key);
                if (!ReferenceEquals(desc, PropertyDescriptor.Undefined))
                {
                    if (desc.IsAccessorDescriptor())
                    {
                        return desc.Set ?? Undefined;
                    }

                    return Undefined;
                }

                o = o.GetPrototypeOf();
                if (o is null)
                {
                    return Undefined;
                }
            }
        }

        private JsValue PropertyIsEnumerable(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToPropertyKey(arguments[0]);
            var o = TypeConverter.ToObject(_realm, thisObject);
            var desc = o.GetOwnProperty(p);
            if (desc == PropertyDescriptor.Undefined)
            {
                return JsBoolean.False;
            }
            return desc.Enumerable;
        }

        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            return o;
        }

        private JsValue IsPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments[0];
            if (!arg.IsObject())
            {
                return JsBoolean.False;
            }

            var v = arg.AsObject();

            var o = TypeConverter.ToObject(_realm, thisObject);
            while (true)
            {
                v = v.Prototype;

                if (ReferenceEquals(v, null))
                {
                    return JsBoolean.False;
                }

                if (ReferenceEquals(o, v))
                {
                    return JsBoolean.True;
                }
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.tolocalestring
        /// </summary>
        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            return Invoke(thisObject, "toString", System.Array.Empty<JsValue>());
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object.prototype.tostring
        /// </summary>
        internal JsValue ToObjectString(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                return "[object Undefined]";
            }

            if (thisObject.IsNull())
            {
                return "[object Null]";
            }

            var o = TypeConverter.ToObject(_realm, thisObject);
            var isArray = o.IsArray();

            var tag = o.Get(GlobalSymbolRegistry.ToStringTag);
            if (!tag.IsString())
            {
                if (isArray)
                {
                    tag = "Array";
                }
                else if (o.IsCallable)
                {
                    tag = "Function";
                }
                else
                {
                    tag = (o is JsProxy ? ObjectClass.Object : o.Class).ToString();
                }
            }

            return "[object " + tag + "]";
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.5
        /// </summary>
        private JsValue HasOwnProperty(JsValue thisObject, JsValue[] arguments)
        {
            var p = TypeConverter.ToPropertyKey(arguments[0]);
            var o = TypeConverter.ToObject(_realm, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }
    }
}
