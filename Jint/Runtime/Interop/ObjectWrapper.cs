using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

#pragma warning disable IL2067
#pragma warning disable IL2072
#pragma warning disable IL2075

namespace Jint.Runtime.Interop;

/// <summary>
/// Wraps a CLR instance
/// </summary>
public class ObjectWrapper : ObjectInstance, IObjectWrapper, IEquatable<ObjectWrapper>
{
    internal readonly TypeDescriptor _typeDescriptor;
    private bool _lengthPropertyPending;

    internal ObjectWrapper(
        Engine engine,
        object obj,
        Type? type = null)
        : base(engine)
    {
        // Member access resolves against the wrapped CLR object, not ordinary own-property-then-prototype
        // lookup, so the prototype-method inline cache must skip this receiver and any object whose
        // prototype is a wrapper. See InternalTypes.ExoticGet.
        _type |= InternalTypes.ExoticGet;
        Target = obj;
        ClrType = GetClrType(obj, type);
        _typeDescriptor = TypeDescriptor.Get(ClrType);

        if (_typeDescriptor.LengthProperty is not null)
        {
            // the "length" forwarder (produced from Count or Length) is materialized lazily on first
            // own-property consultation: plain length reads are served by the ICollection fast path in
            // Get, so most wrappers never observe the descriptor itself
            _lengthPropertyPending = true;

            if (_typeDescriptor.IsArrayLike && engine.Options.Interop.AttachArrayPrototype)
            {
                // if we have array-like object, we can attach array prototype
                _prototype = engine.Intrinsics.Array.PrototypeObject;
            }
        }

        if (_typeDescriptor.IsDisposable)
        {
            SetProperty(GlobalSymbolRegistry.Dispose, new PropertyDescriptor(new ClrFunction(engine, "dispose", static (thisObject, _) =>
            {
                ((thisObject as ObjectWrapper)?.Target as IDisposable)?.Dispose();
                return Undefined;
            }), PropertyFlag.NonEnumerable));
        }

#if SUPPORTS_ASYNC_DISPOSE
        if (_typeDescriptor.IsAsyncDisposable)
        {
            SetProperty(GlobalSymbolRegistry.AsyncDispose, new PropertyDescriptor(new ClrFunction(engine, "asyncDispose", (thisObject, _) =>
            {
                var target = ((thisObject as ObjectWrapper)?.Target as IAsyncDisposable)?.DisposeAsync();
                if (target is not null)
                {
                    return ConvertAwaitableToPromise(engine, target);
                }
                return Undefined;
            }), PropertyFlag.NonEnumerable));
        }
#endif

        if (_typeDescriptor.ToJsonMethod is not null)
        {
            // Wrap the toJSON method in a ClrFunction with the expected signature for JSON.stringify
            var toJsonFunction = new ClrFunction(engine, "toJSON", (thisObject, arguments) =>
            {
                var wrapper = thisObject as ObjectWrapper;
                if (wrapper is null)
                {
                    return Undefined;
                }

                try
                {
                    // Call the CLR toJSON method with no arguments (as expected by JSON.stringify)
                    var result = _typeDescriptor.ToJsonMethod.Invoke(wrapper.Target, null);
                    return FromObject(engine, result);
                }
                catch (TargetInvocationException exception)
                {
                    Throw.MeaningfulException(engine, exception);
                    return Undefined;
                }
            });

            // toJSON should be writable, configurable, and non-enumerable to match JavaScript standard
            // (e.g., Date.prototype.toJSON has these same flags)
            SetProperty("toJSON", new PropertyDescriptor(toJsonFunction, PropertyFlag.Writable | PropertyFlag.Configurable | PropertyFlag.NonEnumerable));
        }
    }

    /// <summary>
    /// Creates a new object wrapper for given object instance and exposed type.
    /// </summary>
    public static ObjectInstance Create(Engine engine, object target, Type? type = null)
    {
        if (target == null)
        {
            Throw.ArgumentNullException(nameof(target));
        }

        // STJ integration
        if (string.Equals(type?.FullName, "System.Text.Json.Nodes.JsonNode", StringComparison.Ordinal))
        {
            // we need to always expose the actual type instead of the type nodes provide
            type = target.GetType();
        }

        type ??= target.GetType();

        if (TryBuildArrayLikeWrapper(engine, target, type, out var wrapper))
        {
            return wrapper;
        }

        return new ObjectWrapper(engine, target, type);
    }

