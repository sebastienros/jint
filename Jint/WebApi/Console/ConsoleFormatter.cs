#if NET8_0_OR_GREATER
using System.Globalization;
using System.Text;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Console;

/// <summary>
/// The Console Standard's <i>Formatter</i>, plus the object rendering its <c>%o</c>/<c>%O</c> specifiers and
/// <c>console.dir</c> leave to the implementation.
/// <para>
/// https://console.spec.whatwg.org/#formatter
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The rendering is deliberately bounded and deterministic rather than clever: depth-capped, entry-capped
/// and cycle-safe, and it never invokes a script-visible getter — an accessor property is reported as
/// <c>[Getter]</c> instead of being called. A console that can be made to run arbitrary script, recurse
/// without bound, or emit a gigabyte because a host object exposed a large collection would be a liability
/// in exactly the embedding a bounded engine exists for.
/// </para>
/// <para>
/// User code <i>is</i> reached where the specification requires it: <c>%s</c>, <c>%d</c>, <c>%i</c> and
/// <c>%f</c> are defined in terms of <c>String()</c> and <c>Number()</c>, which coerce through
/// <c>toString</c>/<c>valueOf</c>.
/// </para>
/// </remarks>
internal static class ConsoleFormatter
{
    /// <summary>How deep the object walk goes before collapsing a value to <c>[Object]</c>/<c>[Array]</c>.</summary>
    private const int MaxDepth = 2;

    /// <summary>How many array elements or object entries are rendered before the rest are summarized.</summary>
    private const int MaxEntries = 100;

