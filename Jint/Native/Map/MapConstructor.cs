using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Map
{
    public sealed class MapConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Map");

        internal MapConstructor(
            Engine engine,
            Realm realm,
            FunctionPrototype functionPrototype,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            _prototype = functionPrototype;
            PrototypeObject = new MapPrototype(engine, realm, this, objectPrototype);
            _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        }

        public MapPrototype PrototypeObject { get; private set; }

        protected override void Initialize()
        {
            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(_engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        private static JsValue Species(JsValue thisObject, JsValue[] arguments)
        {
            return thisObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Constructor Map requires 'new'");
            return null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-map-iterable
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var map = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Map.PrototypeObject,
                static (engine, realm, _) => new MapInstance(engine, realm));
            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var adder = map.Get("set");
                var iterator = arguments.At(0).GetIterator(_realm);

                IteratorProtocol.AddEntriesFromIterable(map, iterator, adder);
            }

            return map;
        }
    }
}