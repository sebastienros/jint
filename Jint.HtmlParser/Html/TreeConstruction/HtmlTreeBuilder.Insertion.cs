using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.1, appropriate place for inserting a node
    // (2026-09-22). Fragment root adjustment is added with H7b.
    private readonly record struct InsertionLocation(Node Parent, Node? Before);

    // HTML Standard §13.2.6.1, adjusted insertion location. H7b adds the
    // root insertion target after the appropriate-place calculation.
    private InsertionLocation FindAdjustedInsertionLocation(Node? overrideTarget = null)
        => FindAppropriatePlace(overrideTarget);

    private InsertionLocation FindAppropriatePlace(Node? overrideTarget = null)
    {
        var target = overrideTarget ?? CurrentParent;
        if (_fosterParenting && target is Element element && element.NamespaceUri == Namespaces.Html &&
            element.LocalName is "table" or "tbody" or "tfoot" or "thead" or "tr")
        {
            var tableIndex = Last("table");
            var templateIndex = Last("template");
            Charge(1);
            if (templateIndex > tableIndex)
            {
                var template = _open[templateIndex];
                return new InsertionLocation(template.TemplateContent ??
                    throw new InvalidOperationException("HTML template has no contents."), null);
            }
            if (tableIndex < 0)
                return new InsertionLocation(AdjustTemplateTarget(_open[0]), null);

            var table = _open[tableIndex];
            if (table.ParentNode is { } parent)
                return new InsertionLocation(parent, table);
            if (tableIndex > 0)
                return new InsertionLocation(AdjustTemplateTarget(_open[tableIndex - 1]), null);
            return new InsertionLocation(AdjustTemplateTarget(_open[0]), null);
        }

        return new InsertionLocation(AdjustTemplateTarget(target), null);
    }

    private static Node AdjustTemplateTarget(Node target) =>
        target is Element { TemplateContent: { } contents } ? contents : target;

    private void InsertAt(InsertionLocation location, Node node)
    {
        if (location.Before is null)
        {
            location.Parent.AppendParsedChild(node);
            return;
        }

        // This node was created for the resolved destination and has never
        // been exposed or linked. Native insertion keeps the semantic commit
        // atomic without a repeated ancestor walk.
        Charge(1);
        location.Parent.InsertParsedBefore(node, location.Before, _cancellationToken);
    }
}
