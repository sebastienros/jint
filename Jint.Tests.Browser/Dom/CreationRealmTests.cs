using AngleSharp;
using AngleSharp.Html.Parser;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Runtime;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;

namespace Jint.Tests.Browser.Dom;

public class CreationRealmTests
{
    [TestCase("const n = a.document.implementation.createDocumentType('~', 'p', 's'); document.adoptNode(n); return n instanceof a.DocumentType && !(n instanceof DocumentType) && n.ownerDocument === document;")]
    [TestCase("const n = a.document.createElement('div'); b.document.body.append(n, {toString() { n.innerHTML = '<p>late</p>'; return ''; }}); return n.firstChild instanceof a.HTMLParagraphElement && n.firstChild.firstChild instanceof a.Text;")]
    [TestCase("const n = a.document.createComment('x'); document.adoptNode(n); return n instanceof a.Comment && !(n instanceof Comment) && n.ownerDocument === document;")]
    [TestCase("const n = a.document.createElement('div'); n.innerHTML = '<p>text</p><!--x-->'; document.adoptNode(n); return n.firstChild instanceof a.HTMLParagraphElement && n.firstChild.firstChild instanceof a.Text && n.lastChild instanceof a.Comment;")]
    [TestCase("const n = a.document.createElement('div'); document.body.appendChild(n); n.innerHTML = '<p>text</p>'; b.document.body.appendChild(n); return n instanceof a.HTMLDivElement && n.firstChild instanceof HTMLParagraphElement && n.firstChild.firstChild instanceof Text;")]
    [TestCase("const n = a.document.getElementById('parsed'); document.adoptNode(n); return n instanceof a.HTMLDivElement && n.firstChild instanceof a.Text && n.lastChild instanceof a.Comment;")]
    [TestCase("return a.Node !== Node && a.Node !== b.Node && a.MouseEvent !== MouseEvent && a.MouseEvent !== b.MouseEvent && new a.MouseEvent('x') instanceof a.Event && a.document.createEvent('MouseEvent') instanceof a.MouseEvent;")]
    [TestCase("const n = new a.Text('x'); return n.ownerDocument === a.document && n instanceof a.Text;")]
    [TestCase("return a.document.body.childNodes instanceof a.NodeList && a.document.body.querySelectorAll('*') instanceof a.NodeList;")]
    [TestCase("try { a.document.createElement('a b'); } catch(e) { return e instanceof a.DOMException && !(e instanceof DOMException); } return false;")]
    [TestCase("const n = a.document.createTextNode('x'); document.body.appendChild(n); return a.document.createTreeWalker(n).currentNode === n && n instanceof a.Text;")]
    [TestCase("const n = a.Document.prototype.createTextNode.call(document, 'x'); return n instanceof Text && !(n instanceof a.Text);")]
    [TestCase("const n = new a.ProcessingInstruction('t', 'data'); return n.ownerDocument === a.document && n instanceof a.ProcessingInstruction && !(n instanceof ProcessingInstruction);")]
    [TestCase("const n = new a.ProcessingInstruction('𐀀', 'data'); return n.ownerDocument === a.document && n instanceof a.ProcessingInstruction && !(n instanceof ProcessingInstruction);")]
    [TestCase("const p = new a.ProcessingInstruction('t'); return p.getAttributeNames() instanceof a.Array && !(p.getAttributeNames() instanceof Array);")]
    public async Task FrameNodesAndConstructorsKeepTheirOwningRealm(string assertion)
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", "<!doctype html><body><div id=parsed>text<!--comment--></div>")
            .MapHtml("/", "<!doctype html><body><iframe src=/child></iframe><iframe src=/child></iframe>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.EvaluateAsync<bool>("(() => { const a = frames[0], b = frames[1]; " + assertion + " })()"))
            .Should().BeTrue(assertion);
    }

