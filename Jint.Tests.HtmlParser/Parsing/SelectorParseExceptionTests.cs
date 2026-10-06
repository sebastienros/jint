#nullable enable
using System.Reflection;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Parsing;

public class SelectorParseExceptionTests
{
    [TestCase("selector/invalid-syntax", 0)]
    [TestCase("selector/undeclared-prefix", 7)]
    [TestCase("selector/unsupported-construct", 19)]
    public void CarriesStableCodeAndOriginalInputOffset(string code, long offset)
    {
        var error = new SelectorParseException(code, offset);
        error.Code.Should().Be(code);
        error.Offset.Should().Be(offset);
        error.Message.Should().Be($"Selector parse error {code} at UTF-16 offset {offset}.");
    }

    [Test]
    public void StaysInternalUntilTheSelectorFacadeIsImplemented()
    {
        typeof(SelectorParseException).IsNotPublic.Should().BeTrue();
        typeof(SelectorParseException).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should().BeEmpty();
    }
}
