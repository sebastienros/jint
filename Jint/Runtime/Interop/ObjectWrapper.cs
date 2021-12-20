using System;
using System.Collections;
using System.Collections.Generic;
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

        public ObjectWrapper(Engine engine, object obj)
            : base(engine)
        {
            Target = obj;
            _typeDescriptor = TypeDescriptor.Get(obj.GetType());
            if (_typeDescriptor.LengthProperty is not null)
            {
                // create a forwarder to produce length from Count or Length if one of them is present
                var functionInstance = new ClrFunctionInstance(engine, "length", GetLength);
                var descriptor = new GetSetPropertyDescriptor(functionInstance, Undefined, PropertyFlag.Configurable);
                SetProperty(KnownKeys.Length, descriptor);
            }
        }

        public object Target { get; }

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
                    var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, Target.GetType(), member);

                    // CanPut logic
                    if (!accessor.Writable || !_engine.Options.Interop.AllowWrite)
                    {
                        return false;
                    }

                    accessor.SetValue(_engine, Target, value);
                    return true;
                }
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

            if (ownDesc == null)
            {
                return false;
            }

            ownDesc.Value = value;
            return true;
        }

        public override object ToObject()
        {
            return Target;
        }

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (property.IsInteger() && Target is IList list)
            {
                var index = (int) ((JsNumber) property)._value;
                return (uint) index < list.Count ? FromObject(_engine, list[index]) : Undefined;
            }

            if (property.IsSymbol() && property != GlobalSymbolRegistry.Iterator)
            {
                // wrapped objects cannot have symbol properties
                return Undefined;
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
            var basePropertyKeys = base.GetOwnPropertyKeys(types);
            // prefer object order, add possible other properties after
            var processed = basePropertyKeys.Count > 0 ? new HashSet<JsValue>() : null;

            var includeStrings = (types & Types.String) != 0;
            if (includeStrings && _typeDescriptor.IsStringKeyedGenericDictionary) // expando object for instance
            {
                var keys = _typeDescriptor.GetKeys(Target);
                foreach (var key in keys)
                {
                    var jsString = JsString.Create(key);
                    processed?.Add(jsString);
                    yield return jsString;
                }
            }
            else if (includeStrings && Target is IDictionary dictionary)
            {
                // we take values exposed as dictionary keys only
                foreach (var key in dictionary.Keys)
                {
                    object stringKey = key as string;
                    if (stringKey is not null
                        || _engine.ClrTypeConverter.TryConvert(key, typeof(string), CultureInfo.InvariantCulture, out stringKey))
                    {
                        var jsString = JsString.Create((string) stringKey);
                        processed?.Add(jsString);
                        yield return jsString;
                    }
                }
            }
            else if (includeStrings)
            {
                // we take public properties and fields
                var type = Target.GetType();
                foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    var indexParameters = p.GetIndexParameters();
                    if (indexParameters.Length == 0)
                    {
                        var jsString = JsString.Create(p.Name);
                        processed?.Add(jsString);
                        yield return jsString;
                    }
                }

                foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    var jsString = JsString.Create(f.Name);
                    processed?.Add(jsString);
                    yield return jsString;
                }
            }

            if (processed != null)
            {
                // we have base keys
                for (var i = 0; i < basePropertyKeys.Count; i++)
                {
                    var key = basePropertyKeys[i];
                    if (processed.Add(key))
                    {
                        yield return key;
                    }
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
            if (property.IsSymbol() && property == GlobalSymbolRegistry.Iterator && _typeDescriptor.Iterable)
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

            var member = property.ToString();

            if (_typeDescriptor.IsStringKeyedGenericDictionary)
            {
                if (_typeDescriptor.TryGetValue(Target, member, out var value))
                {
                    return new PropertyDescriptor(FromObject(_engine, value), PropertyFlag.OnlyEnumerable);
                }
            }

            var result = Engine.Options.Interop.MemberAccessor(Engine, Target, member);
            if (result is not null)
            {
                return new PropertyDescriptor(result, PropertyFlag.OnlyEnumerable);
            }

            var accessor = _engine.Options.Interop.TypeResolver.GetAccessor(_engine, Target.GetType(), member);
            var descriptor = accessor.CreatePropertyDescriptor(_engine, Target);
            SetProperty(member, descriptor);
            return descriptor;
        }

        // need to be public for advanced cases like RavenDB yielding properties from CLR objects
        public static PropertyDescriptor GetPropertyDescriptor(Engine engine, object target, MemberInfo member)
        {
            // fast path which uses slow search if not found for some reason
            ReflectionAccessor Factory()
            {
                return member switch
                {
                    PropertyInfo pi => new PropertyAccessor(pi.Name, pi),
                    MethodBase mb => new MethodAccessor(MethodDescriptor.Build(new[] {mb})),
                    FieldInfo fi => new FieldAccessor(fi),
                    _ => null
                };
            }
            return engine.Options.Interop.TypeResolver.GetAccessor(engine, target.GetType(), member.Name, Factory).CreatePropertyDescriptor(engine, target);
        }

        private static JsValue Iterator(JsValue thisObj, JsValue[] arguments)
        {
            var wrapper = (ObjectWrapper) thisObj;

            return wrapper._typeDescriptor.IsDictionary
                ? new DictionaryIterator(wrapper._engine, wrapper)
                : new EnumerableIterator(wrapper._engine, (IEnumerable) wrapper.Target);
        }

        private static JsValue GetLength(JsValue thisObj, JsValue[] arguments)
        {
            var wrapper = (ObjectWrapper) thisObj;
            return JsNumber.Create((int) wrapper._typeDescriptor.LengthProperty.GetValue(wrapper.Target));
        }

        public override bool Equals(JsValue obj)
        {
            return Equals(obj as ObjectWrapper);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ObjectWrapper);
        }

        public bool Equals(ObjectWrapper other)
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

        public override int GetHashCode()
        {
            return Target?.GetHashCode() ?? 0;
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

                    nextItem = new KeyValueIteratorPosition(_engine, key, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done(_engine);
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

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_enumerator.MoveNext())
                {
                    var value = _enumerator.Current;
                    nextItem = new ValueIteratorPosition(_engine, FromObject(_engine, value));
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done(_engine);
                return false;
            }
        }
    }
}
