using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.4.3 (2026-09-22). H5a admits markers only;
    // element entries arrive with reconstruction in H6a.
    private abstract class FormattingEntry;
    private sealed class FormattingMarker : FormattingEntry;

    private readonly List<FormattingEntry> _formatting = [];

    private void PushFormattingMarker()
    {
        _formatting.Add(new FormattingMarker());
        Charge(1);
    }

    private void ClearFormattingToMarker()
    {
        for (var i = _formatting.Count - 1; i >= 0; i--)
        {
            var marker = _formatting[i] is FormattingMarker;
            _formatting.RemoveAt(i);
            Charge(1);
            if (marker) return;
        }
        throw new InvalidOperationException("HTML formatting marker was not found.");
    }
}
