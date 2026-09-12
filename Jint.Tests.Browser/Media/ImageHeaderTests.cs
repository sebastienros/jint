using System.Text;
using Jint.Browser.Media;
using Jint.Tests.Browser.Parsing;

namespace Jint.Tests.Browser.Media;

/// <summary>
/// The container-header reader, held to the field offsets each format's specification gives.
/// </summary>
/// <remarks>
/// These are the unit half of the image model; <c>Parsing/ImageLoadingTests</c> is the half that runs a real
/// document over a real socket. Both are needed: this one is what says <i>which byte</i> was misread when a
/// page reports the wrong <c>naturalWidth</c>, and there are eight formats to say it about.
/// </remarks>
public class ImageHeaderTests
{
    [Test]
    public void APngStatesItsSizeInTheIhdrChunk()
    {
        ImageHeader.TryRead(ImageBytes.Png(4000, 3000), out var width, out var height).Should().BeTrue();
        width.Should().Be(4000);
        height.Should().Be(3000);
    }

    [Test]
    public void AJpegSizeComesFromTheFirstStartOfFrameAndNotFromASegmentBeforeIt()
    {
        // The helper puts a JFIF APP0 segment first, so a reader that did not skip by the declared length
        // would find its bytes where the frame header's dimensions are.
        ImageHeader.TryRead(ImageBytes.Jpeg(1024, 768), out var width, out var height).Should().BeTrue();
        width.Should().Be(1024);
        height.Should().Be(768);
    }

    [Test]
    public void AGifIsItsLogicalScreen()
    {
        ImageHeader.TryRead(ImageBytes.Gif(300, 200), out var width, out var height).Should().BeTrue();
        width.Should().Be(300);
        height.Should().Be(200);
    }

    [Test]
    public void ALossyWebPReadsThroughItsFrameTagAndSyncCode()
    {
        ImageHeader.TryRead(ImageBytes.WebPLossy(640, 480), out var width, out var height).Should().BeTrue();
        width.Should().Be(640);
        height.Should().Be(480);
    }

    [Test]
    public void ALosslessWebPUnpacksTwoFourteenBitFieldsStoredOneLess()
    {
        ImageHeader.TryRead(ImageBytes.WebPLossless(300, 175), out var width, out var height).Should().BeTrue();
        width.Should().Be(300);
        height.Should().Be(175);
    }

    [Test]
    public void AnExtendedWebPIsItsCanvasSize()
    {
        ImageHeader.TryRead(ImageBytes.WebPExtended(1920, 1080), out var width, out var height).Should().BeTrue();
        width.Should().Be(1920);
        height.Should().Be(1080);
    }

    [Test]
    public void ATopDownBitmapsNegativeHeightIsADirectionAndNotASize()
    {
        ImageHeader.TryRead(ImageBytes.Bmp(64, 32), out var width, out var height).Should().BeTrue();
        width.Should().Be(64);
        height.Should().Be(32);
    }

    [Test]
    public void AnIconAnswersItsLargestEntry()
    {
        ImageHeader.TryRead(ImageBytes.Icon(48, 48), out var width, out var height).Should().BeTrue();
        width.Should().Be(48);
        height.Should().Be(48);
    }

    [Test]
    public void AnIconEntryOfZeroMeansTwoHundredAndFiftySix()
    {
        ImageHeader.TryRead(ImageBytes.Icon(0, 0), out var width, out var height).Should().BeTrue();
        width.Should().Be(256);
        height.Should().Be(256);
    }

    [TestCase("width=\"120\" height=\"60\"", 120, 60)]
    [TestCase("width='120px' height='60px'", 120, 60)]
    [TestCase("height=\"60\" width=\"120\"", 120, 60)]
    [TestCase("width = \"120\" height = \"60\"", 120, 60)]
    public void AnSvgIsItsRootWidthAndHeight(string attributes, int expectedWidth, int expectedHeight)
    {
        ImageHeader.TryRead(ImageBytes.Svg(attributes), out var width, out var height).Should().BeTrue();
        width.Should().Be(expectedWidth);
        height.Should().Be(expectedHeight);
    }

    [Test]
    public void AnSvgRootIsFoundPastAnXmlDeclarationAndAComment()
    {
        ImageHeader.TryRead(ImageBytes.Svg("width=\"10\" height=\"20\"", withProlog: true), out var width, out var height)
            .Should().BeTrue();
        width.Should().Be(10);
        height.Should().Be(20);
    }

    [TestCase("viewBox=\"0 0 10 10\"")]
    [TestCase("width=\"100%\" height=\"100%\"")]
    [TestCase("width=\"10em\" height=\"20em\"")]
    public void AnSvgWithNoIntrinsicSizeIsAvailableWithNone(string attributes)
    {
        // Available rather than broken, which is the difference between a load event and an error one: the
        // document is an SVG and this browser simply has no layout to resolve a relative length against.
        ImageHeader.TryRead(ImageBytes.Svg(attributes), out var width, out var height).Should().BeTrue();
        width.Should().Be(0);
        height.Should().Be(0);
    }

    [Test]
    public void AnAttributeWhoseNameMerelyEndsInWidthIsNotTheWidth()
    {
        ImageHeader.TryRead(ImageBytes.Svg("stroke-width=\"7\" height=\"20\""), out var width, out var height)
            .Should().BeTrue();
        width.Should().Be(0);
        height.Should().Be(20);
    }

    [TestCase("<html><body>404 not found</body></html>")]
    [TestCase("{\"error\":\"not an image\"}")]
    [TestCase("")]
    public void SomethingThatIsNotAnImageIsRefusedRatherThanMeasuredAtZero(string text)
    {
        // The refusal is what HTML's update-the-image-data step 25 turns into the broken state and an error
        // event; answering 0 x 0 with a load event would tell a page an image it cannot use is available.
        ImageHeader.TryRead(Encoding.UTF8.GetBytes(text), out _, out _).Should().BeFalse();
    }

    [Test]
    public void AFormatWithNoHeaderReaderIsRefused()
    {
        // 'ftypavif' is the ISO base media brand AVIF uses; its dimensions live in an ispe box that only a
        // box walk finds, so the honest answer is that this browser cannot read it.
        var avif = new byte[] { 0, 0, 0, 0x20 }
            .Concat(Encoding.ASCII.GetBytes("ftypavif"))
            .Concat(new byte[16])
            .ToArray();

        ImageHeader.TryRead(avif, out _, out _).Should().BeFalse();
    }

    [Test]
    public void ATruncatedPngIsRefusedRatherThanReadPastItsEnd()
    {
        ImageHeader.TryRead(ImageBytes.Png(8, 8).AsSpan(0, 20).ToArray(), out _, out _).Should().BeFalse();
    }
}