    [TestCase("t")]
    [TestCase("𐀀")]
    public async Task SavedProcessingInstructionConstructorKeepsItsDocumentAfterFrameNavigation(string target)
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", "<!doctype html><body>old")
            .MapHtml("/", "<!doctype html><body><iframe src=/child></iframe>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("""
            var frame = document.querySelector('iframe');
            var oldDocument = frame.contentDocument;
            var OldPI = frame.contentWindow.ProcessingInstruction;
            frame.srcdoc = '<!doctype html><body>new';
            """);
        (await loopback.Page.WaitForAsync("frame.contentDocument !== oldDocument", TimeSpan.FromSeconds(30)))
            .Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>($$"""
            (() => {
              const pi = new OldPI('{{target}}', 'data');
              return pi.ownerDocument === oldDocument && pi instanceof OldPI &&
                !(pi instanceof frame.contentWindow.ProcessingInstruction) && pi.data === 'data';
            })()
            """)).Should().BeTrue();
    }

    [Test]
    public void LazyShapesAndRestoredGlobalsUseTheirOwningIntrinsics()
    {
        using var engine = new Engine(options => options.UseWebApis());
        DomBindings.Install(engine);
        var second = engine._host.CreateRealm();
        WebApiRegistration.InstallInRealm(engine, second);
        DomBindings.Install(engine, second);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var a = DomRealm.Of(engine);
        var b = DomRealm.Of(engine, second);
        using (new RealmScope(engine, second))
        {
            a.PrototypeOf(DomInterfaces.Node).Get("appendChild").AsObject().Prototype
                .Should().BeSameAs(engine._mainRealm.Intrinsics.Function.PrototypeObject);
        }
        b.PrototypeOf(DomInterfaces.Node).Get("appendChild").AsObject().Prototype
            .Should().BeSameAs(second.Intrinsics.Function.PrototypeObject);
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Evaluate("Node").Should().BeSameAs(a.InterfaceObjectOf(DomInterfaces.Node));
        a.InterfaceObjectOf(DomInterfaces.Node).Should().NotBeSameAs(b.InterfaceObjectOf(DomInterfaces.Node));
    }

    [Test]
    public void DisposingTheEngineReleasesDocumentAssociations()
    {
        var engine = new Engine();
        var dom = DomRealm.Of(engine);
        using var context = BrowsingContext.New();
        using var document = new HtmlParser(default, context).ParseDocument("");
        dom.AssociateDocument(document, associatedGlobal: true);
        engine.Dispose();
        dom.Document.Should().BeNull();
        dom.TryGetDocumentRealm(document, out _).Should().BeFalse();
    }

    [TestCase("container.innerHTML = '<p>text</p><!--comment-->'")]
    [TestCase("container.textContent = 'text'")]
    [TestCase("container.append('text')")]
    [TestCase("container.prepend('text')")]
    [TestCase("container.replaceChildren('text')")]
    [TestCase("container.insertAdjacentHTML('beforeend', '<p>text</p><!--comment-->')")]
    [TestCase("container.innerHTML = '<div></div>'; container.firstChild.outerHTML = '<p>text</p><!--comment-->'")]
    public void BindingMarkupCreationIsRecordedBeforeNativeAdoption(string create)
    {
        using var fixture = DomTestFixture.Create("<div id=container></div>");
        fixture.Execute("var container = document.getElementById('container'); " + create);
        var a = DomRealm.Of(fixture.Engine);
        var second = fixture.Engine._host.CreateRealm();
        var b = DomRealm.Of(fixture.Engine, second);
        using var context = BrowsingContext.New();
        using var document = new HtmlParser(default, context).ParseDocument("");
        b.AssociateDocument(document);
        var container = fixture.Document.GetElementById("container")!;
        document.Adopt(container);
        b.WrapNode(container.FirstChild!).DomRealm.Should().BeSameAs(a);
        b.WrapNode(container.LastChild!).DomRealm.Should().BeSameAs(a);
    }

    [Test]
    public void InvalidRealmsAreRejectedBeforeDocumentAssociation()
    {
        using var engine = new Engine();
        using var other = new Engine();
        var realm = DomRealm.Of(engine);
        Action foreign = () => DomRealm.Of(engine, other._mainRealm);
        Action incomplete = () => DomRealm.Of(engine, new Realm());
        foreign.Should().Throw<ArgumentException>();
        incomplete.Should().Throw<ArgumentException>();
        realm.Document.Should().BeNull();
    }

    [Test]
    public void ARecordedNativeSubtreeKeepsItsRealmBeforeAnyWrapperExists()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var second = engine._host.CreateRealm();
        WebApiRegistration.InstallInRealm(engine, second);
        DomBindings.Install(engine, second);
        BrowserEventRealm.Install(engine, second);
        var a = DomRealm.Of(engine);
        var b = DomRealm.Of(engine, second);
        using var contextA = BrowsingContext.New();
        using var contextB = BrowsingContext.New();
        using var documentA = new HtmlParser(default, contextA).ParseDocument("<div>text<!--comment--></div>");
        using var documentB = new HtmlParser(default, contextB).ParseDocument("");
        a.AssociateDocument(documentA);
        b.AssociateDocument(documentB);
        a.WrapNode(documentA);
        var node = documentA.QuerySelector("div")!;
        documentB.Adopt(node);
        b.WrapNode(node.FirstChild!).DomRealm.Should().BeSameAs(a);
        b.WrapNode(node.LastChild!).DomRealm.Should().BeSameAs(a);
        b.WrapNode(node).Should().BeSameAs(a.WrapNode(node));
    }

    [Test]
    public void RepeatedRecordingPreservesAttributesTemplatesAndDetachedCreationRealms()
    {
        using var fixture = DomTestFixture.Create("<div id=host data-original=x><template><span title=y>text</span></template></div>");
        fixture.Execute("""
            var host = document.getElementById('host');
            var shadow = host.attachShadow({mode: 'open'});
            shadow.innerHTML = '<b title=z>shadow</b>';
            host.saved = { value: 42 };
            var saved = host.saved;
            """);
        var a = DomRealm.Of(fixture.Engine);
        var second = fixture.Engine._host.CreateRealm();
        var b = DomRealm.Of(fixture.Engine, second);
        using var context = BrowsingContext.New();
        using var document = new HtmlParser(default, context).ParseDocument("");
        b.AssociateDocument(document);
        var host = fixture.Document.GetElementById("host")!;
        var attribute = host.Attributes["data-original"]!;
        var template = (AngleSharp.Html.Dom.IHtmlTemplateElement) host.FirstElementChild!;
        var content = template.Content;
        var shadow = host.ShadowRoot!;
        a.RecordSubtree(host);
        document.Adopt(host);
        b.RecordSubtree(host);
        b.RecordSubtree(host);
        b.CreationRealmOf(attribute).Should().BeSameAs(a);
        b.CreationRealmOf(content).Should().BeSameAs(a);
        b.CreationRealmOf(content.FirstChild!).Should().BeSameAs(a);
        b.CreationRealmOf(shadow).Should().BeSameAs(a);
        b.CreationRealmOf(shadow.FirstChild!).Should().BeSameAs(a);
        b.WrapNode(host).Should().BeSameAs(a.WrapNode(host));
        fixture.Bool("host.saved === saved && host.saved.value === 42").Should().BeTrue();

        // A late native attribute belongs to the current document, not its element's creation realm.
        host.SetAttribute("data-late", "new");
        a.RecordSubtree(host);
        a.CreationRealmOf(host.Attributes["data-late"]!).Should().BeSameAs(b);
        a.CreationRealmOf(attribute).Should().BeSameAs(a);
    }
}
