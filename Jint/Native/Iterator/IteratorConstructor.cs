using System.Collections.Generic;
using System.Linq;
using Jint.Native.Function;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Native.Set;

namespace Jint.Native.Iterator
{
    public sealed class IteratorConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("iterator");

        private IteratorConstructor(Engine engine)
            : base(engine, _functionName)
        {
            ArrayIteratorPrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, "Array Iterator", this);
            GenericIteratorPrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, null, this);
            MapIteratorPrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, "Map Iterator", this);
            RegExpStringIteratorPrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, "RegExp String Iterator", this);
            SetIteratorPrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, "Set Iterator", this);
            StringIteratorPrototypeObject = IteratorPrototype.CreatePrototypeObject(engine, "String Iterator", this);
        }

        private IteratorPrototype ArrayIteratorPrototypeObject { get; }
        private IteratorPrototype GenericIteratorPrototypeObject { get; }
        private IteratorPrototype MapIteratorPrototypeObject { get; }
        private IteratorPrototype RegExpStringIteratorPrototypeObject { get; }
        private IteratorPrototype SetIteratorPrototypeObject { get; }
        private IteratorPrototype StringIteratorPrototypeObject { get; }

        public static IteratorConstructor CreateIteratorConstructor(Engine engine)
        {
            return new IteratorConstructor(engine);
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
                _prototype = GenericIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(List<JsValue> enumerable)
        {
            var instance = new IteratorInstance.ListIterator(Engine, enumerable)
            {
                _prototype = GenericIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(ObjectInstance array)
        {
            var instance = new IteratorInstance.ArrayLikeIterator(Engine, array)
            {
                _prototype = GenericIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(MapInstance map)
        {
            var instance = new IteratorInstance.MapIterator(Engine, map)
            {
                _prototype = MapIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance Construct(SetInstance set)
        {
            var instance = new IteratorInstance.SetIterator(Engine, set)
            {
                _prototype = SetIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance ConstructEntryIterator(SetInstance set)
        {
            var instance = new IteratorInstance.SetEntryIterator(Engine, set)
            {
                _prototype = GenericIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance ConstructArrayLikeKeyIterator(ObjectInstance array)
        {
            var instance = new IteratorInstance.ArrayLikeKeyIterator(Engine, array)
            {
                _prototype = ArrayIteratorPrototypeObject
            };

            return instance;
        }

        internal ObjectInstance ConstructArrayLikeValueIterator(ObjectInstance array)
        {
            var instance = new IteratorInstance.ArrayLikeValueIterator(Engine, array)
            {
                _prototype = ArrayIteratorPrototypeObject
            };

            return instance;
        }
       
        internal ObjectInstance CreateRegExpStringIterator(ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode)
        {
            var instance = new IteratorInstance.RegExpStringIterator(Engine, iteratingRegExp, iteratedString, global, unicode)
            {
                _prototype = RegExpStringIteratorPrototypeObject
            };

            return instance;
        }

        public ObjectInstance Construct(string str)
        {
            var instance = new IteratorInstance.StringIterator(Engine, str)
            {
                _prototype = StringIteratorPrototypeObject
            };

            return instance;
        }
    }
}