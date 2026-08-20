namespace Jint.Tests.Runtime;

/// <summary>
/// A String exotic object owns an index only for the <em>canonical</em> numeric string of that index —
/// StringGetOwnProperty steps 3-6, https://tc39.es/ecma262/#sec-stringgetownproperty. Jint parsed the key
/// with a plain <c>ToNumber</c>, so <c>"01"</c>, <c>"+1"</c>, <c>"1.0"</c>, <c>" 1"</c> and <c>"-0"</c> all
/// resolved to a character that the spec says is not there.
/// </summary>
public class StringObjectIndexKeyTests
{
    [Theory]
    [InlineData("-0")]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("1e0")]
    [InlineData("0x1")]
    public void ANonCanonicalNumericKeyIsNotAnOwnIndex(string key)
    {
        var engine = new Engine();
        engine.SetValue("key", key);

        engine.Evaluate("var s = new String('hello');");

        engine.Evaluate("key in s").AsBoolean().Should().BeFalse();
        engine.Evaluate("Reflect.has(s, key)").AsBoolean().Should().BeFalse();
        engine.Evaluate("s.hasOwnProperty(key)").AsBoolean().Should().BeFalse();
        engine.Evaluate("s[key]").IsUndefined().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(s, key)").IsUndefined().Should().BeTrue();
    }

    [Fact]
    public void ACanonicalNumericKeyStillResolves()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var s = new String('hello');
            ['0' in s, '4' in s, '5' in s, s[1], s['3']].join(',');
            """).AsString();

        result.Should().Be("true,true,false,e,l");
    }

    /// <summary>
    /// A numeric key is turned into a property key by ToPropertyKey before any internal method sees it, and
    /// <c>ToString(-0)</c> is <c>"0"</c> — so <c>str[-0]</c> is index 0 even though <c>str["-0"]</c> is nothing.
    /// </summary>
    [Fact]
    public void NegativeZeroAsANumberIsIndexZero()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var s = new String('hello');
            [Reflect.has(s, -0), Reflect.has(s, '-0'), s[-0]].join(',');
            """).AsString();

        result.Should().Be("true,false,h");
    }

    /// <summary>
    /// A non-canonical key is an ordinary property name, so it can be defined and read back like any other.
    /// </summary>
    [Fact]
    public void ANonCanonicalNumericKeyCanBeAnOrdinaryOwnProperty()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var s = new String('hello');
            s['01'] = 'x';
            ['01' in s, s['01'], s[1]].join(',');
            """).AsString();

        result.Should().Be("true,x,e");
    }
}
