using AngleSharp.Dom;
using Jint.Browser.Dom;

namespace Jint.Tests.Browser.Dom;

public class DocumentTypeFactoryTests
{
    [TestCase("")]
    [TestCase("~")]
    [TestCase("edi:{")]
    [TestCase("ns:EEE.")]
    [TestCase("\U00010000")]
    [TestCase("/=")]
    public void CreatedDoctypePreservesDataAndNativeIdentity(string name)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Engine.SetValue("name", name);
        fixture.Execute("""
            var publicId = "f'o\"o>\0\ud800";
            var systemId = "s'\"y>\0\udfff";
            var node = document.implementation.createDocumentType(name, publicId, systemId);
            document.insertBefore(node, document.documentElement);
            """);
        fixture.Engine.Evaluate("""
            node === document.doctype && node.ownerDocument === document &&
            node.name === name && node.publicId === publicId && node.systemId === systemId &&
            node.cloneNode().isEqualNode(node)
            """).Should().Be(true);
        var native = ((DomNodeObject) fixture.Engine.GetValue("node")).Node;
        native.Should().BeSameAs(fixture.Document.Doctype);
        ((IDocumentType) native).Name.Should().Be(name);
        fixture.Engine.Evaluate("""
            var other = document.implementation.createHTMLDocument();
            other.doctype.remove();
            other.adoptNode(node);
            other.insertBefore(node, other.documentElement);
            node === other.doctype && node.ownerDocument === other && node.name === name &&
            node.publicId === publicId && node.systemId === systemId
            """).Should().Be(true);
    }

    [TestCase("document.implementation.createHTMLDocument()")]
    [TestCase("new Document()")]
    public void UsesTheImplementationsDocumentAndConvertsArgumentsOnceBeforeValidation(string documentSource)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Execute("var other = " + documentSource);
        fixture.Engine.Evaluate("""
            var saved = other.implementation;
            var calls = [];
            function arg(label, value) { return {toString() { calls.push(label); return value; }}; }
            var node = saved.createDocumentType(arg('name', '~'), arg('public', 'p'), arg('system', 's'));
            node.ownerDocument === other && calls.join() === 'name,public,system'
            """).Should().Be(true);
        fixture.Engine.Evaluate("""
            calls = [];
            var error;
            try { saved.createDocumentType(arg('name', '>'), arg('public', 'p'), arg('system', 's')); }
            catch (e) { error = e.name; }
            error === 'InvalidCharacterError' && calls.join() === 'name,public,system'
            """).Should().Be(true);
        fixture.Engine.Evaluate("""
            calls = [];
            try { saved.createDocumentType(arg('name', '~'), arg('public', 'p')); }
            catch (e) { error = e.name; }
            error === 'TypeError' && calls.length === 0
            """).Should().Be(true);
    }
}
