#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Crypto;

/// <summary>
/// <c>CryptoKey.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/webcrypto/#cryptokey-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// All four members are WebIDL attributes, so they are accessors here rather than own properties of the key,
/// and each brand-checks its receiver with a <c>TypeError</c> — including <c>CryptoKey.prototype</c> itself,
/// which is not a key. None of them can reach the key material; see <see cref="JsCryptoKey"/>.
/// </para>
/// <para>
/// One documented simplification against WebIDL, which every prototype in this subtree carries: the members
/// are non-enumerable, where a WebIDL interface prototype object's attributes are enumerable. That is how
/// every built-in Jint has ever shipped declares its prototype members, and it is observable only to code
/// inspecting property attributes.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class CryptoKeyPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CryptoKeyConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CryptoKeyToStringTag = new("CryptoKey");

    internal CryptoKeyPrototype(
        Engine engine,
        Realm realm,
        CryptoKeyConstructor constructor,
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
    /// https://w3c.github.io/webcrypto/#dom-cryptokey-type — "Reflects the [[type]] internal slot".
    /// </summary>
    [JsAccessor("type", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString TypeGet(JsValue thisObject)
    {
        return JsString.Create(Brand(thisObject).KeyType);
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#dom-cryptokey-extractable
    /// </summary>
    [JsAccessor("extractable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean ExtractableGet(JsValue thisObject)
    {
        return Brand(thisObject).Extractable ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#dom-cryptokey-algorithm — "Returns the cached ECMAScript object
    /// associated with the [[algorithm]] internal slot".
    /// </summary>
    [JsAccessor("algorithm", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private ObjectInstance AlgorithmGet(JsValue thisObject)
    {
        return Brand(thisObject).AlgorithmObject();
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#dom-cryptokey-usages — "Returns the cached ECMAScript object
    /// associated with the [[usages]] internal slot".
    /// </summary>
    [JsAccessor("usages", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsArray UsagesGet(JsValue thisObject)
    {
        return Brand(thisObject).UsagesObject();
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>. An attribute is not a promise-returning operation, so this is
    /// an ordinary throw rather than a rejection.
    /// </summary>
    private JsCryptoKey Brand(JsValue thisObject)
    {
        if (thisObject is JsCryptoKey key)
        {
            return key;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CryptoKey");
        return null!;
    }
}
#endif
