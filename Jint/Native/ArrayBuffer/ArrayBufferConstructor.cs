using Jint.Collections;
using Jint.Native.DataView;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.ArrayBuffer
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-the-arraybuffer-constructor
    /// </summary>
    internal sealed class ArrayBufferConstructor : Constructor
    {
        private static readonly JsString _functionName = new("ArrayBuffer");

        internal ArrayBufferConstructor(
            Engine engine,
            Realm realm,
            FunctionPrototype functionPrototype,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            _prototype = functionPrototype;
            PrototypeObject = new ArrayBufferPrototype(engine, this, objectPrototype);
            _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        }

        private ArrayBufferPrototype PrototypeObject { get; }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(1, checkExistingKeys: false)
            {
                ["isView"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunctionInstance(Engine, "isView", IsView, 1, lengthFlags), PropertyFlag.Configurable | PropertyFlag.Writable)),
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get [Symbol.species]", Species, 0, lengthFlags), set: Undefined,PropertyFlag.Configurable),
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-arraybuffer.isview
        /// </summary>
        private static JsValue IsView(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments.At(0);
            return arg is JsDataView or JsTypedArray;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-arraybuffer-@@species
        /// </summary>
        private static JsValue Species(JsValue thisObject, JsValue[] arguments)
        {
            return thisObject;
        }

        public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var byteLength = TypeConverter.ToIndex(_realm, arguments.At(0));
            return AllocateArrayBuffer(newTarget, byteLength);
        }

        internal JsArrayBuffer AllocateArrayBuffer(JsValue constructor, ulong byteLength)
        {
            var obj = OrdinaryCreateFromConstructor(
                constructor,
                static intrinsics => intrinsics.ArrayBuffer.PrototypeObject,
                static (engine, realm, state) => new JsArrayBuffer(engine, (ulong) state!._value),
                JsNumber.Create(byteLength));

            return obj;
        }
    }
}
