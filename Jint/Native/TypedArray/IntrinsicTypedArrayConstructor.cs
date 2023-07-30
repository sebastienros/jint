using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.TypedArray
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%-intrinsic-object
    /// </summary>
    internal sealed class IntrinsicTypedArrayConstructor : Constructor
    {
        internal IntrinsicTypedArrayConstructor(
            Engine engine,
            Realm realm,
            ObjectInstance functionPrototype,
            ObjectInstance objectPrototype,
            string functionName) : base(engine, realm, new JsString(functionName))
        {
            _prototype = functionPrototype;
            PrototypeObject = new IntrinsicTypedArrayPrototype(engine, objectPrototype, this);
            _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        }

        public IntrinsicTypedArrayPrototype PrototypeObject { get; }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, false)
            {
                ["from"] = new(new PropertyDescriptor(new ClrFunctionInstance(Engine, "from", From, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)),
                ["of"] = new(new PropertyDescriptor(new ClrFunctionInstance(Engine, "of", Of, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable))
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.from
        /// </summary>
        private JsValue From(JsValue thisObject, JsValue[] arguments)
        {
            var c = thisObject;
            if (!c.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var source = arguments.At(0);
            var mapFunction = arguments.At(1);
            var thisArg = arguments.At(2);

            var mapping = !mapFunction.IsUndefined();
            if (mapping)
            {
                if (!mapFunction.IsCallable)
                {
                    ExceptionHelper.ThrowTypeError(_realm);
                }
            }

            var usingIterator = GetMethod(_realm, source, GlobalSymbolRegistry.Iterator);
            if (usingIterator is not null)
            {
                var values = TypedArrayConstructor.IterableToList(_realm, source, usingIterator);
                var iteratorLen = values.Count;
                var iteratorTarget = TypedArrayCreate(_realm, (IConstructor) c, new JsValue[] { iteratorLen });
                for (var k = 0; k < iteratorLen; ++k)
                {
                    var kValue = values[k];
                    var mappedValue = mapping
                        ? ((ICallable) mapFunction).Call(thisArg, new[] { kValue, k })
                        : kValue;
                    iteratorTarget[k] = mappedValue;
                }

                return iteratorTarget;
            }

            if (source.IsNullOrUndefined())
            {
                ExceptionHelper.ThrowTypeError(_realm, "Cannot convert undefined or null to object");
            }

            var arrayLike = TypeConverter.ToObject(_realm, source);
            var len = arrayLike.Length;

            var argumentList = new JsValue[] { JsNumber.Create(len) };
            var targetObj = TypedArrayCreate(_realm, (IConstructor) c, argumentList);

            var mappingArgs = mapping ? new JsValue[2] : null;
            for (uint k = 0; k < len; ++k)
            {
                var Pk = JsNumber.Create(k);
                var kValue = arrayLike.Get(Pk);
                JsValue mappedValue;
                if (mapping)
                {
                    mappingArgs![0] = kValue;
                    mappingArgs[1] = Pk;
                    mappedValue = ((ICallable) mapFunction).Call(thisArg, mappingArgs);
                }
                else
                {
                    mappedValue = kValue;
                }

                targetObj.Set(Pk, mappedValue, true);
            }

            return targetObj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.of
        /// </summary>
        private JsValue Of(JsValue thisObject, JsValue[] items)
        {
            var len = items.Length;

            if (!thisObject.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var newObj = TypedArrayCreate(_realm, (IConstructor) thisObject, new JsValue[] { len });

            var k = 0;
            while (k < len)
            {
                var kValue = items[k];
                newObj[k] = kValue;
                k++;
            }

            return newObj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#typedarray-species-create
        /// </summary>
        internal JsTypedArray TypedArraySpeciesCreate(JsTypedArray exemplar, JsValue[] argumentList)
        {
            var defaultConstructor = exemplar._arrayElementType.GetConstructor(_realm.Intrinsics)!;
            var constructor = SpeciesConstructor(exemplar, defaultConstructor);
            var result = TypedArrayCreate(_realm, constructor, argumentList);
            if (result._contentType != exemplar._contentType)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return result;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#typedarray-create
        /// </summary>
        internal static JsTypedArray TypedArrayCreate(Realm realm, IConstructor constructor, JsValue[] argumentList)
        {
            var newTypedArray = Construct(constructor, argumentList).ValidateTypedArray(realm);
            if (argumentList.Length == 1 && argumentList[0] is JsNumber number)
            {
                if (newTypedArray.Length < number._value)
                {
                    ExceptionHelper.ThrowTypeError(realm);
                }
            }

            return newTypedArray;
        }

        private static JsValue Species(JsValue thisObject, JsValue[] arguments)
        {
            return thisObject;
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Abstract class TypedArray not directly callable");
            return Undefined;
        }

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Abstract class TypedArray not directly constructable");
            return null;
        }
    }
}
