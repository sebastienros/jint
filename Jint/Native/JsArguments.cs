using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-arguments-exotic-objects
/// </summary>
public sealed class JsArguments : ObjectInstance
{
    private Function.Function _func = null!;
    private Key[] _names = null!;
    private JsValue[] _args = null!;
    private DeclarativeEnvironment _env = null!;
    private Realm _realm = null!;
    private bool _canReturnToPool;
    private bool _hasRestParameter;
    private bool _materialized;

    // Virtual read mode: while set, in-range index reads, length, @@iterator and (mapped) callee
    // are answered from _args/_env/_func without materializing any property descriptors — the
    // common `arguments[i]` / `arguments.length` / apply-forwarding patterns never build the
    // length/callee/iterator descriptors, the parameter-map object or its ClrAccessDescriptors.
    // Any operation that needs real properties routes through EnsureInitialized -> Initialize,
    // which clears the flag, so every materializing path stays consistent.
    private bool _virtualMode;

    // Set when the owning call returned: the parameter map is detached, so mapped index reads
    // switch from live bindings to the escaped snapshot (which write-syncs while mapped), and a
    // later materialization must not rebuild the map — matching the long-standing behavior of
    // nulling ParameterMap in FunctionWasCalled.
    private bool _mapDetached;

    internal JsArguments(Engine engine)
        : base(engine, ObjectClass.Arguments)
    {
        // Member reads must route through the virtual-mode [[Get]] instead of the
        // GetOwnProperty-first protocol (which would materialize the property surface);
        // arguments objects gain nothing from the member caches anyway — their identity
        // churns per call, so a cached receiver never re-matches.
        _type |= InternalTypes.ExoticGet;
    }

    internal void Prepare(
        Function.Function func,
        Key[] names,
        JsValue[] args,
        DeclarativeEnvironment env,
        bool hasRestParameter)
    {
        _func = func;
        _names = names;
        _args = args;
        _env = env;
        _hasRestParameter = hasRestParameter;
        _canReturnToPool = true;
        _virtualMode = true;
        _mapDetached = false;

        // Materialization can now be deferred past the owning call (even cross-realm queries),
        // so pin the creating realm: %ThrowTypeError%, %Array.prototype.values% and the map's
        // prototype are per-realm intrinsics.
        _realm = _engine.Realm;

        ClearProperties();
    }

    protected override void Initialize()
    {
        _virtualMode = false;
        _canReturnToPool = false;

        // Materialization reconstructs properties that conceptually existed from the object's
        // creation; a preventExtensions/freeze that happened while still virtual must not block
        // them, so bypass the extensibility gate for the duration.
        var wasExtensible = Extensible;
        Extensible = true;
        try
        {
            InitializeCore();
        }
        finally
        {
            Extensible = wasExtensible;
        }
    }

    private void InitializeCore()
    {
        var args = _args;

        DefinePropertyOrThrow(CommonProperties.Length, new PropertyDescriptor(_args.Length, PropertyFlag.NonEnumerable));

        if (_func is null)
        {
            // unmapped
            ParameterMap = null;

            for (uint i = 0; i < (uint) args.Length; i++)
            {
                var val = args[i];
                CreateDataProperty(JsString.Create(i), val);
            }

            DefinePropertyOrThrow(CommonProperties.Callee, new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(_realm, PropertyFlag.None));
        }
        else
        {
            ObjectInstance? map = null;
            if (args.Length > 0)
            {
                // index properties always materialize; the live-binding parameter map is only
                // built while the owning call is still running (post-return the mapping is
                // detached and the snapshot values are authoritative)
                if (!_mapDetached)
                {
                    map = _realm.Intrinsics.Object.Construct(Arguments.Empty);
                }

                for (uint i = 0; i < (uint) args.Length; i++)
                {
                    SetOwnProperty(JsString.Create(i), new PropertyDescriptor(args[i], PropertyFlag.ConfigurableEnumerableWritable));
                    if (map is not null && TryGetMappedName(i, out var name))
                    {
                        map.SetOwnProperty(JsString.Create(i), new ClrAccessDescriptor(_env, Engine, name));
                    }
                }
            }

            ParameterMap = map;

            // step 13
            DefinePropertyOrThrow(CommonProperties.Callee, new PropertyDescriptor(_func, PropertyFlag.NonEnumerable));
        }

        // Spec (10.4.4.6 step 19) requires arguments[@@iterator] to be %Array.prototype.values%
        // — the same function object, not a fresh wrapper.
        var iteratorFunction = _realm.Intrinsics.Array.PrototypeObject.Get("values");
        DefinePropertyOrThrow(GlobalSymbolRegistry.Iterator, new PropertyDescriptor(iteratorFunction, PropertyFlag.Writable | PropertyFlag.Configurable));
    }

