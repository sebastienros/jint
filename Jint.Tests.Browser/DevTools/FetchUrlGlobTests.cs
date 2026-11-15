#nullable enable

using Jint.Browser.DevTools;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// <c>Fetch.enable</c>'s <c>RequestPattern.urlPattern</c>, matched the way Chromium matches it: <c>base::MatchPattern</c>
/// over the request URL with its fragment removed.
/// </summary>
/// <remarks>
/// <para>
/// Every expectation here is what Chromium answers, worked through <c>base/strings/pattern.cc</c> rather than read
/// off the protocol's one-line description. The two disagree in one place, and these rows side with the browser:
/// the description says <c>?</c> is "exactly one" character, and Chromium's header says — and its code does — "0 or
/// 1".
/// </para>
/// <para>
/// <c>FetchDomainTests</c> pins the same matcher end to end, through a real page's requests.
/// </para>
/// </remarks>
public sealed class FetchUrlGlobTests
{
    private const string Origin = "https://example.com";

    /// <summary>The pattern must cover the whole URL: nothing before the first piece, nothing after the last.</summary>
    [TestCase(Origin + "/a", Origin + "/a", true)]
    [TestCase(Origin + "/a", Origin + "/ab", false)]
    [TestCase(Origin + "/a", Origin + "/a/", false)]
    [TestCase("example.com/a", Origin + "/a", false)]
    [TestCase("/a", Origin + "/a", false)]
    [TestCase(Origin, Origin + "/", false)]
    public void APatternIsAnchoredAtBothEnds(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary><c>*</c> is any run of characters, the empty run included, and it crosses <c>/</c>, <c>?</c> and <c>.</c>.</summary>
    [TestCase("*", Origin + "/a/b?c=d", true)]
    [TestCase("**", Origin + "/", true)]
    [TestCase("*/app.js", Origin + "/static/app.js", true)]
    [TestCase("*/app.js", Origin + "/static/app.json", false)]
    [TestCase("https://*.example.com/x", "https://cdn.example.com/x", true)]
    [TestCase("https://*.example.com/x", "https://a.b.example.com/x", true)]
    [TestCase("https://*.example.com/x", Origin + "/x", false)]
    [TestCase(Origin + "/*", Origin + "/", true)]
    [TestCase(Origin + "/*", Origin + "/a/b?c=d", true)]
    [TestCase(Origin + "/*/d", Origin + "/a/b/c/d", true)]
    [TestCase("*a*b*c", Origin + "/xaxbxc", true)]
    [TestCase("*a*b*c", Origin + "/xcxbxa", false)]
    public void AStarIsAnyRunOfCharacters(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// <c>?</c> is at most one character — one, or none — wherever it stands, and a run of them is at most that
    /// many; a literal <c>?</c> in the URL is one character like any other.
    /// </summary>
    [TestCase(Origin + "/a?c", Origin + "/abc", true)]
    [TestCase(Origin + "/a?c", Origin + "/a?c", true)]
    [TestCase(Origin + "/a?c", Origin + "/ac", true)]
    [TestCase(Origin + "/a?c", Origin + "/abbc", false)]
    [TestCase("?ttps://example.com/", Origin + "/", true)]
    [TestCase("??https://example.com/", Origin + "/", true)]
    [TestCase("?https://example.com/", "xxhttps://example.com/", false)]
    [TestCase(Origin + "/a?", Origin + "/a", true)]
    [TestCase(Origin + "/a?", Origin + "/ab", true)]
    [TestCase(Origin + "/a?", Origin + "/abc", false)]
    [TestCase(Origin + "/a??d", Origin + "/ad", true)]
    [TestCase(Origin + "/a??d", Origin + "/abd", true)]
    [TestCase(Origin + "/a??d", Origin + "/abcd", true)]
    [TestCase(Origin + "/a??d", Origin + "/abcxd", false)]
    [TestCase("*.js?v=*", Origin + "/app.js?v=3", true)]
    [TestCase("*.js?v=*", Origin + "/app.jsv=3", true)]
    [TestCase("*.js?v=*", Origin + "/app.js&&v=3", false)]
    [TestCase(Origin + "/?*", Origin + "/", true)]
    [TestCase(Origin + "/*?", Origin + "/anything", true)]
    public void AQuestionMarkIsAtMostOneCharacter(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// The piece after a run of wildcards is taken at the first place it fits and never reconsidered, which is
    /// Chromium's search rather than a backtracking one.
    /// </summary>
    /// <remarks>
    /// <c>/?a?b</c> could cover <c>/aaab</c> — the first <c>?</c> taking an <c>a</c>, the second another — but
    /// Chromium's first <c>?</c> takes nothing, because <c>a</c> already fits there, and the second <c>?</c>
    /// cannot then reach the <c>b</c>. A browser does not intercept that request, so neither does this.
    /// </remarks>
    [TestCase(Origin + "/?a?b", Origin + "/aab", true)]
    [TestCase(Origin + "/?a?b", Origin + "/aaab", false)]
    [TestCase(Origin + "/?a?b", Origin + "/xab", true)]
    public void APieceIsTakenAtTheFirstPlaceItFits(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// A backslash makes the character after it literal — a wildcard, a backslash, or anything else — and is not
    /// itself matched.
    /// </summary>
    [TestCase(Origin + @"/\*", Origin + "/*", true)]
    [TestCase(Origin + @"/\*", Origin + "/a", false)]
    [TestCase(Origin + @"/\*", Origin + "/", false)]
    [TestCase(Origin + @"/a\?c", Origin + "/a?c", true)]
    [TestCase(Origin + @"/a\?c", Origin + "/abc", false)]
    [TestCase(Origin + @"/a\?c", Origin + "/ac", false)]
    [TestCase(@"*.js\?v=*", Origin + "/app.js?v=3", true)]
    [TestCase(@"*.js\?v=*", Origin + "/app.jsXv=3", false)]
    [TestCase(Origin + @"/a\\b", Origin + @"/a\b", true)]
    [TestCase(Origin + @"/a\\b", Origin + "/ab", false)]
    [TestCase(Origin + @"/a\\*", Origin + @"/a\anything", true)]
    [TestCase(Origin + @"/a\\*", Origin + "/anything", false)]
    [TestCase(Origin + @"/\a", Origin + "/a", true)]
    [TestCase(Origin + @"/a\b", Origin + @"/a\b", false)]
    public void ABackslashEscapesTheNextCharacter(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// A backslash with nothing after it escapes nothing: it matches the end of the URL, and never a backslash.
    /// </summary>
    [TestCase(Origin + @"/a\", Origin + "/a", true)]
    [TestCase(Origin + @"/a\", Origin + @"/a\", false)]
    [TestCase(Origin + @"/a\", Origin + "/ab", false)]
    public void ATrailingBackslashMatchesOnlyTheEnd(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>Characters compare ordinally: no case folding, in the scheme and host as anywhere else.</summary>
    [TestCase("https://EXAMPLE.com/a", Origin + "/a", false)]
    [TestCase("HTTPS://example.com/a", Origin + "/a", false)]
    [TestCase("*/APP.js", Origin + "/app.js", false)]
    [TestCase("*/app.js", Origin + "/APP.js", false)]
    [TestCase("*/App.js", Origin + "/App.js", true)]
    public void MatchingIsCaseSensitive(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// An empty <c>urlPattern</c> is a pattern, not an absent one: it matches only an empty string, so no request.
    /// Only an omitted one means <c>*</c>, and that default is the domain's to apply.
    /// </summary>
    [TestCase("", Origin + "/", false)]
    [TestCase("", "about:blank", false)]
    [TestCase("*", Origin + "/", true)]
    [TestCase("*", "about:blank", true)]
    public void AnEmptyPatternMatchesNoRequest(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// The URL is matched without its fragment, as Chromium matches it, so a pattern sees exactly the URL a
    /// request goes out for.
    /// </summary>
    [TestCase(Origin + "/a", Origin + "/a#frag", true)]
    [TestCase(Origin + "/a", Origin + "/a#", true)]
    [TestCase("*#frag", Origin + "/a#frag", false)]
    [TestCase("*frag", Origin + "/a#frag", false)]
    [TestCase(Origin + "/a?q", Origin + "/a?q#frag", true)]
    public void TheFragmentIsNotMatched(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>A wildcard counts code points, so a <c>?</c> covers a character outside the Basic Multilingual Plane.</summary>
    [TestCase(Origin + "/?", Origin + "/\U0001F600", true)]
    [TestCase(Origin + "/?", Origin + "/\U0001F600\U0001F600", false)]
    [TestCase(Origin + "/\U0001F600", Origin + "/\U0001F600", true)]
    public void AWildcardCountsCodePoints(string pattern, string url, bool expected)
        => FetchUrlGlob.Matches(pattern, url).Should().Be(expected);

    /// <summary>
    /// The search is a loop, not a recursion per star, so a pattern of many stars against a long URL answers rather
    /// than exploring every way to split it.
    /// </summary>
    [Test]
    public void ManyStarsAgainstALongUrlAnswerWithoutBacktracking()
    {
        var url = Origin + "/" + new string('a', 4096);
        var pattern = string.Concat(Enumerable.Repeat("*a", 24)) + "*b";

        FetchUrlGlob.Matches(pattern, url).Should().BeFalse();
        FetchUrlGlob.Matches(pattern, url + "b").Should().BeTrue();
    }
}
