using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>
/// The WebIDL conversion table, as the generated members call it: one method per direction per IDL type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Arguments</b> follow
/// <a href="https://webidl.spec.whatwg.org/#es-overloads">overload resolution</a>'s simple case — there are no
/// overloads in the generated surface, because AngleSharp gives two overloads two different <c>[DomName]</c>s
/// (<c>getAttribute</c> and <c>getAttributeNS</c>) — so each parameter converts independently. A missing
/// argument for a required parameter is a <c>TypeError</c>; a missing argument for an optional one takes the
/// CLR default the interface declared, which is AngleSharp's record of the IDL default value.
/// </para>
/// <para>
/// <b>Returns</b> have one decision worth stating. A CLR <c>string</c> maps <see langword="null"/> to the
/// <b>empty string</b>, because WebIDL's <c>DOMString</c> is not nullable and the overwhelming majority of
/// these members are reflected content attributes, whose IDL type is <c>DOMString</c> and whose specified
/// value when the attribute is absent is <c>""</c>. AngleSharp returns <see langword="null"/> for most of
/// them, which is a divergence from the DOM standard on its side rather than a nullable IDL type on ours.
/// The members whose IDL type genuinely <em>is</em> <c>DOMString?</c> — <c>getAttribute</c>,
/// <c>Node.nodeValue</c>, <c>Element.namespaceURI</c> and the rest — are listed by name in the generator's
/// <c>overrides.json</c> and emit <see cref="NullableText(string?)"/> instead. That list is the artefact: a member
/// missing from it answers <c>""</c> where a browser answers <c>null</c>, and a member wrongly in it does the
/// reverse, so it is checked against the assembly on every build.
/// </para>
/// </remarks>
internal static class DomConvert
{
    private static readonly JsString _empty = JsString.Create("");

    /// <summary>A non-nullable <c>DOMString</c> return: <see langword="null"/> becomes <c>""</c>.</summary>
    internal static JsValue Text(string? value) => value is null ? _empty : JsString.Create(value);

    /// <summary>A <c>DOMString?</c> return: <see langword="null"/> becomes <c>null</c>.</summary>
    internal static JsValue NullableText(string? value) => value is null ? JsValue.Null : JsString.Create(value);

    /// <summary>A <c>long</c>, <c>unsigned long</c> or <c>double</c> return.</summary>
    internal static JsValue Number(int value) => JsNumber.Create(value);

    /// <inheritdoc cref="Number(int)" />
    internal static JsValue Number(uint value) => JsNumber.Create(value);

    /// <inheritdoc cref="Number(int)" />
    internal static JsValue Number(long value) => JsNumber.Create(value);

    /// <inheritdoc cref="Number(int)" />
    internal static JsValue Number(double value) => JsNumber.Create(value);

    /// <inheritdoc cref="Number(int)" />
    internal static JsValue Number(float value) => JsNumber.Create(value);

    /// <summary>A <c>boolean</c> return.</summary>
    internal static JsValue Bool(bool value) => value ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// A <c>DOMTimeStamp</c> return: milliseconds since the epoch, which is what a JavaScript date is counted
    /// in. AngleSharp models these as <see cref="DateTime"/>, so the conversion is explicit here rather than
    /// left to the interop layer, which would have produced a CLR object wrapper.
    /// </summary>
    internal static JsValue Timestamp(DateTime value)
        => JsNumber.Create(new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeMilliseconds());

    /// <inheritdoc cref="Timestamp(DateTime)" />
    internal static JsValue Timestamp(DateTime? value)
        => value is null ? JsValue.Null : Timestamp(value.Value);

    /// <summary>A nullable numeric return.</summary>
    internal static JsValue Number(int? value) => value is null ? JsValue.Null : JsNumber.Create(value.Value);

    /// <summary>
    /// A WebIDL <c>any</c> return. AngleSharp types a handful of members as <c>object</c>, and what comes back
    /// is whatever the host put in; it crosses through Jint's ordinary CLR conversion, so a host object
    /// arrives as an <c>ObjectWrapper</c> and a primitive as a primitive.
    /// </summary>
    internal static JsValue Any(DomRealm realm, object? value)
        => value is null ? JsValue.Null : JsValue.FromObject(realm.Engine, value);

