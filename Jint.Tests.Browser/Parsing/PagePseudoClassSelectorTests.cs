#nullable enable

namespace Jint.Tests.Browser.Parsing;

using Browser = global::Jint.Browser.Browser;

/// <summary>The selector states whose answer the page corrects around AngleSharp's defaults.</summary>
public sealed class PagePseudoClassSelectorTests
{
    /// <summary>
    /// Selectors §8.2 permits a user agent with no visited-history model to treat every hyperlink as
    /// unvisited. HTML defines those hyperlinks as <c>a</c> and <c>area</c> elements carrying an
    /// <c>href</c>, including the empty string; a <c>link</c> element is not one of them.
    /// </summary>
    [Test]
    public async Task LinkMatchesEveryHtmlHyperlinkAndVisitedMatchesNone()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><head>
              <link id="link" href="/next">
            </head><body>
              <div id="root">
                <a id="anchor"></a><a id="emptyAnchor" href=""></a><a id="anchorHref" href="/next"></a>
                <map><area id="area"><area id="emptyArea" href=""><area id="areaHref" href="/next"></map>
                <div id="ordinary" href="/next"></div>
              </div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const states = ['anchor', 'emptyAnchor', 'anchorHref', 'area', 'emptyArea', 'areaHref', 'link', 'ordinary']
                .map(id => {
                  const element = document.getElementById(id);
                  return id + ':' + element.matches(':link') + ':' + element.matches(':visited');
                }).join(',');
              const links = Array.from(document.querySelectorAll('#root :link'), element => element.id).join(',');
              const visited = document.querySelectorAll('#root :visited').length;
              return states + '|' + links + '|' + visited;
            })()
            """)).Should().Be(
            "anchor:false:false,emptyAnchor:true:false,anchorHref:true:false,area:false:false," +
            "emptyArea:true:false,areaHref:true:false,link:false:false,ordinary:false:false|" +
            "emptyAnchor,anchorHref,emptyArea,areaHref|0");
    }

    /// <summary>The link state reads the current presence of <c>href</c>, rather than a parsed URL snapshot.</summary>
    [Test]
    public async Task LinkStateTracksHrefPresence()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<a id='first' href=''></a><a id='second'></a>");

        (await page.EvaluateAsync<string>("""
            (() => {
              const before = Array.from(document.querySelectorAll(':link'), element => element.id).join(',');
              const anyBefore = Array.from(document.querySelectorAll(':any-link'), element => element.id).join(',');
              first.removeAttribute('href');
              second.setAttribute('href', '');
              const after = Array.from(document.querySelectorAll(':link'), element => element.id).join(',');
              const anyAfter = Array.from(document.querySelectorAll(':any-link'), element => element.id).join(',');
              return before + '|' + anyBefore + '|' + after + '|' + anyAfter + '|'
                + second.matches(':link:visited');
            })()
            """)).Should().Be("first|first|second|second|false");
    }

    /// <summary>
    /// Selectors §8.1 defines <c>:any-link</c> as exactly the union of <c>:link</c> and <c>:visited</c>.
    /// Every selector API must therefore expose the same HTML hyperlink set, including customized built-ins.
    /// </summary>
    [Test]
    public async Task AnyLinkIsTheLinkHistoryUnionForHtmlHyperlinks()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><head>
              <link id="link" href="/next">
            </head><body>
              <div id="root">
                <a id="anchor"></a><a id="emptyAnchor" href=""></a><a id="anchorHref" href="/next"></a>
                <map><area id="area"><area id="emptyArea" href=""><area id="areaHref" href="/next"></map>
                <div id="ordinary" href="/next"></div>
              </div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              class FancyAnchor extends HTMLAnchorElement {}
              customElements.define('fancy-anchor', FancyAnchor, { extends: 'a' });
              const customized = document.createElement('a', { is: 'fancy-anchor' });
              customized.id = 'customized';
              customized.setAttribute('href', '');
              root.append(customized);

              const ids = ['anchor', 'emptyAnchor', 'anchorHref', 'area', 'emptyArea', 'areaHref',
                'customized', 'link', 'ordinary'];
              const states = ids.map(id => {
                const element = document.getElementById(id);
                return id + ':' + element.matches(':link') + ':' + element.matches(':visited') + ':'
                  + element.matches(':any-link') + ':' + element.webkitMatchesSelector(':any-link');
              }).join(',');
              const first = document.querySelector('#root :any-link').id;
              const any = Array.from(document.querySelectorAll('#root :any-link'), element => element.id).join(',');
              const union = Array.from(document.querySelectorAll('#root :link, #root :visited'), element => element.id).join(',');
              return states + '|' + first + '|' + any + '|' + union;
            })()
            """)).Should().Be(
            "anchor:false:false:false:false,emptyAnchor:true:false:true:true," +
            "anchorHref:true:false:true:true,area:false:false:false:false," +
            "emptyArea:true:false:true:true,areaHref:true:false:true:true," +
            "customized:true:false:true:true,link:false:false:false:false,ordinary:false:false:false:false|" +
            "emptyAnchor|emptyAnchor,anchorHref,emptyArea,areaHref,customized|" +
            "emptyAnchor,anchorHref,emptyArea,areaHref,customized");
    }

    /// <summary>
    /// SVG 2 §16.2 makes an SVG <c>a</c> a hyperlink when either the unnamespaced <c>href</c> or the
    /// deprecated XLink <c>href</c> is present. Element and attribute namespace checks stay exact.
    /// </summary>
    [Test]
    public async Task AnyLinkIsTheLinkHistoryUnionForSvgHyperlinks()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <svg id="root" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
                <a id="missing"></a><a id="empty" href=""></a><a id="href" href="/next"></a>
                <a id="emptyXlink" xlink:href=""></a><a id="xlinkHref" xlink:href="/next"></a>
              </svg>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const wrongNamespace = document.createElementNS('urn:example', 'a');
              wrongNamespace.id = 'wrongNamespace';
              wrongNamespace.setAttribute('href', '');
              root.append(wrongNamespace);

              const wrongCase = document.createElementNS('http://www.w3.org/2000/svg', 'A');
              wrongCase.id = 'wrongCase';
              wrongCase.setAttribute('href', '');
              root.append(wrongCase);

              const wrongAttributeNamespace = document.createElementNS('http://www.w3.org/2000/svg', 'a');
              wrongAttributeNamespace.id = 'wrongAttributeNamespace';
              wrongAttributeNamespace.setAttributeNS('urn:example', 'example:href', '');
              root.append(wrongAttributeNamespace);

              const ids = ['missing', 'empty', 'href', 'emptyXlink', 'xlinkHref',
                'wrongNamespace', 'wrongCase', 'wrongAttributeNamespace'];
              const states = ids.map(id => {
                const element = document.getElementById(id);
                return id + ':' + element.matches(':link') + ':' + element.matches(':visited') + ':'
                  + element.matches(':any-link') + ':' + element.webkitMatchesSelector(':any-link');
              }).join(',');
              const first = root.querySelector(':any-link').id;
              const any = Array.from(root.querySelectorAll(':any-link'), element => element.id).join(',');
              const union = Array.from(root.querySelectorAll(':link, :visited'), element => element.id).join(',');
              return states + '|' + first + '|' + any + '|' + union;
            })()
            """)).Should().Be(
            "missing:false:false:false:false,empty:true:false:true:true,href:true:false:true:true," +
            "emptyXlink:true:false:true:true,xlinkHref:true:false:true:true," +
            "wrongNamespace:false:false:false:false,wrongCase:false:false:false:false," +
            "wrongAttributeNamespace:false:false:false:false|empty|empty,href,emptyXlink,xlinkHref|" +
            "empty,href,emptyXlink,xlinkHref");
    }

    /// <summary>
    /// SVG 2 §16.2 makes a nested SVG <c>a</c> inactive beneath a hyperlink from either the HTML or SVG
    /// namespace. The rule follows the DOM ancestry even in a detached clone and ignores non-link names and
    /// namespaced attributes.
    /// </summary>
    [Test]
    public async Task NestedSvgAnchorsIgnoreTheirLinkAttributes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='root'></div>");

        (await page.EvaluateAsync<string>("""
            (() => {
              const svgNamespace = 'http://www.w3.org/2000/svg';
              const xlinkNamespace = 'http://www.w3.org/1999/xlink';
              const makeSvgAnchor = (id, namespace, value = '') => {
                const element = document.createElementNS(svgNamespace, 'a');
                element.id = id;
                if (namespace === null) element.setAttribute('href', value);
                else if (namespace !== undefined) element.setAttributeNS(namespace, 'xlink:href', value);
                return element;
              };
              const state = element => element.id + ':' + element.matches(':link') + ':'
                + element.matches(':visited') + ':' + element.matches(':any-link');

              const outerHref = makeSvgAnchor('outerHref', null);
              const innerHref = makeSvgAnchor('innerHref', null);
              const innerXlink = makeSvgAnchor('innerXlink', xlinkNamespace);
              outerHref.append(innerHref, innerXlink);

              const outerXlink = makeSvgAnchor('outerXlink', xlinkNamespace);
              const belowXlink = makeSvgAnchor('belowXlink', null);
              outerXlink.append(belowXlink);

              const htmlOuter = document.createElement('a');
              htmlOuter.id = 'htmlOuter';
              htmlOuter.setAttribute('href', '');
              const belowHtml = makeSvgAnchor('belowHtml', xlinkNamespace);
              htmlOuter.append(belowHtml);

              const foreignOuter = document.createElementNS('urn:example', 'a');
              foreignOuter.setAttribute('href', '');
              const belowForeign = makeSvgAnchor('belowForeign', null);
              foreignOuter.append(belowForeign);

              const wrongAttributeOuter = makeSvgAnchor('wrongAttributeOuter');
              wrongAttributeOuter.setAttributeNS('urn:example', 'example:href', '');
              const belowWrongAttribute = makeSvgAnchor('belowWrongAttribute', null);
              wrongAttributeOuter.append(belowWrongAttribute);

              const wrongCaseOuter = document.createElementNS(svgNamespace, 'A');
              wrongCaseOuter.setAttribute('href', '');
              const belowWrongCase = makeSvgAnchor('belowWrongCase', null);
              wrongCaseOuter.append(belowWrongCase);

              const missingOuter = makeSvgAnchor('missingOuter');
              const belowMissing = makeSvgAnchor('belowMissing', xlinkNamespace);
              missingOuter.append(belowMissing);

              root.append(outerHref, outerXlink, htmlOuter, foreignOuter, wrongAttributeOuter,
                wrongCaseOuter, missingOuter);

              const detached = outerHref.cloneNode(true);
              detached.id = 'detachedOuter';
              detached.firstElementChild.id = 'detachedInnerHref';
              detached.lastElementChild.id = 'detachedInnerXlink';

              const states = [outerHref, innerHref, innerXlink, outerXlink, belowXlink, htmlOuter,
                belowHtml, belowForeign, belowWrongAttribute, belowWrongCase, missingOuter, belowMissing,
                detached, detached.firstElementChild, detached.lastElementChild].map(state).join(',');
              const any = Array.from(root.querySelectorAll(':any-link'), element => element.id).join(',');
              return states + '|' + any + '|'
                + (outerHref.querySelector(':any-link') === null) + ':'
                + foreignOuter.querySelector(':any-link').id + ':'
                + (detached.querySelector(':any-link') === null);
            })()
            """)).Should().Be(
            "outerHref:true:false:true,innerHref:false:false:false,innerXlink:false:false:false," +
            "outerXlink:true:false:true,belowXlink:false:false:false,htmlOuter:true:false:true," +
            "belowHtml:false:false:false,belowForeign:true:false:true,belowWrongAttribute:true:false:true," +
            "belowWrongCase:true:false:true,missingOuter:false:false:false,belowMissing:true:false:true," +
            "detachedOuter:true:false:true,detachedInnerHref:false:false:false," +
            "detachedInnerXlink:false:false:false|" +
            "outerHref,outerXlink,htmlOuter,belowForeign,belowWrongAttribute,belowWrongCase,belowMissing|" +
            "true:belowForeign:true");
    }

    /// <summary>
    /// Selectors §13.1 limits <c>:enabled</c> to elements which have a disabled state. Links have no such
    /// state, with or without an <c>href</c>; form controls retain AngleSharp's enabled/disabled behavior.
    /// </summary>
    [Test]
    public async Task EnabledMatchesControlsButNeverLinks()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><head>
              <link id="link" rel="alternate" href="/next">
            </head><body>
              <a id="anchor"></a><a id="anchorHref" href="/next"></a>
              <map><area id="area"><area id="areaHref" href="/next"></map>
              <button id="button"></button><button id="disabledButton" disabled></button>
              <input id="input"><input id="disabledInput" disabled>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const links = ['anchor', 'anchorHref', 'area', 'areaHref', 'link']
                .map(id => id + ':' + document.getElementById(id).matches(':enabled')).join(',');
              const enabled = Array.from(document.querySelectorAll(':enabled'), element => element.id).join(',');
              const disabled = Array.from(document.querySelectorAll(':disabled'), element => element.id).join(',');
              return links + '|' + enabled + '|' + disabled;
            })()
            """)).Should().Be(
            "anchor:false,anchorHref:false,area:false,areaHref:false,link:false|button,input|disabledButton,disabledInput");
    }

    /// <summary>
    /// HTML §4.16.3 limits <c>:enabled</c> to the seven element types which have a disabled state, and
    /// §4.15 makes the <c>disabled</c> content attribute a boolean one. This is upstream's
    /// <c>html/semantics/selectors/pseudo-classes/enabled.html</c>.
    /// </summary>
    [Test]
    public async Task EnabledMatchesEveryControlThatIsNotActuallyDisabled()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
            <a id=link3></a>
            <area id=link4>
            <link id=link5>
            <a href="http://www.w3.org" id=link6></a>
            <area href="http://www.w3.org" id=link7>
            <link href="http://www.w3.org" id=link8>
            <button id=button1>button1</button>
            <button id=button2 disabled>button2</button>
            <input id=input1>
            <input id=input2 disabled>
            <select id=select1>
             <optgroup label="options" id=optgroup1>
              <option value="option1" id=option1 selected>option1
            </select>
            <select disabled id=select2>
             <optgroup label="options" disabled id=optgroup2>
              <option value="option2" disabled id=option2>option2
            </select>
            <textarea id=textarea1>textarea1</textarea>
            <textarea disabled id=textarea2>textarea2</textarea>
            <form>
             <p><input type=submit id=submitbutton></p>
            </form>
            <fieldset id=fieldset1></fieldset>
            <fieldset disabled id=fieldset2></fieldset>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            Array.from(document.querySelectorAll(':enabled'), element => element.id).join(',')
            """)).Should().Be(
            "button1,input1,select1,optgroup1,option1,textarea1,submitbutton,fieldset1");
    }

    /// <summary>
    /// HTML §4.16.3 matches <c>:disabled</c> against every actually disabled element, which §4.15 defines
    /// over the <c>disabled</c> attribute's presence, the disabled fieldset a control descends from and the
    /// nearest ancestor <c>select</c> of an <c>optgroup</c> or <c>option</c>. This is upstream's
    /// <c>html/semantics/selectors/pseudo-classes/disabled.html</c>.
    /// </summary>
    [Test]
    public async Task DisabledMatchesEveryActuallyDisabledElement()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
            <button id=button1 type=submit>button1</button>
            <button id=button2 disabled>button2</button>
            <input id=input1>
            <input id=input2 disabled>
            <input id=input3 readonly>
            <select id=select1>
             <optgroup label="options" id=optgroup1>
              <option value="option1" id=option1 selected>option1
            </select>
            <select disabled id=select2>
             <optgroup label="options" disabled id=optgroup2>
              <option value="option2" disabled id=option2>option2
            </select>
            <textarea id=textarea1>textarea1</textarea>
            <textarea disabled id=textarea2>textarea2</textarea>
            <fieldset id=fieldset1></fieldset>
            <fieldset disabled id=fieldset2>
              <legend><input type=checkbox id=club></legend>
              <p><label>Name on card: <input id=clubname required></label></p>
              <p><label>Card number: <input id=clubnum required pattern="[-0-9]+"></label></p>
            </fieldset>
            <label disabled></label>
            <object disabled></object>
            <output disabled></output>
            <img disabled>
            <meter disabled></meter>
            <progress disabled></progress>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = () => Array.from(document.querySelectorAll(':disabled'), element => element.id).join(',');
              const initial = ids();
              button2.removeAttribute('disabled');
              const removed = ids();
              button1.setAttribute('disabled', 'disabled');
              const added = ids();
              input2.setAttribute('type', 'submit');
              const retyped = ids();
              const detached = document.createElement('input');
              detached.setAttribute('disabled', 'disabled');
              const afterDetached = ids();

              const nested = document.createElement('fieldset');
              nested.id = 'fieldset_nested';
              nested.innerHTML = `
                <input id=input_nested>
                <button id=button_nested>button nested</button>
                <select id=select_nested>
                  <optgroup label="options" id=optgroup_nested>
                    <option value="options" id=option_nested>option nested</option>
                  </optgroup>
                </select>
                <textarea id=textarea_nested>textarea nested</textarea>
                <object id=object_nested></object>
                <output id=output_nested></output>
                <fieldset id=fieldset_nested2>
                  <input id=input_nested2>
                </fieldset>
              `;
              fieldset2.appendChild(nested);
              const within = Array.from(document.querySelectorAll('#fieldset2 :disabled'), element => element.id).join(',');
              return [initial, removed, added, retyped, afterDetached, within].join('|');
            })()
            """)).Should().Be(string.Join('|',
            "button2,input2,select2,optgroup2,option2,textarea2,fieldset2,clubname,clubnum",
            "input2,select2,optgroup2,option2,textarea2,fieldset2,clubname,clubnum",
            "button1,input2,select2,optgroup2,option2,textarea2,fieldset2,clubname,clubnum",
            "button1,input2,select2,optgroup2,option2,textarea2,fieldset2,clubname,clubnum",
            "button1,input2,select2,optgroup2,option2,textarea2,fieldset2,clubname,clubnum",
            "clubname,clubnum,fieldset_nested,input_nested,button_nested,select_nested,optgroup_nested," +
            "option_nested,textarea_nested,fieldset_nested2,input_nested2"));
    }

    /// <summary>
    /// HTML §4.15 excuses a control only from the first <c>legend</c> child of the disabled fieldset it
    /// descends from, so an outer disabled fieldset still reaches a control sheltered by an inner one, and a
    /// second <c>legend</c> shelters nothing.
    /// </summary>
    [Test]
    public async Task ADisabledFieldsetExcusesOnlyItsOwnFirstLegend()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
            <fieldset disabled id=outer>
              <fieldset disabled id=inner>
                <legend><input id=innerLegend></legend>
                <input id=innerBody>
              </fieldset>
            </fieldset>
            <fieldset disabled id=twoLegends>
              <legend><input id=firstLegend></legend>
              <legend><input id=secondLegend></legend>
            </fieldset>
            <fieldset id=enabledOuter>
              <legend><input id=enabledLegend></legend>
              <input id=enabledBody>
            </fieldset>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            ['outer', 'inner', 'innerLegend', 'innerBody', 'twoLegends', 'firstLegend', 'secondLegend',
              'enabledOuter', 'enabledLegend', 'enabledBody']
              .map(id => id + ':' + document.getElementById(id).matches(':disabled')).join(',')
            """)).Should().Be(
            "outer:true,inner:true,innerLegend:true,innerBody:true,twoLegends:true,firstLegend:false," +
            "secondLegend:true,enabledOuter:false,enabledLegend:false,enabledBody:false");
    }
}
