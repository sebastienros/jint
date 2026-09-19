using System.Runtime.CompilerServices;
using System.Text;
using AngleSharp.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>DOM §4.13's ordered attribute map, attached to the native PI identity.</summary>
internal static class DomProcessingInstructionAttributes
{
    private static readonly ConditionalWeakTable<IProcessingInstruction, State> _states = new();

    internal static JsValue Invoke(DomRealm realm, IProcessingInstruction node, string operation, JsValue[] arguments)
    {
        var member = "ProcessingInstruction." + operation;
        if (operation == "setAttribute")
        {
            DomConvert.Require(arguments, 1, member);
        }
        var name = operation is "hasAttributes" or "getAttributeNames"
            ? string.Empty : DomConvert.RequiredText(arguments, 0, member);
        var value = operation == "setAttribute" ? DomConvert.RequiredText(arguments, 1, member) : string.Empty;
        var force = arguments.Length > 1 && !arguments[1].IsUndefined()
            ? (bool?) TypeConverter.ToBoolean(arguments[1]) : null;
        if (operation is "setAttribute" or "toggleAttribute" && !DomNames.IsValidAttributeLocalName(name))
        {
            DomFailures.Refuse(realm, member, DomExceptionNames.InvalidCharacter, "the attribute name is invalid.");
        }

        var state = Read(node);
        var index = state.Attributes.FindIndex(pair => pair.Key == name);
        switch (operation)
        {
            case "hasAttributes":
                return JsBoolean.Create(state.Attributes.Count != 0);
            case "getAttributeNames":
                var names = new JsValue[state.Attributes.Count];
                for (var i = 0; i < names.Length; i++)
                {
                    names[i] = JsString.Create(state.Attributes[i].Key);
                }
                return realm.OwningRealm.Intrinsics.Array.ConstructFast(names);
            case "getAttribute":
                return index < 0 ? JsValue.Null : JsString.Create(state.Attributes[index].Value);
            case "hasAttribute":
                return JsBoolean.Create(index >= 0);
            case "setAttribute":
                if (index < 0)
                {
                    state.Attributes.Add(new(name, value));
                }
                else
                {
                    state.Attributes[index] = new(name, value);
                }
                break;
            case "removeAttribute":
                if (index >= 0)
                {
                    state.Attributes.RemoveAt(index);
                }
                break;
            case "toggleAttribute":
                if (index < 0)
                {
                    if (force == false)
                    {
                        return JsBoolean.False;
                    }
                    state.Attributes.Add(new(name, string.Empty));
                }
                else
                {
                    if (force == true)
                    {
                        return JsBoolean.True;
                    }
                    state.Attributes.RemoveAt(index);
                }
                Write(node, state);
                return JsBoolean.Create(index < 0);
        }
        Write(node, state);
        return JsValue.Undefined;
    }

    // Called only after a successful script-visible replace-data operation, including equal-value writes.
    internal static void DataChanged(IProcessingInstruction node) => _states.Remove(node);

    // DOM §5.5 replaces data at CharacterData boundaries even when it removes an empty span.
    // Snapshot the native identities before the operation adjusts its live range endpoints.
    internal readonly struct RangeDataReplacement(IRange? range)
    {
        private readonly IProcessingInstruction? _head = range is { IsCollapsed: false } ? range.Head as IProcessingInstruction : null;
        private readonly IProcessingInstruction? _tail = range is { IsCollapsed: false } ? range.Tail as IProcessingInstruction : null;

        internal void Complete()
        {
            if (_head is not null)
            {
                DataChanged(_head);
            }
            if (_tail is not null && !ReferenceEquals(_head, _tail))
            {
                DataChanged(_tail);
            }
        }
    }

    // Preserve native clone data; the new identity parses its own data on first attribute access.
    internal static void Cloned(INode source, INode copy)
    {
        var pending = new Stack<(INode Source, INode Copy)>();
        pending.Push((source, copy));
        while (pending.TryPop(out var pair))
        {
            if (pair.Source is IProcessingInstruction from && pair.Copy is IProcessingInstruction instruction)
            {
                instruction.Data = from.Data;
            }
            var count = Math.Min(pair.Source.ChildNodes.Length, pair.Copy.ChildNodes.Length);
            for (var i = 0; i < count; i++)
            {
                pending.Push((pair.Source.ChildNodes[i]!, pair.Copy.ChildNodes[i]!));
            }
            if (pair.Source is AngleSharp.Html.Dom.IHtmlTemplateElement template
                && pair.Copy is AngleSharp.Html.Dom.IHtmlTemplateElement clonedTemplate)
            {
                pending.Push((template.Content, clonedTemplate.Content));
            }
        }
    }

