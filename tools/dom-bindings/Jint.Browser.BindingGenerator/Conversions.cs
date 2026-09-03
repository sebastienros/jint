using System.Reflection;

namespace Jint.Browser.BindingGenerator;

/// <summary>
/// The WebIDL conversion table, decided at generation time: given a CLR type in a return or parameter
/// position, the C# expression that moves it across the boundary — or the reason it cannot be moved, which
/// becomes a recorded skip rather than a silent omission.
/// </summary>
internal sealed class Conversions
{
    private readonly BindingModel _model;
    private readonly Func<Type, InterfaceModel?> _lookup;
    private readonly Func<Type, bool> _isStringEnum;
    private readonly Func<string, int, bool> _isNullableParameter;

    internal Conversions(
        BindingModel model,
        Func<Type, InterfaceModel?> lookup,
        Func<Type, bool> isStringEnum,
        Func<string, int, bool> isNullableParameter)
    {
        _model = model;
        _lookup = lookup;
        _isStringEnum = isStringEnum;
        _isNullableParameter = isNullableParameter;
    }

    /// <summary>
    /// The expression that turns <paramref name="value"/> — a C# expression of type <paramref name="type"/> —
    /// into a <c>JsValue</c>. <paramref name="realm"/> names the realm variable a wrapping conversion needs.
    /// </summary>
    internal bool TryReturn(Type type, string value, string realm, bool nullableString, out string code, out string reason)
    {
        code = "";
        reason = "";

        switch (type.FullName)
        {
            case "System.Void":
                code = value;
                return true;
            case "System.String":
                code = (nullableString ? "global::Jint.Browser.Dom.DomConvert.NullableText(" : "global::Jint.Browser.Dom.DomConvert.Text(") + value + ")";
                return true;
            case "System.Boolean":
                code = "global::Jint.Browser.Dom.DomConvert.Bool(" + value + ")";
                return true;
            case "System.Int32":
            case "System.UInt32":
            case "System.Int64":
            case "System.Double":
            case "System.Single":
                code = "global::Jint.Browser.Dom.DomConvert.Number(" + value + ")";
                return true;
            case "System.Int16":
            case "System.UInt16":
            case "System.Byte":
            case "System.SByte":
                code = "global::Jint.Browser.Dom.DomConvert.Number((int) (" + value + "))";
                return true;
            case "System.DateTime":
                code = "global::Jint.Browser.Dom.DomConvert.Timestamp(" + value + ")";
                return true;
            case "System.Object":
                code = "global::Jint.Browser.Dom.DomConvert.Any(" + realm + ", " + value + ")";
                return true;
        }

        if (IsNullableOf(type, "System.DateTime"))
        {
            code = "global::Jint.Browser.Dom.DomConvert.Timestamp(" + value + ")";
            return true;
        }

        if (IsNullableOf(type, "System.Int32"))
        {
            code = "global::Jint.Browser.Dom.DomConvert.Number(" + value + ")";
            return true;
        }

        if (type.IsEnum)
        {
            // A numeric enum crosses as its value, cast through a CLR type wide enough to hold it. That
            // matters for exactly one enum today: FilterSettings is a ulong whose SHOW_ALL is 0xFFFFFFFF, and
            // an int cast would make TreeWalker.whatToShow answer -1 where DOM says 4294967295.
            var underlying = type.GetEnumUnderlyingType().Name;
            var cast = underlying is "Int64" or "UInt64" or "UInt32" ? "long" : "int";

            code = _isStringEnum(type)
                ? "global::Jint.Browser.Dom.DomEnums.From" + type.Name + "(" + value + ")"
                : "global::Jint.Browser.Dom.DomConvert.Number((" + cast + ") (" + value + "))";
            return true;
        }

        if (IsHtmlCollection(type, out var element))
        {
            _model.HtmlCollectionElements.Add(CSharpNames.Render(element!));
            code = realm + ".WrapCollection<" + CSharpNames.Render(element!) + ">(" + value + ")";
            return true;
        }

        if (type.IsInterface && _lookup(type) is { } target)
        {
            code = target.Kind == WrapperKind.Node
                ? realm + ".WrapNodeValue(" + value + ")"
                : realm + ".Wrap(" + value + ")";
            return true;
        }

        if (IsEnumerableOfNode(type))
        {
            code = "global::Jint.Browser.Dom.DomConvert.NodeSequence(" + realm + ", " + value + ")";
            return true;
        }

        reason = "returns " + Describe(type) + ", which the conversion table has no entry for";
        return false;
    }

