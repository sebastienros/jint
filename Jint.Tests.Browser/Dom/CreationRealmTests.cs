using Jint.HtmlParser;
using Jint.Browser.Accessibility;
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
    // Parsed nodes are branded lazily; each native adoption path must capture the brand before moving.
    [TestCase("const n = a.document.getElementById('parsed'); document.body.append(n); return n.firstChild instanceof a.Text && !(n.firstChild instanceof Text) && n.lastChild instanceof a.Comment;")]
    [TestCase("const n = a.document.getElementById('parsed'); const r = document.createRange(); r.selectNodeContents(document.body); r.insertNode(n); return n.firstChild instanceof a.Text && n.lastChild instanceof a.Comment;")]
    [TestCase("const n = a.document.getElementById('parsed'); document.adoptNode(n); const id = n.getAttributeNode('id'); return id instanceof a.Attr && !(id instanceof Attr) && id.ownerDocument === document;")]
    [TestCase("const e = a.document.getElementById('parsed'); const id = e.getAttributeNode('id'); e.removeAttributeNode(id); document.body.setAttributeNode(id); return id instanceof a.Attr && !(id instanceof Attr);")]
    [TestCase("const p = a.document.getElementById('tpl').content.firstChild; document.body.append(p); return p instanceof a.HTMLParagraphElement && !(p instanceof HTMLParagraphElement) && p.firstChild instanceof a.Text;")]
    public async Task FrameNodesAndConstructorsKeepTheirOwningRealm(string assertion)
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", "<!doctype html><body><div id=parsed>text<!--comment--></div><template id=tpl><p>t</p></template>")
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
        var document = MarkupParser.ParseHtml("");
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
        var document = MarkupParser.ParseHtml("");
        b.AssociateDocument(document);
        var container = ContentDom.ElementById(fixture.Document, "container")!;
        document.AdoptNode(container);
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
        var documentA = MarkupParser.ParseHtml("<div>text<!--comment--></div>");
        var documentB = MarkupParser.ParseHtml("");
        a.AssociateDocument(documentA);
        b.AssociateDocument(documentB);
        a.WrapNode(documentA);
        var node = ContentDom.First(documentA, "div")!;
        documentB.AdoptNode(node);
        b.WrapNode(node.FirstChild!).DomRealm.Should().BeSameAs(a);
        b.WrapNode(node.LastChild!).DomRealm.Should().BeSameAs(a);
        b.WrapNode(node).Should().BeSameAs(a.WrapNode(node));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DetachedNativeCharacterNodesKeepTheirCreationRealmBeforeFirstBindingObservation(bool comment)
    {
        // https://dom.spec.whatwg.org/#concept-node-adopt — adoption changes the node document,
        // not the creation realm. Reproduce #3971 without binding creation or subtree recording.
        using var engine = new Engine();
        var a = DomRealm.Of(engine);
        var b = DomRealm.Of(engine, engine._host.CreateRealm());
        var documentA = MarkupParser.ParseHtml("");
        var documentB = MarkupParser.ParseHtml("");
        a.AssociateDocument(documentA);
        b.AssociateDocument(documentB);

        Node node = comment ? documentA.CreateComment("native") : documentA.CreateTextNode("native");
        documentB.AdoptNode(node);
        node.OwnerDocument.Should().BeSameAs(documentB);

        var wrapper = b.WrapNode(node);
        wrapper.DomRealm.Should().BeSameAs(a);
        wrapper.Prototype.Should().BeSameAs(a.PrototypeOf(comment ? DomInterfaces.Comment : DomInterfaces.Text));
        wrapper.Prototype.Should().NotBeSameAs(b.PrototypeOf(comment ? DomInterfaces.Comment : DomInterfaces.Text));
        a.WrapNode(node).Should().BeSameAs(wrapper);
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
        var document = MarkupParser.ParseHtml("");
        b.AssociateDocument(document);
        var host = ContentDom.ElementById(fixture.Document, "host")!;
        var attribute = host.GetAttributeNode("data-original")!;
        var template = ContentDom.Children(host).First();
        var content = template.TemplateContent!;
        var shadow = (ShadowRoot) ((DomNodeObject) fixture.Engine.GetValue("shadow")).Node!;
        a.RecordSubtree(host);
        document.AdoptNode(host);
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
        a.CreationRealmOf(host.GetAttributeNode("data-late")!).Should().BeSameAs(b);
        a.CreationRealmOf(attribute).Should().BeSameAs(a);
    }
}
