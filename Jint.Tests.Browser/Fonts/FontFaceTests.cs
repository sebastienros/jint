using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Fonts;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// CSS Font Loading: <c>FontFace</c> built from a string or a buffer, its descriptors parsed and serialized
/// by the parser's typed grammar, its <c>load()</c> fetched through the page's resource owner, and
/// <c>document.fonts</c> as the set-like <c>FontFaceSet</c> whose status, <c>ready</c> promise and load
/// events follow the faces it holds.
/// </summary>
public sealed class FontFaceTests
{
    // "\0\x01\0\0" and eight more bytes: a TrueType signature a sniff accepts, with no tables behind it.
    private const string TrueType = "new Uint8Array([0,1,0,0, 0,0,0,0, 0,0,0,0])";

    private static async Task<global::Jint.Browser.Page> BlankAsync(Browser browser)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");
        return page;
    }

    [Test]
    public async Task InterfacesHaveTheShapeTheirIdlDeclares()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let refused = '';
              try { new FontFaceSet([]); } catch (e) { refused = e.name; }
              const proto = FontFaceSet.prototype;
              return [
                FontFace.length,
                Object.getPrototypeOf(proto) === EventTarget.prototype,
                refused,
                proto.keys === proto.values,
                proto[Symbol.iterator] === proto.values,
                Object.getOwnPropertyDescriptor(FontFace.prototype, 'family').enumerable,
                document.fonts === document.fonts,
                document.fonts instanceof FontFaceSet,
                Object.prototype.toString.call(document.fonts),
                Object.prototype.toString.call(new FontFace('a', 'url(x)')),
                Object.getPrototypeOf(FontFaceSetLoadEvent.prototype) === Event.prototype,
              ].join(',');
            })()
            """)).Should().Be("2,true,TypeError,true,true,true,true,true,[object FontFaceSet],[object FontFace],true");
    }

    [Test]
    public async Task DescriptorsDefaultAndSerializeThroughTheParser()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const d = new FontFace('My Font', 'url(a.woff2)');
              const e = new FontFace('x', 'url(x)', { style: 'oblique 10deg', weight: '100   400', unicodeRange: 'U+0-7F, u+4??', display: 'swap' });
              return [
                d.family, d.style, d.weight, d.stretch, d.unicodeRange, d.featureSettings, d.display, d.status,
                e.style, e.weight, e.unicodeRange, e.display,
              ].join('|');
            })()
            """)).Should().Be("My Font|normal|normal|normal|U+0-10FFFF|normal|auto|unloaded|oblique 10deg|100 400|U+0-7F, U+400-4FF|swap");
    }

    [Test]
    public async Task AnInvalidDescriptorErrorsTheFaceAndASetterThrows()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const bad = new FontFace('x', 'url(x)', { weight: 'heavy' });
              let rejected = '';
              try { await bad.loaded; } catch (e) { rejected = e.name; }
              const good = new FontFace('x', 'url(x)');
              let thrown = '';
              try { good.weight = 'heavy'; } catch (e) { thrown = e.name; }
              good.weight = 'bold';
              return [bad.status, rejected, bad.weight === '', thrown, good.weight].join(',');
            })()
            """)).Should().Be("error,SyntaxError,true,SyntaxError,bold");
    }

    [Test]
    public async Task BinaryDataLoadsWhenItIsAFontFileAndFailsOtherwise()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAndAwaitAsync<string>(
            $$"""
            (async () => {
              const face = new FontFace('bin', {{TrueType}});
              const initial = face.status;
              const loaded = await face.loaded;
              const junk = new FontFace('junk', new Uint8Array(16).buffer);
              let failed = '';
              try { await junk.load(); } catch (e) { failed = e.name; }
              return [initial, loaded === face, face.status, face.load() === face.loaded, failed, junk.status].join(',');
            })()
            """)).Should().Be("unloaded,true,loaded,true,SyntaxError,error");
    }

    [Test]
    public async Task DataUrlSourcesLoadAndLocalOnesAreSkipped()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const face = new FontFace('d', 'local(Arial), url(data:font/ttf;base64,AAEAAAAAAAAAAAAA) format("truetype")');
              const promise = face.load();
              const during = face.status;
              await promise;
              const local = new FontFace('l', 'local(Arial)');
              let failed = '';
              try { await local.load(); } catch (e) { failed = e.name; }
              return [during, face.status, failed, local.status].join(',');
            })()
            """)).Should().Be("loading,loaded,NetworkError,error");
    }

    [Test]
    public async Task UrlSourcesAreFetchedAsFontsAndFallBackInOrder()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<p>fonts</p>")
            .Map("/good.ttf", _ => LoopbackResponse.Raw([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], "font/ttf")));
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const face = new FontFace('f', 'url(/missing.ttf), url(/good.ttf)');
              await face.load();
              const missing = new FontFace('m', 'url(/missing.ttf)');
              let failed = '';
              try { await missing.load(); } catch (e) { failed = e.name; }
              return [face.status, failed, missing.status].join(',');
            })()
            """)).Should().Be("loaded,NetworkError,error");

        fixture.Server.Received.Select(r => r.Path).Should().Contain("/good.ttf");
    }

    [Test]
    public async Task TheSetIsSetLike()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const set = document.fonts;
              const a = new FontFace('a', 'url(a)');
              const b = new FontFace('b', 'url(b)');
              const chained = set.add(a) === set;
              set.add(b).add(a);
              const seen = [];
              set.forEach((v, k, s) => seen.push(v.family + (v === k) + (s === set)));
              const iterated = [...set].map(f => f.family).join('');
              const entries = [...set.entries()].map(([k, v]) => k === v).join('');
              const had = set.has(a) && !set.has(new FontFace('c', 'url(c)'));
              const deleted = set.delete(a);
              const again = set.delete(a);
              const sizeAfter = set.size;
              set.clear();
              return [chained, seen.join(''), iterated, entries, had, deleted, again, sizeAfter, set.size].join(',');
            })()
            """)).Should().Be("true,atruetruebtruetrue,ab,truetrue,true,true,false,1,0");
    }

    [Test]
    public async Task TheSetTracksLoadsAndFiresItsEvents()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const set = document.fonts;
              await set.ready;
              const log = [];
              const names = e => e.fontfaces.map(f => f.family).join('+');
              set.onloading = e => log.push('loading:' + names(e));
              set.addEventListener('loadingdone', e => log.push('done:' + names(e) + ':' + Object.isFrozen(e.fontfaces) + ':' + (e.fontfaces === e.fontfaces) + ':' + e.isTrusted));
              set.onloadingerror = e => log.push('error:' + names(e));
              const good = new FontFace('good', 'url(data:font/ttf;base64,AAEAAAAAAAAAAAAA)');
              const bad = new FontFace('bad', 'url(data:font/ttf;base64,AAAA)');
              set.add(good); set.add(bad);
              const idle = set.status;
              const ready = set.ready;
              good.load(); bad.load();
              const busy = set.status;
              const fresh = set.ready !== ready;
              await set.ready;
              await new Promise(r => setTimeout(r, 0));
              return [idle, busy, fresh, set.status, log.join(' ')].join(',');
            })()
            """)).Should().Be("loaded,loading,true,loaded,loading: done:good:true:true:true error:bad");
    }

    [Test]
    public async Task CheckAndLoadMatchByFamilyAndUnicodeRange()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const set = document.fonts;
              const data = 'url(data:font/ttf;base64,AAEAAAAAAAAAAAAA)';
              const latin = new FontFace('Fancy', data, { unicodeRange: 'U+0-7F' });
              const greek = new FontFace('Fancy', data, { unicodeRange: 'U+370-3FF' });
              set.add(latin); set.add(greek);
              const before = [set.check('12px Fancy'), set.check('12px Unknown'), set.check('12px "fancy"', 'abc')];
              let syntax = '';
              try { set.check('not a font'); } catch (e) { syntax = e.name; }
              let rejected = '';
              try { await set.load('not a font'); } catch (e) { rejected = e.name; }
              const faces = await set.load('12px Fancy', 'abc');
              return [before.join('|'), syntax, rejected, faces.length, faces[0] === latin, latin.status, greek.status, set.check('12px Fancy', 'abc'), set.check('12px Fancy', '\u03b1')].join(',');
            })()
            """)).Should().Be("false|true|false,SyntaxError,SyntaxError,1,true,loaded,unloaded,true,false");
    }

    [Test]
    public async Task TheLoadEventIsConstructibleWithASequenceOfFaces()
    {
        await using var browser = new Browser();
        var page = await BlankAsync(browser);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const f = new FontFace('a', 'url(a)');
              const e = new FontFaceSetLoadEvent('loadingdone', { fontfaces: new Set([f]) });
              const empty = new FontFaceSetLoadEvent('x');
              let refused = '';
              try { new FontFaceSetLoadEvent('x', { fontfaces: [f, {}] }); } catch (err) { refused = err.name; }
              return [e.type, e.fontfaces.length, e.fontfaces[0] === f, Object.isFrozen(e.fontfaces), empty.fontfaces.length, e.isTrusted, refused, FontFaceSetLoadEvent.length].join(',');
            })()
            """)).Should().Be("loadingdone,1,true,true,0,false,TypeError,1");
    }
}
