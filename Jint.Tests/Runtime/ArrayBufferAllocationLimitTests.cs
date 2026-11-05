#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A byte length the CLR cannot allocate is a <c>RangeError</c> in every path that creates a data block, from
/// <c>Array.MaxLength</c> + 1 (2147483592) up, not only from <c>2 ** 31</c> (issue #4433).
/// </summary>
public class ArrayBufferAllocationLimitTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// The constructor name of whatever <paramref name="source"/> throws, or <c>did not throw</c>. Written in
    /// script so that a CLR exception leaking out of the engine fails the test instead of being caught.
    /// </summary>
    private string ThrownErrorName(string source) => _engine
        .Evaluate($"(function () {{ try {{ {source}; return 'did not throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
        .AsString();

    [TestCase("new ArrayBuffer({0})")]
    [TestCase("new SharedArrayBuffer({0})")]
    [TestCase("new SharedArrayBuffer(0, {{ maxByteLength: {0} }})")]
    [TestCase("new Uint8Array({0})")]
    [TestCase("new ArrayBuffer(0, {{ maxByteLength: {0} }}).resize({0})")]
    [TestCase("new ArrayBuffer(0).transfer({0})")]
    [TestCase("new ArrayBuffer(0).transferToFixedLength({0})")]
    public void ALengthTheClrCannotAllocateIsARangeError(string template)
    {
        foreach (var length in new[] { "2147483592", "2 ** 31 - 1", "2 ** 31" })
        {
            ThrownErrorName(string.Format(template, length)).Should().Be("RangeError", length);
        }
    }

    /// <summary>
    /// AllocateArrayBuffer step 9.a: a resizable buffer whose <c>maxByteLength</c> could never be allocated is a
    /// RangeError when it is constructed, not first at the <c>resize</c> that reaches it.
    /// </summary>
    [TestCase("2147483592")]
    [TestCase("2 ** 31 - 1")]
    [TestCase("2 ** 31")]
    [TestCase("2 ** 53 - 1")]
    public void AResizableBufferWhoseMaxByteLengthTheClrCannotAllocateIsARangeErrorAtConstruction(string maxByteLength)
    {
        ThrownErrorName($"new ArrayBuffer(0, {{ maxByteLength: {maxByteLength} }})").Should().Be("RangeError");
        ThrownErrorName($"new ArrayBuffer(8, {{ maxByteLength: {maxByteLength} }})").Should().Be("RangeError");
    }

    /// <summary>
    /// The largest <c>maxByteLength</c> the CLR can allocate is still accepted, and it is only a limit: nothing
    /// that large is allocated until a resize asks for it.
    /// </summary>
    [Test]
    public void TheLargestAllocatableMaxByteLengthIsStillAccepted()
    {
        _engine.Evaluate("""
            var b = new ArrayBuffer(8, { maxByteLength: 2147483591 });
            b.resize(16);
            [b.maxByteLength, b.byteLength, b.resizable].join()
            """).AsString().Should().Be("2147483591,16,true");
    }
}