    /// <summary>
    /// Turns the arguments of a console method into the single string the printer emits.
    /// </summary>
    internal static string Format(ReadOnlySpan<JsValue> data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var next = 1;

        // Formatter step 1: with a single argument there is no substitution at all, so "50%" stays "50%".
        if (data.Length > 1 && data[0] is JsString target)
        {
            AppendWithSpecifiers(builder, target.ToString(), data, ref next);
        }
        else
        {
            AppendTopLevel(builder, data[0]);
        }

        for (var i = next; i < data.Length; i++)
        {
            builder.Append(' ');
            AppendTopLevel(builder, data[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders one value the way <c>%o</c>, <c>%O</c> and <c>console.dir</c> do — strings quoted, objects
    /// walked.
    /// </summary>
    internal static string Inspect(JsValue value)
    {
        var builder = new StringBuilder();
        Inspect(builder, value, depth: 0, seen: null);
        return builder.ToString();
    }

    /// <summary>
    /// <c>console.table</c>'s renderer: "Try to construct a table with the columns of the properties of
    /// tabularData (or use properties) and rows of tabularData", https://console.spec.whatwg.org/#table.
    /// </summary>
    /// <param name="tabularData">The value to tabulate.</param>
    /// <param name="properties">
    /// The <c>properties</c> argument, already converted from its WebIDL <c>sequence&lt;DOMString&gt;</c>, or
    /// <see langword="null"/> when it was not given. When given it is the column set outright, in its own
    /// order, and a name it lists that a row does not have simply renders an empty cell.
    /// </param>
    /// <param name="text">The rendered table, when there is one.</param>
    /// <returns>
    /// Whether the value could be parsed as tabular. <see langword="false"/> is the standard's "fall back to
    /// just logging the argument", which is the caller's job.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The standard's whole normative text for this method is the sentence quoted above, followed by "TODO:
    /// This will need a good algorithm." So the shape below is an interpretation, and it is the one every
    /// implementation converged on: one row per own enumerable property of <paramref name="tabularData"/>,
    /// keyed in an <c>(index)</c> column; one column per own enumerable property of the row <i>values</i> that
    /// are objects, unioned across rows in first-seen order; and a <c>Values</c> column for the rows whose
    /// value is not an object, which is what makes <c>console.table(['a', 'b'])</c> show anything at all.
    /// </para>
    /// <para>
    /// The drawing is deliberately plain ASCII rather than the box-drawing characters a terminal-oriented
    /// implementation uses: a <see cref="ConsoleSink"/> may be a log file, a structured logger or a test
    /// assertion, and none of those is a UTF-8 terminal by assumption.
    /// </para>
    /// <para>
    /// It inherits the rest of this class's discipline: no script-visible getter is ever invoked (an accessor
    /// cell reads <c>[Getter]</c>), cells are rendered by <see cref="Inspect(JsValue)"/> and so are
    /// depth-capped and cycle-safe, and both the row and the column count are bounded by
    /// <see cref="MaxEntries"/> with the remainder reported on a trailing line. A console that can be made to
    /// emit a gigabyte because a script built a large array is a liability, and a table is the easiest way to
    /// ask for one.
    /// </para>
    /// </remarks>
    internal static bool TryFormatTable(JsValue tabularData, List<string>? properties, out string text)
    {
        // "if it can't be parsed as tabular": a primitive has no rows, and a function is an object whose own
        // properties (`length`, `name`) are not a table anybody wanted.
        if (tabularData is not ObjectInstance data || data is Native.Function.Function)
        {
            text = string.Empty;
            return false;
        }

        var rowKeys = new List<string>();
        var rowValues = new List<PropertyDescriptor>();
        var droppedRows = CollectRows(data, rowKeys, rowValues);

        var columns = new List<string>();
        var droppedColumns = CollectColumns(rowValues, properties, columns, out var needsValuesColumn);

        text = Draw(rowKeys, rowValues, columns, needsValuesColumn, droppedRows, droppedColumns);
        return true;
    }

    /// <summary>
    /// One row per own enumerable string-keyed property, in own-key order — which for an array is its
    /// indices, since <c>length</c> is not enumerable.
    /// </summary>
    /// <returns>How many rows were dropped for exceeding <see cref="MaxEntries"/>.</returns>
    private static int CollectRows(ObjectInstance data, List<string> rowKeys, List<PropertyDescriptor> rowValues)
    {
        var dropped = 0;
        var keys = data.GetOwnPropertyKeys(Types.String);

        for (var i = 0; i < keys.Count; i++)
        {
            var descriptor = data.GetOwnProperty(keys[i]);
            if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined) || !descriptor.Enumerable)
            {
                continue;
            }

            if (rowKeys.Count >= MaxEntries)
            {
                dropped++;
                continue;
            }

            rowKeys.Add(keys[i].ToString());
            rowValues.Add(descriptor);
        }

        return dropped;
    }

    /// <summary>
    /// The column set: the caller's <paramref name="properties"/> when it gave one, otherwise the union of
    /// every object row's own enumerable keys in first-seen order.
    /// </summary>
    /// <returns>How many columns were dropped for exceeding <see cref="MaxEntries"/>.</returns>
    private static int CollectColumns(
        List<PropertyDescriptor> rowValues,
        List<string>? properties,
        List<string> columns,
        out bool needsValuesColumn)
    {
        var dropped = 0;
        needsValuesColumn = false;

        if (properties is not null)
        {
            for (var i = 0; i < properties.Count; i++)
            {
                dropped += AddColumn(columns, properties[i]);
            }
        }

        for (var i = 0; i < rowValues.Count; i++)
        {
            var row = RowObject(rowValues[i]);
            if (row is null)
            {
                // A primitive, a function, or an accessor whose getter must not run: it has no columns of its
                // own and belongs in the Values column.
                needsValuesColumn = true;
                continue;
            }

            if (properties is not null)
            {
                continue;
            }

            var keys = row.GetOwnPropertyKeys(Types.String);
            for (var k = 0; k < keys.Count; k++)
            {
                var descriptor = row.GetOwnProperty(keys[k]);
                if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined) || !descriptor.Enumerable)
                {
                    continue;
                }

                dropped += AddColumn(columns, keys[k].ToString());
            }
        }

        return dropped;
    }

    private static int AddColumn(List<string> columns, string name)
    {
        if (columns.Contains(name))
        {
            return 0;
        }

        if (columns.Count >= MaxEntries)
        {
            return 1;
        }

        columns.Add(name);
        return 0;
    }

