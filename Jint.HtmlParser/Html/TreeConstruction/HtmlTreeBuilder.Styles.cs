using System.Collections.Generic;
using System.Threading;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTreeBuilder
{
    private Queue<Element>? _completedStyles;
    internal bool HasCompletedStyles => _completedStyles is { Count: > 0 };

    internal bool IsStyleOpen(Element element) => ScriptRequestsEnabled && IsStyle(element) && _openIdentity.Contains(element);

    private static bool IsStyle(Element element) => element.LocalName == "style" &&
        element.NamespaceUri is Namespaces.Html or Namespaces.Svg;

    private void CompleteStyle(Element element)
    {
        if (!ScriptRequestsEnabled || !IsStyle(element)) return;
        // HTML Standard §13.2.6 (stack pop/end-tag) and §13.2.7 (EOF cleanup):
        // HTML/SVG style closures supply metadata only. The host consumes it after Drive returns, before the next token.
        Charge(1);
        (_completedStyles ??= new Queue<Element>()).Enqueue(element);
    }

    internal bool TryTakeCompletedStyle(out Element? element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_completedStyles is not { Count: > 0 }) { element = null; return false; }
        element = _completedStyles.Dequeue();
        // Draining is outside a Drive quota but remains part of the session's work ledger.
        if (_work < long.MaxValue) _work++;
        return true;
    }

    internal void ClearCompletedStyles() => _completedStyles = null;
}
