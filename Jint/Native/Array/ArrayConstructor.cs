#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using System.Collections;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Array;

public sealed class ArrayConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Array");

    internal ArrayConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ArrayPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public ArrayPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["from"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "from", From, 1, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)),
            ["isArray"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "isArray", IsArray, 1), PropertyFlag.NonEnumerable)),
            ["of"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "of", Of, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable))
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunction(Engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable), set: Undefined,PropertyFlag.Configurable),
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-array.from
    /// </summary>
    private JsValue From(JsValue thisObject, JsCallArguments arguments)
    {
        var items = arguments.At(0);
        var mapFunction = arguments.At(1);
        var callable = !mapFunction.IsUndefined() ? GetCallable(mapFunction) : null;
        var thisArg = arguments.At(2);

        if (items.IsNullOrUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Cannot convert undefined or null to object");
        }

        var usingIterator = GetMethod(_realm, items, GlobalSymbolRegistry.Iterator);
        if (usingIterator is not null)
        {
            ObjectInstance instance;
            if (!ReferenceEquals(this, thisObject) && thisObject is IConstructor constructor)
            {
                instance = constructor.Construct([], thisObject);
            }
            else
            {
                instance = ArrayCreate(0);
            }

            var iterator = items.GetIterator(_realm, method: usingIterator);
            var protocol = new ArrayProtocol(_engine, thisArg, instance, iterator, callable);
            protocol.Execute();
            return instance;
        }

        if (items is IObjectWrapper { Target: IEnumerable enumerable })
        {
            return ConstructArrayFromIEnumerable(enumerable);
        }

        var source = ArrayOperations.For(_realm, items, forWrite: false);
        return ConstructArrayFromArrayLike(thisObject, source, callable, thisArg);
    }

    private ObjectInstance ConstructArrayFromArrayLike(
        JsValue thisObj,
        ArrayOperations source,
        ICallable? callable,
        JsValue thisArg)
    {
        var length = source.GetLength();

        ObjectInstance a;
        if (!ReferenceEquals(thisObj, this) && thisObj is IConstructor constructor)
        {
            var argumentsList = new JsValue[] { length };
            a = Construct(constructor, argumentsList);
        }
        else
        {
            a = ArrayCreate(length);
        }

        var args = callable is not null
            ? _engine._jsValueArrayPool.RentArray(2)
            : null;

        var target = ArrayOperations.For(a, forWrite: true);
        uint n = 0;
        for (uint i = 0; i < length; i++)
        {
            var value = source.Get(i);
            if (callable is not null)
            {
                args![0] = value;
                args[1] = i;
                value = callable.Call(thisArg, args);

                // function can alter data
                length = source.GetLength();
            }

            target.CreateDataPropertyOrThrow(i, value);
            n++;
        }

        if (callable is not null)
        {
            _engine._jsValueArrayPool.ReturnArray(args!);
        }

        target.SetLength(length);
        return a;
    }

    private sealed class ArrayProtocol : IteratorProtocol
    {
        private readonly JsValue _thisArg;
        private readonly ArrayOperations _instance;
        private readonly ICallable? _callable;
        private long _index = -1;

        public ArrayProtocol(
            Engine engine,
            JsValue thisArg,
            ObjectInstance instance,
            IteratorInstance iterator,
            ICallable? callable) : base(engine, iterator, 2)
        {
            _thisArg = thisArg;
            _instance = ArrayOperations.For(instance, forWrite: true);
            _callable = callable;
        }

        protected override void ProcessItem(JsValue[] arguments, JsValue currentValue)
        {
            _index++;
            JsValue jsValue;
            if (_callable is not null)
            {
                arguments[0] = currentValue;
                arguments[1] = _index;
                jsValue = _callable.Call(_thisArg, arguments);
            }
            else
            {
                jsValue = currentValue;
            }

            _instance.CreateDataPropertyOrThrow((ulong) _index, jsValue);
        }

        protected override void IterationEnd()
        {
            _instance.SetLength((ulong) (_index + 1));
        }
    }

    private JsValue Of(JsValue thisObject, JsCallArguments arguments)
    {
        var len = arguments.Length;
        ObjectInstance a;
        if (thisObject.IsConstructor)
        {
            a = ((IConstructor) thisObject).Construct([len], thisObject);
        }
        else
        {
            a = _realm.Intrinsics.Array.Construct(len);
        }

        if (a is JsArray ai)
        {
            // faster for real arrays
            for (uint k = 0; k < arguments.Length; k++)
            {
                var kValue = arguments[(int)k];
                ai.SetIndexValue(k, kValue, updateLength: k == arguments.Length - 1);
            }
        }
        else
        {
            // slower version
            for (uint k = 0; k < arguments.Length; k++)
            {
                var kValue = arguments[(int)k];
                var key = JsString.Create(k);
                a.CreateDataPropertyOrThrow(key, kValue);
            }

            a.Set(CommonProperties.Length, len, true);
        }

        return a;
    }

    private static JsValue Species(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    private static JsValue IsArray(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0);

        return IsArray(o);
    }

    private static JsValue IsArray(JsValue o)
    {
        if (!(o is ObjectInstance oi))
        {
            return JsBoolean.False;
        }

        return oi.IsArray();
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public JsArray Construct(JsCallArguments arguments)
    {
        return (JsArray) Construct(arguments, this);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            newTarget = this;
        }

        var proto = _realm.Intrinsics.Function.GetPrototypeFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Array.PrototypeObject);

        // check if we can figure out good size
        var capacity = arguments.Length > 0 ? (ulong) arguments.Length : 0;
        if (arguments.Length == 1 && arguments[0].IsNumber())
        {
            var number = ((JsNumber) arguments[0])._value;
            ValidateLength(number);
            capacity = (ulong) number;
        }
        return Construct(arguments, capacity, proto);
    }

    public JsArray Construct(int capacity)
    {
        return Construct([], (uint) capacity);
    }

    public JsArray Construct(uint capacity)
    {
        return Construct([], capacity);
    }

    public JsArray Construct(JsCallArguments arguments, uint capacity)
    {
        return Construct(arguments, capacity, PrototypeObject);
    }

    private JsArray Construct(JsCallArguments arguments, ulong capacity, ObjectInstance prototypeObject)
    {
        JsArray instance;
        if (arguments.Length == 1)
        {
            switch (arguments[0])
            {
                case JsNumber number:
                    ValidateLength(number._value);
                    instance = ArrayCreate((ulong) number._value, prototypeObject);
                    break;
                case IObjectWrapper objectWrapper:
                    instance = objectWrapper.Target is IEnumerable enumerable
                        ? ConstructArrayFromIEnumerable(enumerable)
                        : ArrayCreate(0, prototypeObject);
                    break;
                case JsArray array:
                    // direct copy
                    instance = (JsArray) ConstructArrayFromArrayLike(Undefined, ArrayOperations.For(array, forWrite: false), callable: null, this);
                    break;
                default:
                    instance = ArrayCreate(capacity, prototypeObject);
                    instance._length!._value = JsNumber.PositiveZero;
                    instance.Push(arguments);
                    break;
            }
        }
        else
        {
            instance = ArrayCreate((ulong) arguments.Length, prototypeObject);
            instance._length!._value = JsNumber.PositiveZero;
            if (arguments.Length > 0)
            {
                instance.Push(arguments);
            }
        }

        return instance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraycreate
    /// </summary>
    internal JsArray ArrayCreate(ulong length, ObjectInstance? proto = null)
    {
        if (length > ArrayOperations.MaxArrayLength)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Invalid array length " + length);
        }

        proto ??= PrototypeObject;
        var instance = new JsArray(Engine, (uint) length, (uint) length)
        {
            _prototype = proto
        };
        return instance;
    }

    private JsArray ConstructArrayFromIEnumerable(IEnumerable enumerable)
    {
        var jsArray = Construct(Arguments.Empty);
        var tempArray = _engine._jsValueArrayPool.RentArray(1);
        foreach (var item in enumerable)
        {
            var jsItem = FromObject(Engine, item);
            tempArray[0] = jsItem;
            _realm.Intrinsics.Array.PrototypeObject.Push(jsArray, tempArray);
        }

        _engine._jsValueArrayPool.ReturnArray(tempArray);
        return jsArray;
    }

    public JsArray ConstructFast(JsValue[] contents)
    {
        var array = new JsValue[contents.Length];
        System.Array.Copy(contents, array, contents.Length);
        return new JsArray(_engine, array);
    }

    internal JsArray ConstructFast(List<JsValue> contents)
    {
        var array = new JsValue[contents.Count];
        contents.CopyTo(array);
        return new JsArray(_engine, array);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arrayspeciescreate
    /// </summary>
    internal ObjectInstance ArraySpeciesCreate(ObjectInstance originalArray, ulong length)
    {
        var isArray = originalArray.IsArray();
        if (!isArray)
        {
            return ArrayCreate(length);
        }

        var c = originalArray.Get(CommonProperties.Constructor);

        if (c.IsConstructor)
        {
            var thisRealm = _engine.ExecutionContext.Realm;
            var realmC = GetFunctionRealm(c);
            if (!ReferenceEquals(thisRealm, realmC))
            {
                if (ReferenceEquals(c, realmC.Intrinsics.Array))
                {
                    c = Undefined;
                }
            }
        }

        if (c.IsObject())
        {
            c = c.Get(GlobalSymbolRegistry.Species);
            if (c.IsNull())
            {
                c = Undefined;
            }
        }

        if (c.IsUndefined())
        {
            return ArrayCreate(length);
        }

        if (!c.IsConstructor)
        {
            ExceptionHelper.ThrowTypeError(_realm, $"{c} is not a constructor");
        }

        return ((IConstructor) c).Construct([JsNumber.Create(length)], c);
    }

    internal JsArray CreateArrayFromList<T>(List<T> values) where T : JsValue
    {
        var jsArray = ArrayCreate((uint) values.Count);
        var index = 0;
        for (; index < values.Count; index++)
        {
            var item = values[index];
            jsArray.SetIndexValue((uint) index, item, false);
        }

        jsArray.SetLength((uint) index);
        return jsArray;
    }

    internal JsArray CreateArrayFromList<T>(T[] values) where T : JsValue
    {
        var jsArray = ArrayCreate((uint) values.Length);
        var index = 0;
        for (; index < values.Length; index++)
        {
            var item = values[index];
            jsArray.SetIndexValue((uint) index, item, false);
        }

        jsArray.SetLength((uint) index);
        return jsArray;
    }

    private void ValidateLength(double length)
    {
        if (length < 0 || length > ArrayOperations.MaxArrayLikeLength || ((long) length) != length)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Invalid array length");
        }
    }
}
