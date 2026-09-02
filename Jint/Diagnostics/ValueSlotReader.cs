using Jint.Native;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Diagnostics;

/// <summary>
/// The internal-slot reads a value renderer is allowed to make: everything here answers from an object's
/// own state, never through a property of the same name and never by calling anything.
/// </summary>
/// <remarks>
/// <para>
/// Two renderers share it — the <c>console</c>'s Node-shaped one and <see cref="ValueInspector"/>'s
/// DevTools-shaped one — so the two can never disagree about what an object <i>is</i> while disagreeing,
/// deliberately, about how to write it down. The rule these methods exist to keep is that
/// <c>source</c>, <c>flags</c>, <c>byteLength</c>, <c>name</c> and <c>toISOString</c> are all configurable,
/// so reading a value <i>through the property of that name</i> is calling whatever a script left there.
/// </para>
/// </remarks>
internal static class ValueSlotReader
{
    /// <summary>How many proxies a chain may nest before a walk names it instead of descending further.</summary>
    internal const int MaxProxyHops = 8;

    /// <summary>How far up a prototype chain a descriptor walk climbs before giving up on an answer.</summary>
    private const int MaxPrototypeHops = 32;

    /// <summary>
    /// The non-proxy object a proxy stands for; <see langword="null"/> when the chain is revoked, and a
    /// <see cref="JsProxy"/> again when it is longer than <see cref="MaxProxyHops"/>.
    /// </summary>
    internal static ObjectInstance? ProxyTarget(JsProxy proxy)
    {
        // A revoked proxy has no target at all, and that is the one state a renderer has to tell apart:
        // every trap on it throws, so there is nothing left to show but the fact of it.
        ObjectInstance? current = proxy._target;
        for (var hops = 1; hops < MaxProxyHops && current is JsProxy next; hops++)
        {
            current = next._target;
        }

        return current;
    }

    /// <summary>
    /// A date's <c>[[DateValue]]</c> as text: NaN for exactly the values <c>toISOString</c> raises a
    /// <c>RangeError</c> for, and "Invalid Date" is what every implementation shows in its place.
    /// </summary>
    internal static string DateText(JsDate date)
        => double.IsNaN(date.DateValue) ? "Invalid Date" : DatePrototype.FormatIsoString(date);

    /// <summary>A regular expression as its <c>[[OriginalSource]]</c> and <c>[[OriginalFlags]]</c>.</summary>
    internal static string RegExpText(JsRegExp regExp) => "/" + regExp.Source + "/" + regExp.Flags;

    /// <summary>Which of the two array-buffer interfaces a buffer is an instance of.</summary>
    internal static string ArrayBufferTypeName(JsArrayBuffer buffer)
        => buffer is JsSharedArrayBuffer ? "SharedArrayBuffer" : "ArrayBuffer";

    /// <summary>Which primitive a wrapper object boxes, named as its constructor is.</summary>
    internal static string BoxedPrimitiveTypeName(IJsPrimitive boxed) => boxed.PrimitiveValue switch
    {
        JsString => "String",
        JsNumber => "Number",
        JsBoolean => "Boolean",
        JsSymbol => "Symbol",
        JsBigInt => "BigInt",
        _ => "Object",
    };

    /// <summary>Whether a function object is a class constructor rather than an ordinary function.</summary>
    internal static bool IsClassConstructor(Function function)
        => function is ScriptFunction { _isClassConstructor: true };

    /// <summary>Which of the four function kinds a function's declaration makes it.</summary>
    internal static string FunctionKindName(Function function)
    {
        var declaration = function.FunctionDeclaration;
        if (declaration is null)
        {
            return "Function";
        }

        if (declaration.Async)
        {
            return declaration.Generator ? "AsyncGeneratorFunction" : "AsyncFunction";
        }

        return declaration.Generator ? "GeneratorFunction" : "Function";
    }

    /// <summary>The half of an accessor property a renderer may report, since it may call neither.</summary>
    internal static string AccessorLabel(PropertyDescriptor descriptor)
    {
        var hasGet = descriptor.Get is not null && !descriptor.Get.IsUndefined();
        var hasSet = descriptor.Set is not null && !descriptor.Set.IsUndefined();
        return hasGet && hasSet ? "[Getter/Setter]" : hasGet ? "[Getter]" : "[Setter]";
    }

    /// <summary>
    /// The name of the constructor an object was built by, read as a descriptor off the prototype chain, or
    /// <see langword="null"/> when no step of that walk can be taken without calling something.
    /// </summary>
    /// <remarks>
    /// Every step refuses an accessor, so a script that installed a <c>constructor</c> getter — or a
    /// <c>name</c> getter on the function it points at — produces no label rather than a call.
    /// </remarks>
    internal static string? ConstructorName(ObjectInstance obj)
    {
        var prototype = obj.Prototype;
        for (var hops = 0; prototype is not null && hops < MaxPrototypeHops; hops++)
        {
            if (prototype is JsProxy)
            {
                return null;
            }

            var descriptor = prototype.GetOwnProperty(CommonProperties.Constructor);
            if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
            {
                if (descriptor.IsAccessorDescriptor())
                {
                    return null;
                }

                if (descriptor.Value is not Function constructor)
                {
                    return null;
                }

                var name = constructor.GetOwnFunctionNameForDisplay();
                return string.IsNullOrEmpty(name) ? null : name;
            }

            prototype = prototype.Prototype;
        }

        return null;
    }

    /// <summary>
    /// An error's <c>name</c> and <c>message</c> read as descriptors up the prototype chain, with an
    /// accessor anywhere on the walk treated as absent rather than called.
    /// </summary>
    internal static void ErrorText(ObjectInstance error, out string name, out string message)
    {
        name = DataPropertyText(error, CommonProperties.Name) ?? ConstructorName(error) ?? "Error";
        message = DataPropertyText(error, CommonProperties.Message) ?? string.Empty;
    }

    /// <summary>
    /// The text of a string-valued data property somewhere on <paramref name="obj"/>'s chain, or
    /// <see langword="null"/> when it is absent, an accessor, or anything a renderer would have to coerce.
    /// </summary>
    private static string? DataPropertyText(ObjectInstance obj, JsString key)
    {
        ObjectInstance? current = obj;
        for (var hops = 0; current is not null && hops < MaxPrototypeHops; hops++)
        {
            if (current is JsProxy)
            {
                return null;
            }

            var descriptor = current.GetOwnProperty(key);
            if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
            {
                if (descriptor.IsAccessorDescriptor())
                {
                    return null;
                }

                // Coercing anything but a string would reach `toString`/`valueOf`, which is script.
                return descriptor.Value is JsString text ? text.ToString() : null;
            }

            current = current.Prototype;
        }

        return null;
    }
}
