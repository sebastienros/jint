using System.Collections;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a CLR instance
    /// </summary>
    public sealed class ObjectWrapper : ObjectInstance, IObjectWrapper, IEquatable<ObjectWrapper>
    {
        private readonly TypeDescriptor _typeDescriptor;

        public ObjectWrapper(Engine engine, object obj, Type? type = null)
            : base(engine)
        {
            Target = obj;
            ClrType = GetClrType(obj, type);
            _typeDescriptor = TypeDescriptor.Get(ClrType);
            if (_typeDescriptor.LengthProperty is not null)
            {
                // create a forwarder to produce length from Count or Length if one of them is present
                var functionInstance = new ClrFunctionInstance(engine, "length", GetLength);
                var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
                SetProperty(KnownKeys.Length, descriptor);
            }
        }

        public object Target { get; }
        internal Type ClrType { get; }

        public override bool IsArrayLike => _typeDescriptor.IsArrayLike;

        internal override bool HasOriginalIterator => IsArrayLike;

        internal override bool IsIntegerIndexedArray => _typeDescriptor.IsIntegerIndexedArray;

        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            // check if we can take shortcuts for empty object, no need to generate properties
            if (property is JsString stringKey)
            {
                var member = stringKey.ToString();
                if (_properties is null || !_properties.ContainsKey(member))
                {
                    // can try utilize fast path
                    var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member, forWrite: true);

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

                    accessor.SetValue(_engine, Target, value);
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

        public override object ToObject()
        {
            return Target;
        }

        public override void RemoveOwnProperty(JsValue property)
        {
            if (_engine.Options.Interop.AllowWrite && property is JsString jsString)
            {
                _typeDescriptor.Remove(Target, jsString.ToString());
            }
        }

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (property.IsInteger() && Target is IList list)
            {
                var index = (int) ((JsNumber) property)._value;
                return (uint) index < list.Count ? FromObject(_engine, list[index]) : Undefined;
            }

            return base.Get(property, receiver);
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.None | Types.String | Types.Symbol)
        {
            return new List<JsValue>(EnumerateOwnPropertyKeys(types));
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
            var includeStrings = (types & Types.String) != 0;
            if (includeStrings && _typeDescriptor.IsStringKeyedGenericDictionary) // expando object for instance
            {
                var keys = _typeDescriptor.GetKeys(Target);
                foreach (var key in keys)
                {
                    var jsString = JsString.Create(key);
                    yield return jsString;
                }
            }
            else if (includeStrings && Target is IDictionary dictionary)
            {
                // we take values exposed as dictionary keys only
                foreach (var key in dictionary.Keys)
                {
                    object? stringKey = key as string;
                    if (stringKey is not null
                        || _engine.ClrTypeConverter.TryConvert(key, typeof(string), CultureInfo.InvariantCulture, out stringKey))
                    {
                        var jsString = JsString.Create((string) stringKey!);
                        yield return jsString;
                    }
                }
            }
            else if (includeStrings)
            {
                // we take public properties and fields
                var type = ClrType;
                foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    var indexParameters = p.GetIndexParameters();
                    if (indexParameters.Length == 0)
                    {
                        var jsString = JsString.Create(p.Name);
                        yield return jsString;
                    }
                }

                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    var jsString = JsString.Create(f.Name);
                    yield return jsString;
                }
            }
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
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
                    var iteratorFunction = new ClrFunctionInstance(
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

            var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, ClrType, member);
            var descriptor = accessor.CreatePropertyDescriptor(_engine, Target, enumerable: !isDictionary);
            if (!isDictionary && !ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
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
                    PropertyInfo pi => new PropertyAccessor(pi.Name, pi),
                    MethodBase mb => new MethodAccessor(target.GetType(), member.Name, MethodDescriptor.Build(new[] { mb })),
                    FieldInfo fi => new FieldAccessor(fi),
                    _ => null
                };
            }
            return engine.Options.Interop.TypeResolver.GetAccessor(engine, target.GetType(), member.Name, Factory).CreatePropertyDescriptor(engine, target);
        }

        internal static Type GetClrType(object obj, Type? type)
        {
            if (type is null || type == typeof(object))
            {
                return obj.GetType();
            }
            else
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                if (underlyingType is not null)
                {
                    return underlyingType;
                }
                else
                {
                    return type;
                }
            }
        }

        private static JsValue Iterator(JsValue thisObject, JsValue[] arguments)
        {
            var wrapper = (ObjectWrapper) thisObject;

            return wrapper._typeDescriptor.IsDictionary
                ? new DictionaryIterator(wrapper._engine, wrapper)
                : new EnumerableIterator(wrapper._engine, (IEnumerable) wrapper.Target);
        }

        private static JsValue GetLength(JsValue thisObject, JsValue[] arguments)
        {
            var wrapper = (ObjectWrapper) thisObject;
            return JsNumber.Create((int) (wrapper._typeDescriptor.LengthProperty?.GetValue(wrapper.Target) ?? 0));
        }

        internal override ulong GetSmallestIndex(ulong length)
        {
            return Target is ICollection ? 0 : base.GetSmallestIndex(length);
        }

        public override bool Equals(JsValue? obj)
        {
            return Equals(obj as ObjectWrapper);
        }

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

            return Equals(Target, other.Target) && Equals(ClrType, other.ClrType);
        }

        public override int GetHashCode()
        {
            var hashCode = -1468639730;
            hashCode = hashCode * -1521134295 + Target.GetHashCode();
            hashCode = hashCode * -1521134295 + ClrType.GetHashCode();
            return hashCode;
        }

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
}