    /// <summary>
    /// The object whose properties become this row's cells, or <see langword="null"/> when the row has no
    /// such object and so belongs in the <c>Values</c> column.
    /// </summary>
    private static ObjectInstance? RowObject(PropertyDescriptor descriptor)
    {
        if (descriptor.IsAccessorDescriptor())
        {
            return null;
        }

        return descriptor.Value is ObjectInstance obj && obj is not Native.Function.Function ? obj : null;
    }

    private static string Draw(
        List<string> rowKeys,
        List<PropertyDescriptor> rowValues,
        List<string> columns,
        bool needsValuesColumn,
        int droppedRows,
        int droppedColumns)
    {
        var width = columns.Count + (needsValuesColumn ? 2 : 1);

        var header = new string[width];
        header[0] = "(index)";
        for (var i = 0; i < columns.Count; i++)
        {
            header[i + 1] = columns[i];
        }

        if (needsValuesColumn)
        {
            header[width - 1] = "Values";
        }

        var rows = new List<string[]>(rowKeys.Count);
        for (var r = 0; r < rowKeys.Count; r++)
        {
            var cells = new string[width];
            cells[0] = rowKeys[r];

            var row = RowObject(rowValues[r]);
            for (var c = 0; c < columns.Count; c++)
            {
                cells[c + 1] = row is null ? string.Empty : Cell(row, columns[c]);
            }

            if (needsValuesColumn)
            {
                cells[width - 1] = row is null ? DescriptorText(rowValues[r]) : string.Empty;
            }

            rows.Add(cells);
        }

        var widths = new int[width];
        for (var c = 0; c < width; c++)
        {
            var max = header[c].Length;
            for (var r = 0; r < rows.Count; r++)
            {
                max = System.Math.Max(max, rows[r][c].Length);
            }

            widths[c] = max;
        }

        var builder = new StringBuilder();
        AppendBorder(builder, widths);
        AppendRow(builder, header, widths);
        AppendBorder(builder, widths);

        // With no rows the separator just drawn IS the closing border; drawing another would put two
        // identical lines under the header for what is simply an empty table.
        for (var r = 0; r < rows.Count; r++)
        {
            AppendRow(builder, rows[r], widths);
        }

        if (rows.Count > 0)
        {
            AppendBorder(builder, widths);
        }

        if (droppedRows > 0)
        {
            builder.Append('\n').Append("... ").Append(droppedRows.ToString(CultureInfo.InvariantCulture)).Append(" more rows");
        }

        if (droppedColumns > 0)
        {
            builder.Append('\n').Append("... ").Append(droppedColumns.ToString(CultureInfo.InvariantCulture)).Append(" more columns");
        }

        return builder.ToString();
    }

    /// <summary>
    /// One cell of an object row. A name the row does not own renders empty, which is what makes a ragged
    /// array of objects tabulate at all.
    /// </summary>
    /// <remarks>
    /// Enumerability is not re-checked here: an auto-derived column is enumerable by construction, and a
    /// column the caller named explicitly is shown even when the property behind it is not enumerable,
    /// because asking for it by name is the whole point of the argument.
    /// </remarks>
    private static string Cell(ObjectInstance row, string column)
    {
        var descriptor = row.GetOwnProperty(JsString.Create(column));
        return ReferenceEquals(descriptor, PropertyDescriptor.Undefined) ? string.Empty : DescriptorText(descriptor);
    }

    private static string DescriptorText(PropertyDescriptor descriptor)
    {
        var builder = new StringBuilder();
        AppendDescriptorValue(builder, descriptor, depth: 0, seen: new List<ObjectInstance>());
        return builder.ToString();
    }

    private static void AppendBorder(StringBuilder builder, int[] widths)
    {
        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        for (var c = 0; c < widths.Length; c++)
        {
            builder.Append('+').Append('-', widths[c] + 2);
        }

        builder.Append('+');
    }

