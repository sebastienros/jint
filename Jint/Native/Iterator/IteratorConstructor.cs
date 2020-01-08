using System.Collections.Generic;
using System.Linq;
using Jint.Native.Function;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Native.Set;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator
{
    public sealed class IteratorConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("iterator");

        private IteratorConstructor(Engine engine)
            : base(engine, _functionName, strict: false)
        {
        }

        internal IteratorPrototype PrototypeObject { get; private set; }

        public static IteratorConstructor CreateIteratorConstructor(Engine engine)
        {
            var obj = new IteratorConstructor(engine);

            // The value of the [[Prototype]] internal property of the Map constructor is the Function prototype object
            obj._prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);

            // The initial value of Map.prototype is the Map prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }


        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            return Construct(Enumerable.Empty<JsValue>());
        }

        internal ObjectInstance Construct(IEnumerable<JsValue> enumerable)
        {
            var instance = new IteratorInstance(Engine, enumerable)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(List<JsValue> enumerable)
        {
            var instance = new IteratorInstance.ListIterator(Engine, enumerable)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(ObjectInstance array)
        {
            var instance = new IteratorInstance.ArrayLikeIterator(Engine, array)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(MapInstance map)
        {
            var instance = new IteratorInstance.MapIterator(Engine, map)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(SetInstance set)
        {
            var instance = new IteratorInstance.SetIterator(Engine, set)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance ConstructEntryIterator(SetInstance set)
        {
            var instance = new IteratorInstance.SetEntryIterator(Engine, set)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance ConstructArrayLikeKeyIterator(ObjectInstance array)
        {
            var instance = new IteratorInstance.ArrayLikeKeyIterator(Engine, array)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        internal ObjectInstance ConstructArrayLikeValueIterator(ObjectInstance array)
        {
            var instance = new IteratorInstance.ArrayLikeValueIterator(Engine, array)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }

        public ObjectInstance Construct(string str)
        {
            var instance = new IteratorInstance.StringIterator(Engine, str)
            {
                _prototype = PrototypeObject
            };

            return instance;
        }
    }
}