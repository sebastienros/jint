using Jint.DevTools.Domains;

namespace Jint.Tests.DevTools.Domains;

/// <summary>
/// The mapping from the name a host parsed a program under to the URL the protocol publishes it as.
/// </summary>
/// <remarks>
/// Both path shapes are exercised on every operating system on purpose: a source name reaches an engine from
/// wherever the host got it, and a Windows path published from a Linux host is still a Windows path. Nothing
/// here asks the operating system anything.
/// </remarks>
public class ScriptUrlTests
{
    [TestCase(@"D:\Work\jint\app.js", "file:///D:/Work/jint/app.js")]
    [TestCase("D:/Work/jint/app.js", "file:///D:/Work/jint/app.js")]
    [TestCase(@"c:\a.js", "file:///c:/a.js")]
    [TestCase("/home/marko/app.js", "file:///home/marko/app.js")]
    [TestCase("/app.js", "file:///app.js")]
    [TestCase(@"\\build\share\app.js", "file://build/share/app.js")]
    public void AnAbsolutePathBecomesAFileUrl(string sourceName, string url)
    {
        ScriptUrl.From(sourceName).Should().Be(url);
    }

    [TestCase(@"D:\Work\a b\app.js", "file:///D:/Work/a%20b/app.js")]
    [TestCase("/home/a b/app.js", "file:///home/a%20b/app.js")]
    [TestCase("/home/caf\u00e9/app.js", "file:///home/caf%C3%A9/app.js")]
    public void EachSegmentIsPercentEncodedAndTheSeparatorsAreNot(string sourceName, string url)
    {
        ScriptUrl.From(sourceName).Should().Be(url);
    }

    [TestCase("app.js")]
    [TestCase("<anonymous>")]
    [TestCase("stdin")]
    [TestCase("main.js")]
    [TestCase("./relative/app.js")]
    [TestCase("https://example.org/app.js")]
    [TestCase("file:///D:/already/mapped.js")]
    [TestCase("jint://repl")]
    [TestCase("data:text/javascript,1")]
    public void EveryOtherNameIsPublishedUnchanged(string sourceName)
    {
        ScriptUrl.From(sourceName).Should().Be(sourceName);
    }

    [Test]
    public void NoNameIsTheEmptyString()
    {
        ScriptUrl.From(null).Should().BeEmpty();
        ScriptUrl.From("").Should().BeEmpty();
    }

    [Test]
    public void ASingleLetterIsADriveRatherThanAScheme()
    {
        // "D:" is a drive letter and "js:" would be a scheme; the difference is the one character, and it is
        // why the scheme test refuses a one-character prefix.
        ScriptUrl.From(@"D:\a.js").Should().Be("file:///D:/a.js");
        ScriptUrl.From("js:whatever").Should().Be("js:whatever");
    }

    [Test]
    public void EitherFormIdentifiesTheSameScript()
    {
        // A client sends back the URL it read off scriptParsed; a host driving the protocol itself has only
        // ever seen the name it passed.
        ScriptUrl.Same(@"D:\Work\app.js", "file:///D:/Work/app.js").Should().BeTrue();
        ScriptUrl.Same(@"D:\Work\app.js", @"D:\Work\app.js").Should().BeTrue();
        ScriptUrl.Same("app.js", "app.js").Should().BeTrue();
        ScriptUrl.Same(@"D:\Work\app.js", "file:///D:/Work/other.js").Should().BeFalse();
    }
}
