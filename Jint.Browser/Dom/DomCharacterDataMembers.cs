using System.Text;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>WebIDL conversions for DOM §4.10's native CharacterData operations.</summary>
internal static class DomCharacterDataMembers
{
    internal static string Data(Node node) => NativeCharacterData.SubstringData(node, 0, uint.MaxValue);

    internal static void SetData(Node node, string data) => NativeCharacterData.ReplaceData(node, 0, uint.MaxValue, data);

    internal static JsValue AppendData(Node node, JsValue[] arguments)
    {
        var data = DomConvert.RequiredText(arguments, 0, "CharacterData.appendData");
        NativeCharacterData.ReplaceData(node, NativeCharacterData.GetLength(node), 0, data);
        return JsValue.Undefined;
    }

    internal static JsValue SubstringData(DomRealm realm, Node node, JsValue[] arguments)
    {
        const string member = "CharacterData.substringData";
        DomConvert.Require(arguments, 1, member);
        var offset = DomConvert.RequiredUInt32(arguments, 0, member);
        var count = DomConvert.RequiredUInt32(arguments, 1, member);
        return JsString.Create(NativeCharacterData.SubstringData(node, offset, count));
    }

    internal static JsValue InsertData(DomRealm realm, Node node, JsValue[] arguments)
    {
        const string member = "CharacterData.insertData";
        DomConvert.Require(arguments, 1, member);
        var offset = DomConvert.RequiredUInt32(arguments, 0, member);
        var data = DomConvert.RequiredText(arguments, 1, member);
        NativeCharacterData.ReplaceData(node, offset, 0, data);
        return JsValue.Undefined;
    }

    internal static JsValue DeleteData(DomRealm realm, Node node, JsValue[] arguments)
    {
        const string member = "CharacterData.deleteData";
        DomConvert.Require(arguments, 1, member);
        var offset = DomConvert.RequiredUInt32(arguments, 0, member);
        var count = DomConvert.RequiredUInt32(arguments, 1, member);
        NativeCharacterData.ReplaceData(node, offset, count, "");
        return JsValue.Undefined;
    }

    internal static JsValue ReplaceData(DomRealm realm, Node node, JsValue[] arguments)
    {
        const string member = "CharacterData.replaceData";
        DomConvert.Require(arguments, 2, member);
        var offset = DomConvert.RequiredUInt32(arguments, 0, member);
        var count = DomConvert.RequiredUInt32(arguments, 1, member);
        var data = DomConvert.RequiredText(arguments, 2, member);
        NativeCharacterData.ReplaceData(node, offset, count, data);
        return JsValue.Undefined;
    }

    internal static JsValue SplitText(DomRealm realm, Node node, JsValue[] arguments)
    {
        var offset = DomConvert.RequiredUInt32(arguments, 0, "Text.splitText");
        return realm.WrapNodeValue(NativeCharacterData.SplitText(node, offset));
    }

    // DOM §4.11: wholeText is the concatenation of contiguous Text nodes.
    internal static string WholeText(Node node)
    {
        while (node.PreviousSibling is Text or CDataSection)
        {
            node = node.PreviousSibling;
        }
        var result = new StringBuilder();
        do
        {
            result.Append(Data(node));
            node = node.NextSibling!;
        } while (node is Text or CDataSection);
        return result.ToString();
    }
}
