using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

internal sealed class IteratorConstructor : Constructor
{
    private static readonly JsString _functionName = new("Iterator");

    internal IteratorConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new IteratorPrototype(engine, realm, this);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private IteratorPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false) { ["from"] = new(new PropertyDescriptor(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags)), };
        SetProperties(properties);
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined() || ReferenceEquals(this, newTarget))
        {
            Throw.TypeError(_realm);
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Iterator.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsObject(engine));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.from
    /// </summary>
    private JsValue From(JsValue thisObject, JsValue[] arguments)
    {
        var iteratorRecord = GetIteratorFlattenable(thisObject, StringHandlingType.IterateStringPrimitives);
        var hasInstance = _engine.Intrinsics.Iterator.OrdinaryHasInstance(iteratorRecord);
        if (hasInstance)
        {
            return iteratorRecord;
        }

        var wrapper = new WrapForValidIteratorPrototype(_engine, iteratorRecord);
        return wrapper;
    }

    private IteratorInstance.ObjectIterator GetIteratorFlattenable(JsValue obj, StringHandlingType stringHandling)
    {
        if (obj is not ObjectInstance)
        {
            if (stringHandling == StringHandlingType.RejectStrings || obj.IsString())
            {
                Throw.TypeError(_realm);
            }
        }

        JsValue iterator;
        var method = JsValue.GetMethod(_realm, obj, GlobalSymbolRegistry.Iterator);
        if (method is null)
        {
            iterator = obj;
        }
        else
        {
            iterator = method.Call(obj);
        }

        if (iterator is not ObjectInstance objectInstance)
        {
            Throw.TypeError(_realm);
            return null;
        }

        return new IteratorInstance.ObjectIterator(objectInstance);
    }

    private sealed class WrapForValidIteratorPrototype : ObjectInstance
    {
        public WrapForValidIteratorPrototype(
            Engine engine,
            IteratorInstance.ObjectIterator iterated) : base(engine)
        {
            Iterated = iterated;
            SetPrototypeOf(engine.Intrinsics.IteratorPrototype);
        }

        public IteratorInstance.ObjectIterator Iterated { get; }

        public ObjectInstance Next()
        {
            var iteratorRecord = Iterated;
            iteratorRecord.TryIteratorStep(out var obj);
            return obj;
        }

        public JsValue Return()
        {
            var iterator = Iterated.GetIterator(_engine.Realm);
            var returnMethod = iterator.GetMethod(CommonProperties.Return);
            if (returnMethod is null)
            {
                return CreateIteratorResultObject(Undefined, done: JsBoolean.True);
            }

            return returnMethod.Call(iterator);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createiterresultobject
        /// </summary>
        private IteratorResult CreateIteratorResultObject(JsValue value, JsBoolean done)
        {
            return IteratorResult.CreateValueIteratorPosition(_engine, value, done);
        }
    }

    private enum StringHandlingType
    {
        IterateStringPrimitives,
        RejectStrings
    }
}
