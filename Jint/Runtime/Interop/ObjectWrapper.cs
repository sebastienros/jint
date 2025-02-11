using System.Collections;
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

    internal ObjectWrapper(
        Engine engine,
        object obj,
        Type? type = null)
        : base(engine)
    {
        Target = obj;
        ClrType = GetClrType(obj, type);
        _typeDescriptor = TypeDescriptor.Get(ClrType);

        if (_typeDescriptor.LengthProperty is not null)
        {
            // create a forwarder to produce length from Count or Length if one of them is present
            var functionInstance = new ClrFunction(engine, "length", GetLength);
            var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
            SetProperty(KnownKeys.Length, descriptor);

            if (_typeDescriptor.IsArrayLike && engine.Options.Interop.AttachArrayPrototype)
            {
                // if we have array-like object, we can attach array prototype
                _prototype = engine.Intrinsics.Array.PrototypeObject;
            }
        }
    }

    /// <summary>
    /// Creates a new object wrapper for given object instance and exposed type.
    /// </summary>
    public static ObjectInstance Create(Engine engine, object target, Type? type = null)
    {
        if (target == null)
        {
            ExceptionHelper.ThrowArgumentNullException(nameof(target));
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

    private static bool TryBuildArrayLikeWrapper(
        Engine engine,
        object target,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type,
        [NotNullWhen(true)] out ArrayLikeWrapper? result)
    {
#pragma warning disable IL2055
#pragma warning disable IL3050

        result = null;

        // check for generic interfaces
        foreach (var i in type.GetInterfaces())
        {
            if (!i.IsGenericType)
            {
                continue;
            }

            var arrayItemType = i.GenericTypeArguments[0];

            if (i.GetGenericTypeDefinition() == typeof(IList<>))
            {
                var arrayWrapperType = typeof(GenericListWrapper<>).MakeGenericType(arrayItemType);
                result = (ArrayLikeWrapper) Activator.CreateInstance(arrayWrapperType, engine, target, type)!;
                break;
            }

            if (i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var arrayWrapperType = typeof(ReadOnlyListWrapper<>).MakeGenericType(arrayItemType);
                result = (ArrayLikeWrapper) Activator.CreateInstance(arrayWrapperType, engine, target, type)!;
                break;
            }
        }

#pragma warning restore IL3050
#pragma warning restore IL2055

        // least specific
        if (result is null && target is IList list)
        {
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
            if (_properties is null || !_properties.ContainsKey(member))
            {
                // can try utilize fast path
                var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member, mustBeReadable: false, mustBeWritable: true);

                if (ReferenceEquals(accessor, ConstantValueAccessor.NullAccessor))
                {
                    // there's no such property, but we can allow extending by calling base
                    // which will add properties, this allows for example JS class to extend a CLR type
                    return base.Set(property, value, receiver);
                }

                // CanPut logic
                if (!accessor.Writable || !_engine.Options.Interop.AllowWrite)
                {
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

    public override object ToObject() => Target;

    public override void RemoveOwnProperty(JsValue property)
    {
        if (_engine.Options.Interop.AllowWrite && property is JsString jsString && _typeDescriptor.RemoveMethod is not null)
        {
            _typeDescriptor.RemoveMethod.Invoke(Target, [jsString.ToString()]);
        }
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (!_typeDescriptor.IsDictionary
            && Target is ICollection c
            && CommonProperties.Length.Equals(property))
        {
            return JsNumber.Create(c.Count);
        }

        var desc = GetOwnProperty(property, mustBeReadable: true, mustBeWritable: false);
        if (desc != PropertyDescriptor.Undefined)
        {
            return UnwrapJsValue(desc, receiver);
        }

        return Prototype?.Get(property, receiver) ?? Undefined;
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        return [..EnumerateOwnPropertyKeys(types)];
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
        if (includeStrings && _typeDescriptor.IsStringKeyedGenericDictionary) // expando object for instance
        {
            var keys = (ICollection<string>) _typeDescriptor.KeysAccessor!.GetValue(Target)!;
            foreach (var key in keys)
            {
                yield return JsString.Create(key);
            }
        }
        else if (includeStrings && Target is IDictionary dictionary)
        {
            // we take values exposed as dictionary keys only
            foreach (var key in dictionary.Keys)
            {
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

    private PropertyDescriptor GetOwnProperty(JsValue property, bool mustBeReadable, bool mustBeWritable)
    {
        if (TryGetProperty(property, out var x))
        {
            return x;
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

        var member = property.ToString();

        // if type is dictionary, we cannot enumerate anything other than keys
        // and we cannot store accessors as dictionary can change dynamically

        var isDictionary = _typeDescriptor.IsStringKeyedGenericDictionary;
        if (isDictionary)
        {
            if (_typeDescriptor.TryGetValue(Target, member, out var value))
            {
                var flags = PropertyFlag.Enumerable;
                if (_engine.Options.Interop.AllowWrite)
                {
                    flags |= PropertyFlag.Configurable;
                }
                return new PropertyDescriptor(FromObject(_engine, value), flags);
            }
        }

        var result = Engine.Options.Interop.MemberAccessor(Engine, Target, member);
        if (result is not null)
        {
            return new PropertyDescriptor(result, PropertyFlag.OnlyEnumerable);
        }

        var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member, mustBeReadable, mustBeWritable);
        if (accessor == ConstantValueAccessor.NullAccessor && ClrType != Target.GetType())
        {
            accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, Target.GetType(), member, mustBeReadable, mustBeWritable);
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

        var accessor = engine.Options.Interop.TypeResolver.GetAccessor(engine, target.GetType(), member.Name, mustBeReadable: false, mustBeWritable: false, Factory);
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
        var wrapper = (ObjectWrapper) thisObject;

        return wrapper._typeDescriptor.IsDictionary
            ? new DictionaryIterator(wrapper._engine, wrapper)
            : new EnumerableIterator(wrapper._engine, (IEnumerable) wrapper.Target);
    }

    private static JsNumber GetLength(JsValue thisObject, JsCallArguments arguments)
    {
        var wrapper = (ObjectWrapper) thisObject;
        return JsNumber.Create((int) (wrapper._typeDescriptor.LengthProperty?.GetValue(wrapper.Target) ?? 0));
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
            if (_enumerator.MoveNext())
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
            if (_enumerator.MoveNext())
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
