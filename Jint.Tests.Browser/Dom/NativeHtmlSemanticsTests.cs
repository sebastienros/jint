#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Runtime;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeHtmlSemanticsTests
{
    [TestCase("", "true", true)]
    [TestCase("TrUe", "true", true)]
    [TestCase("plaintext-ONLY", "plaintext-only", true)]
    [TestCase("false", "false", false)]
    [TestCase(" true ", "inherit", true)]
    [TestCase("\u00a0false", "inherit", true)]
    public void ContentEditableUsesExactKeywordsAndOrdinaryInheritance(string raw, string canonical, bool editable)
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("input");
        parent.SetAttributeNS(null, "contenteditable", "true");
        parent.AppendChild(child);
        child.SetAttributeNS(null, "contenteditable", raw);
        BrowserHtmlSemantics.GetContentEditable(realm, child).Should().Be(canonical);
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().Be(editable);
    }

    [Test]
    public void ContentEditableSetterCanonicalizesRemovesAndRefusesWhitespace()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        BrowserHtmlSemantics.SetContentEditable(realm, element, "PLAINTEXT-ONLY");
        element.GetAttributeNS(null, "contenteditable").Should().Be("plaintext-only");
        Action invalid = () => BrowserHtmlSemantics.SetContentEditable(realm, element, " true ");
        Caught.Exception(invalid).Should().BeOfType<JavaScriptException>();
        element.GetAttributeNS(null, "contenteditable").Should().Be("plaintext-only");
        BrowserHtmlSemantics.SetContentEditable(realm, element, "INHERIT");
        element.GetAttributeNS(null, "contenteditable").Should().BeNull();
    }

    [Test]
    public void DesignModeUsesActualDocumentParentAndCurrentAdoptedTree()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        DomDocumentState.Of(document).DesignModeEnabled = true;
        root.SetAttributeNS(null, "contenteditable", "false");
        BrowserHtmlSemantics.IsContentEditable(realm, root).Should().BeTrue();
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().BeTrue();
        child.SetAttributeNS(null, "contenteditable", "false");
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().BeFalse();
        child.RemoveAttributeNS(null, "contenteditable");
        Document.CreateHtml().AdoptNode(child);
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().BeFalse();
    }

    [Test]
    public void TranslateCrossesForeignAncestorsAndSpellcheckHasExplicitFalseDefault()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var foreign = document.CreateElementNS("urn:test", "foreign");
        var child = document.CreateElement("span");
        parent.AppendChild(foreign);
        foreign.AppendChild(child);
        parent.SetAttributeNS(null, "translate", "no");
        parent.SetAttributeNS(null, "spellcheck", "true");
        foreign.SetAttributeNS(null, "translate", "yes");
        foreign.SetAttributeNS(null, "spellcheck", "false");
        BrowserHtmlSemantics.GetTranslate(realm, child).Should().BeFalse();
        BrowserHtmlSemantics.GetSpellcheck(realm, child).Should().BeFalse();
        child.SetAttributeNS(null, "spellcheck", "");
        BrowserHtmlSemantics.GetSpellcheck(realm, child).Should().BeTrue();
        child.SetAttributeNS(null, "spellcheck", "invalid");
        child.SetAttributeNS(null, "translate", " no ");
        BrowserHtmlSemantics.GetTranslate(realm, child).Should().BeFalse();
        foreign.RemoveChild(child);
        BrowserHtmlSemantics.GetTranslate(realm, child).Should().BeTrue();
        BrowserHtmlSemantics.GetSpellcheck(realm, child).Should().BeFalse();
    }

    [TestCase("http://www.w3.org/2000/svg", "svg", true)]
    [TestCase("http://www.w3.org/2000/svg", "g", false)]
    [TestCase("http://www.w3.org/1998/Math/MathML", "math", true)]
    [TestCase("urn:test", "svg", false)]
    public void EditabilityUsesExactForeignEligibility(string ns, string name, bool expected)
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        parent.SetAttributeNS(null, "contenteditable", "true");
        var foreign = document.CreateElementNS(ns, name);
        var child = document.CreateElement("span");
        parent.AppendChild(foreign); foreign.AppendChild(child);
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().Be(expected);
    }

    [Test]
    public void XmlDocumentDesignModeHostsHtmlRootButNotSvgRoot()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var xml = Document.CreateXml();
        var htmlRoot = xml.CreateElementNS(Namespaces.Html, "html");
        xml.AppendChild(htmlRoot);
        DomDocumentState.Of(xml).DesignModeEnabled = true;
        BrowserHtmlSemantics.IsContentEditable(realm, htmlRoot).Should().BeTrue();
        var html = Document.CreateHtml();
        var svgRoot = html.CreateElementNS(Namespaces.Svg, "svg");
        var child = html.CreateElement("span");
        svgRoot.AppendChild(child); html.AppendChild(svgRoot);
        DomDocumentState.Of(html).DesignModeEnabled = true;
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().BeFalse();
    }

    [Test]
    public void InheritanceDoesNotCrossShadowHostAndDesignModeDoesNotApplyToDetachedNodes()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        host.SetAttributeNS(null, "contenteditable", "true");
        host.SetAttributeNS(null, "translate", "no");
        DomDocumentState.Of(document).DesignModeEnabled = true;
        var shadow = ShadowTree.Attach(host, new(ShadowRootMode.Open), default);
        var child = document.CreateElement("span"); shadow.AppendChild(child);
        BrowserHtmlSemantics.IsContentEditable(realm, child).Should().BeFalse();
        BrowserHtmlSemantics.GetTranslate(realm, child).Should().BeTrue();
        var detached = document.CreateElement("div");
        BrowserHtmlSemantics.IsContentEditable(realm, detached).Should().BeFalse();
    }

    [Test]
    public void DraggableUsesActualAutomaticCategoriesAndNoMimeGuess()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var image = document.CreateElement("img");
        BrowserHtmlSemantics.GetDraggable(realm, image).Should().BeTrue();
        image.SetAttributeNS(null, "draggable", "FALSE");
        BrowserHtmlSemantics.GetDraggable(realm, image).Should().BeFalse();
        image.SetAttributeNS(null, "draggable", " false ");
        BrowserHtmlSemantics.GetDraggable(realm, image).Should().BeTrue();
        var link = document.CreateElement("a");
        BrowserHtmlSemantics.GetDraggable(realm, link).Should().BeFalse();
        link.SetAttributeNS(null, "href", "");
        BrowserHtmlSemantics.GetDraggable(realm, link).Should().BeTrue();
        var obj = document.CreateElement("object");
        obj.SetAttributeNS(null, "type", "image/png");
        BrowserHtmlSemantics.GetDraggable(realm, obj).Should().BeFalse();
        BrowserHtmlSemantics.AccessKeyLabel(realm, image).Should().BeEmpty();
    }

    [Test]
    public void ContextMenuUsesFirstIdAndAssignmentSurvivesAdoptionButNotClone()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var target = document.CreateElement("div");
        var blocker = document.CreateElement("div");
        var menu = document.CreateElement("menu");
        blocker.SetAttributeNS(null, "id", "menu");
        menu.SetAttributeNS(null, "id", "menu");
        target.SetAttributeNS(null, "contextmenu", "menu");
        root.AppendChild(target); root.AppendChild(blocker); root.AppendChild(menu);
        BrowserHtmlSemantics.GetContextMenu(realm, target).Should().BeNull();
        root.RemoveChild(blocker);
        BrowserHtmlSemantics.GetContextMenu(realm, target).Should().BeSameAs(menu);
        var assigned = document.CreateElement("menu");
        BrowserHtmlSemantics.SetContextMenu(realm, target, assigned);
        target.GetAttributeNS(null, "contextmenu").Should().Be("menu");
        Document.CreateHtml().AdoptNode(target);
        BrowserHtmlSemantics.GetContextMenu(realm, target).Should().BeSameAs(assigned);
        BrowserHtmlSemantics.GetContextMenu(realm, (Element) target.CloneNode()).Should().BeNull();
        BrowserHtmlSemantics.SetContextMenu(realm, target, null);
        BrowserHtmlSemantics.GetContextMenu(realm, target).Should().BeNull();
        root.AppendChild(target);
        target.SetAttributeNS(null, "contextmenu", "");
        menu.SetAttributeNS(null, "id", "");
        BrowserHtmlSemantics.GetContextMenu(realm, target).Should().BeNull();
    }

    [TestCase("SECTION-Work SHIPPING HOME TeL WEBAUTHN", false, "section-work shipping home TeL WEBAUTHN")]
    [TestCase("home name", false, "")]
    [TestCase("section- name", false, "section- name")]
    [TestCase("on", true, "")]
    [TestCase("ON", false, "on")]
    [TestCase("webauthn", true, "webauthn")]
    [TestCase("billing email webauthn", true, "billing email webauthn")]
    [TestCase("section-a shipping home tel extra webauthn", false, "")]
    [TestCase("name\u00a0", false, "")]
    [TestCase("\tname\n", false, "name")]
    public void AutocompleteAppliesTokenCategoriesAndPreservesFieldSpelling(string raw, bool hidden, string expected)
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var input = Document.CreateHtml().CreateElement("input");
        input.SetAttributeNS(null, "autocomplete", raw);
        if (hidden) input.SetAttributeNS(null, "type", "HIDDEN");
        BrowserAutocomplete.Get(realm, input).Should().Be(expected);
        input.GetAttributeNS(null, "autocomplete").Should().Be(raw);
    }

    [Test]
    public void AutocompleteChargesLongTokenCharacters()
    {
        using var cancellation = new CancellationTokenSource();
        var work = new DomReadWork(units => { if (units >= 256) cancellation.Cancel(); }, cancellation.Token);
        Action read = () => BrowserAutocomplete.Parse(new string('a', 8192), false, work);
        Caught.Exception(read).Should().BeOfType<OperationCanceledException>();
    }
}