    private static void AppendRow(StringBuilder builder, string[] cells, int[] widths)
    {
        builder.Append('\n');
        for (var c = 0; c < cells.Length; c++)
        {
            builder.Append("| ").Append(cells[c]).Append(' ', widths[c] - cells[c].Length + 1);
        }

        builder.Append('|');
    }

    private static void AppendWithSpecifiers(StringBuilder builder, string target, ReadOnlySpan<JsValue> data, ref int next)
    {
        for (var i = 0; i < target.Length; i++)
        {
            var c = target[i];
            if (c != '%' || i + 1 >= target.Length)
            {
                builder.Append(c);
                continue;
            }

            var specifier = target[i + 1];
            if (specifier == '%')
            {
                builder.Append('%');
                i++;
                continue;
            }

            // A specifier with no argument left to consume stays in the output verbatim, which is what every
            // implementation does and the only behaviour that round-trips a string containing a bare "%s".
            if (!IsSpecifier(specifier) || next >= data.Length)
            {
                builder.Append(c);
                continue;
            }

            var current = data[next];
            next++;
            i++;

            switch (specifier)
            {
                case 's':
                    AppendAsString(builder, current);
                    break;
                case 'd':
                case 'i':
                    AppendAsInteger(builder, current);
                    break;
                case 'f':
                    AppendAsNumber(builder, current);
                    break;
                case 'o':
                case 'O':
                    Inspect(builder, current, depth: 0, seen: null);
                    break;
                default:
                    // '%c' carries CSS. There is no styling to apply to a string, so the argument is
                    // consumed and produces nothing, exactly as the specification allows.
                    break;
            }
        }
    }

    private static bool IsSpecifier(char c) => c is 's' or 'd' or 'i' or 'f' or 'o' or 'O' or 'c';

    private static void AppendAsString(StringBuilder builder, JsValue value)
    {
        if (value is JsSymbol)
        {
            // String(symbol) is the one coercion a symbol survives, and it is what the specification calls for.
            builder.Append(value.ToString());
            return;
        }

        builder.Append(TypeConverter.ToString(value));
    }

    private static void AppendAsInteger(StringBuilder builder, JsValue value)
    {
        if (value is JsSymbol)
        {
            builder.Append("NaN");
            return;
        }

        if (value is JsBigInt)
        {
            builder.Append(TypeConverter.ToString(value));
            return;
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            builder.Append(TypeConverter.ToString(JsNumber.Create(number)));
            return;
        }

        builder.Append(TypeConverter.ToString(JsNumber.Create(System.Math.Truncate(number))));
    }

    private static void AppendAsNumber(StringBuilder builder, JsValue value)
    {
        if (value is JsSymbol)
        {
            builder.Append("NaN");
            return;
        }

        if (value is JsBigInt)
        {
            builder.Append(TypeConverter.ToString(value));
            return;
        }

        builder.Append(TypeConverter.ToString(JsNumber.Create(TypeConverter.ToNumber(value))));
    }

    /// <summary>
    /// A top-level argument: a string goes in raw (so <c>console.log("a", "b")</c> is <c>a b</c>, not
    /// <c>a 'b'</c>), everything else is inspected.
    /// </summary>
    private static void AppendTopLevel(StringBuilder builder, JsValue value)
    {
        if (value is JsString s)
        {
            builder.Append(s.ToString());
            return;
        }

        Inspect(builder, value, depth: 0, seen: null);
    }

    private static void Inspect(StringBuilder builder, JsValue value, int depth, List<ObjectInstance>? seen)
    {
        if (value is null || value.IsUndefined())
        {
            builder.Append("undefined");
            return;
        }

        if (value.IsNull())
        {
            builder.Append("null");
            return;
        }

        switch (value)
        {
            case JsString s:
                AppendQuoted(builder, s.ToString());
                return;
            case JsBoolean:
            case JsNumber:
                builder.Append(TypeConverter.ToString(value));
                return;
            case JsBigInt:
                builder.Append(TypeConverter.ToString(value)).Append('n');
                return;
            case JsSymbol:
                builder.Append(value.ToString());
                return;
            case ObjectInstance obj:
                InspectObject(builder, obj, depth, seen);
                return;
            default:
                builder.Append(TypeConverter.ToString(value));
                return;
        }
    }

