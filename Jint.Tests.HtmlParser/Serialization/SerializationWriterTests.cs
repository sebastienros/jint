using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

[TestFixture]
public sealed class SerializationWriterTests
{
    [Test]
    public void AppendsRunsAndAcceptsExactInclusiveLimit()
    {
        var writer = new SerializationWriter(new SerializationWork(default), new SerializationLimits { MaxOutputCharacters = 8 });
        writer.Append("ab");
        writer.Append("😀");
        writer.Append("cdef");
        writer.Length.Should().Be(8);
        writer.Materialize().Should().Be("ab😀cdef");
    }

    [Test]
    public void ReportsFirstKnownExcessWithoutReturningPartialOutput()
    {
        var writer = new SerializationWriter(new SerializationWork(default), new SerializationLimits { MaxOutputCharacters = 5 });
        writer.Append("abc");
        var failure = Assert.Throws<SerializationLimitException>(() => writer.Append("def"));
        failure!.Limit.Should().Be(5);
        failure.Observed.Should().Be(6);
        writer.Materialize().Should().Be("abc");
    }

    [Test]
    public void NegativeLimitIsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SerializationLimits { MaxOutputCharacters = -1 });

    [Test]
    public void EntryAndEmptyFinalAreCancellationPoints()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => _ = new SerializationWork(cancellation.Token));

        using var finalCancellation = new CancellationTokenSource();
        var work = new SerializationWork(finalCancellation.Token, stage =>
        {
            if (stage == SerializationStage.Final) finalCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => new SerializationWriter(work).Materialize());
    }

    [Test]
    public void AppendAndMaterializationHaveIndependentCheckpoints()
    {
        using var appendCancellation = new CancellationTokenSource();
        var appendPolls = 0;
        var appendWork = new SerializationWork(appendCancellation.Token, stage =>
        {
            if (stage == SerializationStage.Append && ++appendPolls == 4) appendCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => new SerializationWriter(appendWork).Append(new string('x', 1024)));
        appendPolls.Should().Be(4);

        using var materializeCancellation = new CancellationTokenSource();
        var materializePolls = 0;
        var materializeWork = new SerializationWork(materializeCancellation.Token, stage =>
        {
            // Entry and post-copy checks alone cannot reach the third checkpoint.
            if (stage == SerializationStage.Materialize && ++materializePolls == 3) materializeCancellation.Cancel();
        });
        var writer = new SerializationWriter(materializeWork);
        writer.Append(new string('x', 1024));
        Assert.Throws<OperationCanceledException>(() => writer.Materialize());
        materializePolls.Should().Be(3);
    }
}
