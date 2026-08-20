#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Storage;

/// <summary>
/// A <c>Storage</c> instance — the object <c>localStorage</c> and <c>sessionStorage</c> name.
/// <para>
/// https://html.spec.whatwg.org/multipage/webstorage.html#the-storage-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The six members live on <see cref="StoragePrototype"/>; what is here is the other half of the interface,
/// the one that makes <c>storage.foo = 'bar'</c> work. <c>Storage</c> declares a named property getter,
/// setter and deleter, which makes it a <b>legacy platform object</b>
/// (https://webidl.spec.whatwg.org/#legacy-platform-objects) whose <c>[[GetOwnProperty]]</c>,
/// <c>[[Set]]</c>, <c>[[DefineOwnProperty]]</c>, <c>[[Delete]]</c>, <c>[[OwnPropertyKeys]]</c> and
/// <c>[[PreventExtensions]]</c> are all specified — each is overridden below, and each cites its section.
/// </para>
/// <para>
/// The rule that ties them together is the <b>named property visibility algorithm</b>
/// (https://webidl.spec.whatwg.org/#dfn-named-property-visibility): a stored key is exposed as a property
/// only when nothing on the prototype chain already carries that name, because <c>Storage</c> is not
/// declared <c>[LegacyOverrideBuiltIns]</c>. So <c>storage.getItem</c> is always the method however the
/// script stores a key of that name — and storing it still works, and <c>storage.getItem('getItem')</c>
/// still reads it back. The specification spells the resulting order out: own properties, then the prototype
/// chain, then named properties.
/// </para>
/// <para>
/// <b>There is no <c>storage</c> event.</b> Every mutating algorithm ends in "broadcast this", which fires a
/// <c>StorageEvent</c> at <i>other</i> browsing contexts sharing the same origin — a multi-context feature,
/// and an engine has exactly one context. The step is therefore a no-op here rather than a partial
/// implementation, and <c>StorageEvent</c> is absent rather than present-and-never-firing so that feature
/// detection sees the truth.
/// </para>
/// </remarks>
internal sealed class JsStorage : ObjectInstance
{
    /// <summary>
    /// The attributes <c>LegacyPlatformObjectGetOwnProperty</c> gives a named property: writable because the
    /// interface has a named property setter, enumerable because it is not declared
    /// <c>[LegacyUnenumerableNamedProperties]</c>, and configurable always. So <c>Object.keys(storage)</c>
    /// lists the stored keys, exactly as in a browser.
    /// </summary>
    private const PropertyFlag NamedPropertyFlags = PropertyFlag.ConfigurableEnumerableWritable;

    private readonly Realm _realm;

    internal JsStorage(Engine engine, Realm realm, StorageProvider provider, ObjectInstance prototype)
        : base(engine, ObjectClass.Object)
    {
        _realm = realm;
        Provider = provider;
        _prototype = prototype;
    }

    /// <summary>
    /// The host's map. Read live on every operation, so a provider whose contents change behind the engine's
    /// back is observed as it is at each step.
    /// </summary>
    internal StorageProvider Provider { get; }

