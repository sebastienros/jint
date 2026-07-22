namespace Jint.Tests.Runtime;

public class TypedArrayInteropTests
{
    [Fact]
    public void CanInteropWithInt8()
    {
        var engine = new Engine();
        var source = new sbyte[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Int8Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Int8Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsInt8Array().Should().BeTrue();
        fromEngine.AsInt8Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithUint8()
    {
        var engine = new Engine();
        var source = new byte[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint8Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Uint8Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsUint8Array().Should().BeTrue();
        fromEngine.AsUint8Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithUint8Clamped()
    {
        var engine = new Engine();
        var source = new byte[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint8ClampedArray.Construct(source));
        ValidateCreatedTypeArray(engine, "Uint8ClampedArray");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsUint8ClampedArray().Should().BeTrue();
        fromEngine.AsUint8ClampedArray().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithInt16()
    {
        var engine = new Engine();
        var source = new short[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Int16Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Int16Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsInt16Array().Should().BeTrue();
        fromEngine.AsInt16Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithUint16()
    {
        var engine = new Engine();
        var source = new ushort[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint16Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Uint16Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsUint16Array().Should().BeTrue();
        fromEngine.AsUint16Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithInt32()
    {
        var engine = new Engine();
        var source = new[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Int32Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Int32Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsInt32Array().Should().BeTrue();
        fromEngine.AsInt32Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithUint32()
    {
        var engine = new Engine();
        var source = new uint[] { 42, 12 };

        engine.SetValue("testSubject", engine.Realm.Intrinsics.Uint32Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Uint32Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsUint32Array().Should().BeTrue();
        fromEngine.AsUint32Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithBigInt64()
    {
        var engine = new Engine();
        var source = new long[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.BigInt64Array.Construct(source));
        ValidateCreatedBigIntegerTypeArray(engine, "BigInt64Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsBigInt64Array().Should().BeTrue();
        fromEngine.AsBigInt64Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithBigUint64()
    {
        var engine = new Engine();
        var source = new ulong[] { 42, 12 };
        engine.SetValue("testSubject", engine.Realm.Intrinsics.BigUint64Array.Construct(source));
        ValidateCreatedBigIntegerTypeArray(engine, "BigUint64Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsBigUint64Array().Should().BeTrue();
        fromEngine.AsBigUint64Array().Should().Equal(source);
    }

#if NET6_0_OR_GREATER
        [Fact]
        public void CanInteropWithFloat16()
        {
            var engine = new Engine();
            var source = new[] { (Half) 42, (Half) 12 };
            
            engine.SetValue("testSubject", engine.Realm.Intrinsics.Float16Array.Construct(source));
            ValidateCreatedTypeArray(engine, "Float16Array");
            
            var fromEngine = engine.GetValue("testSubject");
            fromEngine.IsFloat16Array().Should().BeTrue();
            fromEngine.AsFloat16Array().Should().Equal(source);

            engine.SetValue("testFunc", new Func<Native.JsTypedArray, Native.JsTypedArray>(v => v));
            engine.Evaluate("testFunc(testSubject)").AsFloat16Array().Should().Equal(source);
        }
#endif

    [Fact]
    public void CanInteropWithFloat32()
    {
        var engine = new Engine();
        var source = new float[] { 42f, 12f };

        engine.SetValue("testSubject", engine.Realm.Intrinsics.Float32Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Float32Array");

        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsFloat32Array().Should().BeTrue();
        fromEngine.AsFloat32Array().Should().Equal(source);
    }

    [Fact]
    public void CanInteropWithFloat64()
    {
        var engine = new Engine();
        var source = new double[] { 42f, 12f };
            
        engine.SetValue("testSubject", engine.Realm.Intrinsics.Float64Array.Construct(source));
        ValidateCreatedTypeArray(engine, "Float64Array");
            
        var fromEngine = engine.GetValue("testSubject");
        fromEngine.IsFloat64Array().Should().BeTrue();
        fromEngine.AsFloat64Array().Should().Equal(source);
    }
        
    private static void ValidateCreatedTypeArray(Engine engine, string arrayName)
    {
        engine.Evaluate("testSubject.constructor.name").AsString().Should().Be(arrayName);
        engine.Evaluate("testSubject.length").AsNumber().Should().Be(2);
        engine.Evaluate("testSubject[0]").AsNumber().Should().Be(42);
        engine.Evaluate("testSubject[1]").AsNumber().Should().Be(12);
    }

    private static void ValidateCreatedBigIntegerTypeArray(Engine engine, string arrayName)
    {
        engine.Evaluate("testSubject.constructor.name").AsString().Should().Be(arrayName);
        engine.Evaluate("testSubject.length").AsNumber().Should().Be(2);
        engine.Evaluate("testSubject[0]").AsBigInt().Should().Be(42);
        engine.Evaluate("testSubject[1]").AsBigInt().Should().Be(12);
    }
}
