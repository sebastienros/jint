#nullable enable
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Tests.Browser.Dom;

/// <summary>HTML §2.6.1 reflects only content attributes in the null namespace.</summary>
public sealed class NullNamespaceReflectionTests
{
    [TestCase("div", "title", "title", "", "ordinary")]
    [TestCase("div", "class", "className", "", "ordinary")]
    [TestCase("div", "id", "id", "", "ordinary")]
    [TestCase("input", "name", "name", "", "ordinary")]
    [TestCase("div", "dir", "dir", "", "rtl")]
    [TestCase("img", "src", "src", "", "https://example.test/")]
    [TestCase("input", "disabled", "disabled", false, true)]
    [TestCase("img", "crossorigin", "crossOrigin", null, "anonymous")]
    [TestCase("td", "colspan", "colSpan", 1d, 4d)]
    [TestCase("progress", "value", "value", 0d, 0.5d)]
    public void ReflectedWritesPreserveAForeignAttribute(string tag, string attribute, string member, object? absent, object value)
    {
        using var fixture = DomTestFixture.Create("");
        var element = fixture.Document.CreateElement(tag);
        element.SetAttributeNS("urn:foreign", attribute, "foreign");
        var foreign = element.GetAttributeNodeNS("urn:foreign", attribute);
        fixture.Engine.SetValue("target", DomBindings.Wrap(fixture.Engine, element));
        fixture.Engine.SetValue("member", member);
        fixture.Engine.SetValue("value", value);

        fixture.Evaluate("target[member]").ToObject().Should().Be(absent);
        fixture.Execute("target[member] = value");
        element.GetAttributeNodeNS("urn:foreign", attribute).Should().BeSameAs(foreign);
        foreign!.Value.Should().Be("foreign");
        element.GetAttributeNodeNS(null, attribute).Should().NotBeNull();
        fixture.Evaluate("target[member]").ToObject().Should().Be(value);

        // DOM's qualified-name lookup remains independent of HTML reflection.
        element.GetAttribute(attribute).Should().Be("foreign");
    }

    [TestCase("input", "disabled", "disabled", false)]
    [TestCase("img", "crossorigin", "crossOrigin", null)]
    public void ReflectedRemovalPreservesAForeignAttribute(string tag, string attribute, string member, object? value)
    {
        using var fixture = DomTestFixture.Create("");
        var element = fixture.Document.CreateElement(tag);
        element.SetAttributeNS("urn:foreign", attribute, "foreign");
        element.SetAttributeNS(null, attribute, "anonymous");
        var foreign = element.GetAttributeNodeNS("urn:foreign", attribute);
        fixture.Engine.SetValue("target", DomBindings.Wrap(fixture.Engine, element));
        fixture.Engine.SetValue("member", member);
        fixture.Engine.SetValue("value", value);

        fixture.Execute("target[member] = value");
        element.GetAttributeNodeNS(null, attribute).Should().BeNull();
        element.GetAttributeNodeNS("urn:foreign", attribute).Should().BeSameAs(foreign);
        foreign!.Value.Should().Be("foreign");
        fixture.Evaluate("target[member]").ToObject().Should().Be(value);
    }

    [Test]
    public void NullableTextRemovalAndNonceSlotIgnoreForeignAttributes()
    {
        using var fixture = DomTestFixture.Create("");
        var element = fixture.Document.CreateElement("div");
        element.SetAttributeNS("urn:foreign", "nonce", "foreign nonce");
        element.SetAttributeNS("urn:foreign", "data", "foreign data");
        var realm = DomRealm.Of(fixture.Engine);
        var nullable = ReflectedAttribute.Text("X.data", "data", nullable: true);
        nullable.Get(realm, element).Should().Be(JsValue.Null);
        nullable.Set(realm, element, [JsString.Create("ordinary")]);
        nullable.Get(realm, element).AsString().Should().Be("ordinary");
        nullable.Set(realm, element, [JsValue.Null]);
        element.GetAttributeNS(null, "data").Should().BeNull();
        element.GetAttributeNS("urn:foreign", "data").Should().Be("foreign data");

        var nonce = ReflectedAttribute.Nonce("HTMLElement.nonce", "nonce");
        nonce.Get(realm, element).AsString().Should().BeEmpty();
        nonce.Set(realm, element, [JsString.Create("slot")]);
        element.SetAttributeNS("urn:foreign", "nonce", "changed foreign nonce");
        nonce.Get(realm, element).AsString().Should().Be("slot");
        element.GetAttributeNS(null, "nonce").Should().BeNull();
        element.GetAttributeNS("urn:foreign", "nonce").Should().Be("changed foreign nonce");
    }

