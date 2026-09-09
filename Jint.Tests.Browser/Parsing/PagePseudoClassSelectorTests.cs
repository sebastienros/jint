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

    /// <summary>
    /// HTML §4.16.3 matches <c>:default</c> against a form's own default button, an <c>input</c> which the
    /// <c>checked</c> attribute applies to and carries, and an <c>option</c> with a <c>selected</c>
    /// attribute. This is upstream's <c>html/semantics/selectors/pseudo-classes/default.html</c>.
    /// </summary>
    [Test]
    public async Task DefaultMatchesTheFormDefaultButtonAndTheCheckedAndSelectedAttributes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
            <form>
              <button id=button1 type=button>button1</button>
              <button id=button2 type=submit>button2</button>
            </form>
            <form>
              <button id=button3 type=reset>button3</button>
              <button id=button4>button4</button>
            </form>
            <button id=button5 type=submit>button5</button>
            <form id=form1>
              <input type=text id=input1>
            </form>
            <input type=text id=input2 form=form1>
            <form>
              <input type=submit id=input3>
              <input type=submit id=input4>
            </form>
            <form>
              <input type=image id=input5>
              <input type=image id=input6>
            </form>
            <form>
              <input type=submit id=input7>
            </form>
            <input type=checkbox id=checkbox1 checked>
            <input type=checkbox id=checkbox2>
            <input type=checkbox id=checkbox3 default>
            <input type=radio name=radios id=radio1 checked>
            <input type=radio name=radios id=radio2>
            <input type=radio name=radios id=radio3 default>
            <select id=select1>
             <optgroup label="options" id=optgroup1>
              <option value="option1" id=option1>option1
              <option value="option2" id=option2 selected>option2
            </select>
            <dialog id="dialog">
              <input type=submit id=input8>
            </dialog>
            <form>
              <button id=button6 type='invalid'>button6</button>
              <button id=button7>button7</button>
            </form>
            <form>
              <button id=button8>button8</button>
              <button id=button9>button9</button>
            </form>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = () => Array.from(document.querySelectorAll(':default'), element => element.id).join(',');
              const initial = ids();
              button1.type = 'submit';
              return initial + '|' + ids();
            })()
            """)).Should().Be(
            "button2,button4,input3,input5,input7,checkbox1,radio1,option2,button6,button8|" +
            "button1,button4,input3,input5,input7,checkbox1,radio1,option2,button6,button8");
    }

    /// <summary>
    /// A form's default button is its first submit button in tree order, which HTML decides over form
    /// ownership rather than containment, so a control the <c>form</c> attribute associates counts and one
    /// the form contains for another owner does not.
    /// </summary>
    [Test]
    public async Task TheDefaultButtonIsTheFormsFirstSubmitButtonInTreeOrder()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
            <button id=before type=submit form=late>before</button>
            <form id=late>
              <button id=inside type=submit>inside</button>
            </form>
            <div id="detached"></div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const attached = Array.from(document.querySelectorAll(':default'), element => element.id).join(',');
              const orphan = document.createElement('button');
              orphan.id = 'orphan';
              detached.append(orphan);
              const withOrphan = Array.from(document.querySelectorAll(':default'), element => element.id).join(',');

              const away = document.createElement('form');
              const awayButton = document.createElement('button');
              awayButton.id = 'awayButton';
              away.append(awayButton);
              return [attached, withOrphan, awayButton.matches(':default'), orphan.matches(':default')].join('|');
            })()
            """)).Should().Be("before|before|true|false");
    }
    /// <summary>
    /// HTML §4.16.3 matches <c>:open</c> against a <c>details</c> or a <c>dialog</c> carrying <c>open</c>, a
    /// drop-down <c>select</c> whose drop-down box is open, and an <c>input</c> whose picker is open. The last
    /// two need a user interface, so only the first two can ever match here. AngleSharp's <c>IsOpen()</c>
    /// returns <c>false</c> for every element, so <c>&lt;details open&gt;</c> matched nothing at all.
    /// </summary>
    [Test]
    public async Task OpenMatchesTheDetailsAndDialogElementsCarryingTheAttribute()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <details id="closedDetails"><summary>s</summary>body</details>
              <details id="openDetails" open><summary>s</summary>body</details>
              <dialog id="closedDialog">d</dialog>
              <dialog id="openDialog" open>d</dialog>
              <div id="openDiv" open></div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['closedDetails', 'openDetails', 'closedDialog', 'openDialog', 'openDiv'];
              const states = ids.map(id => id + ':' + document.getElementById(id).matches(':open')).join(',');
              const open = Array.from(document.querySelectorAll(':open'), element => element.id).join(',');
              openDetails.removeAttribute('open');
              closedDialog.setAttribute('open', '');
              const after = Array.from(document.querySelectorAll(':open'), element => element.id).join(',');
              return states + '|' + open + '|' + after;
            })()
            """)).Should().Be(
            "closedDetails:false,openDetails:true,closedDialog:false,openDialog:true,openDiv:false|" +
            "openDetails,openDialog|closedDialog,openDialog");
    }

    /// <summary>
    /// Selectors §10.5: <c>:closed</c> is an element which has an open and a closed state and is in the
    /// closed one, so it is not the complement of <c>:open</c> over every element — only over the four
    /// categories HTML §4.16.3 gives the pair. AngleSharp registers no <c>:closed</c> selector at all, so the
    /// whole selector was a parse failure and every API that took one threw a <c>SyntaxError</c>.
    /// </summary>
    [Test]
    public async Task ClosedMatchesOnlyAnElementThatHasBothStatesAndIsInTheClosedOne()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <details id="closedDetails"><summary>s</summary>body</details>
              <details id="openDetails" open><summary>s</summary>body</details>
              <dialog id="closedDialog">d</dialog>
              <dialog id="openDialog" open>d</dialog>
              <div id="plainDiv"></div>
              <span id="plainSpan"></span>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['closedDetails', 'openDetails', 'closedDialog', 'openDialog', 'plainDiv', 'plainSpan'];
              const states = ids.map(id => id + ':' + document.getElementById(id).matches(':closed')).join(',');
              const closed = Array.from(document.querySelectorAll(':closed'), element => element.id).join(',');
              const both = document.querySelectorAll(':open:closed').length;
              return states + '|' + closed + '|' + both;
            })()
            """)).Should().Be(
            "closedDetails:true,openDetails:false,closedDialog:true,openDialog:false," +
            "plainDiv:false,plainSpan:false|closedDetails,closedDialog|0");
    }

    /// <summary>
    /// The two categories this browser can never open are still in the pair: a drop-down <c>select</c> and an
    /// <c>input</c> supporting a picker have both states, so each of them is <c>:closed</c>. HTML §4.10.7
    /// makes a <c>select</c> a drop-down box when it has no <c>multiple</c> attribute and its display size —
    /// the <c>size</c> attribute parsed as a non-negative integer, else 4 with <c>multiple</c> and 1 without —
    /// is 1; §4.10.5 makes the File Upload state the one that must support a picker.
    /// </summary>
    [Test]
    public async Task ADropDownSelectAndAFileInputAreClosedAndAListBoxIsNeither()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <select id="dropDown"><option>a</option></select>
              <select id="sizeOne" size="1"><option>a</option></select>
              <select id="listBox" size="4"><option>a</option></select>
              <select id="multiple" multiple><option>a</option></select>
              <select id="unparseableSize" size="wide"><option>a</option></select>
              <input id="file" type="file">
              <input id="text" type="text">
              <input id="color" type="color">
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['dropDown', 'sizeOne', 'listBox', 'multiple', 'unparseableSize', 'file', 'text', 'color'];
              const states = ids.map(id => id + ':' + document.getElementById(id).matches(':closed')).join(',');
              const open = document.querySelectorAll(':open').length;
              listBox.removeAttribute('size');
              return states + '|' + open + '|' + listBox.matches(':closed');
            })()
            """)).Should().Be(
            "dropDown:true,sizeOne:true,listBox:false,multiple:false,unparseableSize:true," +
            "file:true,text:false,color:false|0|true");
    }

    /// <summary>
    /// A selector this package registers has to reach every API a page can spell it in, and has to carry the
    /// text and the one-class specificity the parser gives an ordinary pseudo-class — <c>:closed</c> is the
    /// first one here that AngleSharp does not supply a default for, so both come from this file.
    /// </summary>
    [Test]
    public async Task ClosedIsAnOrdinaryPseudoClassEverySelectorApiAccepts()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <div id="host"><details id="details"><summary>s</summary>body</details></div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const sheet = document.createElement('style');
              sheet.textContent = 'details:closed { color: rgb(1, 2, 3) }';
              document.head.append(sheet);
              const rule = document.styleSheets[document.styleSheets.length - 1].cssRules[0];
              return [
                details.matches(':closed'),
                details.webkitMatchesSelector(':closed'),
                details.closest(':closed').id,
                host.querySelector(':closed').id,
                document.querySelectorAll('details:closed, dialog:closed').length,
                rule.selectorText,
                getComputedStyle(details).color,
              ].join('|');
            })()
            """)).Should().Be("true|true|details|details|1|details:closed|rgba(1, 2, 3, 1)");
    }
    /// <summary>
    /// HTML §4.16.3 matches <c>:in-range</c> and <c>:out-of-range</c> only against an element which is a
    /// candidate for constraint validation <i>and</i> has range limitations. AngleSharp asks neither
    /// question: its <c>IsInRange()</c> is "any <c>IValidation</c> element that is neither overflowing nor
    /// underflowing", so every input the <c>min</c> and <c>max</c> attributes do not apply to matched
    /// <c>:in-range</c>, and so did one they do apply to that carries neither.
    /// </summary>
    [Test]
    public async Task InRangeNeedsBothACandidateAndRangeLimitations()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <input type="number" value="0" min="0" max="10" id="numberIn">
              <input type="number" value="0" min="1" max="10" id="numberUnder">
              <input type="number" value="11" min="0" max="10" id="numberOver">
              <input type="number" value="0" min="0" max="10" id="numberDisabled" disabled>
              <input type="number" value="0" min="0" max="10" id="numberReadOnly" readonly>
              <input type="number" value="0" id="numberNoLimit">
              <input type="date" min="2005-10-10" max="2020-10-10" value="2010-10-10" id="dateIn">
              <input type="date" min="2010-10-10" max="2020-10-10" value="2005-10-10" id="dateUnder">
              <input type="date" value="2010-10-10" id="dateNoLimit">
              <input type="month" min="2000-04" value="2000-06" id="monthMinOnly">
              <input type="text" min="1" value="0" id="text">
              <input type="checkbox" min="1" id="checkbox">
              <input type="hidden" min="1" value="0" id="hidden">
              <textarea id="textarea"></textarea>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['numberIn', 'numberUnder', 'numberOver', 'numberDisabled', 'numberReadOnly',
                'numberNoLimit', 'dateIn', 'dateUnder', 'dateNoLimit', 'monthMinOnly', 'text', 'checkbox',
                'hidden', 'textarea'];
              const states = ids.map(id => {
                const element = document.getElementById(id);
                return id + ':' + element.matches(':in-range') + ':' + element.matches(':out-of-range');
              }).join(',');
              numberIn.value = -10;
              return states + '|' + numberIn.matches(':in-range') + ':' + numberIn.matches(':out-of-range');
            })()
            """)).Should().Be(
            "numberIn:true:false,numberUnder:false:true,numberOver:false:true," +
            "numberDisabled:false:false,numberReadOnly:false:false,numberNoLimit:false:false," +
            "dateIn:true:false,dateUnder:false:true,dateNoLimit:false:false,monthMinOnly:true:false," +
            "text:false:false,checkbox:false:false,hidden:false:false,textarea:false:false|false:true");
    }

    /// <summary>
    /// HTML §4.10.5.4 gives the Range state a default minimum of 0 and a default maximum of 100, so a range
    /// control always has range limitations; and its value sanitization algorithm clamps the value to the
    /// nearest boundary point, so it can suffer neither an underflow nor an overflow. AngleSharp reads the
    /// content attribute back unclamped, so a value outside the range reported both.
    /// </summary>
    [Test]
    public async Task ARangeControlIsAlwaysInRangeBecauseItsValueIsClamped()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <input type="range" value="50" id="noLimits">
              <input type="range" min="2" max="7" value="5" id="within">
              <input type="range" min="2" max="7" value="1" id="below">
              <input type="range" min="2" max="7" value="9" id="above">
              <input type="range" min="2" max="7" value="5" id="disabled" disabled>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['noLimits', 'within', 'below', 'above', 'disabled'];
              const states = ids.map(id => {
                const element = document.getElementById(id);
                return id + ':' + element.matches(':in-range') + ':' + element.matches(':out-of-range');
              }).join(',');
              const inRange = Array.from(document.querySelectorAll(':in-range'), e => e.id).join(',');
              const out = document.querySelectorAll(':out-of-range').length;
              return states + '|' + inRange + '|' + out;
            })()
            """)).Should().Be(
            "noLimits:true:false,within:true:false,below:true:false,above:true:false,disabled:false:false|" +
            "noLimits,within,below,above|0");
    }

    /// <summary>
    /// HTML §4.16.3 matches <c>:valid</c> and <c>:invalid</c> against elements which are <i>candidates for
    /// constraint validation</i>, so an element §4.10.19.2 bars from it matches neither. AngleSharp's
    /// <c>IsInvalid()</c> is <c>!CheckValidity()</c> and its <c>CheckValidity()</c> is
    /// <c>WillValidate &amp;&amp; Validity.IsValid</c>, so a barred element answered <c>false</c> and came
    /// back <c>:invalid</c> — a disabled control, a read-only one, a hidden input and a reset button all did.
    /// </summary>
    [Test]
    public async Task ValidAndInvalidMatchNeitherWhenTheElementIsBarredFromConstraintValidation()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <input id="satisfied" required value="x">
              <input id="failing" required value="">
              <input id="disabledFailing" required value="" disabled>
              <input id="readOnlyFailing" required value="" readonly>
              <input id="hidden" type="hidden" required value="">
              <input id="reset" type="reset">
              <button id="button" type="button"></button>
              <button id="submit" type="submit"></button>
              <div id="ordinary"></div>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['satisfied', 'failing', 'disabledFailing', 'readOnlyFailing', 'hidden', 'reset',
                'button', 'submit', 'ordinary'];
              const states = ids.map(id => {
                const element = document.getElementById(id);
                return id + ':' + element.matches(':valid') + ':' + element.matches(':invalid');
              }).join(',');
              disabledFailing.disabled = false;
              return states + '|' + disabledFailing.matches(':valid') + ':' + disabledFailing.matches(':invalid');
            })()
            """)).Should().Be(
            "satisfied:true:false,failing:false:true,disabledFailing:false:false,readOnlyFailing:false:false," +
            "hidden:false:false,reset:false:false,button:false:false,submit:true:false,ordinary:false:false|" +
            "false:true");
    }

    /// <summary>
    /// HTML §4.16.3's third category: a <c>fieldset</c> is <c>:invalid</c> when a descendant of it is a
    /// candidate for constraint validation that does not satisfy its constraints, and <c>:valid</c> otherwise.
    /// AngleSharp's <c>IsValid()</c> answers only for an <c>IValidation</c> element and a form, and a fieldset
    /// is barred from constraint validation itself, so it came back <c>:invalid</c> whatever it contained.
    /// </summary>
    [Test]
    public async Task AFieldsetIsInvalidWhenADescendantCandidateDoesNotSatisfyItsConstraints()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><html><body>
              <fieldset id="empty"></fieldset>
              <fieldset id="satisfied"><input required value="x"></fieldset>
              <fieldset id="failing"><input id="inner" required value=""></fieldset>
              <fieldset id="barred"><input required value="" disabled></fieldset>
              <fieldset id="nested"><fieldset id="inner2"><input required value=""></fieldset></fieldset>
            </body></html>
            """);

        (await page.EvaluateAsync<string>("""
            (() => {
              const ids = ['empty', 'satisfied', 'failing', 'barred', 'nested', 'inner2'];
              const before = ids.map(id => {
                const element = document.getElementById(id);
                return id + ':' + element.matches(':valid') + ':' + element.matches(':invalid');
              }).join(',');
              inner.value = 'x';
              const after = failing.matches(':valid') + ':' + failing.matches(':invalid');
              const detached = document.getElementById('satisfied');
              detached.remove();
              detached.querySelector('input').setCustomValidity('nope');
              return before + '|' + after + '|' + detached.matches(':valid') + ':' + detached.matches(':invalid');
            })()
            """)).Should().Be(
            "empty:true:false,satisfied:true:false,failing:false:true,barred:true:false," +
            "nested:false:true,inner2:false:true|true:false|false:true");
    }
}
