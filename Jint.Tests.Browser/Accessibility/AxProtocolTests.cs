using System.Text.Json;
using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// The Chrome DevTools Protocol shape: what <c>Accessibility.getFullAXTree</c> puts on the wire.
/// </summary>
/// <remarks>
/// The assertions are on the JSON rather than on the record, because the record is a private arrangement
/// between this package and the protocol domain while the JSON is the contract a DevTools front end and both
/// .NET clients read. <c>backendDOMNodeId</c> is deliberately absent: only a session that owns a DOM node
/// registry can fill it in.
/// </remarks>
public sealed class AxProtocolTests
{
    [Test]
    public void FlattensTheTreeIntoNodesLinkedByIdentifier()
    {
        using var document = PageFixture.Parse("<h1>Title</h1><button>Save</button>");
        var root = AccessibilityTree.Build(document);

        using var parsed = JsonDocument.Parse(AccessibilityTree.ToJson(root));
        var nodes = parsed.RootElement.EnumerateArray().ToArray();

        nodes.Should().HaveCount(3);

        var rootNode = nodes[0];
        rootNode.GetProperty("role").GetProperty("value").GetString().Should().Be("RootWebArea");
        rootNode.TryGetProperty("parentId", out _).Should().BeFalse("the root has no parent");

        var childIds = rootNode.GetProperty("childIds").EnumerateArray().Select(static id => id.GetString()).ToArray();
        childIds.Should().Equal(nodes[1].GetProperty("nodeId").GetString(), nodes[2].GetProperty("nodeId").GetString());
        nodes[1].GetProperty("parentId").GetString().Should().Be(rootNode.GetProperty("nodeId").GetString());
    }

    [Test]
    public void EveryValueCarriesTheTypeTagTheProtocolDefines()
    {
        using var document = PageFixture.Parse("<h2 id=t aria-describedby=d>Heading</h2><span id=d>Help</span>");
        var node = AccessibilityTree.Build(document.GetElementById("t")!)!;

        using var parsed = JsonDocument.Parse(JsonSerializer.Serialize(AccessibilityTree.ToProtocol(node), AxProtocolJsonContext.Default.AxProtocolNode));
        var root = parsed.RootElement;

        root.GetProperty("ignored").GetBoolean().Should().BeFalse();
        root.GetProperty("role").GetProperty("type").GetString().Should().Be("role");
        root.GetProperty("role").GetProperty("value").GetString().Should().Be("heading");
        root.GetProperty("name").GetProperty("type").GetString().Should().Be("computedString");
        root.GetProperty("name").GetProperty("value").GetString().Should().Be("Heading");
        root.GetProperty("description").GetProperty("value").GetString().Should().Be("Help");

        var level = root.GetProperty("properties").EnumerateArray().Single(static p => p.GetProperty("name").GetString() == "level");
        level.GetProperty("value").GetProperty("type").GetString().Should().Be("integer");
        level.GetProperty("value").GetProperty("value").GetInt32().Should().Be(2);
    }

    [Test]
    public void ABooleanPropertyIsAJsonBooleanAndATristateIsAString()
    {
        using var document = PageFixture.Parse("<input id=t type=checkbox checked required>");
        var node = AccessibilityTree.Build(document.GetElementById("t")!)!;

        using var parsed = JsonDocument.Parse(JsonSerializer.Serialize(AccessibilityTree.ToProtocol(node), AxProtocolJsonContext.Default.AxProtocolNode));
        var properties = parsed.RootElement.GetProperty("properties").EnumerateArray()
            .ToDictionary(static p => p.GetProperty("name").GetString()!, static p => p.GetProperty("value"));

        properties["checked"].GetProperty("type").GetString().Should().Be("tristate");
        properties["checked"].GetProperty("value").GetString().Should().Be("true");
        properties["required"].GetProperty("type").GetString().Should().Be("boolean");
        properties["required"].GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void AnIgnoredNodeSaysThatItIsAndWhy()
    {
        using var document = PageFixture.Parse("<div id=t hidden>x</div>");
        var node = AccessibilityTree.Build(document.GetElementById("t")!, AccessibilityOptions.Full)!;

        using var parsed = JsonDocument.Parse(JsonSerializer.Serialize(AccessibilityTree.ToProtocol(node), AxProtocolJsonContext.Default.AxProtocolNode));

        parsed.RootElement.GetProperty("ignored").GetBoolean().Should().BeTrue();
        var reasons = parsed.RootElement.GetProperty("ignoredReasons").EnumerateArray().ToArray();
        reasons.Should().ContainSingle();
        reasons[0].GetProperty("name").GetString().Should().Be("hidden");
        reasons[0].GetProperty("value").GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void NullMembersAreLeftOutRatherThanWrittenAsNull()
    {
        using var document = PageFixture.Parse("<hr id=t>");
        var node = AccessibilityTree.Build(document.GetElementById("t")!)!;

        var json = JsonSerializer.Serialize(AccessibilityTree.ToProtocol(node), AxProtocolJsonContext.Default.AxProtocolNode);

        json.Should().NotContain("null");
        json.Should().NotContain("backendDOMNodeId", "only a protocol session can supply one");
        json.Should().NotContain("childIds");
    }

    [Test]
    public void TheIndentedFormIsTheSameDocument()
    {
        using var document = PageFixture.Parse("<button>Save</button>");
        var root = AccessibilityTree.Build(document);

        var compact = AccessibilityTree.ToJson(root);
        var indented = AccessibilityTree.ToJson(root, indented: true);

        indented.Should().Contain("\n");
        using var a = JsonDocument.Parse(compact);
        using var b = JsonDocument.Parse(indented);
        b.RootElement.GetArrayLength().Should().Be(a.RootElement.GetArrayLength());
    }
}