    private ObjectInstance? _parameterMap;

    internal ObjectInstance? ParameterMap
    {
        get
        {
            // internal consumers (and tests) expect the map to exist for a live mapped
            // arguments object; materialize on demand when still in virtual mode
            if (_virtualMode)
            {
                EnsureInitialized();
            }

            return _parameterMap;
        }
        set => _parameterMap = value;
    }

    internal override bool IsArrayLike => true;

    internal override bool IsIntegerIndexedArray => true;

    public uint Length => (uint) _args.Length;

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (_virtualMode && ReferenceEquals(this, receiver))
        {
            if (Array.ArrayInstance.IsArrayIndex(property, out var index))
            {
                if (index < (uint) _args.Length)
                {
                    // the parameter map binds the LAST index carrying each name (earlier duplicates
                    // read the positional argument), matching CreateMappedArgumentsObject; once the
                    // call returned the map is detached and the snapshot is authoritative
                    if (_func is not null && !_mapDetached && TryGetMappedName(index, out var mappedName))
                    {
                        return _env.GetBindingValue(mappedName, strict: false);
                    }

                    return _args[index];
                }
            }
            else if (CommonProperties.Length.Equals(property))
            {
                return JsNumber.Create(_args.Length);
            }
            else if (CommonProperties.Callee.Equals(property) && _func is not null)
            {
                return _func;
            }
            else if (ReferenceEquals(property, GlobalSymbolRegistry.Iterator))
            {
                // spec identity: %Array.prototype.values%
                return _realm.Intrinsics.Array.PrototypeObject.Get("values");
            }
        }