    private static void InspectObject(StringBuilder builder, ObjectInstance obj, int depth, List<ObjectInstance>? seen)
    {
        if (obj is Native.Function.Function function)
        {
            builder.Append(function.ToString());
            return;
        }

        if (obj is ErrorInstance error)
        {
            builder.Append(error.ToString());
            return;
        }

        var array = obj as JsArray;

        if (depth > MaxDepth)
        {
            builder.Append(array is not null ? "[Array]" : "[Object]");
            return;
        }

        if (seen is not null && seen.Contains(obj))
        {
            builder.Append("[Circular]");
            return;
        }

        seen ??= new List<ObjectInstance>();
        seen.Add(obj);
        try
        {
            if (array is not null)
            {
                InspectArray(builder, array, depth, seen);
            }
            else
            {
                InspectPlainObject(builder, obj, depth, seen);
            }
        }
        finally
        {
            seen.RemoveAt(seen.Count - 1);
        }
    }

    private static void InspectArray(StringBuilder builder, JsArray array, int depth, List<ObjectInstance> seen)
    {
        var length = array.Length;
        if (length == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append("[ ");
        var rendered = System.Math.Min(length, (uint) MaxEntries);
        for (var i = 0u; i < rendered; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            AppendDescriptorValue(builder, array.GetOwnProperty(JsString.Create((int) i)), depth + 1, seen);
        }

        if (length > rendered)
        {
            builder.Append(", ... ").Append((length - rendered).ToString(CultureInfo.InvariantCulture)).Append(" more items");
        }

        builder.Append(" ]");
    }

    private static void InspectPlainObject(StringBuilder builder, ObjectInstance obj, int depth, List<ObjectInstance> seen)
    {
        var keys = obj.GetOwnPropertyKeys(Types.String);
        var written = 0;
        var skipped = 0;

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var descriptor = obj.GetOwnProperty(key);
            if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined) || !descriptor.Enumerable)
            {
                continue;
            }

            if (written >= MaxEntries)
            {
                skipped++;
                continue;
            }

            builder.Append(written == 0 ? "{ " : ", ");
            AppendKey(builder, key.ToString());
            builder.Append(": ");
            AppendDescriptorValue(builder, descriptor, depth + 1, seen);
            written++;
        }

        if (written == 0)
        {
            builder.Append("{}");
            return;
        }

        if (skipped > 0)
        {
            builder.Append(", ... ").Append(skipped.ToString(CultureInfo.InvariantCulture)).Append(" more properties");
        }

        builder.Append(" }");
    }

    /// <summary>
    /// Renders a property's value without invoking anything: an accessor is named, never called.
    /// </summary>
    private static void AppendDescriptorValue(StringBuilder builder, PropertyDescriptor descriptor, int depth, List<ObjectInstance> seen)
    {
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            builder.Append("undefined");
            return;
        }

        if (descriptor.IsAccessorDescriptor())
        {
            var hasGet = descriptor.Get is not null && !descriptor.Get.IsUndefined();
            var hasSet = descriptor.Set is not null && !descriptor.Set.IsUndefined();
            builder.Append(hasGet && hasSet ? "[Getter/Setter]" : hasGet ? "[Getter]" : "[Setter]");
            return;
        }

        Inspect(builder, descriptor.Value, depth, seen);
    }

    private static void AppendKey(StringBuilder builder, string key)
    {
        if (IsIdentifierLike(key))
        {
            builder.Append(key);
            return;
        }

        AppendQuoted(builder, key);
    }

    private static bool IsIdentifierLike(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            var ok = c is '_' or '$'
                || (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (i > 0 && c >= '0' && c <= '9');

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('\'');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\'':
                    builder.Append("\\'");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        builder.Append('\'');
    }
}
#endif