    /// <summary>
    /// A frozen array of nodes, for the one member AngleSharp types as a sequence rather than as a
    /// <c>NodeList</c> (<c>HTMLSlotElement.assignedNodes</c>). WebIDL's <c>sequence&lt;Node&gt;</c> is a
    /// snapshot by definition, so a plain array is the right shape and there is nothing live to keep.
    /// </summary>
    internal static JsValue NodeSequence(DomRealm realm, System.Collections.Generic.IEnumerable<AngleSharp.Dom.INode>? nodes)
    {
        if (nodes is null)
        {
            return JsValue.Null;
        }

        var values = new List<JsValue>();
        foreach (var node in nodes)
        {
            values.Add(realm.WrapNode(node));
        }

        return realm.PrincipalRealm.Intrinsics.Array.Construct([.. values]);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-enumeration — a value the enumeration does not carry is a
    /// <c>TypeError</c>. Generic so that the generated <c>DomEnums</c> switch can end in an expression.
    /// </summary>
    internal static T BadEnumValue<T>(JsValue value, string member)
    {
        var message = "Failed to execute '" + member + "': the provided value '" + TypeConverter.ToString(value) + "' is not a valid enum value.";

        if (value is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return default!;
    }

    /// <summary>The raw argument at <paramref name="index"/>, or <c>undefined</c> when absent.</summary>
    internal static JsValue At(JsValue[] arguments, int index)
        => index < arguments.Length ? arguments[index] : JsValue.Undefined;

    /// <summary>A required <c>DOMString</c> parameter.</summary>
    internal static string RequiredText(JsValue[] arguments, int index, string member)
    {
        Require(arguments, index, member);
        return TypeConverter.ToString(arguments[index]);
    }

    /// <summary>An optional <c>DOMString</c> parameter with an IDL default.</summary>
    internal static string? OptionalText(JsValue[] arguments, int index, string? fallback)
        => index < arguments.Length && !arguments[index].IsUndefined()
            ? TypeConverter.ToString(arguments[index])
            : fallback;

    /// <summary>A <c>DOMString?</c> parameter: <c>null</c> and <c>undefined</c> stay null.</summary>
    internal static string? NullableText(JsValue[] arguments, int index)
        => index < arguments.Length && !arguments[index].IsNullOrUndefined()
            ? TypeConverter.ToString(arguments[index])
            : null;

    /// <summary>A required <c>long</c> parameter — WebIDL's <c>ToInt32</c>.</summary>
    internal static int RequiredInt32(JsValue[] arguments, int index, string member)
    {
        Require(arguments, index, member);
        return TypeConverter.ToInt32(arguments[index]);
    }

    /// <summary>An optional <c>long</c> parameter.</summary>
    internal static int OptionalInt32(JsValue[] arguments, int index, int fallback)
        => index < arguments.Length && !arguments[index].IsUndefined()
            ? TypeConverter.ToInt32(arguments[index])
            : fallback;

    /// <summary>A required <c>unsigned long</c> parameter — WebIDL's <c>ToUint32</c>.</summary>
    internal static uint RequiredUInt32(JsValue[] arguments, int index, string member)
    {
        Require(arguments, index, member);
        return TypeConverter.ToUint32(arguments[index]);
    }

    /// <summary>An optional <c>unsigned long</c> parameter.</summary>
    internal static uint OptionalUInt32(JsValue[] arguments, int index, uint fallback)
        => index < arguments.Length && !arguments[index].IsUndefined()
            ? TypeConverter.ToUint32(arguments[index])
            : fallback;

    /// <summary>A required <c>double</c> parameter.</summary>
    internal static double RequiredDouble(JsValue[] arguments, int index, string member)
    {
        Require(arguments, index, member);
        return TypeConverter.ToNumber(arguments[index]);
    }

    /// <summary>An optional <c>double</c> parameter.</summary>
    internal static double OptionalDouble(JsValue[] arguments, int index, double fallback)
        => index < arguments.Length && !arguments[index].IsUndefined()
            ? TypeConverter.ToNumber(arguments[index])
            : fallback;

    /// <summary>A <c>boolean</c> parameter — never required in practice, always defaulted in IDL.</summary>
    internal static bool OptionalBool(JsValue[] arguments, int index, bool fallback)
        => index < arguments.Length && !arguments[index].IsUndefined()
            ? TypeConverter.ToBoolean(arguments[index])
            : fallback;

    /// <summary>A <c>long?</c> parameter: <c>null</c> and <c>undefined</c> stay null.</summary>
    internal static int? NullableInt32(JsValue[] arguments, int index)
        => index < arguments.Length && !arguments[index].IsNullOrUndefined()
            ? TypeConverter.ToInt32(arguments[index])
            : null;

    /// <summary>
    /// A <c>DOMTimeStamp?</c> parameter — milliseconds since the epoch, or null. <c>input.valueAsDate</c> is
    /// the one that has it, and its IDL type is <c>object?</c> holding a <c>Date</c>; both a number and a
    /// <c>Date</c> arrive as a number here, since <c>ToNumber</c> on a <c>Date</c> is its time value.
    /// </summary>
    internal static DateTime? NullableTimestamp(JsValue[] arguments, int index)
    {
        if (index >= arguments.Length || arguments[index].IsNullOrUndefined())
        {
            return null;
        }

        var milliseconds = TypeConverter.ToNumber(arguments[index]);
        return double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)
            ? null
            : DateTimeOffset.FromUnixTimeMilliseconds((long) milliseconds).UtcDateTime;
    }

    /// <summary>A variadic <c>DOMString...</c> parameter.</summary>
    internal static string[] TextRest(JsValue[] arguments, int from)
    {
        if (arguments.Length <= from)
        {
            return [];
        }

        var values = new string[arguments.Length - from];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = TypeConverter.ToString(arguments[from + i]);
        }

        return values;
    }

