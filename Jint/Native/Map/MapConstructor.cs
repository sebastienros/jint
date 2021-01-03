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

        private MapConstructor(Engine engine)
            : base(engine, _functionName)
        {
        }

        public MapPrototype PrototypeObject { get; private set; }

        public static MapConstructor CreateMapConstructor(Engine engine)
        {
            var obj = new MapConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Map constructor is the Function prototype object
            obj.PrototypeObject = MapPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);

            // The initial value of Map.prototype is the Map prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

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
            if (thisObject.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Constructor Map requires 'new'");
            }

            return Construct(arguments, thisObject);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-map-iterable
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var map = OrdinaryCreateFromConstructor(newTarget, PrototypeObject, static (engine, _) => new MapInstance(engine));
            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var adder = map.Get("set");
                var iterator = arguments.At(0).GetIterator(_engine);

                IteratorProtocol.AddEntriesFromIterable(map, iterator, adder);
            }

            return map;
        }
    }
}