using Jint.Browser.Accessibility;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class SvgDomTests
{
    private static DomTestFixture Fixture(string markup = "")
    {
        var fixture = DomTestFixture.Create("<svg id='svg' viewBox='0 0 200 100'>" + markup + "</svg>");
        fixture.Execute("""
            var svg = document.getElementById('svg');
            var el = svg.firstElementChild;
            function error(fn) { try { fn(); return ''; } catch (e) { return e.name; } }
            """);
        return fixture;
    }

    [TestCase("svg", "SVGSVGElement", "SVGGraphicsElement")]
    [TestCase("g", "SVGGElement", "SVGGraphicsElement")]
    [TestCase("defs", "SVGDefsElement", "SVGGraphicsElement")]
    [TestCase("symbol", "SVGSymbolElement", "SVGGraphicsElement")]
    [TestCase("use", "SVGUseElement", "SVGGraphicsElement")]
    [TestCase("image", "SVGImageElement", "SVGGraphicsElement")]
    [TestCase("switch", "SVGSwitchElement", "SVGGraphicsElement")]
    [TestCase("rect", "SVGRectElement", "SVGGeometryElement")]
    [TestCase("circle", "SVGCircleElement", "SVGGeometryElement")]
    [TestCase("ellipse", "SVGEllipseElement", "SVGGeometryElement")]
    [TestCase("line", "SVGLineElement", "SVGGeometryElement")]
    [TestCase("polyline", "SVGPolylineElement", "SVGGeometryElement")]
    [TestCase("polygon", "SVGPolygonElement", "SVGGeometryElement")]
    [TestCase("path", "SVGPathElement", "SVGGeometryElement")]
    [TestCase("text", "SVGTextElement", "SVGTextPositioningElement")]
    [TestCase("tspan", "SVGTSpanElement", "SVGTextPositioningElement")]
    [TestCase("textPath", "SVGTextPathElement", "SVGTextContentElement")]
    [TestCase("linearGradient", "SVGLinearGradientElement", "SVGGradientElement")]
    [TestCase("radialGradient", "SVGRadialGradientElement", "SVGGradientElement")]
    [TestCase("stop", "SVGStopElement", "SVGElement")]
    [TestCase("pattern", "SVGPatternElement", "SVGElement")]
    [TestCase("clipPath", "SVGClipPathElement", "SVGElement")]
    [TestCase("mask", "SVGMaskElement", "SVGElement")]
    [TestCase("marker", "SVGMarkerElement", "SVGElement")]
    [TestCase("metadata", "SVGMetadataElement", "SVGElement")]
    [TestCase("script", "SVGScriptElement", "SVGElement")]
    [TestCase("style", "SVGStyleElement", "SVGElement")]
    [TestCase("title", "SVGTitleElement", "SVGElement")]
    [TestCase("desc", "SVGDescElement", "SVGElement")]
    [TestCase("foreignObject", "SVGForeignObjectElement", "SVGGraphicsElement")]
    [TestCase("view", "SVGViewElement", "SVGElement")]
    [TestCase("a", "SVGAElement", "SVGGraphicsElement")]
    [TestCase("filter", "SVGFilterElement", "SVGElement")]
    [TestCase("unknown", "SVGElement", "Element")]
    [TestCase("lineargradient", "SVGElement", "Element")]
    public void ElementBrandsAndInheritance(string name, string type, string parent)
    {
        using var f = Fixture();
        f.Bool($$"""
            var e = document.createElementNS(svg.namespaceURI, '{{name}}');
            e instanceof {{type}} && e instanceof SVGElement &&
              Object.getPrototypeOf(e) === {{type}}.prototype &&
              Object.getPrototypeOf({{type}}.prototype) === {{parent}}.prototype &&
              Object.getPrototypeOf({{type}}) === {{parent}}
            """).Should().BeTrue();
        f.Text($"error(() => new {type}())").Should().Be("TypeError");
    }

    [Test]
    public void LengthIdentityAndTwoWayReflection()
    {
        using var f = Fixture("<rect x='2' y='3' width='20' height='10'/>");
        f.Bool("el.x === el.x && el.x.baseVal === el.x.baseVal && el.x.animVal === el.x.animVal && el.x.baseVal !== el.x.animVal").Should().BeTrue();
        f.Bool("el.x instanceof SVGAnimatedLength && el.x.baseVal instanceof SVGLength").Should().BeTrue();
        f.Text("var x = el.x.baseVal; el.setAttribute('x', '12px'); [x.value, x.unitType, x.valueAsString].join()").Should().Be("12,5,12px");
        f.Text("x.value = 5; el.getAttribute('x')").Should().Be("5px");
        f.Text("x.valueAsString = '10%'; [x.value, el.getAttribute('x'), el.x.animVal.value].join()").Should().Be("20,10%,20");
        f.Number("el.removeAttribute('x'); x.value").Should().Be(0);
        f.Text("el.setAttribute('x', 'bad'); [x.value, x.unitType].join()").Should().Be("0,1");
        f.Text("error(() => { x.value = NaN; })").Should().Be("TypeError");
        f.Text("error(() => { x.valueAsString = 'nonsense'; })").Should().Be("SyntaxError");
    }

    [Test]
    public void AnimatedValuesRejectEveryMutationLane()
    {
        using var f = Fixture("<rect x='2' transform='translate(3)'/>");
        f.Text("error(() => { el.x.animVal.value = 9; })").Should().Be("NoModificationAllowedError");
        f.Text("error(() => el.x.animVal.newValueSpecifiedUnits(1, 4))").Should().Be("NoModificationAllowedError");
        f.Text("error(() => { el.pathLength.animVal = 9; })").Should().Be("NoModificationAllowedError");
        f.Text("error(() => el.transform.animVal.clear())").Should().Be("NoModificationAllowedError");
        f.Text("error(() => el.transform.animVal.getItem(0).setScale(2, 2))").Should().Be("NoModificationAllowedError");
        f.Text("error(() => { el.transform.animVal.getItem(0).matrix.e = 8; })").Should().Be("NoModificationAllowedError");
        f.Text("error(() => el.transform.animVal.getItem(0).matrix.translateSelf(2))").Should().Be("NoModificationAllowedError");
        f.Text("error(() => { svg.viewBox.animVal.width = 8; })").Should().Be("NoModificationAllowedError");
        f.Text("error(() => { svg.preserveAspectRatio.animVal.align = 1; })").Should().Be("NoModificationAllowedError");
        f.Text("[el.getAttribute('x'), el.getAttribute('transform')].join()").Should().Be("2,translate(3)");
    }

    [Test]
    public void TransformParsingConsolidationAndMatrixWritesAreLive()
    {
        using var f = Fixture("<g transform='translate(10,20) scale(2)'/>");
        f.Text("var list = el.transform.baseVal; [list.length, list.numberOfItems, list[0].type, list[1].matrix.a].join()").Should().Be("2,2,2,2");
        f.Bool("list[0] === list.getItem(0) && list[0].matrix === list[0].matrix").Should().BeTrue();
        f.Text("String(list.consolidate().matrix)").Should().Be("matrix(2, 0, 0, 2, 10, 20)");
        f.Number("list.length").Should().Be(1);
        f.Text("var tr = svg.createSVGTransform(); tr.setTranslate(5, 6); list.appendItem(tr); String(el.getCTM())").Should().Be("matrix(2, 0, 0, 2, 20, 32)");
        f.Text("tr.matrix.e = 7; [tr.type, el.getAttribute('transform')].join('|')").Should().Be("1|matrix(2 0 0 2 10 20) matrix(1 0 0 1 7 6)");
        f.Text("tr.matrix.translateSelf(2, 3); [tr.matrix.e, tr.matrix.f].join()").Should().Be("9,9");
        f.Text("var retained = list[0]; el.setAttribute('transform', 'rotate(90 1 2)'); String(retained.matrix)").Should().Be("matrix(0, 1, -1, 0, 3, 1)");
        f.Bool("list.getItem(0) === retained").Should().BeTrue();
        f.Number("el.setAttribute('transform', 'translate(1) junk'); list.length").Should().Be(0);
        f.Bool("list.consolidate() === null").Should().BeTrue();
    }

    [Test]
    public void NestedTransformsComposeAncestorFirst()
    {
        using var f = Fixture("<g transform='translate(10 20)'><rect transform='scale(2)'/></g>");
        f.Text("String(el.firstElementChild.getCTM())").Should().Be("matrix(2, 0, 0, 2, 10, 20)");
        f.Text("String(el.firstElementChild.getScreenCTM())").Should().Be("matrix(2, 0, 0, 2, 10, 20)");
        f.Bool("el.getCTM() instanceof DOMMatrix && SVGMatrix === DOMMatrix").Should().BeTrue();
    }

    [TestCase("<rect x='1' y='2' width='3' height='4'/>", "1,2,3,4", 14)]
    [TestCase("<circle cx='10' cy='20' r='5'/>", "5,15,10,10", 31.41592653589793)]
    [TestCase("<line x1='5' y1='6' x2='8' y2='10'/>", "5,6,3,4", 5)]
    [TestCase("<polyline points='1,2 4,6 4,8'/>", "1,2,3,6", 7)]
    [TestCase("<polygon points='0,0 3,0 3,4'/>", "0,0,3,4", 12)]
    [TestCase("<path d='M0 0 L10 10'/>", "0,0,0,0", 0)]
    [TestCase("<g/>", "0,0,0,0", 0)]
    public void GeometryHasDocumentedHeadlessValues(string markup, string box, double length)
    {
        using var f = Fixture(markup);
        f.Text("var b = el.getBBox(); [b.x,b.y,b.width,b.height].join()").Should().Be(box);
        f.Bool("b instanceof DOMRect").Should().BeTrue();
        if (!markup.StartsWith("<g", StringComparison.Ordinal))
            f.Number("el.getTotalLength()").Should().BeApproximately(length, 1e-9);
    }

    [Test]
    public void EllipsesAndDistanceClamping()
    {
        using var f = Fixture("<ellipse cx='2' cy='3' rx='10' ry='5'/>");
        f.Text("var b = el.getBBox(); [b.x,b.y,b.width,b.height].join()").Should().Be("-8,-2,20,10");
        f.Number("el.getTotalLength()").Should().BeApproximately(48.44224, .01);
        f.Text("var p = el.getPointAtLength(-1); [p.x,p.y,p instanceof DOMPoint].join()").Should().Be("12,3,true");
        f.Execute("var line = document.createElementNS(svg.namespaceURI,'line'); line.setAttribute('x2','10');");
        f.Text("[line.getPointAtLength(5).x, line.getPointAtLength(999).x, line.isPointInFill({}), line.isPointInStroke({})].join()").Should().Be("5,10,false,false");
    }

    [Test]
    public void UnitsResolveAgainstTheNearestViewportAndConvertBack()
    {
        using var f = Fixture("<rect x='50%' y='50%'/>");
        f.Text("[el.x.baseVal.value, el.y.baseVal.value].join()").Should().Be("100,50");
        f.Text("var l = svg.createSVGLength(); l.newValueSpecifiedUnits(SVGLength.SVG_LENGTHTYPE_IN, 1); l.convertToSpecifiedUnits(SVGLength.SVG_LENGTHTYPE_PX); l.valueAsString").Should().Be("96px");
        f.Number("l.valueAsString = '2em'; l.value").Should().Be(32);
        f.Number("l.valueAsString = '2ex'; l.value").Should().Be(16);
        f.Number("l.valueAsString = '72pt'; l.value").Should().Be(96);
        f.Number("l.valueAsString = '2.54cm'; l.value").Should().BeApproximately(96, 1e-10);
        f.Text("error(() => l.newValueSpecifiedUnits(99, 1))").Should().Be("NotSupportedError");
        f.Execute("svg.removeAttribute('viewBox'); svg.setAttribute('width','300'); svg.setAttribute('height','80')");
        f.Text("[el.x.baseVal.value, el.y.baseVal.value].join()").Should().Be("150,40");
        f.Execute("svg.removeAttribute('width'); svg.removeAttribute('height')");
        f.Number("el.x.baseVal.value").Should().Be(0);
    }

    [Test]
    public void PointListsReflectItemsAndKeepDetachedValues()
    {
        using var f = Fixture("<polyline points='1,2 3,4'/>");
        f.Text("var list = el.points; var p = list[0]; p.x = 8; el.getAttribute('points')").Should().Be("8,2 3,4");
        f.Text("[p.z,p.w].join()").Should().Be("0,1");
        f.Bool("list === el.points && p === list[0] && p instanceof DOMPoint").Should().BeTrue();
        f.Text("el.setAttribute('points','10,20 30,40'); [p.x,p.y].join()").Should().Be("10,20");
        f.Text("var detached = list.removeItem(0); detached.x = 99; el.getAttribute('points')").Should().Be("30,40");
        f.Bool("detached === p").Should().BeTrue();
        f.Bool("var made = svg.createSVGPoint(); made.x = 7; list.appendItem(made) === made").Should().BeTrue();
        f.Text("made.y = 9; el.getAttribute('points')").Should().Be("30,40 7,9");
        f.Text("made.z = 3; made.w = 4; [made.z,made.w,el.getAttribute('points')].join('|')").Should().Be("3|4|30,40 7,9");
        f.Text("error(() => { el.animatedPoints[0].x = 5; })").Should().Be("NoModificationAllowedError");
        f.Text("error(() => list.getItem(99))").Should().Be("IndexSizeError");
        f.Text("error(() => list.appendItem(svg.createSVGLength()))").Should().Be("TypeError");
        f.Number("list.insertItemBefore(svg.createSVGPoint(), 999); list.length").Should().Be(3);
        f.Bool("Array.from(list).every(p => p instanceof DOMPoint)").Should().BeTrue();
    }

    [Test]
    public void ViewBoxAndAspectRatioAreLiveGeometryAndTypedValues()
    {
        using var f = Fixture();
        f.Bool("svg.viewBox.baseVal instanceof DOMRect && SVGRect === DOMRect && SVGPoint === DOMPoint").Should().BeTrue();
        f.Text("var r = svg.viewBox.baseVal; r.x = 3; r.width = 400; svg.getAttribute('viewBox')").Should().Be("3 0 400 100");
        f.Number("svg.setAttribute('viewBox', '1 2 30 40'); r.width").Should().Be(30);
        f.Number("svg.setAttribute('viewBox', '0 0 -10 2'); r.width").Should().Be(0);
        f.Text("var a = svg.preserveAspectRatio.baseVal; [a.align,a.meetOrSlice].join()").Should().Be("6,1");
        f.Text("a.align = 10; a.meetOrSlice = 2; svg.getAttribute('preserveAspectRatio')").Should().Be("xMaxYMax slice");
        f.Text("svg.setAttribute('preserveAspectRatio','none'); [a.align,a.meetOrSlice].join()").Should().Be("1,1");
    }

    [Test]
    public void PrimitiveAnimatedValuesAndSvgAnchorMembersReflect()
    {
        using var f = Fixture("<a class='old' href='#old' target='_self'/>");
        f.Bool("el instanceof SVGGraphicsElement && el.className instanceof SVGAnimatedString").Should().BeTrue();
        f.Text("el.className.baseVal = 'new'; [el.getAttribute('class'),el.className.animVal].join()").Should().Be("new,new");
        f.Text("el.href.baseVal = '#new'; el.target.baseVal = '_blank'; [el.getAttribute('href'),el.getAttribute('target')].join()").Should().Be("#new,_blank");
        f.Text("error(() => { el.href.animVal = '#bad'; })").Should().Be("NoModificationAllowedError");
        f.Bool("el.ownerSVGElement === svg && el.viewportElement === svg && svg.ownerSVGElement === null").Should().BeTrue();
        f.Text("el.dataset.foo = 'bar'; el.style.color = 'red'; [el.getAttribute('data-foo'),el.style.color].join()").Should().Be("bar,red");
        f.Number("el.tabIndex = 3; el.tabIndex").Should().Be(3);
        f.Text("el.removeAttribute('href'); el.setAttributeNS('http://www.w3.org/1999/xlink','xlink:href','#legacy'); el.href.baseVal").Should().Be("#legacy");
    }

    [Test]
    public void TextListsGradientsMarkersAndFactoryValues()
    {
        using var f = Fixture("<text x='1 2' rotate='10 20'>A&#x1f600;B</text>");
        f.Number("el.getNumberOfChars()").Should().Be(3);
        f.Number("el.getComputedTextLength()").Should().Be(0);
        f.Text("el.x.baseVal[0].value = 7; el.rotate.baseVal[1].value = 45; [el.getAttribute('x'),el.getAttribute('rotate')].join('|')").Should().Be("7 2|10 45");
        f.Execute("var grad = document.createElementNS(svg.namespaceURI,'linearGradient'); var marker = document.createElementNS(svg.namespaceURI,'marker')");
        f.Text("[grad.gradientUnits.baseVal,grad.spreadMethod.baseVal,grad.x2.baseVal.valueAsString].join()").Should().Be("2,1,100%");
        f.Text("grad.gradientUnits.baseVal = 1; grad.getAttribute('gradientUnits')").Should().Be("userSpaceOnUse");
        f.Text("marker.setOrientToAuto(); [marker.orientType.baseVal,marker.getAttribute('orient')].join()").Should().Be("1,auto");
        f.Text("var angle = svg.createSVGAngle(); angle.valueAsString = '100grad'; marker.setOrientToAngle(angle); [angle.value,marker.orientType.baseVal,marker.orientAngle.baseVal.value].join()").Should().Be("90,2,90");
        f.Bool("svg.createSVGNumber() instanceof SVGNumber && svg.createSVGTransformFromMatrix(new DOMMatrix()) instanceof SVGTransform").Should().BeTrue();
        f.Text("el.requiredExtensions.appendItem('urn:test'); el.getAttribute('requiredExtensions')").Should().Be("urn:test");
    }

    [Test]
    public void ValueConstantsAndIllegalConstructors()
    {
        using var f = Fixture();
        f.Bool("""
            [
              [SVGLength, 'SVG_LENGTHTYPE_PC', 10], [SVGTransform, 'SVG_TRANSFORM_SKEWY', 6],
              [SVGAngle, 'SVG_ANGLETYPE_RAD', 3], [SVGPreserveAspectRatio, 'SVG_MEETORSLICE_SLICE', 2],
              [SVGUnitTypes, 'SVG_UNIT_TYPE_OBJECTBOUNDINGBOX', 2], [SVGGradientElement, 'SVG_UNIT_TYPE_USERSPACEONUSE', 1]
            ].every(([c,n,v]) => c[n] === v && c.prototype[n] === v &&
                !Object.getOwnPropertyDescriptor(c,n).writable && !Object.getOwnPropertyDescriptor(c.prototype,n).configurable)
            """).Should().BeTrue();
        f.Bool("""
            ['SVGLength','SVGNumber','SVGAngle','SVGTransform','SVGPreserveAspectRatio',
             'SVGAnimatedString','SVGAnimatedLength','SVGAnimatedLengthList','SVGAnimatedNumberList',
             'SVGAnimatedNumber','SVGAnimatedBoolean','SVGAnimatedInteger','SVGAnimatedEnumeration',
             'SVGAnimatedAngle','SVGAnimatedRect','SVGAnimatedPreserveAspectRatio','SVGAnimatedTransformList',
             'SVGLengthList','SVGNumberList','SVGPointList','SVGTransformList','SVGStringList']
             .every(n => error(() => new globalThis[n]()) === 'TypeError')
            """).Should().BeTrue();
        f.Bool("typeof SVGUnknownElement === 'undefined'").Should().BeTrue();
    }

    [Test]
    public void ListsMaintainPrototypeLengthAndValidateReceivers()
    {
        using var f = Fixture("<text x='1 2'/>");
        f.Bool("!Object.hasOwn(el.x.baseVal, 'length') && Object.getOwnPropertyDescriptor(SVGLengthList.prototype,'length').enumerable").Should().BeTrue();
        f.Text("Object.defineProperty(SVGLengthList.prototype,'length',{get(){return 1}}); Array.from(el.x.baseVal).map(v=>v.value).join()").Should().Be("1");
        f.Text("error(() => SVGLength.prototype.convertToSpecifiedUnits.call(svg.createSVGNumber(),1))").Should().Be("TypeError");
        f.Text("error(() => SVGGraphicsElement.prototype.getBBox.call(document.createElement('div')))").Should().Be("TypeError");
    }

    [Test]
    public void ListReplacementCopiesAssociatedItemsAndDetachesTruncatedItems()
    {
        using var f = Fixture("<text x='1 2 3'/>");
        f.Execute("var list = el.x.baseVal; var first = list[0]; var removed = el.x.animVal[2];");
        f.Bool("list.appendItem(first) !== first && list.length === 4 && list[0] === first").Should().BeTrue();
        f.Execute("el.setAttribute('x','8 9');");
        f.Number("first.value").Should().Be(8);
        f.Text("removed.value = 12; el.getAttribute('x')").Should().Be("8 9");
        f.Text("var n = svg.createSVGLength(); n.value = 4; list.replaceItem(n,1); el.getAttribute('x')").Should().Be("8 4");
        f.Bool("list[1] === n && list.getItem(1) === n").Should().BeTrue();
        f.Bool("list.initialize(n) === n && list[0] === n && list.length === 1").Should().BeTrue();
        f.Bool("list.initialize(svg.createSVGLength()) === list[0] && list.length === 1").Should().BeTrue();
        f.Execute("var animated = el.x.animVal[0]; el.setAttribute('x','');");
        f.Text("animated.value = 6; el.getAttribute('x')").Should().Be("");
        f.Text("list.clear(); el.getAttribute('x')").Should().Be("");
    }

    [Test]
    public void NestedViewportsResolveWithoutRecursiveTreeWalks()
    {
        using var f = Fixture("<svg width='50%' height='50%'><rect x='50%' y='50%'/></svg>");
        f.Text("var r = el.firstElementChild; [r.x.baseVal.value,r.y.baseVal.value].join()").Should().Be("50,25");
        f.Text("el.setAttribute('viewBox','0 0 600 400'); [r.x.baseVal.value,r.y.baseVal.value].join()").Should().Be("300,200");
        f.Execute("""
            var parent = svg;
            for (var i = 0; i < 600; i++) {
                var nested = document.createElementNS(svg.namespaceURI,'svg');
                nested.setAttribute('width','100%'); parent.appendChild(nested); parent = nested;
            }
            var last = document.createElementNS(svg.namespaceURI,'rect');
            last.setAttribute('x','50%'); parent.appendChild(last);
            """);
        f.Number("last.x.baseVal.value").Should().Be(100);
    }

    [Test]
    public void SvgFactoriesAndReflectionsDoNotCrossEngineBoundaries()
    {
        using var first = Fixture("<rect x='1'/>");
        using var second = Fixture("<rect x='2'/>");
        first.Execute("el.x.baseVal.value = 20; SVGLength.prototype.marker = 1;");
        second.Number("el.x.baseVal.value").Should().Be(2);
        second.Bool("!('marker' in SVGLength.prototype)").Should().BeTrue();
        first.Number("el.x.baseVal.value").Should().Be(20);
    }

    [Test]
    public void NativeAttributeWritesAreReflected()
    {
        using var f = Fixture("<rect id='rect' x='1'/>");
        f.Execute("el.x.baseVal.value = 4;");
        var rect = ContentDom.ElementById(f.Document, "rect")!;
        rect.GetAttribute("x").Should().Be("4");
        rect.SetAttribute("x", "9");
        f.Number("el.x.baseVal.value").Should().Be(9);
    }

    [Test]
    public async Task MutationObserversReceiveSvgAttributeWrites()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <svg><rect id="rect" x="1"/></svg>
            <script>
              var records = [];
              var el = document.getElementById('rect');
              var observer = new MutationObserver(rs => records.push(...rs));
              observer.observe(el, { attributes:true, attributeOldValue:true });
              el.x.baseVal.value = 4;
            </script>
            """);
        (await page.EvaluateAsync<string>("records.map(r => [r.attributeName,r.oldValue].join(':')).join()"))
            .Should().Be("x:1");
        page.Errors.Should().BeEmpty();
    }
}
