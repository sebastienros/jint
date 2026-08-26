#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Runtime.NodeCompat;

/// <summary>
/// The opt-in <c>node:querystring</c> builtin module - https://nodejs.org/api/querystring.html - against the
/// real Node implementation.
/// </summary>
/// <remarks>
/// <para>
/// The expectations in <see cref="MatchesNode"/> were produced by running each expression under a real Node
/// (v24). Nothing here depends on the platform or the working directory: <c>querystring</c> is pure string
/// arithmetic over its arguments.
/// </para>
/// <para>
/// The cases that matter most are the ones where <c>querystring</c> is deliberately <em>not</em>
/// <c>URLSearchParams</c>: a space escapes as <c>%20</c> rather than <c>+</c>, <c>!'()*~</c> are left alone, a
/// repeated key becomes an array, the result carries no prototype, and the separators are parameters.
/// </para>
/// </remarks>
public class QueryStringTests
{
    /// <summary>
    /// An engine with <c>node:querystring</c> imported and its default export bound to the global
    /// <c>querystring</c>, which is the shape <c>const querystring = require('node:querystring')</c> gives a
    /// script.
    /// </summary>
    private static Engine QueryStringEngine()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());

        engine.SetValue("querystring", engine.Modules.Import("node:querystring").Get("default"));
        return engine;
    }

    [TestCase("querystring.escape('hello world')", "hello%20world")]
    [TestCase("querystring.escape('a=b&c=d')", "a%3Db%26c%3Dd")]
    [TestCase("querystring.escape(\"!'()*-._~\")", "!'()*-._~")]
    [TestCase("querystring.escape('abcABC123')", "abcABC123")]
    [TestCase("querystring.escape('+')", "%2B")]
    [TestCase("querystring.escape('/?:@&=$,#[]')", "%2F%3F%3A%40%26%3D%24%2C%23%5B%5D")]
    [TestCase("querystring.escape('\\u00e9')", "%C3%A9")]
    [TestCase("querystring.escape('\\u4e2d\\u6587')", "%E4%B8%AD%E6%96%87")]
    [TestCase("querystring.escape('\\ud83d\\ude00')", "%F0%9F%98%80")]
    [TestCase("querystring.escape('')", "")]
    [TestCase("querystring.escape(123)", "123")]
    [TestCase("querystring.escape(true)", "true")]
    [TestCase("querystring.unescape('hello%20world')", "hello world")]
    [TestCase("querystring.unescape('a%3Db')", "a=b")]
    [TestCase("querystring.unescape('%E4%B8%AD%E6%96%87')", "\u4e2d\u6587")]
    [TestCase("querystring.unescape('a+b')", "a+b")]
    [TestCase("querystring.unescape('%')", "%")]
    [TestCase("querystring.unescape('%zz')", "%zz")]
    [TestCase("querystring.unescape('%C3%28')", "\ufffd(")]
    [TestCase("querystring.unescape('100%')", "100%")]
    [TestCase("querystring.unescape('')", "")]
    [TestCase("querystring.stringify({ foo: 'bar', baz: ['qux', 'quux'], corge: '' })", "foo=bar&baz=qux&baz=quux&corge=")]
    [TestCase("querystring.stringify({ foo: 'bar', baz: 'qux' }, ';', ':')", "foo:bar;baz:qux")]
    [TestCase("querystring.stringify({})", "")]
    [TestCase("querystring.stringify({ a: 1, b: true, c: null, d: undefined })", "a=1&b=true&c=&d=")]
    [TestCase("querystring.stringify({ a: [] })", "")]
    [TestCase("querystring.stringify({ a: [1, 2, 3] })", "a=1&a=2&a=3")]
    [TestCase("querystring.stringify({ 'a b': 'c d' })", "a%20b=c%20d")]
    [TestCase("querystring.stringify({ a: NaN, b: Infinity })", "a=&b=")]
    [TestCase("querystring.stringify({ a: 1e21 })", "a=1e%2B21")]
    [TestCase("querystring.stringify({ a: 1e20 })", "a=100000000000000000000")]
    [TestCase("querystring.stringify(null)", "")]
    [TestCase("querystring.stringify('nope')", "")]
    [TestCase("querystring.stringify({ a: { b: 1 } })", "a=")]
    [TestCase("querystring.stringify({ a: 10n })", "a=10")]
    [TestCase("querystring.stringify({ w: '\\u4e2d\\u6587', foo: 'bar' })", "w=%E4%B8%AD%E6%96%87&foo=bar")]
    [TestCase("querystring.stringify({ a: 'b' }, null, null)", "a=b")]
    [TestCase("querystring.stringify({ a: 'b', c: 'd' }, '', '')", "a=b&c=d")]
    [TestCase("querystring.stringify({ a: 'x' }, null, null, { encodeURIComponent: (s) => s.toUpperCase() })", "A=X")]
    [TestCase("querystring.encode({ a: 'b' })", "a=b")]
    [TestCase("JSON.stringify(querystring.parse('foo=bar&abc=xyz&abc=123'))", "{\"foo\":\"bar\",\"abc\":[\"xyz\",\"123\"]}")]
    [TestCase("JSON.stringify(querystring.parse(''))", "{}")]
    [TestCase("JSON.stringify(querystring.parse('&=&='))", "{\"\":[\"\",\"\"]}")]
    [TestCase("JSON.stringify(querystring.parse('a=b&&c=d'))", "{\"a\":\"b\",\"c\":\"d\"}")]
    [TestCase("JSON.stringify(querystring.parse('a'))", "{\"a\":\"\"}")]
    [TestCase("JSON.stringify(querystring.parse('a='))", "{\"a\":\"\"}")]
    [TestCase("JSON.stringify(querystring.parse('=b'))", "{\"\":\"b\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=b&'))", "{\"a\":\"b\"}")]
    [TestCase("JSON.stringify(querystring.parse('&a=b'))", "{\"a\":\"b\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=b&a=c&a=d'))", "{\"a\":[\"b\",\"c\",\"d\"]}")]
    [TestCase("JSON.stringify(querystring.parse('hello+world=foo+bar'))", "{\"hello world\":\"foo bar\"}")]
    [TestCase("JSON.stringify(querystring.parse('a%20b=c%20d'))", "{\"a b\":\"c d\"}")]
    [TestCase("JSON.stringify(querystring.parse('w=%E4%B8%AD%E6%96%87'))", "{\"w\":\"\u4e2d\u6587\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=%'))", "{\"a\":\"%\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=%C3%28'))", "{\"a\":\"\ufffd(\"}")]
    [TestCase("JSON.stringify(querystring.parse('foo:bar;baz:qux', ';', ':'))", "{\"foo\":\"bar\",\"baz\":\"qux\"}")]
    [TestCase("JSON.stringify(querystring.parse('a==b'))", "{\"a\":\"=b\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=b=c'))", "{\"a\":\"b=c\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=1&b=2&c=3', null, null, { maxKeys: 2 }))", "{\"a\":\"1\",\"b\":\"2\"}")]
    [TestCase("JSON.stringify(querystring.parse('a=1&b=2&c=3', null, null, { maxKeys: 0 }))", "{\"a\":\"1\",\"b\":\"2\",\"c\":\"3\"}")]
    [TestCase("JSON.stringify(querystring.parse('toString=x'))", "{\"toString\":\"x\"}")]
    [TestCase("String(querystring.parse('a=b').hasOwnProperty)", "undefined")]
    [TestCase("String(Object.getPrototypeOf(querystring.parse('a=b')))", "null")]
    [TestCase("JSON.stringify(querystring.parse('a=b', null, null, { decodeURIComponent: (s) => s.toUpperCase() }))", "{\"A\":\"B\"}")]
    [TestCase("JSON.stringify(querystring.parse('a+b=c+d', null, null, { decodeURIComponent: (s) => s }))", "{\"a%20b\":\"c%20d\"}")]
    [TestCase("JSON.stringify(querystring.decode('a=b'))", "{\"a\":\"b\"}")]
    [TestCase("JSON.stringify(querystring.parse(123))", "{}")]
    [TestCase("JSON.stringify(querystring.parse('a=b&c', '&&'))", "{\"a\":\"b&c\"}")]
    public void MatchesNode(string expression, string expected)
    {
        var engine = QueryStringEngine();

        engine.Evaluate(expression).AsString().Should().Be(expected, expression);
    }

    /// <summary>
    /// Node documents <c>escape</c> and <c>unescape</c> as replaceable - "exported primarily to allow
    /// application code to provide a replacement percent-encoding implementation if necessary by assigning
    /// <c>querystring.escape</c> to an alternative function" - and <c>stringify</c> reads the current one
    /// every time it runs.
    /// </summary>
    [Test]
    public void StringifyUsesAReassignedEscape()
    {
        var engine = QueryStringEngine();

        engine.Evaluate("querystring.escape = (s) => '<' + s + '>';");

        engine.Evaluate("querystring.stringify({ a: 'b' })").AsString().Should().Be("<a>=<b>");
    }

    /// <summary>
    /// The other half of the same contract: <c>parse</c> reads the current <c>unescape</c>, and a decoder that
    /// is not the built-in one is handed every component - including the <c>%20</c> a <c>+</c> was turned into,
    /// rather than an already-substituted space.
    /// </summary>
    [Test]
    public void ParseUsesAReassignedUnescape()
    {
        var engine = QueryStringEngine();

        engine.Evaluate("querystring.unescape = (s) => '[' + s + ']';");

        engine.Evaluate("JSON.stringify(querystring.parse('a+b=c'))").AsString().Should().Be("{\"[a%20b]\":\"[c]\"}");
    }

    /// <summary>
    /// A decoder that throws is not fatal: Node falls back to the built-in one rather than failing the parse.
    /// </summary>
    [Test]
    public void ParseFallsBackWhenACustomDecoderThrows()
    {
        var engine = QueryStringEngine();

        var result = engine.Evaluate(
            "JSON.stringify(querystring.parse('a%20b=c', null, null, { decodeURIComponent() { throw new Error('no'); } }))");

        result.AsString().Should().Be("{\"a b\":\"c\"}");
    }

    /// <summary>
    /// <c>unescapeBuffer</c> is absent because it answers with a <c>Buffer</c>, and <c>node:buffer</c> is not
    /// one of the modules Jint provides.
    /// </summary>
    [Test]
    public void UnescapeBufferIsAbsent()
    {
        var engine = QueryStringEngine();

        engine.Evaluate("typeof querystring.unescapeBuffer").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// The named exports are there beside the default one, so <c>import { parse } from 'node:querystring'</c>
    /// works as it does in Node.
    /// </summary>
    [Test]
    public void ExposesNamedExports()
    {
        var engine = new Engine(options => options.UseNodeBuiltinModules());
        engine.Modules.Add("main", "import { parse, stringify, escape, unescape, encode, decode } from 'node:querystring'; export const ok = [parse, stringify, escape, unescape, encode, decode].every(f => typeof f === 'function');");

        engine.Modules.Import("main").Get("ok").AsBoolean().Should().BeTrue();
    }
}
#endif
