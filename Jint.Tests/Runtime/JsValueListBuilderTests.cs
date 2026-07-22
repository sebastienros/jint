using Jint.Native;
using Jint.Pooling;

namespace Jint.Tests.Runtime;

public class JsValueListBuilderTests
{
    [Fact]
    public void AccumulatesAndMaterializesExactSize()
    {
        var builder = new JsValueListBuilder(4);
        try
        {
            for (var i = 0; i < 1000; i++)
            {
                builder.Add(JsNumber.Create(i));
            }

            builder.Length.Should().Be(1000);

            var array = builder.ToArray();
            array.Length.Should().Be(1000);
            for (var i = 0; i < array.Length; i++)
            {
                ((JsNumber) array[i]).AsNumber().Should().Be(i);
            }
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void GrowsAcrossManyPoolBuckets()
    {
        var builder = new JsValueListBuilder(4);
        try
        {
            const int Count = 100_000;
            for (var i = 0; i < Count; i++)
            {
                builder.Add(JsNumber.Create(i % 7));
            }

            var array = builder.ToArray();
            array.Length.Should().Be(Count);
            ((JsNumber) array[0]).AsNumber().Should().Be(0);
            ((JsNumber) array[Count - 1]).AsNumber().Should().Be((Count - 1) % 7);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void HolesArePreservedAsNulls()
    {
        var builder = new JsValueListBuilder(4);
        try
        {
            builder.Add(JsNumber.Create(1));
            builder.AddHole();
            builder.Add(JsNumber.Create(3));

            builder.Length.Should().Be(3);
            builder[0].Should().NotBeNull();
            builder[1].Should().BeNull();
            builder[2].Should().NotBeNull();

            var array = builder.ToArray();
            array.Length.Should().Be(3);
            array[1].Should().BeNull();
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void AddRangePreservesNullsAndGrows()
    {
        var builder = new JsValueListBuilder(4);
        try
        {
            var source = new JsValue[100];
            for (var i = 0; i < source.Length; i += 2)
            {
                source[i] = JsNumber.Create(i);
            }

            builder.Add(JsNumber.Create(-1));
            builder.AddRange(source);

            builder.Length.Should().Be(101);

            var array = builder.ToArray();
            ((JsNumber) array[0]).AsNumber().Should().Be(-1);
            ((JsNumber) array[3]).AsNumber().Should().Be(2);
            array[2].Should().BeNull();
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void EmptyBuilderMaterializesSharedEmptyArray()
    {
        var builder = new JsValueListBuilder(0);
        try
        {
            var array = builder.ToArray();
            array.Should().BeEmpty();
            array.Should().BeSameAs(System.Array.Empty<JsValue>());
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void DoubleDisposeIsSafe()
    {
        var builder = new JsValueListBuilder(4);
        builder.Add(JsNumber.Create(1));
        builder.Dispose();
        builder.Dispose();
    }
}
