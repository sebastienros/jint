namespace Jint.HtmlParser;

internal sealed partial class HtmlTableGrid
{
    // With at most long.MaxValue/1000 participating declarations/cells, all
    // horizontal sums remain representable. Vertical sums are checked as well.
    private sealed class Builder(Element table, Work work)
    {
        private readonly ChunkedList<ColumnEntry> _columns = new();
        private readonly ChunkedList<CellEntry> _cells = new();
        private readonly ChunkedList<Element> _pendingFooters = new();
        private readonly Occupancy _occupancy = new(work);
        private long _width;
        private long _height;
        private long _currentY;

        internal HtmlTableGrid Build()
        {
            // The first non-column table child ends the declaration phase.
            // Ignored children do not do so, and late groups are not revived.
            var rowsStarted = false;
            for (var child = table.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (child is not Element element || element.NamespaceUri != Namespaces.Html)
                {
                    continue;
                }

                switch (element.LocalName)
                {
                    case "colgroup" when !rowsStarted:
                        ProcessColumnGroup(element);
                        break;
                    case "tr":
                        rowsStarted = true;
                        ProcessRow(element);
                        break;
                    case "thead":
                    case "tbody":
                        rowsStarted = true;
                        EndRowGroup();
                        ProcessRowGroup(element);
                        break;
                    case "tfoot":
                        rowsStarted = true;
                        EndRowGroup();
                        _pendingFooters.Add(element);
                        break;
                }
            }

            // HTML's advance-at-EOF jumps directly to End. There is no extra
            // EndRowGroup here: a direct row's rowspan can cover a queued foot.
            foreach (var footer in _pendingFooters)
            {
                work.Step();
                ProcessRowGroup(footer);
            }

            return Freeze();
        }

        private void ProcessColumnGroup(Element group)
        {
            var hasColumn = false;
            for (var child = group.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (child is not Element column || !IsHtml(column, "col")) continue;
                hasColumn = true;
                var span = ParseSpan(column, "span", 1000, false);
                var end = checked(_width + span);
                _columns.Add(new ColumnEntry(column, new ColumnRange(_width, end)));
                _width = end;
            }

            if (!hasColumn)
            {
                _width = checked(_width + ParseSpan(group, "span", 1000, false));
            }
        }

        private void ProcessRowGroup(Element group)
        {
            for (var child = group.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (child is Element row && IsHtml(row, "tr")) ProcessRow(row);
            }

            EndRowGroup();
        }

        private void EndRowGroup()
        {
            // All finite spans end by the established height. Downward-growing
            // cells grow to it, then cease; no implied-row loop is necessary.
            _currentY = _height;
            _occupancy.Clear();
            work.Step();
        }

        private void ProcessRow(Element row)
        {
            var nextY = checked(_currentY + 1);
            if (_height < nextY) _height = nextY;
            long x = 0;
            for (var child = row.FirstChild; child is not null; child = child.NextSibling)
            {
                work.Step();
                if (child is not Element cell || cell.NamespaceUri != Namespaces.Html ||
                    cell.LocalName is not ("td" or "th")) continue;

                x = _occupancy.FindFirstFree(x, _currentY);
                var colspan = ParseSpan(cell, "colspan", 1000, false);
                var rowspan = ParseSpan(cell, "rowspan", 65534, true);
                var end = checked(x + colspan);
                var expiry = rowspan == 0 ? long.MaxValue : checked(_currentY + rowspan);
                if (_width < end) _width = end;
                if (rowspan != 0 && _height < expiry) _height = expiry;

                _cells.Add(new CellEntry(cell, new ColumnRange(x, end), _cells.Count));
                _occupancy.Cover(x, end, expiry);
                x = end;
            }

            _currentY = nextY;
        }

        private int ParseSpan(Element element, string name, int maximum, bool zeroAllowed)
        {
            string? value = null;
            foreach (var attribute in element.Attributes)
            {
                work.Step();
                if (attribute.NamespaceUri is null && attribute.LocalName == name)
                {
                    value = attribute.Value;
                    break;
                }
            }

            if (value is null) return 1;
            var i = 0;
            while (i < value.Length && IsAsciiSpace(value[i]))
            {
                work.Step();
                i++;
            }

            var negative = false;
            if (i < value.Length && (value[i] == '+' || value[i] == '-'))
            {
                negative = value[i] == '-';
                work.Step();
                i++;
            }

            if (i == value.Length || value[i] is < '0' or > '9') return 1;
            var number = 0;
            while (i < value.Length && value[i] is >= '0' and <= '9')
            {
                work.Step();
                var digit = value[i++] - '0';
                if (number < maximum)
                {
                    number = Math.Min(maximum, number * 10 + digit);
                }
            }

            if (negative && number != 0) return 1;
            return number == 0 && !zeroAllowed ? 1 : number;
        }

        private static bool IsAsciiSpace(char value) => value is ' ' or '\t' or '\n' or '\r' or '\f';

        private HtmlTableGrid Freeze()
        {
            var columns = new ColumnEntry[_columns.Count];
            var offset = 0;
            foreach (var column in _columns) { work.Step(); columns[offset++] = column; }
            var cells = new CellEntry[_cells.Count];
            offset = 0;
            foreach (var cell in _cells) { work.Step(); cells[offset++] = cell; }
            work.Check();
            return new HtmlTableGrid(table, _width, columns, cells, work);
        }
    }
}
