#nullable enable
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Parsing;

public class HtmlParseOptionsTests
{
    [Test]
    public void DefaultsAreInertWithSafeEntityLimits()
    {
        var options = new HtmlParseOptions();
        options.ScriptingEnabled.Should().BeFalse();
        options.Limits.MaxEntityExpansionCharacters.Should().Be(10_000_000);
        options.Diagnostics.Should().BeNull();
    }

    [Test]
    public void InitAcceptsLimitsAndCollectorAndRejectsNullLimits()
    {
        var limits = new ParseLimits { MaxInputCharacters = 12 };
        var diagnostics = new ParseDiagnosticCollector(3);
        var options = new HtmlParseOptions
        {
            ScriptingEnabled = true,
            Limits = limits,
            Diagnostics = diagnostics
        };
        options.ScriptingEnabled.Should().BeTrue();
        options.Limits.Should().BeSameAs(limits);
        options.Diagnostics.Should().BeSameAs(diagnostics);
        Assert.Throws<ArgumentNullException>(() => new HtmlParseOptions { Limits = null! });
    }

    [Test]
    public void SurfaceIsPublicAndAllOptionsAreInitOnly()
    {
        typeof(HtmlParseOptions).IsPublic.Should().BeTrue();
        var properties = typeof(HtmlParseOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        properties.Select(property => property.Name).Should().BeEquivalentTo(
            nameof(HtmlParseOptions.ScriptingEnabled), nameof(HtmlParseOptions.Limits),
            nameof(HtmlParseOptions.Diagnostics));
        foreach (var property in properties)
        {
            property.SetMethod.Should().NotBeNull();
            property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Should().Contain(typeof(IsExternalInit));
        }
    }
}
