#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ParsedTextAppendTests
{
    [Test]
    public void AppendsUseOwnedStorageAndDataCachesCurrentValue()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode("start");
        var source = new[] { 'a', 'b' };
        text.AppendParsedData(source, CancellationToken.None);
        source[0] = 'z';

        var firstRead = text.Data;
        firstRead.Should().Be("startab");
        text.Data.Should().BeSameAs(firstRead);

        text.AppendParsedData("cd".AsSpan(), CancellationToken.None);
        text.Data.Should().Be("startabcd");
        text.Data.Should().NotBeSameAs(firstRead);
        text.Data.Should().BeSameAs(text.Data);
        firstRead.Should().Be("startab");
    }

    [Test]
    public void SetterAndClonesDoNotShareAppendableStorage()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateXml();
        var text = source.CreateTextNode("a");
        text.AppendParsedData("b".AsSpan(), CancellationToken.None);
        var clone = (Text)text.CloneNode();
        var imported = (Text)destination.ImportNode(text);
        text.AppendParsedData("c".AsSpan(), CancellationToken.None);

        text.Data.Should().Be("abc");
        clone.Data.Should().Be("ab");
        imported.Data.Should().Be("ab");
        imported.OwnerDocument.Should().BeSameAs(destination);

        text.Data = "reset";
        text.AppendParsedData("!".AsSpan(), CancellationToken.None);
        text.Data.Should().Be("reset!");
        clone.Data.Should().Be("ab");
    }

    [Test]
    public void CancellationAfterPreparationKeepsPublishedValueAndCache()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode("old");
        document.CreateElement("body").AppendChild(text);
        text.AppendParsedData("-first".AsSpan(), CancellationToken.None);
        var cached = text.Data;
        using var canceled = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() => text.AppendParsedData("-blocked".AsSpan(),
            stage =>
            {
                if (stage == TextAppendCheckpoint.AfterPreparation)
                {
                    canceled.Cancel();
                }
            }, canceled.Token));

        text.Data.Should().Be("old-first");
        text.Data.Should().BeSameAs(cached);
        text.AppendParsedData("-next".AsSpan(), CancellationToken.None);
        text.Data.Should().Be("old-first-next");
    }

    [Test]
    public void CancellationDuringPreparationKeepsPublishedValueAndCache()
    {
        var text = Document.CreateHtml().CreateTextNode("old");
        text.AppendParsedData("-first".AsSpan(), CancellationToken.None);
        var cached = text.Data;
        using var canceled = new CancellationTokenSource();
        var copiedSlices = 0;
        Assert.Throws<OperationCanceledException>(() => text.AppendParsedData(new string('x', 10_000).AsSpan(),
            stage =>
            {
                if (stage == TextAppendCheckpoint.DuringPreparation && ++copiedSlices == 2)
                {
                    canceled.Cancel();
                }
            }, canceled.Token));

        copiedSlices.Should().Be(2);
        text.Data.Should().Be("old-first");
        text.Data.Should().BeSameAs(cached);
    }

    [Test]
    public void CancellationAfterCommitExposesCompleteSliceAndFreshCache()
    {
        var text = Document.CreateHtml().CreateTextNode("old");
        var oldCached = text.Data;
        using var canceled = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => text.AppendParsedData("-complete".AsSpan(),
            stage =>
            {
                if (stage == TextAppendCheckpoint.AfterCommit)
                {
                    canceled.Cancel();
                }
            }, canceled.Token));

        text.Data.Should().Be("old-complete");
        text.Data.Should().NotBeSameAs(oldCached);
        text.Data.Should().BeSameAs(text.Data);
    }

    [Test]
    public void PreCancellationAndEmptyAppendDoNotChangeData()
    {
        var text = Document.CreateHtml().CreateTextNode("same");
        var cached = text.Data;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => text.AppendParsedData("change".AsSpan(), canceled.Token));
        text.Data.Should().BeSameAs(cached);
        text.AppendParsedData(ReadOnlySpan<char>.Empty, CancellationToken.None);
        text.Data.Should().BeSameAs(cached);
    }

    [Test]
    public void ManySmallChunksKeepOneTextIdentityAndCompleteValue()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode("");
        var parent = document.CreateElement("p");
        parent.AppendChild(text);
        for (var i = 0; i < 32_000; i++)
        {
            text.AppendParsedData("x".AsSpan(), CancellationToken.None);
        }

        parent.ChildCount.Should().Be(1);
        parent.FirstChild.Should().BeSameAs(text);
        text.Data.Should().Be(new string('x', 32_000));
    }
}
