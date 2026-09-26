#nullable enable
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionEnvironmentTests
{
    private static CssEnvironmentBinding Binding(string name, string value, params string[] indices) =>
        CssEnvironmentBinding.Create(name, indices, SubstitutionFixture.Input(value), new CssValueWork(default));

    [TestCase("+000", "-0")]
    [TestCase("00012", "+0012")]
    [TestCase("999999999999999999999999999999", "999999999999999999999999999999")]
    public void ExactIntegerKeysNormalizeWithoutMachineIntegerConversion(string supplied, string requested)
    {
        var binding = Binding("segment", "blue", supplied, "2");
        var result = SubstitutionFixture.ResolveWith($"env(segment {requested} 02,red)", [], true, [binding]);
        SubstitutionFixture.Identifier(result).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith($"env(segment {requested},red)", [], true, [binding]))
            .Should().Be("red");
    }

    [TestCase("-1")]
    [TestCase("1.0")]
    [TestCase("1e0")]
    public void InvalidRuntimeIndexGrammarChoosesFallback(string index) =>
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith($"env(segment {index},red)", [], true,
            [Binding("segment", "blue", "1")])).Should().Be("red");

    [Test]
    public void DynamicNamesCaseAndEmptyDeviceValuesHaveDistinctResults()
    {
        var environment = new[] { Binding("Safe", "blue"), Binding("empty", "") };
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("env(var(--name),red)",
            [SubstitutionFixture.Specified("--name", "Safe")], true, environment)).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("env(safe,red)", [], true, environment)).Should().Be("red");
        SubstitutionFixture.ResolveWith("env(empty,red)", [], true, environment).Value.TokenCount.Should().Be(0);
        SubstitutionFixture.ResolveWith("env(absent)", [], true, environment).Kind
            .Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void DeviceValuesRejectSubstitutionPendingSyntaxAndNonintegerKeys()
    {
        foreach (var source in new[] { "var(--x)", "attr(x)", "red;blue" })
            Assert.Throws<ArgumentException>(() => Binding("device", source));
        foreach (var index in new[] { "-1", "1.0", "1e0", "", "+" })
            Assert.Throws<ArgumentException>(() => Binding("device", "red", index));
        Assert.Throws<ArgumentException>(() => Binding("DEFAULT", "red"));
    }
}
