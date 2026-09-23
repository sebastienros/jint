using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    // HTML Standard §13.2.5.42: CDATA is recognized only when the adjusted
    // current node is foreign. The tree builder updates this between tokens.
    internal void SetAllowCData(bool allowCData)
    {
        if (_terminal || _ended || !_canSetCDataContext || !CanSetModeAfterToken())
            throw new InvalidOperationException("CDATA context can only change between complete tokens.");
        _allowCData = allowCData;
    }

    // HTML Standard §13.2.5 and §13.2.6.4: fragment parsing selects its
    // initial tokenizer state without having emitted the context element.
    internal void InitializeFragmentTextMode(HtmlTextMode mode)
    {
        if (mode is not (HtmlTextMode.Data or HtmlTextMode.RcData or HtmlTextMode.RawText or HtmlTextMode.ScriptData or HtmlTextMode.PlainText))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (_terminal || _ended || _hasAcceptedRead || _fragmentTextModeInitialized || !_canSetTextMode || !CanSetModeAfterToken())
            throw new InvalidOperationException("Fragment text mode can only be initialized once before the first read.");

        _fragmentTextModeInitialized = true;
        _textMode = mode;
        _appropriateEndTagName = null;
        _state = ModeBaseState();
        _canSetTextMode = false;
    }
}
