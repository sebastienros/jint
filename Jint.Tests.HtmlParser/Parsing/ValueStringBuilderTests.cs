#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Parsing;

public class ValueStringBuilderTests
{
    [TestCase(0)]
    [TestCase(127)]
    [TestCase(128)]
    [TestCase(129)]
    [TestCase(65536)]
    public void MaterializationOwnsTheStringAndResetsTheBuilder(int length)
    {
        var expected = new string('x', length);
        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            builder.Append(expected);
            Assert.That(builder.AsSpan().SequenceEqual(expected.AsSpan()), Is.True);
            var value = builder.ToString();
            builder.Length.Should().Be(0);
            builder.Capacity.Should().Be(0);

            builder.Append('y', length);
            builder.ToString().Should().Be(new string('y', length));
            value.Should().Be(expected);
        }
        finally { builder.Dispose(); }
    }

    [Test]
    public void ShortBuffersDoNotAllocateBeforeMaterialization()
    {
        BuildShortValue();
        var checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) checksum += BuildShortValue();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        checksum.Should().Be(6000);
    }

    private static int BuildShortValue()
    {
        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            builder.Append("hello");
            builder.AppendSpan(1)[0] = '!';
            return builder.Length;
        }
        finally { builder.Dispose(); }
    }
}
