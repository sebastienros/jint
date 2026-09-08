using AngleSharp.Dom;
using Jint.Native;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// The four <c>CharacterData</c> members whose offsets are WebIDL <c>unsigned long</c>s:
/// <a href="https://dom.spec.whatwg.org/#concept-cd-substring">substring data</a> and
/// <a href="https://dom.spec.whatwg.org/#concept-cd-replace">replace data</a>, which
/// <c>insertData</c> and <c>deleteData</c> are defined in terms of.
/// </summary>
/// <remarks>
/// <para>
/// <b>The clamping was never the problem; the conversion was.</b> AngleSharp's <c>CharacterData.Replace</c>
/// already refuses an offset past the end with an <c>IndexSizeError</c> and already shortens a count that
/// runs off it — the standard's own two steps. But its parameters are <c>Int32</c>, so the generator
/// converted with WebIDL's <c>ToInt32</c>, and <c>node.deleteData(-1, 10)</c> arrived as <c>-1</c>: past
/// neither test, and then an <c>ArgumentOutOfRangeException</c> out of <c>String.Substring</c> that crossed
/// into script as a <c>TypeError</c>.
/// </para>
/// <para>
/// <a href="https://webidl.spec.whatwg.org/#idl-unsigned-long">WebIDL's <c>unsigned long</c></a> is
/// <c>ToUint32</c>, so <c>-1</c> is 4 294 967 295 — past the end of any string, which is why a browser
/// answers <c>IndexSizeError</c>. The conversion is done here, the two steps are applied here in 64-bit
/// arithmetic so that <c>offset + count</c> cannot overflow, and what reaches AngleSharp is a pair that
/// already fits in the string. All WebIDL arguments are converted first, in parameter order, before
/// reading the data length: a later argument's coercion can change the data or throw.
/// </para>
/// </remarks>
internal static class DomCharacterDataMembers
{
    /// <summary>https://dom.spec.whatwg.org/#dom-characterdata-substringdata.</summary>
    internal static JsValue SubstringData(DomRealm realm, ICharacterData node, JsValue[] arguments)
    {
        const string member = "CharacterData.substringData";
        DomConvert.Require(arguments, 1, member);
        var convertedOffset = DomConvert.RequiredUInt32(arguments, 0, member);
        var convertedCount = DomConvert.RequiredUInt32(arguments, 1, member);
        var (offset, count) = Range(realm, node, convertedOffset, convertedCount, member);
        return JsString.Create(node.Substring(offset, count));
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-characterdata-insertdata.</summary>
    /// <remarks>Step 1: replace data with a count of 0, so only the offset is tested.</remarks>
    internal static JsValue InsertData(DomRealm realm, ICharacterData node, JsValue[] arguments)
    {
        const string member = "CharacterData.insertData";
        DomConvert.Require(arguments, 1, member);
        var convertedOffset = DomConvert.RequiredUInt32(arguments, 0, member);
        var data = DomConvert.RequiredText(arguments, 1, member);
        node.Insert(Offset(realm, node, convertedOffset, member), data);
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-characterdata-deletedata.</summary>
    /// <remarks>Step 1: replace data with the empty string.</remarks>
    internal static JsValue DeleteData(DomRealm realm, ICharacterData node, JsValue[] arguments)
    {
        const string member = "CharacterData.deleteData";
        DomConvert.Require(arguments, 1, member);
        var convertedOffset = DomConvert.RequiredUInt32(arguments, 0, member);
        var convertedCount = DomConvert.RequiredUInt32(arguments, 1, member);
        var (offset, count) = Range(realm, node, convertedOffset, convertedCount, member);
        node.Delete(offset, count);
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-characterdata-replacedata.</summary>
    internal static JsValue ReplaceData(DomRealm realm, ICharacterData node, JsValue[] arguments)
    {
        const string member = "CharacterData.replaceData";
        DomConvert.Require(arguments, 2, member);
        var convertedOffset = DomConvert.RequiredUInt32(arguments, 0, member);
        var convertedCount = DomConvert.RequiredUInt32(arguments, 1, member);
        var data = DomConvert.RequiredText(arguments, 2, member);
        var (offset, count) = Range(realm, node, convertedOffset, convertedCount, member);
        node.Replace(offset, count, data);
        return JsValue.Undefined;
    }

    /// <summary>
    /// Steps 2 and 3 of both algorithms: an offset past the end is an <c>IndexSizeError</c>, and a count that
    /// runs off the end is shortened to what is left.
    /// </summary>
    private static (int Offset, int Count) Range(DomRealm realm, ICharacterData node, uint convertedOffset, uint count, string member)
    {
        var offset = Offset(realm, node, convertedOffset, member);

        // 64-bit, because both are unsigned longs and their sum is what the standard tests: at 32 bits
        // `offset + count` wraps and a count that plainly runs off the end reads as one that does not.
        var remaining = (ulong) node.Length - (ulong) offset;
        return (offset, (int) Math.Min(count, remaining));
    }

    private static int Offset(DomRealm realm, ICharacterData node, uint offset, string member)
    {
        if (offset > (uint) node.Length)
        {
            DomFailures.Refuse(
                realm.Engine,
                member,
                DomExceptionNames.IndexSize,
                "the offset " + offset.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is past the end of the data.");
        }

        return (int) offset;
    }
}
