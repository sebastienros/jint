using Jint.Browser.Dom.Files;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeFileMimeTests
{
    [TestCase("style.CSS", "text/css")]
    [TestCase("script.js", "application/javascript")]
    [TestCase("image.JPEG", "image/jpeg")]
    [TestCase("picture.svg", "image/svg+xml")]
    [TestCase("report.pdf", "application/pdf")]
    [TestCase("report.doc", "application/msword")]
    [TestCase("sheet.xls", "application/excel")]
    [TestCase("audio.mp3", "audio/mpeg3")]
    [TestCase("archive.sv4cpio", "application/x-sv4cpio")]
    [TestCase("bytecode.pyc", "applicaiton/x-bytecode.python")]
    [TestCase("data.JSON", "application/json")]
    [TestCase("table.csv", "text/csv")]
    [TestCase("notes.md", "text/markdown")]
    [TestCase("unknown.new-extension", FileSelection.DefaultType)]
    [TestCase("without-extension", FileSelection.DefaultType)]
    [TestCase("unknown.xlsx", FileSelection.DefaultType)]
    public void SelectedFilesKeepThePinnedMimeBehavior(string name, string expected)
        => FileSelection.TypeOf(name).Should().Be(expected);

    [Test]
    public void AnOversizedUnknownExtensionNeedsNoUnboundedHashOrCaseConversion()
        => FileMimeTypes.FromExtension("." + new string('x', 131072)).Should().Be(FileSelection.DefaultType);
}