    /// <summary>
    /// The expression that reads parameter <paramref name="index"/> of <paramref name="member"/> as
    /// <paramref name="parameter"/>'s CLR type.
    /// </summary>
    internal bool TryParameter(ParameterInfo parameter, int index, string member, string? dictionaryMemberName, out string code, out string reason)
    {
        code = "";
        reason = "";

        var type = parameter.ParameterType;
        var optional = parameter.IsOptional;
        var source = dictionaryMemberName is null
            ? null
            : "global::Jint.Browser.Dom.DomConvert.DictionaryMember(args, " + index + ", " + CSharpNames.Literal(dictionaryMemberName) + ")";

        // A dictionary member is read from an object rather than from a positional argument, so every
        // conversion below takes the value form rather than the (args, index) form.
        if (source is not null)
        {
            return TryValueParameter(type, source, member, out code, out reason);
        }

        switch (type.FullName)
        {
            case "System.String":
                code = optional
                    ? "global::Jint.Browser.Dom.DomConvert.OptionalText(args, " + index + ", " + DefaultString(parameter) + ")!"
                    : "global::Jint.Browser.Dom.DomConvert.RequiredText(args, " + index + ", " + CSharpNames.Literal(member) + ")";
                return true;
            case "System.Boolean":
                code = "global::Jint.Browser.Dom.DomConvert.OptionalBool(args, " + index + ", " + (optional && Equals(parameter.RawDefaultValue, true) ? "true" : "false") + ")";
                return true;
            case "System.Int32":
                code = optional
                    ? "global::Jint.Browser.Dom.DomConvert.OptionalInt32(args, " + index + ", " + DefaultInt(parameter) + ")"
                    : "global::Jint.Browser.Dom.DomConvert.RequiredInt32(args, " + index + ", " + CSharpNames.Literal(member) + ")";
                return true;
            case "System.UInt32":
                code = optional
                    ? "global::Jint.Browser.Dom.DomConvert.OptionalUInt32(args, " + index + ", " + DefaultInt(parameter) + "u)"
                    : "global::Jint.Browser.Dom.DomConvert.RequiredUInt32(args, " + index + ", " + CSharpNames.Literal(member) + ")";
                return true;
            case "System.Double":
            case "System.Single":
                code = optional
                    ? "global::Jint.Browser.Dom.DomConvert.OptionalDouble(args, " + index + ", 0)"
                    : "global::Jint.Browser.Dom.DomConvert.RequiredDouble(args, " + index + ", " + CSharpNames.Literal(member) + ")";
                if (type.FullName == "System.Single")
                {
                    code = "(float) (" + code + ")";
                }

                return true;
            case "System.Object":
                code = "global::Jint.Browser.Dom.DomConvert.At(args, " + index + ")";
                return true;
        }

        // WebIDL's `long?` and `DOMTimeStamp?`: null and undefined stay null, anything else converts.
        if (IsNullableOf(type, "System.Int32"))
        {
            code = "global::Jint.Browser.Dom.DomConvert.NullableInt32(args, " + index + ")";
            return true;
        }

        if (IsNullableOf(type, "System.DateTime"))
        {
            code = "global::Jint.Browser.Dom.DomConvert.NullableTimestamp(args, " + index + ")";
            return true;
        }

        if (type.IsArray && parameter.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute"))
        {
            var element = type.GetElementType()!;
            if (element.FullName == "System.String")
            {
                code = "global::Jint.Browser.Dom.DomConvert.TextRest(args, " + index + ")";
                return true;
            }

            if (element.IsInterface && _lookup(element) is not null)
            {
                code = "global::Jint.Browser.Dom.DomConvert.ObjectRest<" + CSharpNames.Render(element) + ">(args, " + index + ", " + CSharpNames.Literal(member) + ")";
                return true;
            }

            reason = "takes a variadic " + Describe(element) + " the conversion table has no entry for";
            return false;
        }

        if (type.IsEnum)
        {
            if (_isStringEnum(type))
            {
                code = "global::Jint.Browser.Dom.DomEnums.To" + type.Name + "(global::Jint.Browser.Dom.DomConvert.At(args, " + index + "), " + CSharpNames.Literal(member) + ")";
                return true;
            }

            code = "(" + CSharpNames.Render(type) + ") global::Jint.Browser.Dom.DomConvert." + (optional
                ? "OptionalInt32(args, " + index + ", " + DefaultInt(parameter) + ")"
                : "RequiredInt32(args, " + index + ", " + CSharpNames.Literal(member) + ")");
            return true;
        }

        if (type.IsInterface && _lookup(type) is not null)
        {
            // Optional means WebIDL's `optional`, which lets the argument be left out; the override table is
            // what says a *required* argument may be null. The two are read differently and only the first
            // is read here: C# optionality is a default value in the signature, while nullability is
            // nullable-reference metadata this emitter deliberately does not decode -- doing so would flip
            // every parameter AngleSharp happens to annotate, in one unreviewable change. So `Node? child`
            // is a table entry, and without it `insertBefore(node, null)` -- how every virtual DOM appends
            // its last row -- is a TypeError.
            code = optional || _isNullableParameter(member, index)
                ? "global::Jint.Browser.Dom.DomBindings.NullableArgument<" + CSharpNames.Render(type) + ">(args, " + index + ", " + CSharpNames.Literal(member) + ")"
                : "global::Jint.Browser.Dom.DomBindings.Argument<" + CSharpNames.Render(type) + ">(args, " + index + ", " + CSharpNames.Literal(member) + ")";
            return true;
        }

        reason = "takes " + Describe(type) + ", which the conversion table has no entry for";
        return false;
    }

    private bool TryValueParameter(Type type, string source, string member, out string code, out string reason)
    {
        code = "";
        reason = "";

        if (type.IsEnum && _isStringEnum(type))
        {
            code = "global::Jint.Browser.Dom.DomEnums.To" + type.Name + "(" + source + ", " + CSharpNames.Literal(member) + ")";
            return true;
        }

        switch (type.FullName)
        {
            case "System.String":
                code = "global::Jint.Runtime.TypeConverter.ToString(" + source + ")";
                return true;
            case "System.Boolean":
                code = "global::Jint.Runtime.TypeConverter.ToBoolean(" + source + ")";
                return true;
            case "System.Int32":
                code = "global::Jint.Runtime.TypeConverter.ToInt32(" + source + ")";
                return true;
            case "System.Double":
                code = "global::Jint.Runtime.TypeConverter.ToNumber(" + source + ")";
                return true;
        }

        reason = "has a dictionary member of type " + Describe(type) + ", which the conversion table has no entry for";
        return false;
    }

    /// <summary>
    /// Whether <paramref name="type"/> is an <c>IHtmlCollection&lt;T&gt;</c> — directly, or as one of the
    /// three interfaces that refine it (<c>IHtmlFormControlsCollection</c>, <c>IHtmlOptionsCollection</c>,
    /// <c>IHtmlAllCollection</c>). A refinement is included because the wrapper is closed over the element
    /// type at the call site, and a member declaring the refinement still has to name one.
    /// </summary>
    internal static bool IsHtmlCollection(Type type, out Type? element)
    {
        element = null;

        var closed = IsClosedHtmlCollection(type)
            ? type
            : Array.Find(type.GetInterfaces(), IsClosedHtmlCollection);

        if (closed is null)
        {
            return false;
        }

        element = closed.GetGenericArguments()[0];
        return true;
    }

    private static bool IsClosedHtmlCollection(Type type)
        => type.IsGenericType
           && !type.IsGenericTypeDefinition
           && type.GetGenericTypeDefinition().FullName == "AngleSharp.Dom.IHtmlCollection`1";

    private static bool IsEnumerableOfNode(Type type)
    {
        if (!type.IsGenericType || type.GetGenericTypeDefinition().FullName != "System.Collections.Generic.IEnumerable`1")
        {
            return false;
        }

        var element = type.GetGenericArguments()[0];
        return element.IsInterface && element.FullName == "AngleSharp.Dom.INode";
    }

    private static bool IsNullableOf(Type type, string inner)
        => type.IsGenericType
           && type.GetGenericTypeDefinition().FullName == "System.Nullable`1"
           && type.GetGenericArguments()[0].FullName == inner;

    private static string DefaultString(ParameterInfo parameter)
        => parameter.RawDefaultValue is string s ? CSharpNames.Literal(s) : "null";

    private static string DefaultInt(ParameterInfo parameter)
        => parameter.RawDefaultValue is null
            ? "0"
            : Convert.ToInt64(parameter.RawDefaultValue, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Describe(Type type) => type.FullName ?? type.Name;
}