    [Test]
    public void DocumentReflectionIgnoresForeignDirAndColourAttributes()
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Execute("""
            const html = document.documentElement;
            const body = document.body;
            html.setAttributeNS('urn:foreign', 'dir', 'rtl');
            body.setAttributeNS('urn:foreign', 'bgcolor', 'red');
            """);
        fixture.Text("document.dir + '|' + document.bgColor").Should().Be("|");
        fixture.Execute("document.dir = 'ltr'; document.bgColor = 'green'");
        fixture.Text("html.getAttributeNS('urn:foreign', 'dir') + '|' + body.getAttributeNS('urn:foreign', 'bgcolor')").Should().Be("rtl|red");
        fixture.Text("document.dir + '|' + document.bgColor").Should().Be("ltr|green");
    }

    [Test]
    public void OrdinaryReflectionChecksConstraintsWhileScanningAttributes()
    {
        var budget = new ReadBudget();
        using var engine = new Engine(options => options.AddConstraint(budget));
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        for (var i = 0; i < 8192; i++) element.SetAttributeNS("urn:foreign", "a" + i, "foreign");
        element.SetAttributeNS(null, "title", "ordinary");

        Caught.Exception(() => ReflectedAttribute.Text("HTMLElement.title", "title").Get(DomRealm.Of(engine), element))
            .Should().BeOfType<ReadBudgetExceededException>();
        budget.Checks.Should().Be(3);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void DocumentReflectionCancelsDuringTargetLookup(bool bodyTarget, bool setter)
    {
        using var cancellation = new CancellationTokenSource();
        var checks = new CancelTargetLookup(cancellation);
        using var engine = new Engine(options => options.AddConstraint(checks)
            .AddConstraint(new Jint.Constraints.CancellationConstraint(cancellation.Token)));
        DomBindings.Install(engine);
        var document = Document.CreateHtml();
        if (!bodyTarget)
            for (var i = 0; i < 2048; i++) document.AppendChild(document.CreateComment("prefix"));
        var html = document.CreateElement("html");
        document.AppendChild(html);
        if (bodyTarget)
            for (var i = 0; i < 2048; i++) html.AppendChild(document.CreateComment("prefix"));
        var body = document.CreateElement("body");
        html.AppendChild(body);
        var reflected = bodyTarget
            ? ReflectedAttribute.Text("Document.bgColor", "bgcolor", target: ReflectedTarget.Body)
            : ReflectedAttribute.Enumerated("Document.dir", "dir", ["ltr", "rtl", "auto"], "", "", ReflectedTarget.DocumentElement);
        var realm = DomRealm.Of(engine);
        Caught.Exception(() =>
        {
            if (setter) reflected.Set(realm, document, [JsString.Create("rtl")]);
            else reflected.Get(realm, document);
        }).Should().BeOfType<Jint.Runtime.ExecutionCanceledException>();
        checks.Checks.Should().Be(2);
        html.GetAttributeNS(null, "dir").Should().BeNull();
        body.GetAttributeNS(null, "bgcolor").Should().BeNull();
    }

    private sealed class CancelTargetLookup(CancellationTokenSource cancellation) : Constraint
    {
        internal int Checks { get; private set; }
        public override void Check()
        {
            if (++Checks == 2) cancellation.Cancel();
        }
        public override void Reset() { }
    }

    private sealed class ReadBudget : Constraint
    {
        internal int Checks { get; private set; }
        public override void Check()
        {
            if (++Checks == 3) throw new ReadBudgetExceededException();
        }
        public override void Reset() { }
    }

    private sealed class ReadBudgetExceededException : Exception { }
}
