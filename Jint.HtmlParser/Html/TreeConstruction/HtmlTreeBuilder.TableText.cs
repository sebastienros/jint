using System;
using System.Collections.Generic;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.4.9–10 (2026-09-22). A whole contiguous table
    // character run must remain pending until its following non-character token.
    private readonly List<PendingTableSegment> _pendingTableText = [];
    private Mode _tableTextOriginalMode;
    private bool _tableTextHasNonwhite;
    private bool _tableTextFlushErrorReported;
    private long _tableTextNonwhiteOffset;
    private int _tableTextFlushSegment;
    private int _tableTextFlushCharacter;

    private readonly record struct PendingTableSegment(string Data, int Start, int Length, long Offset);

    private void EnterTableText()
    {
        if (_pendingTableText.Count != 0) throw new InvalidOperationException("Table text was not flushed.");
        _tableTextOriginalMode = _mode;
        _tableTextHasNonwhite = false;
        _tableTextFlushErrorReported = false;
        _tableTextFlushSegment = 0;
        _tableTextFlushCharacter = 0;
        _mode = Mode.InTableText;
    }

    private void BufferTableText()
    {
        var data = _token.Data;
        var start = _textIndex;
        while (_textIndex < data.Length && _remaining > 0)
        {
            var c = data[_textIndex];
            if (c == '\0')
            {
                AddTableSegment(data, start, _textIndex - start, _token.Offset);
                Error("unexpected-null-character");
                _textIndex++;
                Charge(1);
                start = _textIndex;
                continue;
            }
            if (!White(c) && !_tableTextHasNonwhite)
            {
                _tableTextHasNonwhite = true;
                _tableTextNonwhiteOffset = _token.Offset;
            }
            _textIndex++;
            Charge(1);
        }
        AddTableSegment(data, start, _textIndex - start, _token.Offset);
    }

    private void AddTableSegment(string data, int start, int length, long offset)
    {
        if (length == 0) return;
        if (_pendingTableText.Count > 0)
        {
            var index = _pendingTableText.Count - 1;
            var last = _pendingTableText[index];
            if (ReferenceEquals(last.Data, data) && last.Start + last.Length == start)
            {
                _pendingTableText[index] = last with { Length = last.Length + length };
                return;
            }
        }
        _pendingTableText.Add(new PendingTableSegment(data, start, length, offset));
        Charge(1);
    }

    private bool FlushTableText()
    {
        if (_tableTextHasNonwhite && !_tableTextFlushErrorReported)
        {
            _diagnostics?.Add("html/tree-nonwhite-table-text", _tableTextNonwhiteOffset);
            _tableTextFlushErrorReported = true;
            _framesetOk = false;
        }

        while (_tableTextFlushSegment < _pendingTableText.Count)
        {
            if (_remaining <= 0) return false;
            var segment = _pendingTableText[_tableTextFlushSegment];
            var length = Math.Min(segment.Length - _tableTextFlushCharacter,
                (int) Math.Min(2048, Math.Max(1, _remaining)));
            var oldFoster = _fosterParenting;
            _fosterParenting = _tableTextHasNonwhite;
            try
            {
                InsertText(segment.Data.AsSpan(segment.Start + _tableTextFlushCharacter, length));
            }
            finally
            {
                _fosterParenting = oldFoster;
            }
            _tableTextFlushCharacter += length;
            if (_tableTextFlushCharacter != segment.Length) continue;
            _pendingTableText[_tableTextFlushSegment] = default;
            _tableTextFlushSegment++;
            _tableTextFlushCharacter = 0;
        }

        _pendingTableText.Clear();
        return true;
    }
}
