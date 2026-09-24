using Jint.Native;
using NUnit.Framework;

namespace Jint.Tests.PublicInterface;

public class HostBufferViewMetadataTests
{
    [TestCase("Uint8Array")]
    [TestCase("DataView")]
    public void ReportsConstructionModeAcrossResizeAndDetach(string constructor)
    {
        using var engine = new Engine();
        engine.Execute($$"""
            const buffer = new ArrayBuffer(8, { maxByteLength: 16 });
            const tracking = new {{constructor}}(buffer, 2);
            const fixedLength = new {{constructor}}(buffer, 2, 6);
            const empty = new {{constructor}}(buffer, 8, 0);
            """);
        var tracking = engine.Evaluate("tracking");
        var fixedLength = engine.Evaluate("fixedLength");
        var empty = engine.Evaluate("empty");
        foreach (var mutation in new[] { "", "buffer.resize(16)", "buffer.resize(1)", "buffer.resize(8)", "buffer.transfer()" })
        {
            engine.Execute(mutation);
            Assert.That(tracking.IsLengthTrackingArrayBufferView(), Is.True);
            Assert.That(fixedLength.IsLengthTrackingArrayBufferView(), Is.False);
            Assert.That(empty.IsLengthTrackingArrayBufferView(), Is.False);
        }
    }

    [TestCase("new Uint8Array(new ArrayBuffer(8))")]
    [TestCase("new DataView(new ArrayBuffer(8))")]
    [TestCase("new Proxy(new Uint8Array(new ArrayBuffer(8, {maxByteLength:16})), {})")]
    [TestCase("new Proxy(new DataView(new ArrayBuffer(8, {maxByteLength:16})), {})")]
    [TestCase("({get length(){throw new Error('getter')}, [Symbol.toStringTag]:'Uint8Array'})")]
    [TestCase("null")]
    public void RejectsNonTrackingValuesWithoutReadingTheirProperties(string source)
    {
        using var engine = new Engine();
        Assert.That(engine.Evaluate(source).IsLengthTrackingArrayBufferView(), Is.False);
    }

    [TestCase("Uint8Array")]
    [TestCase("DataView")]
    public void ReportsLengthTrackingForGrowableSharedBuffers(string constructor)
    {
        using var engine = new Engine();
        var value = engine.Evaluate($"new {constructor}(new SharedArrayBuffer(8, {{maxByteLength:16}}))");
        Assert.That(value.IsLengthTrackingArrayBufferView(), Is.True);
    }
}
