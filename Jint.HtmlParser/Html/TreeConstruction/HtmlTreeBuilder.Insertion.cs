using System;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    // HTML Standard §13.2.6.1, appropriate place for inserting a node
    // (2026-09-25).
    private readonly record struct InsertionLocation(Node Parent, Node? Before);

    // HTML Standard §13.2.6.1, adjusted insertion location. Apply the
    // root insertion target after the appropriate-place calculation.
    private InsertionLocation FindAdjustedInsertionLocation(Node? overrideTarget = null)
    {
        var location = FindAppropriatePlace(overrideTarget);
        return _fragmentRoot is not null && ReferenceEquals(location.Parent, _fragmentRoot)
            ? new InsertionLocation(_fragmentResult!, location.Before) : location;
    }

    private InsertionLocation FindAppropriatePlace(Node? overrideTarget = null)
    {
        var target = overrideTarget ?? CurrentParent;
        if (_fosterParenting && target is Element element && element.NamespaceUri == Namespaces.Html &&
            HtmlTableTbodyTfootNames.Match(element.LocalName))
        {
            var tableIndex = Last("table");
            var templateIndex = Last("template");
            Charge(1);
            if (templateIndex > tableIndex)
            {
                var template = _open[templateIndex];
                return AdjustTemplateLocation(new InsertionLocation(template, null));
            }
            if (tableIndex < 0)
                return AdjustTemplateLocation(new InsertionLocation(_open[0], null));

            var table = _open[tableIndex];
            if (table.ParentNode is { } parent)
                return new InsertionLocation(parent, table);
            if (tableIndex > 0)
                return AdjustTemplateLocation(new InsertionLocation(_open[tableIndex - 1], null));
            return AdjustTemplateLocation(new InsertionLocation(_open[0], null));
        }

        return AdjustTemplateLocation(new InsertionLocation(target, null));
    }

    private static InsertionLocation AdjustTemplateLocation(InsertionLocation location)
    {
        if (location.Parent is not Element { TemplateContent: { } contents } template) return location;
        if (template.TemplatePatchState is not { } patch) return new InsertionLocation(contents, null);
        return new InsertionLocation(patch.InsertionTarget,
            patch.EndMarker is { } end && ReferenceEquals(end.ParentNode, patch.InsertionTarget) ? end : null);
    }

    private static Node AdjustTemplateTarget(Node target) => AdjustTemplateLocation(new InsertionLocation(target, null)).Parent;

    private void InsertAt(InsertionLocation location, Node node)
    {
        TrackInsertedRoot(node, location.Parent);
        if (location.Before is null)
        {
            location.Parent.AppendParsedChild(node);
        }
        else
        {
            // This node was created for the resolved destination and has never
            // been exposed or linked. Native insertion keeps the semantic commit
            // atomic without a repeated ancestor walk.
            Charge(1);
            location.Parent.InsertParsedBefore(node, location.Before, _cancellationToken);
        }
        // https://html.spec.whatwg.org/multipage/parsing.html#insert-a-foreign-element
        // Invoke insertion reactions before processing children.
        // Return to the host rather than running script while the tree builder is entered.
        if (ScriptRequestsEnabled && _scriptingMode == HtmlParserScriptingMode.Normal &&
            node is Element { NamespaceUri: Namespaces.Html } element)
        {
            Charge(element.LocalName.Length);
            if (element.IsValue is not null || element.LocalName.Contains('-'))
                _customElementReactionsBoundary = true;
        }
    }
}
