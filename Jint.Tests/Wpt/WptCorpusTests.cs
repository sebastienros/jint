#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Wpt;

/// <summary>
/// The two pieces of driver logic that decide what runs and what is forgiven: how a reference inside the
/// vendored tree is resolved, and how an exclusion decides which tests it covers.
/// </summary>
public class WptCorpusTests
{
    [Theory]
    // Relative to the file that named it — a suite's own corpus.
    [InlineData("url", "resources/setters_tests.json", "url/resources/setters_tests.json")]
    [InlineData("encoding", "resources/encodings.js", "encoding/resources/encodings.js")]
    // A leading slash is the wpt root, which is how wptserve resolves the shared helpers.
    [InlineData("url", "/common/subset-tests-by-key.js", "common/subset-tests-by-key.js")]
    [InlineData("encoding", "/common/sab.js", "common/sab.js")]
    // A variant's query selects a shard of the same file, so it is not part of the path.
    [InlineData("url", "resources/urltestdata.json?1-1000", "url/resources/urltestdata.json")]
    [InlineData("url", "resources/urltestdata.json#x", "url/resources/urltestdata.json")]
    // Dot segments are resolved rather than taken literally.
    [InlineData("url/resources", "./urltestdata.json", "url/resources/urltestdata.json")]
    [InlineData("url/resources", "../url-tojson.any.js", "url/url-tojson.any.js")]
    // Which is the shape every streams suite's META lines have: out of the sub-directory and into resources/.
    [InlineData("streams/piping", "../resources/test-utils.js", "streams/resources/test-utils.js")]
    public void AReferenceResolvesToAPathInTheTree(string directory, string reference, string expected)
        => WptCorpus.ResolveReference(directory, reference).Should().Be(expected);

    [Fact]
    public void AReferenceCannotClimbOutOfTheVendoredTree()
    {
        Invoking(() => WptCorpus.ResolveReference("url", "../../../../secrets.json"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the vendored web-platform-tests tree*");
    }

    [Theory]
    [InlineData("exactly this", "exactly this", true)]
    [InlineData("exactly this", "exactly THIS", false)]
    [InlineData("exactly this", "exactly this ", false)]
    // A trailing star is a prefix.
    [InlineData("prefix: *", "prefix: anything at all", true)]
    [InlineData("prefix: *", "prefix: ", true)]
    [InlineData("prefix: *", "not the prefix: x", false)]
    // A leading star is a suffix — this is the shape "*(SharedArrayBuffer)" has.
    [InlineData("*(SharedArrayBuffer)", "Streaming decode: utf-8, 1 byte window (SharedArrayBuffer)", true)]
    [InlineData("*(SharedArrayBuffer)", "Streaming decode: utf-8, 1 byte window (ArrayBuffer)", false)]
    // A star in the middle, which is how the label suites are covered.
    [InlineData("* => windows-1252", "cp1252 => windows-1252", true)]
    [InlineData("* => windows-1252", "cp1252 => windows-1252 extra", false)]
    [InlineData("* => windows-1252", "utf8 => UTF-8", false)]
    // Several stars, and a case that only matches if the matcher backtracks.
    [InlineData("Invalid *: *, backed by: SharedArrayBuffer", "Invalid destination: Int8Array, backed by: SharedArrayBuffer", true)]
    [InlineData("a*b*c", "abababc", true)]
    [InlineData("a*b*c", "ababab", false)]
    // A lone star is every name, which is what an exclusion over a whole file spells. It is only safe
    // because the driver still refuses it if any test in that file passes.
    [InlineData("*", "anything", true)]
    [InlineData("*", "", true)]
    public void AnExclusionMatchesExactlyWhatItsPatternSays(string pattern, string testName, bool expected)
        => WptExclusion.MatchesPattern(pattern, testName).Should().Be(expected);
}
#endif
