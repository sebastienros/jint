using System.Runtime.CompilerServices;
using System.Text;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Events;

/// <summary>The output element's nullable default value override, realized only by a value assignment.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/form-elements.html#the-output-element</remarks>
internal static class BrowserOutputValue
{
    private static readonly ConditionalWeakTable<Element, DefaultValue> Defaults = new();

    internal static string GetValue(Element output, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var text = new StringBuilder();
        var node = output.FirstChild;
        while (node is not null)
        {
            work.Step();
            var data = node switch { Text value => value.Data, CDataSection value => value.Data, _ => null };
            if (data is not null)
            {
                for (var offset = 0; offset < data.Length;)
                {
                    var length = Math.Min(256, data.Length - offset);
                    text.Append(data, offset, length);
                    offset += length;
                    work.Check();
                }
            }
            if (node.FirstChild is { } child) { node = child; continue; }
            while (node.NextSibling is null && !ReferenceEquals(node.ParentNode, output))
            {
                work.Step();
                node = node.ParentNode!;
            }
            node = node.NextSibling;
        }
        work.Check();
        token.ThrowIfCancellationRequested();
        var result = text.ToString();
        work.Check();
        token.ThrowIfCancellationRequested();
        return result;
    }

    internal static string GetDefaultValue(Element output, Action<int>? checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke(1);
        token.ThrowIfCancellationRequested();
        return Defaults.TryGetValue(output, out var saved) ? saved.Value : GetValue(output, checkpoint, token);
    }

    internal static void SetValue(Element output, string value, Action<int>? checkpoint, CancellationToken token)
    {
        var defaultValue = GetDefaultValue(output, checkpoint, token);
        CheckBoundary(value, checkpoint, token);
        Defaults.GetOrCreateValue(output).Value = defaultValue;
        Replace(output, value);
        CheckBoundary(value, checkpoint, token);
    }

    internal static void SetDefaultValue(Element output, string value, Action<int>? checkpoint, CancellationToken token)
    {
        CheckBoundary(value, checkpoint, token);
        if (Defaults.TryGetValue(output, out var saved)) saved.Value = value;
        else
        {
            Replace(output, value);
        }
        CheckBoundary(value, checkpoint, token);
    }

    internal static void Reset(Element output, Action<int>? checkpoint, CancellationToken token)
    {
        var value = GetDefaultValue(output, checkpoint, token);
        CheckBoundary(value, checkpoint, token);
        Replace(output, value);
        Defaults.Remove(output);
        CheckBoundary(value, checkpoint, token);
    }

    private static void CheckBoundary(string value, Action<int>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke(1);
        token.ThrowIfCancellationRequested();
    }

    // Native ReplaceChildren publishes coherently. Its boundary can overrun a checkpoint quantum;
    // cancellation observed after publication propagates without promising whole-operation rollback.
    private static void Replace(Element output, string value)
        => output.ReplaceChildren(value.Length == 0 ? null : output.OwnerDocument!.CreateTextNode(value));

    private sealed class DefaultValue
    {
        internal string Value = string.Empty;
    }
}
