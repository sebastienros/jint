using Jint.Runtime;

namespace Jint.Native.TypedArray
{
    public sealed class Int8ArrayConstructor : TypedArrayConstructor
    {
        internal Int8ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Int8)
        {
        }

        public TypedArrayInstance Construct(sbyte[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint8ArrayConstructor : TypedArrayConstructor
    {
        internal Uint8ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint8)
        {
        }

        public TypedArrayInstance Construct(byte[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint8ClampedArrayConstructor : TypedArrayConstructor
    {
        internal Uint8ClampedArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint8C)
        {
        }

        public TypedArrayInstance Construct(byte[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Int16ArrayConstructor : TypedArrayConstructor
    {
        internal Int16ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Int16)
        {
        }

        public TypedArrayInstance Construct(short[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint16ArrayConstructor : TypedArrayConstructor
    {
        internal Uint16ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint16)
        {
        }

        public TypedArrayInstance Construct(ushort[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Int32ArrayConstructor : TypedArrayConstructor
    {
        internal Int32ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Int32)
        {
        }

        public TypedArrayInstance Construct(int[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Uint32ArrayConstructor : TypedArrayConstructor
    {
        internal Uint32ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint32)
        {
        }

        public TypedArrayInstance Construct(uint[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Float32ArrayConstructor : TypedArrayConstructor
    {
        internal Float32ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Float32)
        {
        }

        public TypedArrayInstance Construct(float[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class Float64ArrayConstructor : TypedArrayConstructor
    {
        internal Float64ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Float64)
        {
        }

        public TypedArrayInstance Construct(double[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class BigInt64ArrayConstructor : TypedArrayConstructor
    {
        internal BigInt64ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.BigInt64)
        {
        }

        public TypedArrayInstance Construct(long[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }

    public sealed class BigUint64ArrayConstructor : TypedArrayConstructor
    {
        internal BigUint64ArrayConstructor(
            Engine engine,
            Realm realm,
            IntrinsicTypedArrayConstructor functionPrototype,
            IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.BigUint64)
        {
        }

        public TypedArrayInstance Construct(ulong[] values)
        {
            var array = (TypedArrayInstance) base.Construct(new JsValue[] { values.Length }, this);
            FillTypedArrayInstance(array, values);
            return array;
        }
    }
}