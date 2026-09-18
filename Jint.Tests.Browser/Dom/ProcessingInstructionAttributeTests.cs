using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Native;

namespace Jint.Tests.Browser.Dom;

public class ProcessingInstructionAttributeTests
{
    [TestCase("p.setAttribute('$', 'v'); return p.getAttribute('$')", "v")]
    [TestCase("p.setAttribute('a','1'); p.setAttribute('b','2'); p.setAttribute('a','3'); p.removeAttribute('a'); p.setAttribute('a','4'); return p.getAttributeNames().join(',')", "b,a")]
    [TestCase("p.data = `a='&amp;&#65;&#x10000;'`; return p.getAttribute('a')", "&A𐀀")]
    [TestCase("p.data = `a='1' a='2'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `a='1'b='2'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `a='&#0;'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `a='&unknown;'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `a='&#xD800;'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `a='&#x110000;'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `a='<'`; return p.hasAttributes()", "false")]
    [TestCase("p.data = `𐀀='v'`; return p.getAttribute('𐀀')", "v")]
    [TestCase("p.data = ` a = ' x\\t\\r\\ny ' `; return p.getAttribute('a')", " x\t\r\ny ")]
    public void AttributeMapFollowsPseudoAttributeGrammar(string source, string expected)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate("(() => { const p = document.createProcessingInstruction('t', ''); " + source + "; })()")
            .ToString().Should().Be(expected);
    }

    [TestCase("p.data = p.data")]
    [TestCase("p.nodeValue = p.nodeValue")]
    [TestCase("p.textContent = p.textContent")]
    [TestCase("CharacterData.prototype.appendData.call(p, '')")]
    [TestCase("CharacterData.prototype.insertData.call(p, 0, '')")]
    [TestCase("CharacterData.prototype.deleteData.call(p, 0, 0)")]
    [TestCase("CharacterData.prototype.replaceData.call(p, 0, 0, '')")]
    public void ExplicitEqualDataMutationsReparseTheMap(string mutation)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', '');
              p.setAttribute('$', 'value');
              const before = p.getAttribute('$');
              {{mutation}};
              return before + '|' + p.hasAttributes();
            })()
            """).ToString().Should().Be("value|false");
    }
    [Test]
    public async Task AttributeWritesUseNativeCharacterDataMutationRecords()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<string>("""
            const p = document.createProcessingInstruction('t', 'a="1"');
            const observer = new MutationObserver(() => {});
            observer.observe(p, { characterData: true, characterDataOldValue: true, attributes: true });
            p.setAttribute('a', '2');
            p.removeAttribute('absent');
            p.toggleAttribute('a', true);
            const records = observer.takeRecords();
            [records.length, records[0].type, records[0].target === p, records[0].oldValue,
              records[1].oldValue, p.data].join('|')
            """)).Should().Be("2|characterData|true|a=\"1\"|a=\"2\"|a=\"2\"");
    }

    [TestCase("p.cloneNode()")]
    [TestCase("document.importNode(p)")]
    public void CloningStartsWithAnEmptyAttributeMap(string clone)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', 'a="1"');
              p.setAttribute('$', 'value');
              const copy = {{clone}};
              return copy.hasAttributes() + '|' + p.getAttributeNames().join(',');
            })()
            """).ToString().Should().Be("false|a,$");
    }

    [Test]
    public void AdoptionPreservesTheMapAndFollowingDataWritesReparseIt()
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate("""
            (() => {
              const p = document.createProcessingInstruction('t', '');
              p.setAttribute('$', 'value');
              const other = document.implementation.createHTMLDocument();
              other.adoptNode(p);
              const preserved = p.getAttribute('$');
              p.data = 'a="new"';
              return preserved + '|' + p.getAttribute('a') + '|' + p.ownerDocument.isSameNode(other);
            })()
            """).ToString().Should().Be("value|new|true");
    }

    [Test]
    public void ConversionPrecedesMutationAndPreservesThrownValues()
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate("""
            (() => {
              const p = document.createProcessingInstruction('t', '');
              const sentinel = {};
              const calls = [];
              let caught;
              try {
                p.setAttribute({toString() { calls.push('name'); return ''; }},
                  {toString() { calls.push('value'); throw sentinel; }});
              } catch(e) { caught = e; }
              return calls.join(',') + '|' + (caught === sentinel) + '|' + p.hasAttributes();
            })()
            """).ToString().Should().Be("name,value|true|false");
    }

    [TestCase("container.cloneNode(true).firstChild")]
    [TestCase("document.importNode(container, true).firstChild")]
    [TestCase("document.importNode(p)")]
    public void NativeClonePathsKeepProcessingInstructionData(string operation)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', 'data');
              const container = document.createElement('div');
              container.appendChild(p);
              return ({{operation}}).data;
            })()
            """).ToString().Should().Be("data");
    }

    [Test]
    public void AttributeStateDoesNotRetainDetachedNativeNodes()
    {
        using var fixture = DomTestFixture.Create("");
        var reference = CreateDetachedState(fixture.Document, DomRealm.Of(fixture.Engine));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        reference.IsAlive.Should().BeFalse();
        GC.KeepAlive(fixture);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDetachedState(IDocument document, DomRealm realm)
    {
        var node = document.CreateProcessingInstruction("t", "");
        DomProcessingInstructionAttributes.Invoke(realm, node, "setAttribute", [JsString.Create("a"), JsString.Create("v")]);
        return new WeakReference(node);
    }

}
