using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>Web IDL conversions over the native processing instruction's actual ordered map.</summary>
internal static class DomProcessingInstructionAttributes
{
    internal static JsValue Invoke(DomRealm realm, ProcessingInstruction node, string operation, JsValue[] arguments)
    {
        var member = "ProcessingInstruction." + operation;
        if (operation == "setAttribute") DomConvert.Require(arguments, 1, member);
        var name = operation is "hasAttributes" or "getAttributeNames"
            ? string.Empty : DomConvert.RequiredText(arguments, 0, member);
        var value = operation == "setAttribute" ? DomConvert.RequiredText(arguments, 1, member) : string.Empty;
        var force = arguments.Length > 1 && !arguments[1].IsUndefined()
            ? (bool?) TypeConverter.ToBoolean(arguments[1]) : null;
        var work = new ProcessingInstructionAttributeWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        switch (operation)
        {
            case "hasAttributes": return JsBoolean.Create(node.HasAttributes(work));
            case "getAttributeNames":
                var names = node.GetAttributeNames(work);
                var values = new JsValue[names.Length];
                for (var i = 0; i < names.Length; i++)
                {
                    if ((i & 255) == 0) realm.Engine.Constraints.Check();
                    values[i] = JsString.Create(names[i]);
                }
                return realm.OwningRealm.Intrinsics.Array.ConstructFast(values);
            case "getAttribute": return DomConvert.NullableText(node.GetAttribute(name, work));
            case "hasAttribute": return JsBoolean.Create(node.HasAttribute(name, work));
            case "setAttribute": node.SetAttribute(name, value, work); break;
            case "removeAttribute": node.RemoveAttribute(name, work); break;
            case "toggleAttribute": return JsBoolean.Create(node.ToggleAttribute(name, force, work));
            default: throw new InvalidOperationException("Unknown processing instruction member: " + operation);
        }
        return JsValue.Undefined;
    }
}