    private static readonly ConcurrentDictionary<Type, ArrayLikeWrapperFactory?> _arrayLikeWrapperResolution = new();

    private static bool TryBuildArrayLikeWrapper(
        Engine engine,
        object target,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        [NotNullWhen(true)] out ArrayLikeWrapper? result)
    {
        result = null;

        // resolved once per exposed type: reflection (interface scan + generic instantiation +
        // Activator) runs only on the first sighting, every later wrapper creation is a single
        // virtual call into the cached factory
        var factory = _arrayLikeWrapperResolution.GetOrAdd(type, static t =>
        {
#pragma warning disable IL2055
#pragma warning disable IL2070
#pragma warning disable IL3050

            Type? factoryType = null;

            // single-rank zero-based CLR arrays (T[]) get a fixed-size live wrapper; T[] implements
            // IList<T> and would otherwise flow into GenericListWrapper<T> below, whose growth paths
            // call IList<T>.Add and would leak NotSupportedException from the underlying array.
            // The MakeArrayType equality intentionally excludes multi-rank (T[,]) and non-zero-based
            // (T[*]) arrays, which keep their previous handling.
            if (t.IsArray && t.GetElementType() is { } elementType && t == elementType.MakeArrayType())
            {
                factoryType = typeof(ArrayWrapperFactory<>).MakeGenericType(elementType);
            }
            else
            {
                // check for generic interfaces
                foreach (var i in t.GetInterfaces())
                {
                    if (!i.IsGenericType)
                    {
                        continue;
                    }

                    var arrayItemType = i.GenericTypeArguments[0];

                    if (i.GetGenericTypeDefinition() == typeof(IList<>))
                    {
                        factoryType = typeof(GenericListWrapperFactory<>).MakeGenericType(arrayItemType);
                        break;
                    }

                    if (i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                    {
                        factoryType = typeof(ReadOnlyListWrapperFactory<>).MakeGenericType(arrayItemType);
                        break;
                    }
                }
            }
#pragma warning restore IL3050
#pragma warning restore IL2070
#pragma warning restore IL2055

            if (factoryType is null)
            {
                return null;
            }

            // Activator.CreateInstance may fail in trimmed/AOT scenarios where the constructor
            // was removed by the linker - fall back to the non-generic ListWrapper in that case
            try
            {
                return (ArrayLikeWrapperFactory) Activator.CreateInstance(factoryType)!;
            }
            catch (MissingMethodException)
            {
                return null;
            }
        });

        if (factory is not null)
        {
            result = factory.Create(engine, target, type);
        }
        else if (target is IList list)
        {
            // least specific
            result = new ListWrapper(engine, list, type);
        }

        return result is not null;
    }

    public object Target { get; }
    public Type ClrType { get; }

    internal override bool IsArrayLike => _typeDescriptor.IsArrayLike;

    internal override bool HasOriginalIterator => IsArrayLike;

    internal override bool IsIntegerIndexedArray => _typeDescriptor.IsIntegerIndexed;

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // check if we can take shortcuts for empty object, no need to generate properties
        if (property is JsString stringKey)
        {
            var member = stringKey.ToString();
            if (_lengthPropertyPending && string.Equals(member, "length", StringComparison.Ordinal))
            {
                MaterializeLengthProperty();
            }
            if (_properties is null || !_properties.ContainsKey(member))
            {
                // can try utilize fast path
                var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member, mustBeReadable: false, mustBeWritable: true, throwOnError: false);
                var actualType = Target.GetType();
                if (ClrType != actualType)
                {
                    // When the declared type differs from the actual runtime type:
                    // If only an indexer was found, check if the runtime type has a direct property/field/method
                    // that should take precedence over the indexer
                    if (accessor is IndexerAccessor)
                    {
                        var runtimeAccessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, actualType, member, mustBeReadable: false, mustBeWritable: true, throwOnError: false);
                        if (runtimeAccessor is not IndexerAccessor && runtimeAccessor != ConstantValueAccessor.NullAccessor)
                        {
                            accessor = runtimeAccessor;
                        }
                    }
                    else if (ReferenceEquals(accessor, ConstantValueAccessor.NullAccessor))
                    {
                        accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, actualType, member, mustBeReadable: false, mustBeWritable: true, throwOnError: false);
                    }
                }

