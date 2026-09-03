using System.Globalization;
using System.Text;

namespace Jint.Browser.Accessibility;

/// <summary>
/// Renders an accessibility tree as the compact indented text an agent reads, one node per line.
/// </summary>
/// <remarks>
/// <para>
/// The shape is the one Playwright's <c>ariaSnapshot</c> established — <c>- role "name" [attrs]:</c>, nested
/// by indentation — because it is what an agent's prompt budget can afford and what the models driving these
/// pages have already seen. It is a projection, not a serialization: only the properties that change what a
/// caller would do are printed, and <see cref="AccessibilityTree.ToJson"/> is the lossless form.
/// </para>
/// </remarks>
internal static class AccessibilitySnapshot
{
    private static readonly AxPropertyName[] s_printed =
    [
        AxPropertyName.Level,
        AxPropertyName.Checked,
        AxPropertyName.Pressed,
        AxPropertyName.Expanded,
        AxPropertyName.Selected,
        AxPropertyName.Disabled,
        AxPropertyName.Required,
        AxPropertyName.Readonly,
        AxPropertyName.Focused,
        AxPropertyName.Multiselectable,
        AxPropertyName.Invalid,
    ];

    /// <summary>Renders <paramref name="node"/> and its descendants.</summary>
    /// <param name="node">The root of the tree to render.</param>
    /// <param name="includeReferences">
    /// Whether each element node carries <c>[ref=<i>n</i>]</c>, the identifier its document gave it.
    /// </param>
    /// <remarks>
    /// <b>A reference is what makes a snapshot actionable.</b> An agent reading roles and names cannot write
    /// a CSS selector for what it found, so the line has to carry the handle: the number is
    /// <see cref="AxNode.Id"/>, which is the document's own identifier for that node, stable for as long as
    /// the document is and resolved back through <c>AccessibilityTree.ElementFor</c>. A text node gets none —
    /// there is nothing to click on a run of text — and neither does a node with no element behind it.
    /// </remarks>
    internal static string Render(AxNode node, bool includeReferences = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        var builder = new StringBuilder();
        Write(builder, node, depth: 0, includeReferences);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, AxNode node, int depth, bool includeReferences)
    {
        builder.Append(' ', depth * 2).Append("- ");

        if (string.Equals(node.Role, AriaRoles.StaticText, StringComparison.Ordinal))
        {
            builder.Append("text: ").Append(node.Name ?? string.Empty).Append('\n');
            return;
        }

        builder.Append(node.Role);

        if (node.Name is { Length: > 0 } name)
        {
            builder.Append(" \"").Append(Escape(name)).Append('"');
        }

        foreach (var wanted in s_printed)
        {
            foreach (var property in node.Properties)
            {
                if (property.Name != wanted)
                {
                    continue;
                }

                var value = property.Value.ToDisplayString();
                if (string.Equals(value, "false", StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Append(" [").Append(property.ProtocolName);
                if (!string.Equals(value, "true", StringComparison.Ordinal))
                {
                    builder.Append('=').Append(value);
                }

                builder.Append(']');
            }
        }

        // Last of the attributes, so that a reader who does not want it can stop at the name and a reader who
        // does always finds it in the same place. Only a node with an element behind it: a reference to a run
        // of text is a handle onto something no click can reach.
        if (includeReferences && node.Element is not null)
        {
            builder.Append(" [ref=").Append(node.Id.ToString(CultureInfo.InvariantCulture)).Append(']');
        }

        // A node whose whole content is one run of text says it on its own line rather than in a child:
        // `- strong: non-DOM half` instead of two lines, which is the whole point of the compact form.
        if (node.Children is [{ Role: AriaRoles.StaticText, Name: { Length: > 0 } only }])
        {
            builder.Append(": ").Append(only).Append('\n');
            return;
        }

        if (node.Children.Count > 0)
        {
            builder.Append(":\n");
            foreach (var child in node.Children)
            {
                Write(builder, child, depth + 1, includeReferences);
            }

            return;
        }

        if (node.Value is { Length: > 0 } widgetValue)
        {
            builder.Append(": ").Append(widgetValue);
        }

        builder.Append('\n');
    }

    private static string Escape(string text) => text.Replace("\"", "\\\"", StringComparison.Ordinal);
}
