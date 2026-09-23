using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Cascade 5 § 3.1, Editor's Draft 2026-09-23: all reset exceptions.
[TestFixture]
public sealed class AllResetTests
{
    [TestCase("direction", true)]
    [TestCase("DIRECTION", true)]
    [TestCase("unicode-bidi", true)]
    [TestCase("Unicode-Bidi", true)]
    [TestCase("--Theme", true)]
    [TestCase("--theme", true)]
    [TestCase("color", false)]
    [TestCase("-Theme", false)]
    [TestCase("x--Theme", false)]
    public void ExclusionIsCaseAware(string name, bool excluded) =>
        CssAllReset.IsExcluded(name).Should().Be(excluded);

    [Test]
    public void NullNameIsProgrammerError() =>
        Assert.Throws<ArgumentNullException>(() => CssAllReset.IsExcluded(null!));
}
