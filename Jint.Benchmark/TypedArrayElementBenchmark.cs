using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Exercises the <c>JsArrayBuffer</c> element read/write hot path (typed-array indexed set/get and
/// DataView set/get) that routes through <c>SetValueInBuffer</c> / <c>RawBytesToNumeric</c>. The
/// integer/bigint write path formerly allocated a scratch <c>byte[]</c> per element via
/// <c>BitConverter.GetBytes</c>; the BinaryPrimitives rewrite writes straight into the buffer, so the
/// Allocated column is the primary signal here.
/// </summary>
[MemoryDiagnoser]
public class TypedArrayElementBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _intWriteRead;
    private Prepared<Script> _floatWriteRead;
    private Prepared<Script> _dataViewMixed;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(@"
var i16 = new Int16Array(1024), u16 = new Uint16Array(1024);
var i32 = new Int32Array(1024), u32 = new Uint32Array(1024);
var b64 = new BigInt64Array(1024);
var f64 = new Float64Array(1024), f32 = new Float32Array(1024);
var dv = new DataView(new ArrayBuffer(4096));");

        // Integer typed-array element writes then reads — the allocation-sensitive path.
        _intWriteRead = Engine.PrepareScript(@"
var s = 0;
for (var i = 0; i < 1024; i++) { i16[i] = i - 300; u16[i] = i; i32[i] = i * 40000; u32[i] = i * 40000; b64[i] = BigInt(i); }
for (var i = 0; i < 1024; i++) { s += i16[i] + u16[i] + i32[i] + u32[i] + Number(b64[i]); }
s;");

        _floatWriteRead = Engine.PrepareScript(@"
var s = 0;
for (var i = 0; i < 1024; i++) { f64[i] = i * 1.5; f32[i] = i * 0.25; }
for (var i = 0; i < 1024; i++) { s += f64[i] + f32[i]; }
s;");

        // DataView with explicit endianness (little and big), covering the reverse path.
        _dataViewMixed = Engine.PrepareScript(@"
var s = 0;
for (var i = 0; i < 512; i++) {
  dv.setInt16(0, i - 300, true); s += dv.getInt16(0, false);
  dv.setInt32(0, i * 40000, false); s += dv.getInt32(0, true);
  dv.setFloat64(0, i * 1.5, true); s += dv.getFloat64(0, false);
  dv.setBigInt64(0, BigInt(i), false); s += Number(dv.getBigInt64(0, true));
}
s;");
    }

    [Benchmark]
    public void IntWriteRead() => _engine.Evaluate(_intWriteRead);

    [Benchmark]
    public void FloatWriteRead() => _engine.Evaluate(_floatWriteRead);

    [Benchmark]
    public void DataViewMixed() => _engine.Evaluate(_dataViewMixed);
}
