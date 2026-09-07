#nullable enable

using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The public contract for preparing Acornima syntax trees supplied by a host.
/// </summary>
public class HostAstPreparationTests
{
    [Test]
    public void AnExistingScriptCanBePreparedAndExecuted()
    {
        var script = new Parser().ParseScript("hostValue + 2");

        var prepared = Engine.PrepareScript(script);
        using var engine = new Engine();
        engine.SetValue("hostValue", 40);

        ReferenceEquals(prepared.Program, script).Should().BeTrue();
        engine.Evaluate(prepared).Should().Be(42);
    }

    [Test]
    public void AnExistingModuleCanBePreparedAndImported()
    {
        var module = new Parser().ParseModule("export const answer = 42;");

        var prepared = Engine.PrepareModule(module);
        using var engine = new Engine();
        engine.Modules.Add("direct", builder => builder.AddModule(prepared));

        ReferenceEquals(prepared.Program, module).Should().BeTrue();
        engine.Modules.Import("direct").Get("answer").Should().Be(42);
    }

    [Test]
    public void APreparedTreeCanBeSharedAcrossEnginesConcurrently()
    {
        var prepared = Engine.PrepareScript(new Parser().ParseScript("hostValue * 2"));

        Parallel.For(0, 16, value =>
        {
            using var engine = new Engine();
            engine.SetValue("hostValue", value);
            engine.Evaluate(prepared).Should().Be(value * 2);
        });
    }

    [Test]
    public void PreparationTakesOwnershipOfNodeUserData()
    {
        var script = new Parser().ParseScript("let value = 40; value + 2");
        SetUserData(script, new object());

        var prepared = Engine.PrepareScript(script, new ScriptPreparationOptions { StaticAnalysis = false });

        AllNodes(script).Should().OnlyContain(node => node.UserData == null);
        using var engine = new Engine();
        engine.Evaluate(prepared).Should().Be(42);
    }

    [Test]
    public void ANodeLimitAppliesToTheSuppliedTree()
    {
        var script = new Parser().ParseScript("0 + 1");

        var failure = Invoking(() => Engine.PrepareScript(
                script,
                new ScriptPreparationOptions
                {
                    ParsingOptions = new ScriptParsingOptions { MaxNodeCount = 1 },
                }))
            .Should().ThrowExactly<ParsingLimitException>().Which;

        failure.Kind.Should().Be(ParsingLimitKind.NodeCount);
        failure.Limit.Should().Be(1);
        failure.Actual.Should().Be(2);
    }

    [Test]
    public void ASourceLimitAppliesToLaterDynamicCompilation()
    {
        var script = new Parser().ParseScript("eval(payload)");
        var prepared = Engine.PrepareScript(
            script,
            new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 5 },
            });
        using var engine = new Engine();
        engine.SetValue("payload", "40 + 2");

        var failure = Invoking(() => engine.Evaluate(prepared))
            .Should().ThrowExactly<ParsingLimitException>().Which;

        failure.Kind.Should().Be(ParsingLimitKind.SourceLength);
        failure.Limit.Should().Be(5);
        failure.Actual.Should().Be(6);
    }

    [Test]
    public void ParserDependentPreparationFeaturesAreRejected()
    {
        var script = new Parser().ParseScript("freeName");
        var module = new Parser().ParseModule("export default function named() {}");

        Invoking(() => Engine.PrepareScript(
                script,
                new ScriptPreparationOptions { CollectReferencedGlobals = true }))
            .Should().ThrowExactly<ArgumentException>().WithParameterName("options");
        Invoking(() => Engine.PrepareScript(
                script,
                new ScriptPreparationOptions
                {
                    ParsingOptions = new ScriptParsingOptions { RetainFunctionSourceText = true },
                }))
            .Should().ThrowExactly<ArgumentException>().WithParameterName("options");
        Invoking(() => Engine.PrepareModule(
                module,
                new ModulePreparationOptions { CollectReferencedGlobals = true }))
            .Should().ThrowExactly<ArgumentException>().WithParameterName("options");
        Invoking(() => Engine.PrepareModule(
                module,
                new ModulePreparationOptions
                {
                    ParsingOptions = new ModuleParsingOptions { RetainFunctionSourceText = true },
                }))
            .Should().ThrowExactly<ArgumentException>().WithParameterName("options");
    }

    [Test]
    public void NullTreesAreRejected()
    {
        Invoking(() => Engine.PrepareScript((Script) null!))
            .Should().ThrowExactly<ArgumentNullException>().WithParameterName("script");
        Invoking(() => Engine.PrepareModule((Module) null!))
            .Should().ThrowExactly<ArgumentNullException>().WithParameterName("module");
    }

    private static void SetUserData(Node node, object value)
    {
        foreach (var current in AllNodes(node))
        {
            current.UserData = value;
        }
    }

    private static IEnumerable<Node> AllNodes(Node node)
    {
        yield return node;
        foreach (var child in node.ChildNodes)
        {
            foreach (var descendant in AllNodes(child))
            {
                yield return descendant;
            }
        }
    }
}
