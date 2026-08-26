#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Wpt;

/// <summary>
/// The two pieces of driver logic that decide what runs and what is forgiven: how a reference inside the
/// vendored tree is resolved, and how an exclusion decides which tests it covers.
/// </summary>
public class WptCorpusTests
{
    // Relative to the file that named it — a suite's own corpus.
    [TestCase("url", "resources/setters_tests.json", "url/resources/setters_tests.json")]
    [TestCase("encoding", "resources/encodings.js", "encoding/resources/encodings.js")]
    // A leading slash is the wpt root, which is how wptserve resolves the shared helpers.
    [TestCase("url", "/common/subset-tests-by-key.js", "common/subset-tests-by-key.js")]
    [TestCase("encoding", "/common/sab.js", "common/sab.js")]
    // A variant's query selects a shard of the same file, so it is not part of the path.
    [TestCase("url", "resources/urltestdata.json?1-1000", "url/resources/urltestdata.json")]
    [TestCase("url", "resources/urltestdata.json#x", "url/resources/urltestdata.json")]
    // Dot segments are resolved rather than taken literally.
    [TestCase("url/resources", "./urltestdata.json", "url/resources/urltestdata.json")]
    [TestCase("url/resources", "../url-tojson.any.js", "url/url-tojson.any.js")]
    // Which is the shape every streams suite's META lines have: out of the sub-directory and into resources/.
    [TestCase("streams/piping", "../resources/test-utils.js", "streams/resources/test-utils.js")]
    public void AReferenceResolvesToAPathInTheTree(string directory, string reference, string expected)
        => WptCorpus.ResolveReference(directory, reference).Should().Be(expected);

    [Test]
    public void AReferenceCannotClimbOutOfTheVendoredTree()
    {
        Invoking(() => WptCorpus.ResolveReference("url", "../../../../secrets.json"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the vendored web-platform-tests tree*");
    }

    [TestCase("exactly this", "exactly this", true)]
    [TestCase("exactly this", "exactly THIS", false)]
    [TestCase("exactly this", "exactly this ", false)]
    // A trailing star is a prefix.
    [TestCase("prefix: *", "prefix: anything at all", true)]
    [TestCase("prefix: *", "prefix: ", true)]
    [TestCase("prefix: *", "not the prefix: x", false)]
    // A leading star is a suffix — this is the shape "*(SharedArrayBuffer)" has.
    [TestCase("*(SharedArrayBuffer)", "Streaming decode: utf-8, 1 byte window (SharedArrayBuffer)", true)]
    [TestCase("*(SharedArrayBuffer)", "Streaming decode: utf-8, 1 byte window (ArrayBuffer)", false)]
    // A star in the middle, which is how the label suites are covered.
    [TestCase("* => windows-1252", "cp1252 => windows-1252", true)]
    [TestCase("* => windows-1252", "cp1252 => windows-1252 extra", false)]
    [TestCase("* => windows-1252", "utf8 => UTF-8", false)]
    // Several stars, and a case that only matches if the matcher backtracks.
    [TestCase("Invalid *: *, backed by: SharedArrayBuffer", "Invalid destination: Int8Array, backed by: SharedArrayBuffer", true)]
    [TestCase("a*b*c", "abababc", true)]
    [TestCase("a*b*c", "ababab", false)]
    // A lone star is every name, which is what an exclusion over a whole file spells. It is only safe
    // because the driver still refuses it if any test in that file passes.
    [TestCase("*", "anything", true)]
    [TestCase("*", "", true)]
    // \* is a literal asterisk, for the names that contain one. No entry spells it today — the pair it was
    // introduced for is accept-header.any.js's, and both rows pass since
    // https://github.com/sebastienros/jint/issues/3279 — so this is what keeps the escape honest.
    [TestCase(@"Request through fetch should have 'accept' header with value '\*/\*'",
        "Request through fetch should have 'accept' header with value '*/*'", true)]
    [TestCase(@"Request through fetch should have 'accept' header with value '\*/\*'",
        "Request through fetch should have 'accept' header with value 'custom/*'", false)]
    public void AnExclusionMatchesExactlyWhatItsPatternSays(string pattern, string testName, bool expected)
        => WptExclusion.MatchesPattern(pattern, testName).Should().Be(expected);
}
#endif