    private static State Read(IProcessingInstruction node)
    {
        var state = _states.GetValue(node, static pi => Parse(pi.Data));
        if (!string.Equals(state.Data, node.Data, StringComparison.Ordinal))
        {
            _states.Remove(node);
            state = _states.GetValue(node, static pi => Parse(pi.Data));
        }
        return state;
    }

    private static void Write(IProcessingInstruction node, State state)
    {
        var data = new StringBuilder();
        foreach (var pair in state.Attributes)
        {
            if (data.Length > 0)
            {
                data.Append(' ');
            }
            data.Append(pair.Key).Append("=\"");
            foreach (var c in pair.Value)
            {
                switch (c)
                {
                    case '&': data.Append("&amp;"); break;
                    case '<': data.Append("&lt;"); break;
                    case '>': data.Append("&gt;"); break;
                    case '"': data.Append("&quot;"); break;
                    default: data.Append(c); break;
                }
            }
            data.Append('"');
        }
        // Native replace-data delivers its normal characterData record. Do not parse our serialized
        // map again: DOM attribute names can be broader than XML pseudo-attribute names.
        node.Data = data.ToString();
        state.Data = node.Data;
    }

    /// <summary>https://www.w3.org/TR/xml-stylesheet/#NT-PseudoAtts.</summary>
    private static State Parse(string data)
    {
        var state = new State(data);
        var offset = 0;
        while (offset < data.Length)
        {
            SkipSpace(data, ref offset);
            if (offset == data.Length)
            {
                return state;
            }
            var start = offset;
            while (offset < data.Length && !IsSpace(data[offset]) && data[offset] != '=')
            {
                offset++;
            }
            var name = data[start..offset];
            if (!DomProcessingInstructions.IsXmlName(name, out _) || state.Attributes.Exists(pair => pair.Key == name))
            {
                break;
            }
            SkipSpace(data, ref offset);
            if (offset == data.Length || data[offset++] != '=')
            {
                break;
            }
            SkipSpace(data, ref offset);
            if (offset == data.Length || data[offset] is not ('\'' or '"'))
            {
                break;
            }
            var quote = data[offset++];
            var value = new StringBuilder();
            var valid = true;
            while (offset < data.Length && data[offset] != quote)
            {
                var c = data[offset++];
                if (c == '<' || (c == '&' && !Reference(data, ref offset, value)))
                {
                    valid = false;
                    break;
                }
                if (c != '&')
                {
                    value.Append(c);
                }
            }
            if (!valid || offset == data.Length || data[offset++] != quote)
            {
                break;
            }
            state.Attributes.Add(new(name, value.ToString()));
            if (offset == data.Length)
            {
                return state;
            }
            if (!IsSpace(data[offset]))
            {
                break;
            }
        }
        state.Attributes.Clear();
        return state;
    }

    private static bool Reference(string data, ref int offset, StringBuilder value)
    {
        var end = data.IndexOf(';', offset);
        if (end < 0)
        {
            return false;
        }
        var text = data.AsSpan(offset, end - offset);
        offset = end + 1;
        if (text.Length > 0 && text[0] == '#')
        {
            var hex = text.Length > 1 && text[1] == 'x';
            var digits = text[(hex ? 2 : 1)..];
            var scalar = 0;
            if (digits.IsEmpty)
            {
                return false;
            }
            foreach (var c in digits)
            {
                var digit = c is >= '0' and <= '9' ? c - '0'
                    : hex && c is >= 'a' and <= 'f' ? c - 'a' + 10
                    : hex && c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0 || scalar > 0x10FFFF / (hex ? 16 : 10))
                {
                    return false;
                }
                scalar = scalar * (hex ? 16 : 10) + digit;
            }
            if (scalar is not (0x9 or 0xA or 0xD or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF))
            {
                return false;
            }
            value.Append(char.ConvertFromUtf32(scalar));
            return true;
        }
        var decoded = text switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            _ => null,
        };
        if (decoded is null)
        {
            return false;
        }
        value.Append(decoded);
        return true;
    }

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r';

    private static void SkipSpace(string data, ref int offset)
    {
        while (offset < data.Length && IsSpace(data[offset]))
        {
            offset++;
        }
    }

    private sealed class State(string data)
    {
        internal string Data = data;
        internal readonly List<KeyValuePair<string, string>> Attributes = [];
    }
}
