using System.Text;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host can expose its own string representation by deriving from <see cref="JsString"/> and passing a
/// null backing value to the base constructor, materializing the flat .NET string only when something
/// actually asks for it. These tests pin that contract for the engine paths a script can reach.
/// </summary>
public class LazyHostStringTests
{
    private const string Text = "abc";

    private static Engine CreateEngine(out LazyHostString host, string value = Text)
    {
        host = new LazyHostString(value);
        var engine = new Engine();
        engine.SetValue("host", host);
        return engine;
    }

    [Fact]
    public void CanConcatWithoutMaterializedBackingValue()
    {
        // String.prototype.concat asks the receiver for a growable buffer first; the base
        // implementation used to hand the (still null) backing value to the buffer and blow up.
        var engine = CreateEngine(out var host);

        engine.Evaluate("host.concat('def', 'ghi')").AsString().Should().Be("abcdefghi");
        engine.Evaluate("host.concat()").AsString().Should().Be(Text);
        host.MaterializationCount.Should().Be(1, "the flat value is produced once and cached");
    }

    [Fact]
    public void CanBuildUpWithCompoundAssignmentChain()
    {
        var engine = CreateEngine(out _);

        var result = engine.Evaluate("var s = host; s += '-1'; s += '-2'; s += '-3'; s").AsString();

        result.Should().Be("abc-1-2-3");
    }

    [Fact]
    public void CanBeConcatenatedWithPlusOperator()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host + 'def'").AsString().Should().Be("abcdef");
        engine.Evaluate("'xyz' + host").AsString().Should().Be("xyzabc");
    }

    [Fact]
    public void CanBeUsedInTemplateLiteral()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("`[${host}]`").AsString().Should().Be("[abc]");
        engine.Evaluate("`${host}${host}`").AsString().Should().Be("abcabc");
    }

    [Fact]
    public void ReportsLengthWithoutMaterializing()
    {
        var engine = CreateEngine(out var host);

        engine.Evaluate("host.length").AsNumber().Should().Be(3);

        host.MaterializationCount.Should().Be(0, "an overridden Length must not force the flat value");
    }

    [Fact]
    public void SupportsCharacterAccess()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host[0]").AsString().Should().Be("a");
        engine.Evaluate("host.charAt(1)").AsString().Should().Be("b");
        engine.Evaluate("host.charCodeAt(2)").AsNumber().Should().Be('c');
    }

    [Fact]
    public void IsEqualToTheEquivalentPlainString()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host === 'abc'").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc' === host").AsBoolean().Should().BeTrue();
        engine.Evaluate("host == 'abc'").AsBoolean().Should().BeTrue();
        engine.Evaluate("'abc' == host").AsBoolean().Should().BeTrue();

        engine.Evaluate("host === 'abd'").AsBoolean().Should().BeFalse();
        engine.Evaluate("host !== 'abcd'").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void HasTheSameEqualityAndHashAsTheEquivalentPlainString()
    {
        // the base Equals/GetHashCode implementations are what an unspecialized host subclass
        // inherits, so they have to answer from the materialized value rather than the raw field
        var host = new LazyHostString(Text);
        var flat = new JsString(Text);

        host.Equals(flat).Should().BeTrue();
        flat.Equals(host).Should().BeTrue();
        host.Equals(Text).Should().BeTrue();
        host.GetHashCode().Should().Be(flat.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsPropertyKey()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("var o = {}; o[host] = 42; o.abc").AsNumber().Should().Be(42);
        engine.Evaluate("Object.keys(o)[0]").AsString().Should().Be(Text);
        engine.Evaluate("({ abc: 7 })[host]").AsNumber().Should().Be(7);
        engine.Evaluate("host in { abc: 1 }").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CanBeUsedAsCollectionKey()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("var m = new Map(); m.set(host, 1); m.get('abc')").AsNumber().Should().Be(1);
        engine.Evaluate("new Set([host, 'abc']).size").AsNumber().Should().Be(1);
    }

    [Fact]
    public void CanBeSerializedToJson()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("JSON.stringify(host)").AsString().Should().Be("\"abc\"");
        engine.Evaluate("JSON.stringify({ value: host })").AsString().Should().Be("{\"value\":\"abc\"}");
        engine.Evaluate("JSON.stringify({ [host]: 1 })").AsString().Should().Be("{\"abc\":1}");
        engine.Evaluate("JSON.stringify([host])").AsString().Should().Be("[\"abc\"]");
    }

    [Fact]
    public void SupportsCommonStringPrototypeMethods()
    {
        var engine = CreateEngine(out _);

        engine.Evaluate("host.toUpperCase()").AsString().Should().Be("ABC");
        engine.Evaluate("host.indexOf('b')").AsNumber().Should().Be(1);
        engine.Evaluate("host.slice(1)").AsString().Should().Be("bc");
        engine.Evaluate("host.split('b').join('-')").AsString().Should().Be("a-c");
        engine.Evaluate("String(host)").AsString().Should().Be(Text);
        engine.Evaluate("host.toString()").AsString().Should().Be(Text);
    }

    /// <summary>
    /// Stands in for a host string backed by a native handle: the payload is kept encoded and only
    /// decoded on demand. The backing value handed to the base constructor is null, so every member
    /// that needs the text has to route through <see cref="ToString"/>.
    /// </summary>
    private sealed class LazyHostString : JsString
    {
        // ASCII only, which keeps the encoded length equal to the character length so that Length
        // can be answered without decoding - the whole point of holding the payload encoded.
        private readonly byte[] _encoded;
        private string _materialized;
        private int _materializationCount;

        public LazyHostString(string value) : base(null)
        {
            _encoded = Encoding.ASCII.GetBytes(value);
        }

        /// <summary>
        /// How many times the flat value had to be produced. Used by the tests to assert that
        /// members which can answer lazily really do.
        /// </summary>
        public int MaterializationCount => _materializationCount;

        public override string ToString()
        {
            if (_materialized is null)
            {
                _materializationCount++;
                _materialized = Encoding.ASCII.GetString(_encoded);
            }

            return _materialized;
        }

        public override int Length => _encoded.Length;

        public override char this[int index] => (char) _encoded[index];
    }
}
