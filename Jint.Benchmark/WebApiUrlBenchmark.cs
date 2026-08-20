using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The <c>URL</c> and <c>URLSearchParams</c> interfaces — <see cref="WebApiFeatures.Url"/>, implemented on
/// the WHATWG URL Standard's own parser rather than on <see cref="Uri"/>
/// (https://url.spec.whatwg.org/#dom-url-url).
///
/// <para>Five rows, chosen so that a change to one part of the parser cannot hide in another:
/// <see cref="ParseAbsolute"/> is the full parse of a representative corpus (special and non-special
/// schemes, credentials, a non-default port, an IPv4 and an IPv6 host, an already-punycoded domain,
/// percent-encoding, and the two cannot-be-a-base shapes <c>mailto:</c> and <c>data:</c>);
/// <see cref="ParseRelative"/> is the base-relative path of the same parser, which is the one a module
/// resolver or a link rewriter actually runs; <see cref="ReadComponents"/> never parses at all and is
/// pure serialization, since every component getter rebuilds its string on each read
/// (<c>UrlRecord.Serialize*</c>); <see cref="MutateAndSerialize"/> is the setter path plus the
/// re-serialization each write implies; and the two <c>URLSearchParams</c> rows separate its mutation
/// surface from its iterator.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying <see cref="WebApiFeatures.Url"/>
/// alone, warmed with its own fixture and its own script and nothing else — see
/// <see cref="WebApiBenchmarkSupport"/>. Engine construction, the corpus and the reference values stay in
/// <c>[GlobalSetup]</c> and never enter the measurement; the measured operation is one evaluation of the
/// row's script, which calls one function and compares its answer with the reference.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiUrlBenchmark
{
    /// <summary>
    /// Ten inputs a real embedding sees, not ten variations on one shape. Kept in one place so the parse
    /// and the serialize rows are talking about the same URLs.
    /// </summary>
    private const string Corpus =
        """
        var CORPUS = [
            'https://example.org/',
            'https://user:pass@example.org:8443/a/b/../c/./d?x=1&y=2#frag',
            'https://192.168.0.1:8080/status',
            'https://[2001:db8::1]/v6/index.html',
            'https://cdn.example.com/assets/app.4f2c1b.js?v=3',
            'https://example.org/search?q=hello+world&lang=en-US&page=2',
            'http://xn--8i7caa.example/%E2%9C%93?ok=1',
            'file:///C:/tmp/report.txt',
            'mailto:someone@example.org',
            'data:text/plain;base64,SGVsbG8sIHdvcmxkIQ=='
        ];
        var BASE = 'https://example.org/a/b/c?x=1#f';
        """;

    /// <summary>
    /// The relative references a resolver meets: a plain segment, dot segments, an absolute path, a
    /// query-only and a fragment-only reference, a scheme-relative one, an absolute URL that ignores the
    /// base entirely, and the empty string, which resolves to the base minus its fragment.
    /// </summary>
    private const string Relatives =
        """
        var RELATIVE = ['d', '../e', '/f/g', '?q=2', '#g', '//other.example/h', 'https://abs.example/i', 'j/k/', '../../l', ''];
        """;

    /// <summary>
    /// A query string with repeated names, a plus-encoded space and a mix of lengths — the shape
    /// <c>getAll</c>, <c>sort</c> and the serializer all have something to do.
    /// </summary>
    private const string Query =
        """
        var QUERY = 'a=1&b=2&c=3&tag=x&tag=y&tag=z&q=hello+world&lang=en-US&page=2&sort=desc';
        """;

    private IsolatedScript _parseAbsolute;
    private IsolatedScript _parseRelative;
    private IsolatedScript _readComponents;
    private IsolatedScript _mutateAndSerialize;
    private IsolatedScript _searchParamsMutate;
    private IsolatedScript _searchParamsIterate;

    [GlobalSetup]
    public void Setup()
    {
        _parseAbsolute = Row(
            Corpus +
            """

            function parseAbsolute() {
                var n = 0;
                for (var r = 0; r < 20; r++) {
                    for (var i = 0; i < CORPUS.length; i++) { n += new URL(CORPUS[i]).href.length; }
                }
                return n;
            }
            """,
            "parseAbsolute()");

        _parseRelative = Row(
            Corpus + Environment.NewLine + Relatives +
            """

            function parseRelative() {
                var n = 0;
                for (var r = 0; r < 20; r++) {
                    for (var i = 0; i < RELATIVE.length; i++) { n += new URL(RELATIVE[i], BASE).href.length; }
                }
                return n;
            }
            """,
            "parseRelative()");

        // The corpus is parsed once, at fixture time: this row is about serialization only, and every
        // getter below rebuilds its string on each read rather than answering from a cache.
        _readComponents = Row(
            Corpus +
            """

            var PARSED = CORPUS.map(function (s) { return new URL(s); });
            function readComponents() {
                var n = 0;
                for (var r = 0; r < 20; r++) {
                    for (var i = 0; i < PARSED.length; i++) {
                        var u = PARSED[i];
                        n += u.href.length + u.protocol.length + u.host.length + u.hostname.length
                            + u.port.length + u.pathname.length + u.search.length + u.hash.length
                            + u.origin.length;
                    }
                }
                return n;
            }
            """,
            "readComponents()");

        _mutateAndSerialize = Row(
            Corpus +
            """

            function mutateAndSerialize() {
                var url = new URL(BASE);
                var n = 0;
                for (var i = 0; i < 200; i++) {
                    url.pathname = '/p/' + i;
                    url.search = 'q=' + i + '&r=' + (i * 2);
                    url.hash = 'h' + i;
                    url.port = String(8000 + (i % 100));
                    n += url.href.length;
                }
                return n;
            }
            """,
            "mutateAndSerialize()");

        _searchParamsMutate = Row(
            Query +
            """

            function searchParamsMutate() {
                var n = 0;
                for (var r = 0; r < 50; r++) {
                    var p = new URLSearchParams(QUERY);
                    p.append('extra', 'v' + r);
                    p.set('page', 'p' + r);
                    p.delete('sort');
                    p.sort();
                    n += p.toString().length + p.size;
                }
                return n;
            }
            """,
            "searchParamsMutate()");

        _searchParamsIterate = Row(
            Query +
            """

            var PARAMS = new URLSearchParams(QUERY);
            function searchParamsIterate() {
                var n = 0;
                for (var r = 0; r < 50; r++) {
                    for (var entry of PARAMS) { n += entry[0].length + entry[1].length; }
                    n += PARAMS.getAll('tag').length + PARAMS.get('q').length;
                }
                return n;
            }
            """,
            "searchParamsIterate()");
    }

    private static IsolatedScript Row(string fixture, string call)
        => WebApiBenchmarkSupport.DeterministicRow(WebApiFeatures.Url, fixture, call);

    /// <summary>200 full parses: the whole corpus, twenty times.</summary>
    [Benchmark]
    public JsValue ParseAbsolute() => _parseAbsolute.Run();

    /// <summary>200 base-relative parses, the path a module resolver runs.</summary>
    [Benchmark]
    public JsValue ParseRelative() => _parseRelative.Run();

    /// <summary>1,800 component reads over ten already-parsed URLs — serialization with no parsing at all.</summary>
    [Benchmark]
    public JsValue ReadComponents() => _readComponents.Run();

    /// <summary>200 rounds of four setters plus the <c>href</c> each round re-serializes.</summary>
    [Benchmark]
    public JsValue MutateAndSerialize() => _mutateAndSerialize.Run();

    /// <summary>50 rounds of parse, append, set, delete, sort and serialize over a ten-pair query.</summary>
    [Benchmark]
    public JsValue SearchParamsMutate() => _searchParamsMutate.Run();

    /// <summary>500 iterator steps plus the lookups, over one already-parsed parameter list.</summary>
    [Benchmark]
    public JsValue SearchParamsIterate() => _searchParamsIterate.Run();
}
