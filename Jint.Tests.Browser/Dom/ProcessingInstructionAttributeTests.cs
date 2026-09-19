using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Native;

namespace Jint.Tests.Browser.Dom;

public class ProcessingInstructionAttributeTests
{
    [TestCase("deleteContents", "start", "")]
    [TestCase("deleteContents", "end", "")]
    [TestCase("deleteContents", "collapsed", "$")]
    [TestCase("extractContents", "start", "")]
    [TestCase("extractContents", "end", "")]
    [TestCase("extractContents", "collapsed", "$")]
    public void RangeReplacementResetsTheBoundaryMapEvenWhenDataIsUnchanged(string operation, string edge, string names)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', '');
              p.setAttribute('$', 'value');
              const div = document.createElement('div'); div.append(p);
              const range = document.createRange();
              if ('{{edge}}' === 'end') { range.setStart(div, 0); range.setEnd(p, 0); }
              else {
                range.setStart(p, p.length);
                range.setEnd('{{edge}}' === 'collapsed' ? p : div, '{{edge}}' === 'collapsed' ? p.length : 1);
              }
              const before = p.data;
              range.{{operation}}();
              return (p.data === before) + '|' + p.getAttributeNames().join(',') + '|' + (p.parentNode === div);
            })()
            """).ToString().Should().Be("true|" + names + "|true");
    }

    [TestCase("start", "InvalidStateError")]
    [TestCase("end", "InvalidStateError")]
    [TestCase("collapsed", "HierarchyRequestError")]
    public void RejectedSurroundContentsPreservesBoundaryAttributes(string edge, string error)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', ''); p.setAttribute('$', 'value');
              const div = document.createElement('div'); div.append(p);
              const range = document.createRange();
              if ('{{edge}}' === 'end') { range.setStart(div, 0); range.setEnd(p, 0); }
              else {
                range.setStart(p, p.length);
                range.setEnd('{{edge}}' === 'collapsed' ? p : div, '{{edge}}' === 'collapsed' ? p.length : 1);
              }
              const before = p.data;
              try { range.surroundContents(document.createElement('span')); }
              catch (e) { return e.name + '|' + (p.data === before) + '|' + p.getAttributeNames().join(','); }
            })()
            """).ToString().Should().Be(error + "|true|$");
    }

    [TestCase(false, "")]
    [TestCase(true, "$")]
    public async Task SelectionDeletionResetsOnlyNoncollapsedBoundaryMaps(bool collapsed, string names)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<string>($$"""
            const p = document.createProcessingInstruction('t', ''); p.setAttribute('$', 'value');
            document.body.append(p);
            const range = document.createRange(); range.setStart(p, p.length);
            if (!{{(collapsed ? "true" : "false")}}) range.setEnd(document.body, document.body.childNodes.length);
            const selection = getSelection(); selection.removeAllRanges(); selection.addRange(range);
            selection.deleteFromDocument();
            p.getAttributeNames().join(',')
            """)).Should().Be(names);
    }

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

    [TestCase("p.toggleAttribute('a')", "true|true")]
    [TestCase("p.toggleAttribute('a', undefined)", "true|true")]
    [TestCase("p.toggleAttribute('a', false)", "false|false")]
    [TestCase("p.toggleAttribute('a', null)", "false|false")]
    [TestCase("p.toggleAttribute('a', {})", "true|true")]
    public void ToggleUsesTheOptionalBooleanForce(string operation, string expected)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate("(() => { const p = document.createProcessingInstruction('t', ''); return (" + operation + ") + '|' + p.hasAttribute('a'); })()")
            .ToString().Should().Be(expected);
    }

    [Test]
    public void RequiredArgumentCountIsCheckedBeforeConversions()
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate("""
            (() => {
              const p = document.createProcessingInstruction('t', '');
              let converted = false;
              try { p.setAttribute({toString() { converted = true; return 'a'; }}); }
              catch (e) { return (e instanceof TypeError) + '|' + converted; }
            })()
            """).ToString().Should().Be("true|false");
    }

    [Test]
    public async Task WellFormedXmlParsingInitializesTheNativePiMap()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        (await page.EvaluateAsync<string>("""
            const xml = new DOMParser().parseFromString("<?t a='v'?><root/>", 'application/xml');
            const pi = xml.firstChild;
            [pi.nodeType, pi.target, pi.getAttribute('a'), pi.ownerDocument === xml].join('|')
            """)).Should().Be("7|t|v|true");
    }

    [TestCase("p.cloneNode()")]
    [TestCase("document.importNode(p)")]
    public void EqualDataWritePreservesParsedClonedAttributes(string clone)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', 'a="v"');
              const copy = {{clone}};
              const initial = copy.getAttribute('a');
              copy.data = copy.data;
              return initial + '|' + copy.getAttribute('a');
            })()
            """).ToString().Should().Be("v|v");
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

    [TestCase("p.cloneNode()", "a", "a")]
    [TestCase("document.importNode(p)", "a", "a")]
    [TestCase("container.cloneNode(true).firstChild", "a", "a")]
    [TestCase("document.importNode(container, true).firstChild", "a", "a")]
    [TestCase("p.cloneNode()", "$", "")]
    [TestCase("document.importNode(p)", "$", "")]
    [TestCase("container.cloneNode(true).firstChild", "$", "")]
    [TestCase("document.importNode(container, true).firstChild", "$", "")]
    public void CloningReparsesNativeDataInsteadOfCopyingAttributeState(string clone, string name, string expectedNames)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', ''); p.setAttribute('{{name}}', 'value');
              const container = document.createElement('div'); container.append(p);
              const copy = {{clone}};
              return (copy.data === p.data) + '|' + copy.getAttributeNames().join(',') + '|' + p.getAttribute('{{name}}');
            })()
            """).ToString().Should().Be("true|" + expectedNames + "|value");
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
