using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native.Function;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Native.Set;
using Jint.Runtime;

namespace Jint.Native.Iterator
{
    public sealed class IteratorConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("iterator");

        internal IteratorConstructor(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            ArrayIteratorPrototypeObject = new IteratorPrototype(engine, realm, "Array Iterator", objectPrototype);
            GenericIteratorPrototypeObject = new IteratorPrototype(engine, realm, null, objectPrototype);
            MapIteratorPrototypeObject = new IteratorPrototype(engine, realm, "Map Iterator", objectPrototype);
            RegExpStringIteratorPrototypeObject = new IteratorPrototype(engine, realm, "RegExp String Iterator", objectPrototype);
            SetIteratorPrototypeObject = new IteratorPrototype(engine, realm, "Set Iterator", objectPrototype);
            StringIteratorPrototypeObject = new IteratorPrototype(engine, realm, "String Iterator", objectPrototype);
        }

        internal IteratorPrototype ArrayIteratorPrototypeObject { get; }
        internal IteratorPrototype GenericIteratorPrototypeObject { get; }
        private IteratorPrototype MapIteratorPrototypeObject { get; }
        private IteratorPrototype RegExpStringIteratorPrototypeObject { get; }
        private IteratorPrototype SetIteratorPrototypeObject { get; }
        private IteratorPrototype StringIteratorPrototypeObject { get; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            return Construct(Enumerable.Empty<JsValue>());
        }

        internal IteratorInstance Construct(IEnumerable<JsValue> enumerable)
        {
            var instance = new IteratorInstance(Engine, enumerable)
            {
                _prototype = GenericIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance Construct(List<JsValue> enumerable)
        {
            var instance = new IteratorInstance.ListIterator(Engine, enumerable)
            {
                _prototype = GenericIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance Construct(ObjectInstance array, Func<Intrinsics, Prototype> prototypeSelector)
        {
            var instance = new IteratorInstance.ArrayLikeIterator(Engine, array, ArrayIteratorType.KeyAndValue)
            {
                _prototype = prototypeSelector(_realm.Intrinsics)
            };

            return instance;
        }

        internal IteratorInstance ConstructEntryIterator(MapInstance map)
        {
            var instance = new IteratorInstance.MapIterator(Engine, map)
            {
                _prototype = MapIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance ConstructKeyIterator(MapInstance map)
        {
            var instance = new IteratorInstance(Engine, map._map.Keys)
            {
                _prototype = MapIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance ConstructValueIterator(MapInstance map)
        {
            var instance = new IteratorInstance(Engine, map._map.Values)
            {
                _prototype = MapIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance ConstructEntryIterator(SetInstance set)
        {
            var instance = new IteratorInstance.SetEntryIterator(Engine, set)
            {
                _prototype = SetIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance ConstructValueIterator(SetInstance set)
        {
            var instance = new IteratorInstance.ListIterator(Engine, set._set._list)
            {
                _prototype = SetIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance CreateArrayLikeIterator(ObjectInstance array, ArrayIteratorType kind)
        {
            var instance = new IteratorInstance.ArrayLikeIterator(Engine, array, kind)
            {
                _prototype = ArrayIteratorPrototypeObject
            };

            return instance;
        }

        internal IteratorInstance CreateRegExpStringIterator(ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode)
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