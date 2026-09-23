#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ProcessingInstructionFactoryTests
{
    [Test]
    public void PublicFactoryAcceptsFifthEditionNameStartsAndSupplementaryBounds()
    {
        var document = Document.CreateXml();
        int[] starts =
        [
            ':', 'A', 'Z', '_', 'a', 'z', 0xC0, 0xD6, 0xD8, 0xF6, 0xF8, 0x2FF,
            0x370, 0x37D, 0x37F, 0x1FFF, 0x200C, 0x200D, 0x2070, 0x218F,
            0x2C00, 0x2FEF, 0x3001, 0xD7FF, 0xF900, 0xFDCF,
            0xFDF0, 0xFFFD, 0x10000, 0xEFFFF,
            0x0EC7, 0x3006, 0x3030, 0x3036, 0x309C, 0x309F, 0x30FF
        ];

        foreach (var scalar in starts)
        {
            var target = char.ConvertFromUtf32(scalar) + "end";
            var pi = document.CreateProcessingInstruction(target, "data");
            pi.Target.Should().Be(target);
            pi.Data.Should().Be("data");
            pi.OwnerDocument.Should().BeSameAs(document);
            pi.ParentNode.Should().BeNull();
        }

        document.CreateProcessingInstruction("xml", "").Target.Should().Be("xml");
        document.CreateProcessingInstruction("a:b:c", "").Target.Should().Be("a:b:c");
        document.CreateProcessingInstruction("a-1.\u00B7\u0300\u203F\u2040", "").Target
            .Should().Be("a-1.\u00B7\u0300\u203F\u2040");
    }

    [Test]
    public void PublicFactoryRejectsInvalidNameBoundariesAndUnpairedSurrogates()
    {
        var document = Document.CreateXml();
        string[] invalid =
        [
            "", "1a", "-a", ".a", "\u0300a", "a b", "a/b", "a?b", "a\0b",
            "\uD800", "\uDC00", "\uD800a", "a\uD800", "a\uDC00", "\uDC00\uD800",
            char.ConvertFromUtf32(0xF0000), "a" + char.ConvertFromUtf32(0xF0000),
            "\u00BF", "\u00D7", "\u036F", "\u037E", "\u2FF0", "\u3000",
            "\uF8FF", "\uFDD0", "\uFFFE"
        ];

        foreach (var target in invalid)
        {
            Assert.That(Assert.Throws<DomException>(() => document.CreateProcessingInstruction(target, "data"))!.Name,
                Is.EqualTo("InvalidCharacterError"), target);
        }

        Assert.That(Assert.Throws<DomException>(() => document.CreateProcessingInstruction("valid", "a?>b"))!.Name,
            Is.EqualTo("InvalidCharacterError"));
        Assert.Throws<ArgumentNullException>(() => document.CreateProcessingInstruction(null!, "data"));
        Assert.Throws<ArgumentNullException>(() => document.CreateProcessingInstruction("valid", null!));
    }

    [Test]
    public void TrustedParsedFactorySharesInsertionAndCloneCopiesEditedState()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateElement("root");
        source.AppendChild(root);
        using var subscription = source.ObserveMutations(root, new MutationObserverOptions { ChildList = true });
        var target = "\u3006" + char.ConvertFromUtf32(0xEFFFF);
        var parsed = source.CreateParsedProcessingInstruction(target, "exact data");
        root.AppendParsedChild(parsed);
        subscription.TakeRecords().Single().AddedNodes.Should().Equal(parsed);

        parsed.Data = "edited?>state";
        var sourceBefore = source.MutationStamp;
        var destinationBefore = destination.MutationStamp;
        var clone = (ProcessingInstruction) parsed.CloneNode();
        var imported = (ProcessingInstruction) destination.ImportNode(parsed);
        clone.Target.Should().Be(target);
        imported.Target.Should().Be(target);
        clone.Data.Should().Be("edited?>state");
        imported.Data.Should().Be("edited?>state");
        clone.ParentNode.Should().BeNull();
        imported.OwnerDocument.Should().BeSameAs(destination);
        source.MutationStamp.Should().Be(sourceBefore);
        destination.MutationStamp.Should().Be(destinationBefore);
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void AllSevenCorpusTargetsSurviveCloneAndImportAfterDataEdits()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        int[] targets = [0x0EC7, 0x3006, 0x3030, 0x3036, 0x309C, 0x309F, 0x30FF];
        foreach (var scalar in targets)
        {
            var target = char.ConvertFromUtf32(scalar);
            var original = source.CreateProcessingInstruction(target, "first");
            original.Data = "edited?>value";
            var clone = (ProcessingInstruction) original.CloneNode();
            var imported = (ProcessingInstruction) destination.ImportNode(original);
            clone.Target.Should().Be(target);
            imported.Target.Should().Be(target);
            clone.Data.Should().Be("edited?>value");
            imported.Data.Should().Be("edited?>value");
            imported.OwnerDocument.Should().BeSameAs(destination);
        }
    }
}