    /// <summary>
    /// A variadic interface-typed parameter — <c>(Node or DOMString)...</c> in IDL, where this binding
    /// deliberately takes only the <c>Node</c> half.
    /// </summary>
    /// <remarks>
    /// <c>append</c>, <c>prepend</c>, <c>before</c>, <c>after</c> and <c>replaceWith</c> all accept strings in
    /// the DOM, which are converted to text nodes. AngleSharp's signature is <c>INode[]</c> and there is no
    /// document to create a text node against inside a static member body, so a string argument is a
    /// <c>TypeError</c> here where a browser inserts text. It is recorded as a divergence rather than
    /// approximated, and closing it is a hand-written override once the runtime owns node creation.
    /// </remarks>
    internal static T[] ObjectRest<T>(JsValue[] arguments, int from, string member) where T : class
    {
        if (arguments.Length <= from)
        {
            return [];
        }

        var values = new T[arguments.Length - from];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = DomBindings.Argument<T>(arguments, from + i, member);
        }

        return values;
    }

    /// <summary>
    /// One member of a WebIDL dictionary argument — AngleSharp's <c>[DomInitDict]</c>, which flattens a
    /// dictionary into the parameters from a given offset.
    /// </summary>
    internal static JsValue DictionaryMember(JsValue[] arguments, int offset, string name)
    {
        var dictionary = At(arguments, offset);
        return dictionary is ObjectInstance instance ? instance.Get(name) : JsValue.Undefined;
    }

    private static void Require(JsValue[] arguments, int index, string member)
    {
        if (index < arguments.Length)
        {
            return;
        }

        var message = "Failed to execute '" + member + "': " + (index + 1) + " argument required, but only " + arguments.Length + " present.";

        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is ObjectInstance instance)
            {
                Throw.TypeError(instance.Engine.Realm, message);
            }
        }

        Throw.TypeErrorNoEngine(message);
    }
}
