using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Events;

public sealed class NativeActivationReadTests
{
    [TestCase("text")]
    [TestCase("number")]
    [TestCase("date")]
    public void LegacyPreActivationDoesNotReadAColdNonCheckableValue(string type)
    {
        var shortValueChecks = CountChecks(type, "7");
        CountChecks(type, new string('7', 32768)).Should().Be(shortValueChecks);
    }

    private static int CountChecks(string type, string value)
    {
        var counter = new ReadCounter();
        using var engine = new Engine(options => options.AddConstraint(counter));
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.SetAttribute("type", type);
        input.SetAttribute("value", value);
        var wrapper = DomRealm.Of(engine).WrapNode(input);
        var before = counter.Checks;
        ActivationBehaviors.LegacyPreActivationBehavior(wrapper);
        return counter.Checks - before;
    }

    private sealed class ReadCounter : Constraint
    {
        internal int Checks { get; private set; }
        public override void Check() => Checks++;
        public override void Reset() { }
    }
}