        return base.Get(property, receiver);
    }

    private bool TryGetMappedName(uint index, out Key name)
    {
        var names = _names;
        if (index >= (uint) names.Length)
        {
            name = default;
            return false;
        }

        name = names[index];

        // CreateMappedArgumentsObject maps the LAST parameter of a duplicated name (it walks the
        // parameter list back to front, mapping the first name it has not yet seen). So this index
        // owns the mapping only when no later parameter repeats the same name; earlier duplicates
        // read the positional argument.
        for (var i = index + 1; i < (uint) names.Length; i++)
        {
            if (names[i] == name)
            {
                return false;
            }
        }

        return true;
    }

    // Before virtual read mode, an uninitialized arguments object was unreachable (any user
    // access materialized it first); now the enumeration surfaces must initialize explicitly
    // since they read the property bag directly.
    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        EnsureInitialized();
        return base.GetOwnPropertyKeys(types);
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        EnsureInitialized();
        return base.GetOwnProperties();
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        EnsureInitialized();

        if (ParameterMap is not null)
        {
            var desc = base.GetOwnProperty(property);
            if (desc == PropertyDescriptor.Undefined)
            {
                return desc;
            }

            if (ParameterMap.TryGetValue(property, out var jsValue) && !jsValue.IsUndefined())
            {
                desc.Value = jsValue;
            }

            return desc;
        }

        return base.GetOwnProperty(property);
    }

    /// Implementation from ObjectInstance official specs as the one
    /// in ObjectInstance is optimized for the general case and wouldn't work
    /// for arrays
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // Mapped-index writes go straight to the parameter binding (matching the
        // ClrAccessDescriptor setter the map would install); anything else materializes.
        if (_virtualMode && !_mapDetached && ReferenceEquals(this, receiver)
            && _func is not null
            && Array.ArrayInstance.IsArrayIndex(property, out var virtualIndex)
            && virtualIndex < (uint) _args.Length
            && TryGetMappedName(virtualIndex, out var virtualName))
        {
            _env.SetMutableBinding(virtualName, value, strict: true);
            if (_materialized)
            {
                // keep the escaped snapshot in sync so a later materialization captures the
                // written value (matching today's map-synced descriptor); pre-escape _args is
                // the pooled caller array and must never be written
                _args[virtualIndex] = value;
            }
            return true;
        }

        EnsureInitialized();

        if (!CanPut(property))
        {
            return false;
        }

        var ownDesc = GetOwnProperty(property);

        if (ownDesc.IsDataDescriptor())
        {
            var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
            return DefineOwnProperty(property, valueDesc);
        }

        // property is an accessor or inherited
        var desc = GetOwnProperty(property);

        if (desc.IsAccessorDescriptor())
        {
            if (desc.Set is not ICallable setter)
            {
                return false;
            }
            setter.Call(receiver, value);
        }
        else
        {
            var newDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
            return DefineOwnProperty(property, newDesc);
        }

        return true;
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (_hasRestParameter)
        {
            // immutable
            return true;
        }

        EnsureInitialized();

        if (ParameterMap is not null)
        {
            var map = ParameterMap;
            var isMapped = map.GetOwnProperty(property);
            var allowed = base.DefineOwnProperty(property, desc);
            if (!allowed)
            {
                return false;
            }

            if (isMapped != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    map.Delete(property);
                }
                else
                {
                    var descValue = desc.Value;
                    if (descValue is not null && !descValue.IsUndefined())
                    {
                        map.Set(property, descValue, false);
                    }

                    if (desc.WritableSet && !desc.Writable)
                    {
                        map.Delete(property);
                    }
                }
            }

            return true;
        }

        return base.DefineOwnProperty(property, desc);
    }

    public override bool Delete(JsValue property)
    {
        EnsureInitialized();

        if (ParameterMap is not null)
        {
            var map = ParameterMap;
            var isMapped = map.GetOwnProperty(property);
            var result = base.Delete(property);
            if (result && isMapped != PropertyDescriptor.Undefined)
            {
                map.Delete(property);
            }

            return result;
        }

        return base.Delete(property);
    }

    internal void Materialize()
    {
        if (_materialized)
        {
            // already done
            return;
        }

        _materialized = true;

        // Snapshot the (pooled, caller-owned) argument values so reads stay valid after the
        // call returns. Virtual mode keeps serving reads off the snapshot; the property
        // descriptors materialize lazily only if something actually needs them. Mapped indices are
        // still served from their live binding until the map detaches (FunctionWasCalled freezes the
        // final binding values into this snapshot), so no per-index binding read is needed here.
        var args = _args;
        var copiedArgs = new JsValue[args.Length];
        System.Array.Copy(args, copiedArgs, args.Length);
        _args = copiedArgs;

        _canReturnToPool = false;
    }

    /// <summary>
    /// A mapped index reads its live parameter binding while the owning call runs; a write through
    /// either the parameter name (<c>a = x</c>) or <c>arguments[i] = x</c> updates that binding. Just
    /// before the map detaches on return, freeze each still-mapped index's current binding value into
    /// the authoritative snapshot (the <c>_args</c> array while virtual, or the own data property once
    /// materialized), so a write made at any point during the call survives into the escaped object.
    /// Indices that were deleted or redefined away from a plain data property are no longer in the map
    /// and are left untouched.
    /// </summary>
    private void FreezeMappedBindings()
    {
        if (_virtualMode)
        {
            // Only an escaped (materialized) object is read after return; a still-pooled one is
            // unobserved. _args is a private copy once materialized, so it is safe to write. No index
            // can have been deleted while virtual — a delete routes through EnsureInitialized first.
            if (!_materialized)
            {
                return;
            }

            var args = _args;
            for (uint i = 0; i < (uint) args.Length; i++)
            {
                if (TryGetMappedName(i, out var name))
                {
                    args[i] = _env.GetBindingValue(name, strict: false);
                }
            }
        }
        else if (_parameterMap is { } map)
        {
            // Materialized to real descriptors: the live map overlaid mapped reads during the call.
            // Bake the final binding value into each own data property that is still mapped (present
            // in the map) before the overlay is dropped. Update the value in place so a user-set
            // attribute is preserved — a still-mapped index can be made non-enumerable without
            // unmapping; only accessor / non-writable redefinitions unmap, and the map-membership
            // check already skips those.
            for (uint i = 0; i < (uint) _args.Length; i++)
            {
                var key = JsString.Create(i);
                if (map.HasOwnProperty(key) && TryGetMappedName(i, out var name))
                {
                    var existing = base.GetOwnProperty(key);
                    if (existing.IsDataDescriptor())
                    {
                        existing.Value = _env.GetBindingValue(name, strict: false);
                    }
                }
            }
        }
    }

    internal void FunctionWasCalled()
    {
        // Freeze the final mapped-binding values into the snapshot while the map is still live, then
        // detach: post-call reads see the captured snapshot, not live bindings, and a later
        // materialization must not rebuild the map.
        if (!_mapDetached && _func is not null)
        {
            FreezeMappedBindings();
        }

        _mapDetached = true;

        // should no longer expose arguments which is special name
        ParameterMap = null;

        if (_canReturnToPool)
        {
            _engine._argumentsInstancePool.Return(this);
            // prevent double-return
            _canReturnToPool = false;
        }
    }
}
