using Jint.Browser.Dom;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCssDeferredWorkTests
{
    [TestCase("rules.length")]
    [TestCase("rules[0]")]
    [TestCase("rules.item(0)")]
    [TestCase("Array.from(rules)")]
    public void DeferredRuleCollectionsUseTheCurrentEngineBudget(string expression)
    {
        var sheet = NativeCssParsing.CreateSheet(string.Concat(Enumerable.Repeat("p { color:red }", 1000)),
            new CssValueWork(default));
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        engine.SetValue("rules", DomRealm.Of(engine).Wrap(sheet.Rules));
        probe.Arm();
        Caught.Exception(() => engine.Evaluate(expression)).Should().BeSameAs(probe.Failure);
        sheet.Rules.Should().BeEmpty();
        probe.Disarm();
        engine.Evaluate("rules.length").AsNumber().Should().Be(1000);
    }

    [TestCase("media.length")]
    [TestCase("media[0]")]
    [TestCase("media.item(0)")]
    [TestCase("media.mediaText")]
    public void DeferredMediaCollectionsUseTheCurrentEngineBudget(string expression)
    {
        var sheet = NativeCssParsing.CreateSheet("@media (width:" + new string('0', 100_000) + "1px) {}",
            new CssValueWork(default));
        var media = ((CssMediaRule) NativeCssParsing.ReadRules(sheet.Rules, new CssValueWork(default))[0]).Media;
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        engine.SetValue("media", DomRealm.Of(engine).Wrap(media));
        probe.Arm();
        Caught.Exception(() => engine.Evaluate(expression)).Should().BeSameAs(probe.Failure);
        media.Count.Should().Be(0);
        probe.Disarm();
        engine.Evaluate("media.mediaText").AsString().Should().Be("(width: 1px)");
    }

    private sealed class ReadProbe : Constraint
    {
        internal InvalidOperationException Failure { get; } = new("Deferred parsing checkpoint");
        private int _checks;
        private bool _armed;
        internal void Arm() { _checks = 0; _armed = true; }
        internal void Disarm() => _armed = false;
        public override void Check()
        {
            if (_armed && ++_checks == 20) throw Failure;
        }
        public override void Reset() { }
    }
}
