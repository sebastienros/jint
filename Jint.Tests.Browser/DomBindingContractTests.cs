#nullable enable

using System.Text.Json.Nodes;
using Jint.Browser.BindingGenerator;

namespace Jint.Tests.Browser;

/// <summary>Malformed contracts must fail before the emitter can install a wrong prototype chain.</summary>
public sealed class DomBindingContractTests
{
    [Test]
    public void UnknownParentIsRejected()
    {
        CheckMutation(root => Interface(root, "Element")["Parent"] = "MissingInterface", "Element must follow its known parent MissingInterface");
    }

    [Test]
    public void ParentDeclaredAfterChildIsRejected()
    {
        CheckMutation(root => Interface(root, "Node")["Parent"] = "Element", "Node must follow its known parent Element");
    }

    [Test]
    public void CyclicParentIsRejected()
    {
        CheckMutation(root => Interface(root, "Node")["Parent"] = "Node", "Node must follow its known parent Node");
    }

    [Test]
    public void DuplicateMemberIsRejected()
    {
        CheckMutation(root =>
        {
            var members = Interface(root, "Node")["Members"]!.AsArray();
            members.Add(members[0]!.DeepClone());
        }, "Duplicate DOM member Node.");
    }

    [Test]
    public void OmittedCollectionReceiversDefaultToNativeTypes()
    {
        var contract = JsonNode.Parse(File.ReadAllText(RepositoryPaths.ContractPath))!.AsObject();
        contract.Remove("HtmlCollectionOpenType");
        contract.Remove("HtmlCollectionDefaultElement");
        var path = Path.Combine(Path.GetTempPath(), "jint-dom-contract-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, contract.ToJsonString());
            var result = BindingGenerator.Run(new BindingGeneratorOptions { ContractPath = path });
            result.Files.Should().BeEquivalentTo(DomBindingsStalenessTests.Generate().Files);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static JsonObject Interface(JsonNode root, string name)
        => root["Interfaces"]!.AsArray()
            .Select(entry => entry!.AsObject())
            .Single(entry => (string?) entry["DomName"] == name);

    private static void CheckMutation(Action<JsonNode> mutate, string message)
    {
        var contract = JsonNode.Parse(File.ReadAllText(RepositoryPaths.ContractPath))!;
        mutate(contract);
        var path = Path.Combine(Path.GetTempPath(), "jint-dom-contract-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, contract.ToJsonString());
            Action emit = () => BindingGenerator.Run(new BindingGeneratorOptions { ContractPath = path });
            emit.Should().Throw<InvalidDataException>().WithMessage("*" + message + "*");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
