using System;
using System.Threading;

namespace Jint.HtmlParser.Html;

// An opaque capability: identity, ownership and lifetime belong to the tokenizer.
internal sealed class HtmlInsertionPoint
{
    internal HtmlInsertionPoint(HtmlTokenizer owner, HtmlInput.Node marker)
    {
        Owner = owner;
        Marker = new WeakReference<HtmlInput.Node>(marker);
    }

    internal HtmlTokenizer Owner { get; }
    internal WeakReference<HtmlInput.Node> Marker { get; }
    internal bool Released { get; set; }
}

internal sealed partial class HtmlTokenizer
{
    private bool _reading;

    // HTML Standard §13.2.3 / dynamic markup insertion: insert immediately before
    // the unread suffix. Later writes to this identity follow earlier writes.
    internal HtmlInsertionPoint CreateInsertionPoint()
    {
        EnsureInsertionIdle();
        EnsureInsertionOpen();
        ChargeInsertionOperation();
        return new HtmlInsertionPoint(this, _input.CreateMarker());
    }

    internal void InsertInput(HtmlInsertionPoint point, string text, CancellationToken cancellationToken)
    {
        EnsureInsertionIdle();
        EnsureInsertionOpen();
        ValidateInsertionPoint(point);
        ArgumentNullException.ThrowIfNull(text);
        // Validate before publishing any source slice. A rejected write changes
        // neither the expanded input nor its accounting; it terminates the session.
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxInput > 0 && text.Length > _maxInput - _input.Appended)
                throw new ParseLimitException(ParseLimitKind.InputCharacters, _maxInput, _maxInput + 1);
            cancellationToken.ThrowIfCancellationRequested();
            _input.Insert(GetMarker(point), text);
            ChargeInsertionOperation();
        }
        catch (OperationCanceledException) { _terminal = true; throw; }
        catch (ParseLimitException) { _terminal = true; throw; }
    }

    internal HtmlReadStatus ReadUntil(HtmlInsertionPoint point, int workQuota, bool allowCData,
        CancellationToken cancellationToken, out HtmlToken token)
    {
        EnsureInsertionIdle();
        ValidateInsertionPoint(point);
        return ReadInternal(point, workQuota, allowCData, cancellationToken, out token);
    }

    internal void ReleaseInsertionPoint(HtmlInsertionPoint point)
    {
        EnsureInsertionIdle();
        ValidateInsertionPoint(point, allowPassed: true);
        if (point.Marker.TryGetTarget(out var marker)) _input.Release(marker);
        point.Released = true;
        ChargeInsertionOperation();
    }

    private HtmlReadStatus ReadInternal(HtmlInsertionPoint? point, int workQuota, bool allowCData,
        CancellationToken cancellationToken, out HtmlToken token)
    {
        EnsureInsertionIdle();
        _reading = true;
        try
        {
            _input.BeginRead(point is null ? null : GetMarker(point));
            return ReadCore(workQuota, allowCData, cancellationToken, out token);
        }
        finally { _reading = false; }
    }

    private void EnsureInsertionIdle()
    {
        if (_reading) throw new InvalidOperationException("A tokenizer read is active.");
    }

    private void EnsureInsertionOpen()
    {
        if (_terminal || _ended) throw new InvalidOperationException("The tokenizer session is terminal.");
    }

    private void ValidateInsertionPoint(HtmlInsertionPoint point, bool allowPassed = false)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.Owner != this || point.Released || (!allowPassed && (!point.Marker.TryGetTarget(out var marker) || HtmlInput.HasPassed(marker))))
            throw new InvalidOperationException("The insertion point is foreign, released or already consumed.");
    }

    private static HtmlInput.Node GetMarker(HtmlInsertionPoint point) =>
        point.Marker.TryGetTarget(out var marker) ? marker : throw new InvalidOperationException("The insertion point is already consumed.");

    private void ChargeInsertionOperation()
    {
        if (_work < long.MaxValue) _work++;
    }

    private bool ChargeMarkerTraversal()
    {
        Poll();
        // One traversal may use the scanner iteration's last unit. Further
        // traversal suspends its cached probe (also at quota 1).
        if (_remainingWork < 0) return false;
        _remainingWork--;
        ChargeInsertionOperation();
        return true;
    }
}
