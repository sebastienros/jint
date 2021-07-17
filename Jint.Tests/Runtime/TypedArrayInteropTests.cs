using Xunit;

namespace Jint.Tests.Runtime
{
    public class TypedArrayInteropTests
    {
        [Fact]
        public void CanInteropWithInt8()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Int8Array.Construct(new sbyte[] { 42 }));
            ValidateCreatedTypeArray(engine, "Int8Array");
        }

        [Fact]
        public void CanInteropWithUint8()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint8Array.Construct(new byte[] { 42 }));
            ValidateCreatedTypeArray(engine, "Uint8Array");
        }

        [Fact]
        public void CanInteropWithUint8Clamped()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint8ClampedArray.Construct(new byte[] { 42 }));
            ValidateCreatedTypeArray(engine, "Uint8ClampedArray");
        }

        [Fact]
        public void CanInteropWithInt16()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Int16Array.Construct(new short[] { 42 }));
            ValidateCreatedTypeArray(engine, "Int16Array");
        }

        [Fact]
        public void CanInteropWithUint16()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint16Array.Construct(new ushort[] { 42 }));
            ValidateCreatedTypeArray(engine, "Uint16Array");
        }

        [Fact]
        public void CanInteropWithInt32()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Int32Array.Construct(new int[] { 42 }));
            ValidateCreatedTypeArray(engine, "Int32Array");
        }

        [Fact]
        public void CanInteropWithUint32()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint32Array.Construct(new uint[] { 42 }));
            ValidateCreatedTypeArray(engine, "Uint32Array");
        }

        [Fact(Skip = "BigInt not implemented")]
        public void CanInteropWithBigInt64()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.BigInt64Array.Construct(new long[] { 42 }));
            ValidateCreatedTypeArray(engine, "BigInt64Array");
        }

        [Fact(Skip = "BigInt not implemented")]
        public void CanInteropWithBigUint64()
        {
            var engine = new Engine();
            engine.SetValue("testSubject", engine.Realm.Intrinsics.BigUint64Array.Construct(new ulong[] { 42 }));
            ValidateCreatedTypeArray(engine, "BigUint64Array");
        }

        private static void ValidateCreatedTypeArray(Engine engine, string arrayName)
        {
            Assert.Equal(arrayName, engine.Evaluate("testSubject.constructor.name").AsString());
            Assert.Equal(1, engine.Evaluate("testSubject.length").AsNumber());
            Assert.Equal(42, engine.Evaluate("testSubject[0]").AsNumber());
        }
    }
}