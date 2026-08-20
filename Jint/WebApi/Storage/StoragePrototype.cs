#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Storage;

/// <summary>
/// <c>Storage.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/webstorage.html#the-storage-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The whole interface is here, and it is small: <c>length</c>, <c>key</c>, <c>getItem</c>, <c>setItem</c>,
/// <c>removeItem</c> and <c>clear</c>. The other half of what a <c>Storage</c> can do — <c>storage.foo</c>,
/// <c>storage.foo = 'bar'</c>, <c>delete storage.foo</c>, <c>Object.keys(storage)</c> — is the named property
/// getter/setter/deleter, and lives on <see cref="JsStorage"/> because those are internal methods of the
/// object rather than members of the interface.
/// </para>
/// <para>
/// Every member brand-checks its receiver, so an extracted <c>getItem</c> called on anything that is not a
/// <c>Storage</c> raises a <c>TypeError</c> — <c>Storage.prototype</c> itself included, which is not one.
/// The arity checks come first, before any argument is converted, which is the order WebIDL specifies and is
/// observable: <c>storage.setItem({ toString() { throw 1 } })</c> raises the <c>TypeError</c> about the
/// missing second argument rather than running the <c>toString</c>.
/// </para>
/// <para>
/// One documented simplification against WebIDL, shared with every other prototype Jint ships: the
/// operations are non-enumerable, where a WebIDL interface prototype object's operations are enumerable.
/// <c>length</c> is a real accessor pair, as an attribute must be, because <c>Object.keys(storage)</c>
/// listing a <c>length</c> would be visible to any script that enumerates a storage.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class StoragePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly StorageConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString StorageToStringTag = new("Storage");

    internal StoragePrototype(
        Engine engine,
        Realm realm,
        StorageConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// "The <c>length</c> getter steps are to return this's map's size" —
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-length.
    /// </summary>
    [JsAccessor("length", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LengthGet(JsValue thisObject)
    {
        return JsNumber.Create(Brand(thisObject).Provider.Count);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-key
    /// </summary>
    /// <remarks>
    /// The argument is an <c>unsigned long</c> with neither <c>[EnforceRange]</c> nor <c>[Clamp]</c>, so it
    /// converts exactly as ECMAScript's <c>ToUint32</c> does: <c>NaN</c> and the infinities become zero and
    /// everything else wraps modulo 2³². <c>storage.key(-1)</c> therefore asks for index 4294967295 and
    /// answers <c>null</c> rather than raising.
    /// </remarks>
    [JsFunction(Name = "key", Length = 1)]
    private JsValue Key(JsValue thisObject, JsValue index, [ArgCount] int argCount)
    {
        var storage = Brand(thisObject);
        RequireArguments(argCount, 1, "key");

        // Steps 1 and 2 read the same list: the size it is compared against is the size of the very list the
        // key is then taken from, so the two cannot disagree even if the host's store is changing.
        var keys = storage.Provider.Keys;
        var position = TypeConverter.ToUint32(index);

        return position >= (uint) keys.Count ? Null : JsString.Create(keys[(int) position]);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-getitem
    /// </summary>
    [JsFunction(Name = "getItem", Length = 1)]
    private JsValue GetItem(JsValue thisObject, JsValue key, [ArgCount] int argCount)
    {
        var storage = Brand(thisObject);
        RequireArguments(argCount, 1, "getItem");

        var value = storage.Provider.GetItem(TypeConverter.ToString(key));
        return value is null ? Null : JsString.Create(value);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-setitem
    /// </summary>
    /// <remarks>
    /// Both arguments are <c>DOMString</c>s, so everything is stringified on the way in:
    /// <c>storage.setItem(1, 2)</c> stores <c>"2"</c> under <c>"1"</c>, and <c>storage.setItem('a')</c>
    /// raises the arity <c>TypeError</c> rather than storing <c>"undefined"</c>. The algorithm's steps are
    /// on <see cref="JsStorage.SetItem"/>, which this and the named property setter share.
    /// </remarks>
    [JsFunction(Name = "setItem", Length = 2)]
    private JsValue SetItem(JsValue thisObject, JsValue key, JsValue value, [ArgCount] int argCount)
    {
        var storage = Brand(thisObject);
        RequireArguments(argCount, 2, "setItem");

        storage.SetItem(TypeConverter.ToString(key), TypeConverter.ToString(value));
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-removeitem
    /// </summary>
    [JsFunction(Name = "removeItem", Length = 1)]
    private JsValue RemoveItem(JsValue thisObject, JsValue key, [ArgCount] int argCount)
    {
        var storage = Brand(thisObject);
        RequireArguments(argCount, 1, "removeItem");

        // Step 1's "if this's map[key] does not exist, then return" is the provider's own business: removing
        // an absent key is defined to do nothing, and asking twice would only cost a lookup.
        storage.Provider.RemoveItem(TypeConverter.ToString(key));
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-clear
    /// </summary>
    [JsFunction(Name = "clear", Length = 0)]
    private JsValue Clear(JsValue thisObject)
    {
        Brand(thisObject).Provider.Clear();
        return Undefined;
    }

    /// <summary>
    /// WebIDL's arity check: an operation whose required arguments were not all supplied raises a
    /// <c>TypeError</c> before anything else is converted.
    /// </summary>
    private void RequireArguments(int argCount, int required, string operationName)
    {
        if (argCount < required)
        {
            Throw.TypeError(
                _realm,
                $"Failed to execute '{operationName}' on 'Storage': {required} argument{(required == 1 ? "" : "s")} required, but only {argCount} present.");
        }
    }

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsStorage Brand(JsValue thisObject)
    {
        if (thisObject is JsStorage storage)
        {
            return storage;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Storage");
        return null!;
    }
}
#endif
