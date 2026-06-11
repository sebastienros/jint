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

            Assert.Equal(1000, builder.Length);

            var array = builder.ToArray();
            Assert.Equal(1000, array.Length);
            for (var i = 0; i < array.Length; i++)
            {
                Assert.Equal(i, ((JsNumber) array[i]).AsNumber());
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
            Assert.Equal(Count, array.Length);
            Assert.Equal(0, ((JsNumber) array[0]).AsNumber());
            Assert.Equal((Count - 1) % 7, ((JsNumber) array[Count - 1]).AsNumber());
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

            Assert.Equal(3, builder.Length);
            Assert.NotNull(builder[0]);
            Assert.Null(builder[1]);
            Assert.NotNull(builder[2]);

            var array = builder.ToArray();
            Assert.Equal(3, array.Length);
            Assert.Null(array[1]);
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

            Assert.Equal(101, builder.Length);

            var array = builder.ToArray();
            Assert.Equal(-1, ((JsNumber) array[0]).AsNumber());
            Assert.Equal(2, ((JsNumber) array[3]).AsNumber());
            Assert.Null(array[2]);
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
            Assert.Empty(array);
            Assert.Same(System.Array.Empty<JsValue>(), array);
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