                if (ReferenceEquals(accessor, ConstantValueAccessor.NullAccessor))
                {
                    if (_engine.Options.Interop.ThrowOnUnresolvedMember)
                    {
                        throw new MissingMemberException($"Cannot access property '{member}' on type '{ClrType.FullName}");
                    }

                    // there's no such property, but we can allow extending by calling base
                    // which will add properties, this allows for example JS class to extend a CLR type
                    return base.Set(property, value, receiver);
                }

                // CanPut logic
                if (!accessor.Writable || !_engine.Options.Interop.AllowWrite)
                {
                    return false;
                }

                if (!Extensible)
                {
                    // object is frozen/sealed, cannot add new properties
                    return false;
                }

                accessor.SetValue(_engine, Target, member, value);
                return true;
            }
        }
        else if (property is JsSymbol jsSymbol)
        {
            // symbol addition will never hit any known CLR object properties, so if write is allowed, allow writing symbols too
            if (_engine.Options.Interop.AllowWrite)
            {
                return base.Set(jsSymbol, value, receiver);
            }

            return false;
        }
        else if (ReferenceEquals(receiver, this) && _typeDescriptor.IsNonStringKeyedGenericDictionary)
        {
            // non-string-keyed CLR generic dictionary (e.g. Dictionary<TestModel, string>).
            // Matches the receiver gate in Get: when [[Set]] arrives via Proxy/Reflect.set with a
            // different receiver, fall through to the spec-compliant slow path instead of mutating
            // the underlying dict directly.
            if (!_engine.Options.Interop.AllowWrite || !Extensible)
            {
                return false;
            }

            var keyType = _typeDescriptor.GenericDictionaryKeyType!;
            var valueType = _typeDescriptor.GenericDictionaryValueType!;
            if (!TryConvertJsValueToDictionaryKey(property, keyType, out var clrKey)
                || !TryConvertJsValueToDictionaryValue(value, valueType, out var clrValue))
            {
                return false;
            }

            var written = _typeDescriptor.TrySetDictionaryValue(Target, clrKey!, clrValue);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return written;
        }

        return SetSlow(property, value);
    }

    private bool SetSlow(JsValue property, JsValue value)
    {
        if (!CanPut(property))
        {
            return false;
        }

        var ownDesc = GetOwnProperty(property);
        ownDesc.Value = value;
        return true;
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (_typeDescriptor.IsStringKeyedGenericDictionary && property.IsString() && !TryGetProperty(property, out _))
        {
            // For dictionary-backed objects, GetOwnProperty returns fresh descriptors that are not stored
            // in _properties. ValidateAndApplyPropertyDescriptor mutates descriptors in-place, so mutations
            // (e.g. from Object.freeze/seal) would be lost without pre-storing the descriptor.
            var current = GetOwnProperty(property);
            if (current != PropertyDescriptor.Undefined)
            {
                SetProperty(property, current);
            }
        }

        return base.DefineOwnProperty(property, desc);
    }

    public override object ToObject() => Target;

    public override void RemoveOwnProperty(JsValue property)
    {
        if (_lengthPropertyPending && CommonProperties.Length.Equals(property))
        {
            // an explicit removal of the not-yet-materialized forwarder must behave like removing the
            // eagerly-created one did: the property is gone and does not come back
            _lengthPropertyPending = false;
        }

        if (_engine.Options.Interop.AllowWrite)
        {
            if (property is JsString jsString && _typeDescriptor.IsStringKeyedGenericDictionary)
            {
                _typeDescriptor.TryRemoveDictionaryValue(Target, jsString.ToString());
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            else if (!property.IsString()
                && !property.IsSymbol()
                && _typeDescriptor.IsNonStringKeyedGenericDictionary
                && TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
            {
                _typeDescriptor.TryRemoveDictionaryValue(Target, clrKey!);
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
        }

        // also remove from _properties cache to avoid stale entries
        base.RemoveOwnProperty(property);
    }

    public override bool HasProperty(JsValue property)
    {
        if (!property.IsString()
            && !property.IsSymbol()
            && _typeDescriptor.IsNonStringKeyedGenericDictionary
            && TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
        {
            // Prototype chain is intentionally skipped: non-string non-symbol keys can't resolve
            // to Object.prototype members (which are all string/symbol-keyed). Same rationale as Get.
            var contains = _typeDescriptor.ContainsDictionaryKey(Target, clrKey!);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return contains;
        }

        return base.HasProperty(property);
    }

    private bool TryConvertJsValueToDictionaryKey(JsValue property, Type keyType, out object? key)
    {
        var raw = property.ToObject();
        if (raw is null)
        {
            // standard Dictionary<,> throws ArgumentNullException on null keys; bail before invoking
            key = null;
            return false;
        }

        if (keyType.IsInstanceOfType(raw))
        {
            key = raw;
            return true;
        }
        return _engine.TypeConverter.TryConvert(raw, keyType, CultureInfo.InvariantCulture, out key);
    }

    private bool TryConvertJsValueToDictionaryValue(JsValue value, Type valueType, out object? converted)
    {
        // Pass the JsValue through only for an exact JsValue target. A broader IsAssignableFrom check
        // would also match Dictionary<_, object>, where callers expect the unwrapped CLR value.
        if (valueType == typeof(JsValue))
        {
            converted = value;
            return true;
        }

        var raw = value.ToObject();
        if (raw is null)
        {
            if (!valueType.IsValueType || Nullable.GetUnderlyingType(valueType) is not null)
            {
                converted = null;
                return true;
            }
            converted = null;
            return false;
        }

        if (valueType.IsInstanceOfType(raw))
        {
            converted = raw;
            return true;
        }

        return _engine.TypeConverter.TryConvert(raw, valueType, CultureInfo.InvariantCulture, out converted);
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        // check fast path before producing properties
        if (ReferenceEquals(receiver, this) && property.IsString())
        {
            // try some fast paths
            if (!_typeDescriptor.IsDictionary)
            {
                if (Target is ICollection c && CommonProperties.Length.Equals(property))
                {
                    var count = c.Count;
                    _engine.CheckAmortizedConstraintsAtHostBoundary();
                    return JsNumber.Create(count);
                }
            }
            else
            {
                if (_typeDescriptor.IsStringKeyedGenericDictionary)
                {
                    var found = _typeDescriptor.TryGetDictionaryValue(Target, property.ToString(), out var value);
                    if (found)
                    {
                        // the miss path needs no check here: it falls through to GetOwnProperty,
                        // whose dictionary lane re-probes and checks
                        _engine.CheckAmortizedConstraintsAtHostBoundary();
                        // Check stored properties first - frozen/sealed objects have descriptors in _properties
                        // that must be respected to return the same (frozen) instance
                        if (TryGetProperty(property, out var stored))
                        {
                            return UnwrapJsValue(stored, receiver);
                        }

                        return FromObject(_engine, value);
                    }
                }
            }
        }
        else if (ReferenceEquals(receiver, this)
            && _typeDescriptor.IsNonStringKeyedGenericDictionary
            && !property.IsSymbol()
            && !property.IsString()
            && TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
        {
            // Prototype chain is intentionally skipped on miss: non-string non-symbol keys can't
            // resolve to Object.prototype members (which are all string/symbol-keyed).
            var found = _typeDescriptor.TryGetDictionaryValue(Target, clrKey!, out var raw);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            return found ? FromObject(_engine, raw) : Undefined;
        }

        // slow path requires us to create a property descriptor that might get cached or not
        // suppress ThrowOnUnresolvedMember here so we can fall back to the prototype chain
        // (e.g. valueOf/toString from Object.prototype during implicit coercion)
        var desc = GetOwnProperty(property, mustBeReadable: true, mustBeWritable: false, throwOnError: false);
        if (desc != PropertyDescriptor.Undefined)
        {
            return UnwrapJsValue(desc, receiver);
        }

        var protoResult = Prototype?.Get(property, receiver) ?? Undefined;
        if (protoResult.IsUndefined()
            && property is JsString
            && !_typeDescriptor.IsDictionary
            && _engine.Options.Interop.ThrowOnUnresolvedMember)
        {
            throw new MissingMemberException($"Cannot access property '{property}' on type '{ClrType.FullName}");
        }

        return protoResult;
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        return [.. EnumerateOwnPropertyKeys(types)];
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        foreach (var key in EnumerateOwnPropertyKeys(Types.String | Types.Symbol))
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(key, GetOwnProperty(key));
        }
    }

    private IEnumerable<JsValue> EnumerateOwnPropertyKeys(Types types)
    {
        // prefer object order, add possible other properties after
        var includeStrings = (types & Types.String) != Types.Empty;

        if (includeStrings)
        {
            var customKeys = _engine.Options.Interop.ObjectWrapperReportedPropertyKeys(_engine, Target);
            if (customKeys is not null)
            {
                // each step pulls from the user-supplied sequence
                foreach (var key in customKeys)
                {
                    _engine.CheckAmortizedConstraintsAtHostBoundary();
                    yield return key;
                }
                yield break; // non-null replaces the default key set
            }
        }

        if (includeStrings && _typeDescriptor.IsStringKeyedGenericDictionary) // expando object for instance
        {
            var keys = (ICollection<string>) _typeDescriptor.KeysAccessor!.GetValue(Target)!;
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            // each step pulls from the user dictionary's key enumerator
            foreach (var key in keys)
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                yield return JsString.Create(key);
            }
        }
        else if (includeStrings && Target is IDictionary dictionary)
        {
            // we take values exposed as dictionary keys only; each step pulls from the user enumerator
            foreach (var key in dictionary.Keys)
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                object? stringKey = key as string;
                if (stringKey is not null
                    || _engine.TypeConverter.TryConvert(key, typeof(string), CultureInfo.InvariantCulture, out stringKey))
                {
                    yield return JsString.Create((string) stringKey!);
                }
            }
        }
        else if (includeStrings)
        {
            var interopOptions = _engine.Options.Interop;

            // we take properties, fields and methods
            if ((interopOptions.ObjectWrapperReportedMemberTypes & MemberTypes.Property) == MemberTypes.Property)
            {
                foreach (var p in ClrType.GetProperties(interopOptions.ObjectWrapperReportedPropertyBindingFlags))
                {
                    if (!interopOptions.TypeResolver.Filter(_engine, ClrType, p))
                    {
                        continue;
                    }

                    var indexParameters = p.GetIndexParameters();
                    if (indexParameters.Length == 0)
                    {
                        yield return JsString.Create(p.Name);
                    }
                }
            }

            if ((interopOptions.ObjectWrapperReportedMemberTypes & MemberTypes.Field) == MemberTypes.Field)
            {
                foreach (var f in ClrType.GetFields(interopOptions.ObjectWrapperReportedFieldBindingFlags))
                {
                    if (!interopOptions.TypeResolver.Filter(_engine, ClrType, f))
                    {
                        continue;
                    }

                    yield return JsString.Create(f.Name);
                }
            }

            if ((interopOptions.ObjectWrapperReportedMemberTypes & MemberTypes.Method) == MemberTypes.Method)
            {
                foreach (var m in ClrType.GetMethods(interopOptions.ObjectWrapperReportedMethodBindingFlags))
                {
                    // we won't report anything from base object as it would usually not be something to expect from JS perspective
                    if (m.DeclaringType == typeof(object) || m.IsSpecialName || !interopOptions.TypeResolver.Filter(_engine, ClrType, m))
                    {
                        continue;
                    }

                    yield return JsString.Create(m.Name);
                }
            }
        }
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        // we do not know if we need to read or write
        return GetOwnProperty(property, mustBeReadable: false, mustBeWritable: false);
    }

    private PropertyDescriptor GetOwnProperty(JsValue property, bool mustBeReadable, bool mustBeWritable, bool throwOnError = true)
    {
        if (TryGetProperty(property, out var x))
        {
            return x;
        }

        if (_lengthPropertyPending && CommonProperties.Length.Equals(property))
        {
            return MaterializeLengthProperty();
        }

        // if we have array-like or dictionary or expando, we can provide iterator
        if (property.IsSymbol())
        {
            if (property == GlobalSymbolRegistry.Iterator && _typeDescriptor.Iterable)
            {
                var iteratorFunction = new ClrFunction(
                    Engine,
                    "iterator",
                    Iterator,
                    1,
                    PropertyFlag.Configurable);

                var iteratorProperty = new PropertyDescriptor(iteratorFunction, PropertyFlag.Configurable | PropertyFlag.Writable);
                SetProperty(GlobalSymbolRegistry.Iterator, iteratorProperty);
                return iteratorProperty;
            }

            // not that safe
            return PropertyDescriptor.Undefined;
        }

        if (!property.IsString() && _typeDescriptor.IsNonStringKeyedGenericDictionary)
        {
            // non-string-keyed CLR generic dictionary — resolve via underlying CLR key, not string
            if (TryConvertJsValueToDictionaryKey(property, _typeDescriptor.GenericDictionaryKeyType!, out var clrKey))
            {
                var found = _typeDescriptor.TryGetDictionaryValue(Target, clrKey!, out var raw);
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                if (found)
                {
                    var flags = PropertyFlag.Enumerable;
                    if (_engine.Options.Interop.AllowWrite)
                    {
                        flags |= PropertyFlag.Configurable;
                    }
                    return new PropertyDescriptor(FromObject(_engine, raw), flags);
                }
            }
            return PropertyDescriptor.Undefined;
        }

        var member = property.ToString();

        // if type is dictionary, we cannot enumerate anything other than keys
        // and we cannot store accessors as dictionary can change dynamically

        var isDictionary = _typeDescriptor.IsStringKeyedGenericDictionary;
        if (isDictionary)
        {
            var found = _typeDescriptor.TryGetDictionaryValue(Target, member, out var value);
            _engine.CheckAmortizedConstraintsAtHostBoundary();
            if (found)
            {
                var flags = PropertyFlag.Enumerable;
                if (_engine.Options.Interop.AllowWrite)
                {
                    flags |= PropertyFlag.Configurable;
                }
                return new PropertyDescriptor(FromObject(_engine, value), flags);
            }
        }

        if (!isDictionary
            && _engine.Options.Interop.PreferJsPrototypeMethods
            && _prototype is not null
            && !ReferenceEquals(_prototype, _engine.Realm.Intrinsics.Object.PrototypeObject)
            && _prototype.Get(property, this) is { } protoValue
            && protoValue.IsCallable)
        {
            // Let outer Get fall through to the attached prototype (Array.prototype, etc.)
            // rather than dispatching to a same-named CLR method whose semantics may differ.
            return PropertyDescriptor.Undefined;
        }

        var result = Engine.Options.Interop.MemberAccessor(Engine, Target, member);
        Engine.CheckAmortizedConstraintsAtHostBoundary();
        if (result is not null)
        {
            return new PropertyDescriptor(result, PropertyFlag.OnlyEnumerable);
        }

        var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member, mustBeReadable, mustBeWritable, throwOnError);
        var actualType = Target.GetType();
        if (ClrType != actualType)
        {
            // When the declared type differs from the actual runtime type:
            // - If no accessor was found, fall back to the runtime type (original behavior)
            // - If only an indexer was found, check if the runtime type has a direct property/field/method
            //   that should take precedence over the indexer
            if (accessor == ConstantValueAccessor.NullAccessor)
            {
                accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, actualType, member, mustBeReadable, mustBeWritable, throwOnError);
            }
            else if (accessor is IndexerAccessor)
            {
                var runtimeAccessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, actualType, member, mustBeReadable, mustBeWritable, throwOnError);
                if (runtimeAccessor is not IndexerAccessor && runtimeAccessor != ConstantValueAccessor.NullAccessor)
                {
                    // Prefer direct property/field/method from runtime type over indexer from declared type
                    accessor = runtimeAccessor;
                }
            }
        }
        var descriptor = accessor.CreatePropertyDescriptor(_engine, Target, member, enumerable: !isDictionary);
        if (!isDictionary
            && !ReferenceEquals(descriptor, PropertyDescriptor.Undefined)
            && (!mustBeReadable || accessor.Readable)
            && (!mustBeWritable || accessor.Writable))
        {
            // cache the accessor for faster subsequent accesses
            SetProperty(member, descriptor);
        }

        return descriptor;
    }

    /// <summary>
    /// Per-AST-node inline-cache support (see the wrapper member lane in
    /// <see cref="Runtime.Interpreter.Expressions.JintMemberExpression"/>): returns the already-stored own
    /// descriptor a member read/write would consult, when — and only when — caching it per call site is
    /// sound. The result is exactly what <see cref="Get"/> / <see cref="Set"/> resolve through
    /// <c>TryGetProperty</c> once <see cref="GetOwnProperty(JsValue)"/> has stored the member (a live
    /// <see cref="Descriptors.Specialized.ReflectionDescriptor"/> for CLR properties/fields, so every use
    /// still flows through the CLR accessors), making a receiver-identity + <c>_propertiesVersion</c>
    /// guard sufficient: any define/redefine/delete on the wrapper bumps the version and invalidates.
    /// </summary>
    /// <remarks>
    /// Bails with <c>null</c> when:
    /// <list type="bullet">
    /// <item>the receiver is a subclass (e.g. <see cref="ArrayLikeWrapper"/> overrides Get/Set with its own semantics),</item>
    /// <item>the target is a dictionary (member values are dynamic; descriptors are created fresh per access),</item>
    /// <item>a custom <see cref="Options.InteropOptions.MemberAccessor"/> is configured (stay conservative),</item>
    /// <item>the name is <c>length</c> on an <see cref="ICollection"/> target (<see cref="Get"/> serves the live count ahead of any stored descriptor),</item>
    /// <item>the member has not been resolved-and-stored yet — the caller's slow path performs the store (bumping the version) so a later populate succeeds.</item>
    /// </list>
    /// </remarks>
    internal PropertyDescriptor? TryGetInlineCacheableDescriptor(JsString property)
    {
        if (GetType() != typeof(ObjectWrapper)
            || _typeDescriptor.IsDictionary
            || _properties is null
            || !ReferenceEquals(_engine.Options.Interop.MemberAccessor, Options.InteropOptions._defaultMemberAccessor))
        {
            return null;
        }

        if (Target is ICollection && CommonProperties.Length.Equals(property))
        {
            return null;
        }

        if (!_properties.TryGetValue(property.ToString(), out var descriptor)
            || ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            return null;
        }

        return descriptor;
    }

    // need to be public for advanced cases like RavenDB yielding properties from CLR objects
    public static PropertyDescriptor GetPropertyDescriptor(Engine engine, object target, MemberInfo member)
    {
        // fast path which uses slow search if not found for some reason
        ReflectionAccessor? Factory()
        {
            return member switch
            {
                PropertyInfo pi => new PropertyAccessor(pi),
                MethodBase mb => new MethodAccessor(target.GetType(), MethodDescriptor.Build(new[] { mb })),
                FieldInfo fi => new FieldAccessor(fi),
                _ => null
            };
        }

        var accessor = engine.Options.Interop.TypeResolver.GetAccessor(engine, target.GetType(), member.Name, mustBeReadable: false, mustBeWritable: false, accessorFactory: Factory);
        return accessor.CreatePropertyDescriptor(engine, target, member.Name);
    }

    internal static Type GetClrType(object obj, Type? type)
    {
        if (type is null || type == typeof(object))
        {
            return obj.GetType();
        }

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            return underlyingType;
        }

        return type;
    }

    private static JsValue Iterator(JsValue thisObject, JsCallArguments arguments)
    {
        var wrapper = UnwrapReceiver(thisObject, "[Symbol.iterator]");

        return wrapper._typeDescriptor.IsDictionary
            ? new DictionaryIterator(wrapper._engine, wrapper)
            : new EnumerableIterator(wrapper._engine, (IEnumerable) wrapper.Target);
    }

    private GetSetPropertyDescriptor MaterializeLengthProperty()
    {
        _lengthPropertyPending = false;
        // create a forwarder to produce length from Count or Length if one of them is present
        var functionInstance = new ClrFunction(_engine, "length", GetLength);
        var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
        SetProperty(KnownKeys.Length, descriptor);
        return descriptor;
    }

    private static JsNumber GetLength(JsValue thisObject, JsCallArguments arguments)
    {
        var wrapper = UnwrapReceiver(thisObject, "length");
        return JsNumber.Create((int) (wrapper._typeDescriptor.LengthProperty?.GetValue(wrapper.Target) ?? 0));
    }

    /// <summary>
    /// Resolves the <see cref="ObjectWrapper"/> receiver for the host helper functions above (the
    /// Symbol.iterator implementation and the length getter). Script code can extract these functions
    /// and re-target them (e.g. <c>f.call({})</c>) or invoke them through a (possibly revoked) Proxy,
    /// so a foreign or revoked receiver must surface as a JavaScript TypeError instead of a CLR crash.
    /// </summary>
    private static ObjectWrapper UnwrapReceiver(JsValue thisObject, string functionName)
    {
        var current = thisObject;
        while (current is JsProxy proxy)
        {
            if (proxy.IsRevoked)
            {
                Throw.TypeError(proxy.Engine.Realm, $"Cannot perform '{functionName}' on a proxy that has been revoked");
            }

            current = proxy._target;
        }

        if (current is ObjectWrapper wrapper)
        {
            return wrapper;
        }

        var message = $"Method '{functionName}' called on incompatible receiver";
        if (current is ObjectInstance objectInstance)
        {
            Throw.TypeError(objectInstance.Engine.Realm, message);
        }

        // primitive receiver, no engine reachable - converted to a JS TypeError by the interpreter
        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    internal override ulong GetSmallestIndex(ulong length)
    {
        return Target is ICollection ? 0 : base.GetSmallestIndex(length);
    }

    public override bool Equals(object? obj) => Equals(obj as ObjectWrapper);

    public override bool Equals(JsValue? other) => Equals(other as ObjectWrapper);

    public bool Equals(ObjectWrapper? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Equals(Target, other.Target);
    }

    public override int GetHashCode() => Target.GetHashCode();

    private sealed class DictionaryIterator : IteratorInstance
    {
        private readonly ObjectWrapper _target;
        private readonly IEnumerator<JsValue> _enumerator;

        public DictionaryIterator(Engine engine, ObjectWrapper target) : base(engine)
        {
            _target = target;
            _enumerator = target.EnumerateOwnPropertyKeys(Types.String).GetEnumerator();
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var hasNext = _enumerator.MoveNext();
            try
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            catch
            {
                // for-of only starts closing the iterator record after the first successful
                // step, so dispose the user's enumerator here or it would leak
                _enumerator.Dispose();
                throw;
            }

            if (hasNext)
            {
                var key = _enumerator.Current;
                var value = _target.Get(key);

                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine, key, value);
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    private sealed class EnumerableIterator : IteratorInstance
    {
        private readonly IEnumerator _enumerator;

        public EnumerableIterator(Engine engine, IEnumerable target) : base(engine)
        {
            _enumerator = target.GetEnumerator();
        }

        public override void Close(CompletionType completion)
        {
            (_enumerator as IDisposable)?.Dispose();
            base.Close(completion);
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var hasNext = _enumerator.MoveNext();
            try
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            catch
            {
                // for-of only starts closing the iterator record after the first successful
                // step, so dispose the user's enumerator here or it would leak
                Close(CompletionType.Throw);
                throw;
            }

            if (hasNext)
            {
                var value = _enumerator.Current;
                nextItem = IteratorResult.CreateValueIteratorPosition(_engine, FromObject(_engine, value));
                return true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
