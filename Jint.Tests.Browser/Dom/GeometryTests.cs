namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

// Geometry Interfaces Module Level 1, https://drafts.fxtf.org/geometry/; upstream wpt css/geometry is the
// conformance source, these pin the Browser projection.
public sealed class GeometryTests
{
    [Test]
    public void PointsConstructConvertAndStayMutableOnlyWhereDeclared()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("JSON.stringify(new DOMPoint(1, 2))").Should().Be("{\"x\":1,\"y\":2,\"z\":0,\"w\":1}");
        fixture.Text("JSON.stringify(DOMPoint.fromPoint({ y: 5, w: 3 }))").Should().Be("{\"x\":0,\"y\":5,\"z\":0,\"w\":3}");
        fixture.Bool("new DOMPoint() instanceof DOMPointReadOnly").Should().BeTrue();
        fixture.Bool("DOMPointReadOnly.fromPoint({}) instanceof DOMPoint").Should().BeFalse();
        fixture.Bool("Object.getPrototypeOf(DOMPoint) === DOMPointReadOnly").Should().BeTrue();
        fixture.Number("var p = new DOMPoint(); p.x = '4'; p.x").Should().Be(4);
        fixture.Number("var r = new DOMPointReadOnly(1); r.x = 9; r.x").Should().Be(1);
        fixture.Text("Object.prototype.toString.call(new DOMPoint())").Should().Be("[object DOMPoint]");
        fixture.Bool("(() => { try { DOMPoint(); return false; } catch (e) { return e instanceof TypeError; } })()").Should().BeTrue();
        fixture.Bool("(() => { try { DOMPoint.fromPoint(1); return false; } catch (e) { return e instanceof TypeError; } })()").Should().BeTrue();
    }

    [Test]
    public void DictionaryMembersAreReadInLexicographicOrder()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("""
            var log = [];
            var init = {};
            for (const k of ['z', 'y', 'x', 'w']) Object.defineProperty(init, k, { get() { log.push(k); return 1; } });
            DOMPoint.fromPoint(init);
            log.join()
            """).Should().Be("w,x,y,z");
    }

    [Test]
    public void RectsDeriveTheirEdgesAndPropagateNaN()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("var r = new DOMRect(10, 20, -5, 30); [r.top, r.right, r.bottom, r.left].join()").Should().Be("20,10,50,5");
        fixture.Text("r.width = 7; JSON.stringify(r)")
            .Should().Be("{\"x\":10,\"y\":20,\"width\":7,\"height\":30,\"top\":20,\"right\":17,\"bottom\":50,\"left\":10}");
        fixture.Bool("Number.isNaN(new DOMRectReadOnly(NaN).left)").Should().BeTrue();
        fixture.Text("var f = DOMRectReadOnly.fromRect({ width: 2 }); [f.x, f.width, f instanceof DOMRect].join()").Should().Be("0,2,false");
    }

    [Test]
    public void QuadsHoldTheSamePointsAndBoundThem()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Bool("var q = DOMQuad.fromRect({ x: 1, y: 2, width: 3, height: 4 }); q.p3 === q.p3 && q.p3 instanceof DOMPoint").Should().BeTrue();
        fixture.Text("[q.p3.x, q.p3.y].join()").Should().Be("4,6");
        fixture.Text("q.p1.x = -1; var b = q.getBounds(); [b.x, b.y, b.width, b.height, b instanceof DOMRect].join()").Should().Be("-1,2,5,4,true");
        fixture.Text("JSON.stringify(new DOMQuad({ x: 1 }).toJSON().p1)").Should().Be("{\"x\":1,\"y\":0,\"z\":0,\"w\":1}");
        fixture.Text("JSON.stringify(DOMQuad.fromQuad({ p2: { y: 3 } }).p2)").Should().Be("{\"x\":0,\"y\":3,\"z\":0,\"w\":1}");
    }

    [Test]
    public void MatricesParseTransformLists()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("String(new DOMMatrix())").Should().Be("matrix(1, 0, 0, 1, 0, 0)");
        fixture.Text("String(new DOMMatrix(''))").Should().Be("matrix(1, 0, 0, 1, 0, 0)");
        fixture.Text("String(new DOMMatrix('translate(10px, 20px) scale(2)'))").Should().Be("matrix(2, 0, 0, 2, 10, 20)");
        fixture.Text("String(new DOMMatrix('rotate(90deg)'))").Should().Be("matrix(0, 1, -1, 0, 0, 0)");
        fixture.Bool("new DOMMatrix('translateZ(1px)').is2D").Should().BeFalse();
        fixture.Bool("new DOMMatrix('none').isIdentity").Should().BeTrue();
        fixture.Text("(() => { try { new DOMMatrix('translate(1em)'); } catch (e) { return e.name; } })()").Should().Be("SyntaxError");
        fixture.Text("(() => { try { new DOMMatrix(null); } catch (e) { return e.name; } })()").Should().Be("SyntaxError");
        fixture.Text("var m = new DOMMatrix(); m.setMatrixValue('skewX(45deg)'); m.c.toFixed(6)").Should().Be("1.000000");
        fixture.Bool("new WebKitCSSMatrix() instanceof DOMMatrix && WebKitCSSMatrix === DOMMatrix").Should().BeTrue();
    }

    [Test]
    public void MatricesTakeSequencesAndTypedArrays()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("String(new DOMMatrix([1, 2, 3, 4, 5, 6]))").Should().Be("matrix(1, 2, 3, 4, 5, 6)");
        fixture.Text("String(new DOMMatrixReadOnly([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]))")
            .Should().Be("matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)");
        fixture.Bool("(() => { try { new DOMMatrix([1, 2]); return false; } catch (e) { return e instanceof TypeError; } })()").Should().BeTrue();
        fixture.Text("String(DOMMatrix.fromFloat64Array(new Float64Array([1, 2, 3, 4, 5, 6])))").Should().Be("matrix(1, 2, 3, 4, 5, 6)");
        fixture.Bool("(() => { try { DOMMatrix.fromFloat32Array(new Float64Array(6)); return false; } catch (e) { return e instanceof TypeError; } })()").Should().BeTrue();
        fixture.Text("Array.from(new DOMMatrix([1, 2, 3, 4, 5, 6]).toFloat32Array()).join()").Should().Be("1,2,0,0,3,4,0,0,0,0,1,0,5,6,0,1");
    }

    [Test]
    public void DictionariesAreValidatedAndFixedUp()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("String(DOMMatrix.fromMatrix({ a: 2, m22: 3, f: 4 }))").Should().Be("matrix(2, 0, 0, 3, 0, 4)");
        fixture.Bool("DOMMatrix.fromMatrix({ m33: 2 }).is2D").Should().BeFalse();
        fixture.Bool("(() => { try { DOMMatrix.fromMatrix({ a: 1, m11: 2 }); return false; } catch (e) { return e instanceof TypeError; } })()").Should().BeTrue();
        fixture.Bool("(() => { try { DOMMatrix.fromMatrix({ is2D: true, m13: 1 }); return false; } catch (e) { return e instanceof TypeError; } })()").Should().BeTrue();
        fixture.Bool("DOMMatrix.fromMatrix({ a: NaN, m11: NaN }).is2D").Should().BeTrue();
    }

    [Test]
    public void OperationsComposeAndTrackDimensionality()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("String(new DOMMatrix().translate(5, 6).scale(2))").Should().Be("matrix(2, 0, 0, 2, 5, 6)");
        fixture.Text("String(new DOMMatrix().scale(2, 3, 1, 10, 10))").Should().Be("matrix(2, 0, 0, 3, -10, -20)");
        fixture.Text("String(new DOMMatrix().flipX())").Should().Be("matrix(-1, 0, 0, 1, 0, 0)");
        fixture.Text("String(new DOMMatrix([2, 0, 0, 4, 6, 8]).inverse())").Should().Be("matrix(0.5, 0, 0, 0.25, -3, -2)");
        fixture.Bool("var s = new DOMMatrix([0, 0, 0, 0, 0, 0]).inverse(); Number.isNaN(s.m11) && !s.is2D").Should().BeTrue();
        fixture.Bool("new DOMMatrix().rotate(10, 0, 0).is2D").Should().BeFalse();
        fixture.Bool("new DOMMatrix().rotate(10).is2D").Should().BeTrue();
        fixture.Text("String(new DOMMatrix().rotateFromVector(0, 1))").Should().Be("matrix(0, 1, -1, 0, 0, 0)");
        fixture.Text("String(new DOMMatrix([1, 0, 0, 1, 5, 0]).multiply({ e: 3 }))").Should().Be("matrix(1, 0, 0, 1, 8, 0)");
        fixture.Text("var m = new DOMMatrix([2, 0, 0, 2, 0, 0]); m.preMultiplySelf({ e: 3 }); String(m)").Should().Be("matrix(2, 0, 0, 2, 3, 0)");
        fixture.Bool("var ro = new DOMMatrixReadOnly(); ro.translate(1); ro.isIdentity").Should().BeTrue();
        fixture.Bool("var mm = new DOMMatrix(); mm.m34 = 0; var a = mm.is2D; mm.m34 = 1; a && !mm.is2D").Should().BeTrue();
        fixture.Bool("typeof new DOMMatrixReadOnly().translateSelf === 'undefined'").Should().BeTrue();
    }

    [Test]
    public void PointsTransformThroughMatrices()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("JSON.stringify(new DOMMatrix('translate(10px, 20px)').transformPoint({ x: 1, y: 2 }))")
            .Should().Be("{\"x\":11,\"y\":22,\"z\":0,\"w\":1}");
        fixture.Text("JSON.stringify(new DOMPointReadOnly(1, 0).matrixTransform({ b: 1, a: 0, c: -1, d: 0 }))")
            .Should().Be("{\"x\":0,\"y\":1,\"z\":0,\"w\":1}");
    }

    [Test]
    public void StringifyingANonFiniteMatrixIsAnInvalidStateError()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Text("(() => { try { String(new DOMMatrix([Infinity, 0, 0, 1, 0, 0])); } catch (e) { return e.name; } })()")
            .Should().Be("InvalidStateError");
        fixture.Text("JSON.stringify(Object.keys(new DOMMatrix().toJSON()).slice(-3))").Should().Be("[\"m44\",\"is2D\",\"isIdentity\"]");
    }

    [Test]
    public void MembersFollowWebIdlAttributesAndBrandTheirReceiver()
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Bool("Object.getOwnPropertyDescriptor(DOMMatrixReadOnly.prototype, 'a').enumerable").Should().BeTrue();
        fixture.Bool("Object.getOwnPropertyDescriptor(DOMMatrix, 'fromMatrix').enumerable").Should().BeTrue();
        fixture.Number("DOMMatrix.fromFloat32Array.length").Should().Be(1);
        fixture.Bool("Object.getOwnPropertyDescriptor(globalThis, 'DOMRect').enumerable").Should().BeFalse();
        fixture.Bool("(() => { try { Object.getOwnPropertyDescriptor(DOMPoint.prototype, 'x').set.call(new DOMPointReadOnly(), 1); return false; } catch (e) { return e instanceof TypeError; } })()")
            .Should().BeTrue();
        fixture.Bool("(() => { try { DOMRectReadOnly.prototype.toJSON.call({}); return false; } catch (e) { return e instanceof TypeError; } })()")
            .Should().BeTrue();
    }

    [Test]
    public async Task LayoutAnswersAreGeometryRects()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div style='width: 50px; height: 20px'></div>");
        (await page.EvaluateAsync<bool>("document.querySelector('div').getBoundingClientRect() instanceof DOMRect")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("var r = document.querySelector('div').getBoundingClientRect(); r.bottom === r.y + r.height && r.height > 0")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.querySelector('div').getClientRects()[0] instanceof DOMRect")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.createRange().getBoundingClientRect() instanceof DOMRect")).Should().BeTrue();
    }
}
