#nullable enable

using System.Buffers.Binary;
using System.IO.Compression;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Canvas;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class CanvasTests
{
    private static DomTestFixture Create()
    {
        var fixture = DomTestFixture.Create("<canvas id='canvas' width='3' height='2'></canvas>");
        fixture.Execute("""
            var canvas = document.getElementById('canvas');
            var ctx = canvas.getContext('2d');
            function errorName(callback) {
                try { callback(); return ''; } catch (error) { return error.name; }
            }
            """);
        return fixture;
    }

    [Test]
    public void ContextIdentityModeAndOptionsAreStable()
    {
        using var f = Create();
        f.Bool("ctx instanceof CanvasRenderingContext2D && ctx.canvas === canvas && ctx === canvas.getContext('2d')").Should().BeTrue();
        f.Bool("['webgl','webgl2','experimental-webgl','webgpu','bitmaprenderer','unknown','2D'].every(x => canvas.getContext(x) === null)").Should().BeTrue();
        f.Bool("canvas.probablySupportsContext('2d') && !canvas.probablySupportsContext('webgl')").Should().BeTrue();
        f.Bool("var c = document.createElement('canvas'); c.getContext('webgl') === null && c.getContext('2d') !== null").Should().BeTrue();
        f.Text("""
            var settings = document.createElement('canvas').getContext('2d', {
                alpha: false, colorSpace: 'display-p3', desynchronized: true, willReadFrequently: true
            }).getContextAttributes();
            JSON.stringify(settings)
            """).Should().Be("{\"alpha\":false,\"colorSpace\":\"srgb\",\"colorType\":\"unorm8\",\"desynchronized\":false,\"willReadFrequently\":true}");
        f.Bool("canvas.getContext('2d', {get alpha() {throw 1;}}) === ctx").Should().BeTrue();
        f.Text("errorName(() => document.createElement('canvas').getContext('2d', {colorSpace: 'invalid'}))").Should().Be("TypeError");
        f.Text("errorName(() => canvas.getContext())").Should().Be("TypeError");
        f.Bool("!ctx.isContextLost()").Should().BeTrue();
    }

    [Test]
    public void ReentrantConversionsPreserveContextIdentityAndDimensionResets()
    {
        using var f = Create();
        f.Bool("""
            var nested, c=document.createElement('canvas');
            var outer=c.getContext('2d',{get alpha(){nested=c.getContext('2d');return false;}});
            outer===nested && c.getContext('2d')===nested
            """).Should().BeTrue();
        f.Bool("""
            var off=new OffscreenCanvas(1,1), inside;
            var outside=off.getContext('2d',{get alpha(){inside=off.getContext('2d');return false;}});
            inside===outside && off.getContext('2d')===inside
            """).Should().BeTrue();
        f.Text("""
            ctx.translate(4,5); ctx.save();
            ctx.lineWidth={valueOf(){canvas.width=canvas.width;return 3;}};
            [ctx.lineWidth,ctx.getTransform().isIdentity,(ctx.restore(),ctx.lineWidth)].join()
            """).Should().Be("3,true,3");
        f.Text("""
            var order=[];
            var options={};
            for(const key of ['willReadFrequently','desynchronized','colorType','colorSpace','alpha'])
                Object.defineProperty(options,key,{get(){order.push(key);return undefined;}});
            document.createElement('canvas').getContext('2d',options);
            order.join()
            """).Should().Be("alpha,colorSpace,colorType,desynchronized,willReadFrequently");
    }

    [Test]
    public void DefaultsAndInvalidAssignmentsMatchCanvasState()
    {
        using var f = Create();
        f.Text("[ctx.fillStyle,ctx.strokeStyle,ctx.globalAlpha,ctx.globalCompositeOperation,ctx.lineWidth,ctx.lineCap,ctx.lineJoin,ctx.miterLimit,ctx.lineDashOffset,ctx.font,ctx.textAlign,ctx.textBaseline,ctx.direction,ctx.shadowColor,ctx.filter].join('|')")
            .Should().Be("#000000|#000000|1|source-over|1|butt|miter|10|0|10px sans-serif|start|alphabetic|inherit|rgba(0, 0, 0, 0)|none");
        f.Bool("ctx.imageSmoothingEnabled && ctx.imageSmoothingQuality === 'low' && ctx.fontKerning === 'auto' && ctx.letterSpacing === '0px'").Should().BeTrue();
        f.Text("""
            ctx.globalAlpha = 0.25; ctx.lineWidth = 2; ctx.shadowBlur = 3;
            for (const bad of [-1, NaN, Infinity]) { ctx.globalAlpha = bad; ctx.lineWidth = bad; ctx.shadowBlur = bad; }
            ctx.globalAlpha = 2; ctx.lineWidth = 0; ctx.lineCap = 'invalid'; ctx.textAlign = 'LEFT';
            [ctx.globalAlpha,ctx.lineWidth,ctx.shadowBlur,ctx.lineCap,ctx.textAlign].join()
            """).Should().Be("0.25,2,3,butt,start");
        f.Text("ctx.font = 'bold 20px serif'; ctx.font = 'nonsense'; ctx.font").Should().Be("bold 20px serif");
        f.Text("ctx.letterSpacing = '2px'; ctx.letterSpacing = 'bad'; ctx.letterSpacing").Should().Be("2px");
        f.Text("ctx.filter = 'blur(2px) opacity(50%)'; ctx.filter = 'bad'; ctx.filter").Should().Be("blur(2px) opacity(50%)");
    }

    [TestCase("red", "#ff0000")]
    [TestCase("RebeccaPurple", "#663399")]
    [TestCase("#0f0", "#00ff00")]
    [TestCase("rgba(255, 0, 0, 0.5)", "rgba(255, 0, 0, 0.5)")]
    [TestCase("rgb(100% 0% 0% / 50%)", "rgba(255, 0, 0, 0.5)")]
    [TestCase("hsl(120, 100%, 50%)", "#00ff00")]
    [TestCase("hwb(240 0% 0%)", "#0000ff")]
    [TestCase("transparent", "rgba(0, 0, 0, 0)")]
    public void ColorsAreParsedAndSerialized(string input, string expected)
    {
        using var f = Create();
        f.Engine.SetValue("color", input);
        f.Text("ctx.fillStyle = color; ctx.fillStyle").Should().Be(expected);
        f.Text("ctx.fillStyle = 'not-a-color'; ctx.fillStyle").Should().Be(expected);
    }

    [Test]
    public void SaveAndRestorePreserveEveryAttributeAndCopyMutableArrays()
    {
        using var f = Create();
        f.Bool("""
            const values = {
                fillStyle:'red',strokeStyle:'blue',globalAlpha:.5,globalCompositeOperation:'multiply',
                lineWidth:2,lineCap:'round',lineJoin:'bevel',miterLimit:4,lineDashOffset:3,
                font:'20px serif',textAlign:'right',textBaseline:'top',direction:'rtl',letterSpacing:'2px',
                wordSpacing:'3px',fontKerning:'none',fontStretch:'expanded',fontVariantCaps:'small-caps',
                textRendering:'optimizeSpeed',lang:'en',imageSmoothingEnabled:false,imageSmoothingQuality:'high',
                shadowBlur:3,shadowColor:'green',shadowOffsetX:4,shadowOffsetY:5,filter:'blur(1px)'
            };
            Object.assign(ctx,values);
            const snapshot = Object.fromEntries(Object.keys(values).map(k => [k,ctx[k]]));
            ctx.setLineDash([2,3,4]); ctx.translate(5,6); ctx.save();
            Object.assign(ctx,{fillStyle:'black',font:'10px sans-serif'}); ctx.setLineDash([10]); ctx.resetTransform();
            ctx.restore();
            Object.keys(snapshot).every(k => snapshot[k] === ctx[k]) &&
                ctx.getLineDash().join() === '2,3,4,2,3,4' && ctx.getTransform().e === 5
            """).Should().BeTrue();
        f.Text("var dash = ctx.getLineDash(); dash[0] = 9; ctx.setLineDash([-1]); ctx.setLineDash([Infinity]); ctx.getLineDash().join()").Should().Be("2,3,4,2,3,4");
        f.Text("ctx.restore(); ctx.fillStyle").Should().Be("#ff0000");
    }

    [TestCase("canvas.width = canvas.width")]
    [TestCase("canvas.height = canvas.height")]
    [TestCase("canvas.setAttribute('width', '3')")]
    [TestCase("canvas.setAttributeNS(null, 'height', '2')")]
    [TestCase("canvas.attributes.getNamedItem('width').value = '3'")]
    [TestCase("canvas.removeAttribute('height')")]
    [TestCase("ctx.reset()")]
    public void ResizingAndResetClearStateAndSaveStack(string resize)
    {
        using var f = Create();
        f.Execute("ctx.fillStyle = 'red'; ctx.translate(3,4); ctx.setLineDash([1,2]); ctx.save();");
        f.Execute(resize);
        f.Bool("ctx.fillStyle === '#000000' && ctx.getTransform().isIdentity && ctx.getLineDash().length === 0").Should().BeTrue();
        f.Text("ctx.restore(); ctx.fillStyle").Should().Be("#000000");
        f.Bool("canvas.getContext('2d') === ctx").Should().BeTrue();
    }

    [Test]
    public void TransformsComposeAndReturnIndependentDomMatrices()
    {
        using var f = Create();
        f.Text("ctx.translate(5,6); ctx.scale(2,3); String(ctx.getTransform())").Should().Be("matrix(2, 0, 0, 3, 5, 6)");
        f.Bool("var matrix = ctx.getTransform(); matrix.e = 20; ctx.getTransform().e === 5 && matrix instanceof DOMMatrix").Should().BeTrue();
        f.Text("ctx.setTransform({a:2,d:3,e:8}); ctx.transform(1,0,0,1,2,1); String(ctx.getTransform())").Should().Be("matrix(2, 0, 0, 3, 12, 3)");
        f.Text("errorName(() => ctx.setTransform({a:2,m11:3}))").Should().Be("TypeError");
        f.Bool("ctx.setTransform({get m33(){throw 1;}}); ctx.getTransform().isIdentity").Should().BeTrue();
        f.Text("ctx.setTransform(1,2,3,4,5,6); ctx.translate(NaN, 1); ctx.setTransform(Infinity,0,0,1,0,0); String(ctx.getTransform())").Should().Be("matrix(1, 2, 3, 4, 5, 6)");
        f.Execute("ctx.resetTransform(); ctx.rotate(Math.PI / 2);");
        f.Number("ctx.getTransform().b").Should().BeApproximately(1, 1e-12);
        f.Number("ctx.getTransform().c").Should().BeApproximately(-1, 1e-12);
    }

    [Test]
    public void TextMeasurementsUseTheDocumentedCodePointHeuristic()
    {
        using var f = Create();
        f.Number("ctx.measureText('abc').width").Should().Be(15);
        f.Number("ctx.font = '20px serif'; ctx.measureText('A\\u{1F600}').width").Should().Be(20);
        f.Bool("ctx.measureText('abc') instanceof TextMetrics").Should().BeTrue();
        f.Number("ctx.measureText('abc').actualBoundingBoxAscent").Should().Be(16);
        f.Number("ctx.measureText('abc').actualBoundingBoxDescent").Should().Be(4);
        f.Number("ctx.letterSpacing = '1px'; ctx.wordSpacing = '2px'; ctx.measureText('a b').width").Should().Be(35);
        f.Number("ctx.textAlign = 'center'; ctx.measureText('ab').actualBoundingBoxLeft").Should().Be(11);
    }

    [Test]
    public void PathsAndDrawingConvertArgumentsButNeverDrawPixels()
    {
        using var f = Create();
        f.Bool("""
            const path = new Path2D('M0 0L10 10Z');
            const other = new Path2D(path);
            other.addPath(path, {e: 3});
            for (const target of [ctx, path]) {
                target.moveTo(0,0); target.lineTo(2,3); target.quadraticCurveTo(1,2,3,4);
                target.bezierCurveTo(1,2,3,4,5,6); target.arcTo(1,2,3,4,5);
                target.arc(1,2,3,0,Math.PI); target.ellipse(1,2,3,4,0,0,Math.PI);
                target.rect(0,0,3,2); target.roundRect(0,0,3,2,[1,{x:2,y:3}]); target.closePath();
            }
            ctx.beginPath(); ctx.fill(path,'evenodd'); ctx.clip(path); ctx.stroke(path);
            ctx.fillRect(0,0,3,2); ctx.strokeRect(0,0,3,2); ctx.clearRect(0,0,3,2);
            ctx.fillText('hello',0,0); ctx.strokeText('hello',0,0,20);
            ctx.drawFocusIfNeeded(canvas); ctx.drawFocusIfNeeded(path,canvas); ctx.scrollPathIntoView(path);
            !ctx.isPointInPath(0,0) && !ctx.isPointInPath(path,0,0,'evenodd') &&
                !ctx.isPointInStroke(path,0,0) && other instanceof Path2D &&
                ctx.getImageData(0,0,3,2).data.every(x => x === 0)
            """).Should().BeTrue();
        f.Text("errorName(() => ctx.arc(0,0,-1,0,1))").Should().Be("IndexSizeError");
        f.Text("errorName(() => ctx.arcTo(0,0,1,1,-1))").Should().Be("IndexSizeError");
        f.Text("errorName(() => new Path2D().ellipse(0,0,1,-1,0,0,1))").Should().Be("IndexSizeError");
        f.Text("errorName(() => ctx.roundRect(0,0,1,1,[]))").Should().Be("RangeError");
        f.Text("errorName(() => ctx.roundRect(0,0,1,1,-1))").Should().Be("RangeError");
        f.Text("errorName(() => ctx.arc(NaN,0,-1,0,1))").Should().Be("");
        f.Text("errorName(() => ctx.fill('bad'))").Should().Be("TypeError");
        f.Text("errorName(() => ctx.moveTo(1))").Should().Be("TypeError");
        f.Text("errorName(() => ctx.drawImage(canvas,0,0,1))").Should().Be("TypeError");
        f.Text("errorName(() => ctx.putImageData(new ImageData(1,1),0,0,1))").Should().Be("TypeError");
        f.Text("errorName(() => canvas.toBlob(null))").Should().Be("TypeError");
        f.Text("var log=[]; ctx.fillRect(...[1,2,3,4].map(n=>({valueOf(){log.push(n);return NaN;}}))); log.join()").Should().Be("1,2,3,4");
    }

    [Test]
    public void GradientsAndPatternsValidateAndCanBeSavedAsStyles()
    {
        using var f = Create();
        f.Bool("""
            var gradient = ctx.createLinearGradient(0,0,1,1);
            gradient.addColorStop(0,'red'); gradient.addColorStop(1,'transparent');
            ctx.fillStyle = gradient; ctx.save(); ctx.fillStyle = 'blue'; ctx.restore();
            ctx.fillStyle === gradient && gradient instanceof CanvasGradient &&
                ctx.createRadialGradient(0,0,0,1,1,2) instanceof CanvasGradient &&
                ctx.createConicGradient(0,0,0) instanceof CanvasGradient
            """).Should().BeTrue();
        f.Text("errorName(() => gradient.addColorStop(2,'red'))").Should().Be("IndexSizeError");
        f.Text("errorName(() => gradient.addColorStop(0,'invalid'))").Should().Be("SyntaxError");
        f.Text("errorName(() => gradient.addColorStop(NaN,'red'))").Should().Be("TypeError");
        f.Text("errorName(() => ctx.createLinearGradient(Infinity,0,0,0))").Should().Be("TypeError");
        f.Text("errorName(() => ctx.createRadialGradient(0,0,-1,1,1,1))").Should().Be("IndexSizeError");
        f.Bool("var pattern=ctx.createPattern(canvas,'repeat'); pattern.setTransform({e:1}); ctx.strokeStyle=pattern; ctx.strokeStyle===pattern && pattern instanceof CanvasPattern").Should().BeTrue();
        f.Text("errorName(() => pattern.setTransform({a:1,m11:2}))").Should().Be("TypeError");
        f.Text("errorName(() => ctx.createPattern(canvas,'bad'))").Should().Be("SyntaxError");
        f.Text("errorName(() => ctx.createPattern({},'repeat'))").Should().Be("TypeError");
        f.Bool("var empty = document.createElement('canvas'); empty.width=0; ctx.createPattern(empty,'repeat')===null").Should().BeTrue();
        f.Text("errorName(() => ctx.drawImage(empty,0,0))").Should().Be("InvalidStateError");
        f.Execute("ctx.drawImage(canvas,0,0); ctx.drawImage(canvas,0,0,10,10); ctx.drawImage(canvas,0,0,1,1,0,0,1,1);");
    }

    [Test]
    public void ImageDataUsesRealTypedArraysAndTransparentReads()
    {
        using var f = Create();
        f.Bool("var image = new ImageData(2,3); image.width===2 && image.height===3 && image.data instanceof Uint8ClampedArray && image.data.length===24 && image.data.every(x=>x===0)").Should().BeTrue();
        f.Bool("var data = new Uint8ClampedArray(24); var from = new ImageData(data,2); from.data===data && from.height===3 && from.colorSpace==='srgb' && from.pixelFormat==='rgba-unorm8'").Should().BeTrue();
        f.Bool("image.data[0]=255; var copy=ctx.createImageData(image); copy!==image && copy.data!==image.data && copy.data[0]===0 && copy.width===2").Should().BeTrue();
        f.Text("var negative=ctx.createImageData(-2,-3); [negative.width,negative.height].join()").Should().Be("2,3");
        f.Bool("ctx.putImageData(image,0,0); ctx.putImageData(image,0,0,0,0,2,3); ctx.getImageData(0,0,-2,-3).data.every(x=>x===0)").Should().BeTrue();
    }

    [TestCase("new ImageData(0,1)", "IndexSizeError")]
    [TestCase("new ImageData(new Uint8ClampedArray(0),1)", "InvalidStateError")]
    [TestCase("new ImageData(new Uint8ClampedArray(3),1)", "InvalidStateError")]
    [TestCase("new ImageData(new Uint8ClampedArray(8),3)", "IndexSizeError")]
    [TestCase("new ImageData(new Uint8ClampedArray(8),1,1)", "IndexSizeError")]
    [TestCase("new ImageData(new Uint8Array(4),1)", "TypeError")]
    [TestCase("ctx.getImageData(0,0,0,1)", "IndexSizeError")]
    [TestCase("ctx.getImageData(0,0,Infinity,1)", "TypeError")]
    [TestCase("ctx.createImageData(1,NaN)", "TypeError")]
    [TestCase("ctx.putImageData(new ImageData(1,1),Infinity,0)", "TypeError")]
    [TestCase("ctx.putImageData({},0,0)", "TypeError")]
    [TestCase("new ImageData(1,1,{colorSpace:'bad'})", "TypeError")]
    [TestCase("new ImageData(1,1,{pixelFormat:'rgba-float16'})", "NotSupportedError")]
    [TestCase("new ImageData(16777217,1)", "RangeError")]
    public void ImageDataErrorsAreScriptErrors(string expression, string expected)
    {
        using var f = Create();
        f.Text("errorName(() => " + expression + ")").Should().Be(expected);
    }

    [Test]
    public void DetachedImageDataCannotBeWritten()
    {
        using var f = Create();
        f.Text("var image=new ImageData(1,1); image.data.buffer.transfer(); errorName(() => ctx.putImageData(image,0,0))")
            .Should().Be("InvalidStateError");
    }

    [Test]
    public void DataUrlsContainRealTransparentPngsWithChecksums()
    {
        using var f = Create();
        var url = f.Text("ctx.fillStyle='red';ctx.fillRect(0,0,3,2);canvas.toDataURL('image/jpeg',0.5)")!;
        url.Should().StartWith("data:image/png;base64,");
        var png = Convert.FromBase64String(url["data:image/png;base64,".Length..]);
        AssertPng(png, 3, 2);
        f.Text("canvas.width=0;canvas.toDataURL()").Should().Be("data:,");
        f.Text("canvas.width=1;canvas.height=0;canvas.toDataURL()").Should().Be("data:,");
        f.Text("canvas.width=16777217;canvas.height=1;canvas.toDataURL()").Should().Be("data:,");
    }

    [Test]
    public void LongNarrowPngsDoNotRequireAnImageSizedBuffer()
    {
        using var f = Create();
        var png = CanvasPng.Encode(DomRealm.Of(f.Engine).Canvas, 1_000_000, 1)!;
        png.Length.Should().BeLessThan(25_000);
        AssertPng(png, 1_000_000, 1);
    }

    [Test]
    public async Task BlobCallbacksAreTasksAndSerializeTheCallTimeDimensions()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<canvas width=3 height=2></canvas>");
        await page.EvaluateAsync("""
            var canvas=document.querySelector('canvas'), log=[], blob;
            canvas.toBlob(value=>{blob=value;log.push('blob');},'image/jpeg');
            canvas.width=10;
            log.push('sync'); Promise.resolve().then(()=>log.push('microtask'));
            """);
        (await page.WaitForIdleAsync(TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join()")).Should().Be("sync,microtask,blob");
        (await page.EvaluateAsync<bool>("blob instanceof Blob && blob.type==='image/png' && blob.size>0")).Should().BeTrue();
        (await page.EvaluateAndAwaitAsync<string>("blob.bytes().then(a=>[a[19],a[23]].join())")).Should().Be("3,2");
        await page.EvaluateAsync("canvas.width=0;canvas.toBlob(value=>{blob=value;});");
        (await page.WaitForIdleAsync(TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<bool>("blob===null")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OffscreenCanvasesKeepStateAndResolvePngBlobs()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("");
        (await page.EvaluateAsync<bool>("var off=new OffscreenCanvas(4,5), c=off.getContext('2d'); c instanceof OffscreenCanvasRenderingContext2D && c.canvas===off && c===off.getContext('2d') && off instanceof EventTarget")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("off.getContext('webgl')===null && !('drawFocusIfNeeded' in c) && !('scrollPathIntoView' in c)")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("c.fillStyle='red';c.save();off.width=4;c.restore();c.fillStyle==='#000000'")).Should().BeTrue();
        (await page.EvaluateAndAwaitAsync<bool>("off.convertToBlob({type:'image/webp'}).then(b=>b instanceof Blob && b.type==='image/png')")).Should().BeTrue();
        (await page.EvaluateAndAwaitAsync<string>("off.height=0; off.convertToBlob().catch(e=>e.name)")).Should().Be("IndexSizeError");
        (await page.EvaluateAndAwaitAsync<string>("off.height=1;off.width=16777217;off.convertToBlob().catch(e=>e.name)")).Should().Be("EncodingError");
        (await page.EvaluateAndAwaitAsync<string>("off.convertToBlob(1).catch(e=>e.name)")).Should().Be("TypeError");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public void WebIdlMembersAreEnumerableBrandedAndHaveCorrectLengths()
    {
        using var f = Create();
        f.Bool("""
            const lengths = {save:0,restore:0,reset:0,scale:2,rotate:1,translate:2,transform:6,setTransform:0,
                getTransform:0,arc:5,ellipse:7,roundRect:4,drawImage:3,fillText:3,measureText:1,
                createImageData:1,getImageData:4,putImageData:3,setLineDash:1,createPattern:2,drawFocusIfNeeded:1};
            Object.entries(lengths).every(([name,length]) => {
                const d=Object.getOwnPropertyDescriptor(CanvasRenderingContext2D.prototype,name);
                return d.enumerable && d.configurable && d.writable && d.value.length===length;
            }) && Object.getOwnPropertyDescriptor(CanvasRenderingContext2D.prototype,'fillStyle').enumerable &&
                !Object.getOwnPropertyDescriptor(globalThis,'CanvasRenderingContext2D').enumerable &&
                Object.getPrototypeOf(CanvasRenderingContext2D.prototype)===Object.prototype &&
                HTMLCanvasElement.prototype.toBlob.length===1 && ImageData.length===2 && OffscreenCanvas.length===2 &&
                Path2D.length===0 && CanvasGradient.prototype.addColorStop.length===2
            """).Should().BeTrue();
        f.Text("errorName(() => new CanvasRenderingContext2D())").Should().Be("TypeError");
        f.Text("errorName(() => CanvasRenderingContext2D.prototype.save.call({}))").Should().Be("TypeError");
        f.Text("errorName(() => CanvasRenderingContext2D.prototype.save.call(new OffscreenCanvas(1,1).getContext('2d')))").Should().Be("TypeError");
        f.Bool("Object.keys(ctx).length===0 && Object.prototype.toString.call(ctx)==='[object CanvasRenderingContext2D]'").Should().BeTrue();
        f.Engine.Advanced.HasSharedShape(f.Evaluate("CanvasRenderingContext2D.prototype").AsObject()).Should().BeTrue();
        f.Text("errorName(() => new OffscreenCanvas(-1,2))").Should().Be("TypeError");
        f.Text("errorName(() => new OffscreenCanvas(NaN,2))").Should().Be("TypeError");
        f.Text("errorName(() => new OffscreenCanvas(1,2).getContext('invalid'))").Should().Be("TypeError");
        f.Bool("""
            [CanvasRenderingContext2D,OffscreenCanvasRenderingContext2D,CanvasGradient,CanvasPattern,Path2D,TextMetrics,ImageData,OffscreenCanvas]
                .every(ctor=>Object.getOwnPropertyNames(ctor.prototype).filter(k=>k!=='constructor')
                    .every(k=>Object.getOwnPropertyDescriptor(ctor.prototype,k).enumerable))
            """).Should().BeTrue();
    }

    [Test]
    public void ContextStateDoesNotCopyToClonesOrCrossEngines()
    {
        using var first = Create();
        using var second = Create();
        first.Execute("ctx.fillStyle='red';");
        second.Text("ctx.fillStyle").Should().Be("#000000");
        first.Text("canvas.cloneNode().getContext('2d').fillStyle").Should().Be("#000000");
        first.Bool("var doc=document.implementation.createHTMLDocument(); doc.adoptNode(canvas); canvas.getContext('2d')===ctx").Should().BeTrue();
        first.Execute("canvas.width=4;");
        first.Text("ctx.fillStyle").Should().Be("#000000");
    }

    private static void AssertPng(byte[] bytes, int width, int height)
    {
        bytes[..8].Should().Equal([137, 80, 78, 71, 13, 10, 26, 10]);
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)).Should().Be(width);
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)).Should().Be(height);
        using var idat = new MemoryStream();
        var chunks = new List<string>();
        for (var offset = 8; offset < bytes.Length;)
        {
            var size = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset));
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            chunks.Add(type);
            var crc = uint.MaxValue;
            foreach (var value in bytes.AsSpan(offset + 4, size + 4))
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + size)).Should().Be(~crc);
            if (type == "IDAT") idat.Write(bytes, offset + 8, size);
            offset += size + 12;
        }
        chunks.Should().Equal("IHDR", "IDAT", "IEND");
        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        raw.Length.Should().Be((width * 4L + 1) * height);
        raw.ToArray().Should().OnlyContain(b => b == 0);
    }
}