    /// <summary>
    /// Builds the storage object one of the two globals names, over the provider the engine resolved for it.
    /// </summary>
    /// <remarks>
    /// The map is the engine's, not the realm's: the intrinsic that calls this is only ever reached from the
    /// principal realm's global, and the providers live on the engine's web-API state because that is where
    /// the host's configuration was read into.
    /// </remarks>
    internal static JsStorage Create(Engine engine, Realm realm, StorageConstructor constructor, StorageKind kind)
    {
        var state = engine._webApi;
        if (state is null)
        {
            // Unreachable: the globals that reach these properties are installed only where the state was
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("A storage object was reached on an engine that has no web-API state.");
        }

        var provider = kind == StorageKind.Local ? state.LocalStorageProvider : state.SessionStorageProvider;
        return new JsStorage(engine, realm, provider, constructor.PrototypeObject);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-setitem, and the named property
    /// setter <c>[[Set]]</c> and <c>[[DefineOwnProperty]]</c> invoke.
    /// </summary>
    /// <remarks>
    /// Step 3 in full: an existing entry whose value is already <paramref name="value"/> makes the whole
    /// operation a no-op — the provider is not called, so it cannot refuse a write that changes nothing, and
    /// the key does not move. Step 4's "if value cannot be stored" is the provider's
    /// <see cref="StorageQuotaExceededException"/>, which becomes the <c>QuotaExceededError</c> the
    /// algorithm names. Step 6's reorder and step 7's broadcast are the implementation-defined and
    /// multi-context halves, discussed on the class.
    /// </remarks>
    internal void SetItem(string key, string value)
    {
        if (string.Equals(Provider.GetItem(key), value, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            Provider.SetItem(key, value);
        }
        catch (StorageQuotaExceededException quota)
        {
            var exception = _realm.Intrinsics.DomException.CreateException(DomExceptionNames.QuotaExceeded, quota.Message);
            var location = _engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(_engine, exception, in location);
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-named-property-visibility, with the value the getter produced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Step 1 is <see cref="StorageProvider.GetItem"/> answering non-null — the supported property names of
    /// a <c>Storage</c> object are the keys of its map. Step 2 asks whether the object already has an own
    /// property of that name; a <c>Storage</c> can only ever own symbols (every string define is routed to
    /// the named setter below), but the check is written out rather than assumed away, because it is what
    /// keeps this method and <see cref="GetOwnProperty"/> honest if that ever changes. Step 3 does not
    /// apply: <c>Storage</c> is not <c>[LegacyOverrideBuiltIns]</c>. Steps 4 and 5 walk the prototype chain,
    /// which is what <c>HasProperty</c> does.
    /// </para>
    /// <para>
    /// The order of the tests is chosen so the common answers are cheap: a name that is not stored — every
    /// method name, every miss — costs one lookup in the provider and never walks the prototype chain.
    /// </para>
    /// <para>
    /// One deviation, and only for a chain a script has rearranged itself: the walk asks each prototype for
    /// an <i>own</i> property, and <c>HasProperty</c> asks the chain as a whole. The two answer the same
    /// question, but a <c>Proxy</c> spliced in with <c>Object.setPrototypeOf</c> sees its <c>has</c> trap
    /// rather than one <c>getOwnPropertyDescriptor</c> trap per level.
    /// </para>
    /// </remarks>
    private bool TryGetVisibleNamedProperty(JsValue key, out string value)
    {
        value = null!;

        if (key is not JsString name)
        {
            return false;
        }

        var stored = Provider.GetItem(name.ToString());
        if (stored is null)
        {
            return false;
        }

        if (!ReferenceEquals(base.GetOwnProperty(key), PropertyDescriptor.Undefined))
        {
            return false;
        }

        var prototype = GetPrototypeOf();
        if (prototype is not null && prototype.HasProperty(key))
        {
            return false;
        }

        value = stored;
        return true;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#legacy-platform-object-getownproperty — <c>[[GetOwnProperty]]</c> is
    /// <c>LegacyPlatformObjectGetOwnProperty(O, P, false)</c>, which answers a visible named property with a
    /// fresh descriptor and everything else ordinarily.
    /// </summary>
    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        var key = TypeConverter.ToPropertyKey(property);
        if (TryGetVisibleNamedProperty(key, out var value))
        {
            return new PropertyDescriptor(JsString.Create(value), NamedPropertyFlags);
        }

        return base.GetOwnProperty(key);
    }

    /// <summary>
    /// The existence-and-enumerability half of <see cref="GetOwnProperty"/>, answered without materializing
    /// a descriptor — it backs <c>in</c>, <c>hasOwnProperty</c>, <c>Object.keys</c>, spread,
    /// <c>Object.assign</c> and <c>JSON.stringify</c>.
    /// </summary>
    protected internal override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        var key = TypeConverter.ToPropertyKey(property);
        if (TryGetVisibleNamedProperty(key, out _))
        {
            return OwnPropertyProbe.Enumerable;
        }

        return base.ProbeOwnProperty(key);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#legacy-platform-object-set — with the receiver being this object and
    /// the key a string, the named property setter runs and nothing else is consulted. That is what makes
    /// <c>storage.getItem = 1</c> store a key called <c>getItem</c> while <c>storage.getItem</c> goes on
    /// answering the method.
    /// </summary>
    /// <remarks>
    /// For a different receiver the algorithm reaches
    /// <c>LegacyPlatformObjectGetOwnProperty(O, P, <b>true</b>)</c> — named properties ignored — and a
    /// <c>Storage</c> has no own string properties at all, so that descriptor is always undefined and
    /// <c>OrdinarySetWithOwnDescriptor</c> reduces to continuing on the prototype chain. Symbols take the
    /// ordinary path throughout.
    /// </remarks>
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        var key = TypeConverter.ToPropertyKey(property);
        if (key is JsString name)
        {
            if (ReferenceEquals(this, receiver))
            {
                SetItem(name.ToString(), TypeConverter.ToString(value));
                return true;
            }

            var prototype = GetPrototypeOf();
            if (prototype is not null)
            {
                return prototype.Set(key, value, receiver);
            }
        }

        return base.Set(key, value, receiver);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#legacy-platform-object-defineownproperty — a string key never reaches
    /// <c>OrdinaryDefineOwnProperty</c>: an accessor or otherwise non-data descriptor is refused, and a data
    /// descriptor's value goes to the named property setter with its attributes discarded.
    /// </summary>
    /// <remarks>
    /// So <c>Object.defineProperty(storage, 'a', { value: 1 })</c> stores <c>"1"</c>, and asking for a
    /// getter, or for a descriptor with no fields at all, is a <c>TypeError</c> — returning <c>false</c> is
    /// what <c>DefinePropertyOrThrow</c> turns into one.
    /// </remarks>
    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        var key = TypeConverter.ToPropertyKey(property);
        if (key is JsString name && ReferenceEquals(base.GetOwnProperty(key), PropertyDescriptor.Undefined))
        {
            if (!desc.IsDataDescriptor())
            {
                return false;
            }

            SetItem(name.ToString(), TypeConverter.ToString(desc.Value));
            return true;
        }

        return base.DefineOwnProperty(key, desc);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#legacy-platform-object-delete — a visible named property is removed
    /// through the named property deleter, which for <c>Storage</c> is <c>removeItem</c>. A name the
    /// algorithm does not consider visible (a method's, or one that is simply not stored) deletes vacuously,
    /// leaving the prototype's property alone.
    /// </summary>
    public override bool Delete(JsValue property)
    {
        var key = TypeConverter.ToPropertyKey(property);
        if (TryGetVisibleNamedProperty(key, out _))
        {
            Provider.RemoveItem(key.ToString());
            return true;
        }

        return base.Delete(key);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#legacy-platform-object-ownpropertykeys — the visible named properties
    /// first, in the store's own order, then the object's own keys. The named ones are deliberately not
    /// sorted the way <c>OrdinaryOwnPropertyKeys</c> sorts array indices: a store whose keys are
    /// <c>"b"</c> then <c>"1"</c> enumerates in that order.
    /// </summary>
    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        if ((types & Types.String) == Types.Empty)
        {
            return base.GetOwnPropertyKeys(types);
        }

        var names = Provider.Keys;
        var keys = new List<JsValue>(names.Count + 1);

        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var name = JsString.Create(names[i]);
            if (TryGetVisibleNamedProperty(name, out _))
            {
                keys.Add(name);
            }
        }

        keys.AddRange(base.GetOwnPropertyKeys(types));
        return keys;
    }

    /// <summary>
    /// The same order as <see cref="GetOwnPropertyKeys"/>, with the descriptors materialized. The key list
    /// is snapshotted first because this is a lazy sequence whose consumer may mutate the store between two
    /// elements.
    /// </summary>
    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        var live = Provider.Keys;
        var names = new string[live.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = live[i];
        }

        foreach (var name in names)
        {
            var key = JsString.Create(name);
            if (TryGetVisibleNamedProperty(key, out var value))
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(key, new PropertyDescriptor(JsString.Create(value), NamedPropertyFlags));
            }
        }

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#legacy-platform-object-preventextensions — a legacy platform object
    /// refuses, which keeps it extensible forever and makes <c>Object.freeze(storage)</c> and
    /// <c>Object.preventExtensions(storage)</c> raise a <c>TypeError</c>, exactly as in a browser.
    /// </summary>
    public override bool PreventExtensions() => false;
}

/// <summary>
/// Which of the two globals a <see cref="JsStorage"/> is, which is only ever a question of <i>which map</i>
/// the engine hands it: the objects are otherwise identical, and the lifetime difference the two names carry
/// in a browser is something only a host-supplied <see cref="StorageProvider"/> can express.
/// </summary>
internal enum StorageKind
{
    /// <summary><c>localStorage</c>.</summary>
    Local,

    /// <summary><c>sessionStorage</c>.</summary>
    Session,
}
#endif
