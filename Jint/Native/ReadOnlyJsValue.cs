using System;
using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native
{
    internal class ReadOnlyJsValue
    {
        public static JsValue Create(JsValue inner)
        {
            // immutable types are OK to return as-is
            switch (inner.Type)
            {
                case Types.Undefined:
                case Types.Null:
                case Types.Boolean:
                case Types.String:
                case Types.Number:
                case Types.Symbol:
                    return inner;
                case Types.Object:
                    return new ReadOnlyObject((ObjectInstance) inner);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private sealed class ReadOnlyObject : ObjectInstance, ICallable
        {
            private readonly ObjectInstance _inner;

            public ReadOnlyObject(ObjectInstance inner) : base(inner.Engine, inner.Class, inner._type)
            {
                _inner = inner;
            }

            public override bool Set(JsValue property, JsValue value, JsValue receiver)
            {
                return ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            public override bool Extensible => false;

            public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
            {
                return _inner.GetOwnProperties();
            }

            public override List<JsValue> GetOwnPropertyKeys(Types types = Types.None | Types.String | Types.Symbol)
            {
                return _inner.GetOwnPropertyKeys(types);
            }

            protected override void AddProperty(JsValue property, PropertyDescriptor descriptor)
            {
                ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            protected internal override bool TryGetProperty(JsValue property, out PropertyDescriptor descriptor)
            {
                return _inner.TryGetProperty(property, out descriptor);
            }

            public override void RemoveOwnProperty(JsValue property)
            {
                ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            public override PropertyDescriptor GetOwnProperty(JsValue property)
            {
                return _inner.GetOwnProperty(property);
            }

            protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
            {
                ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            public override bool HasProperty(JsValue property) => _inner.HasProperty(property);

            public override bool Delete(JsValue property) => ExceptionHelper.ThrowNotSupportedException<bool>();

            public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
            {
                return ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            protected internal override void Initialize() => _inner.Initialize();

            internal override bool FindWithCallback(JsValue[] arguments, out uint index, out JsValue value, bool visitUnassigned)
            {
                return _inner.FindWithCallback(arguments, out index, out value, visitUnassigned);
            }

            public override bool IsArrayLike => _inner.IsArrayLike;
            public override uint Length => _inner.Length;

            public override JsValue PreventExtensions()
            {
                return ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            protected internal override ObjectInstance GetPrototypeOf() => (ObjectInstance) Create(_inner.GetPrototypeOf());

            public override bool SetPrototypeOf(JsValue value)
            {
                return ExceptionHelper.ThrowNotSupportedException<bool>();
            }

            public override bool IsArray() => _inner.IsArray();

            internal override bool IsConstructor => _inner.IsConstructor;

            public override bool HasOwnProperty(JsValue property) => _inner.HasOwnProperty(property);

            public override JsValue Get(JsValue property, JsValue receiver) => _inner.Get(property, receiver);

            public override string ToString() => _inner.ToString();

            public override bool Equals(object obj) => _inner.Equals(obj);

            public override int GetHashCode() => _inner.GetHashCode();

            internal override JsValue DoClone() => Create(_inner.DoClone());

            internal override bool IsCallable => _inner.IsCallable;

            public override object ToObject() => _inner.ToObject();

            public override bool Equals(JsValue other) => _inner.Equals(other);

            public JsValue Call(JsValue thisObject, JsValue[] arguments)
            {
                if (!(_inner is ICallable callable))
                {
                    ExceptionHelper.ThrowTypeError(_inner.Engine);
                    return null;
                }

                return callable.Call(thisObject, arguments);
            }
        }
    }
}