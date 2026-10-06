using System.Diagnostics.CodeAnalysis;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// Translates native DOM failures into script-visible DOMException or TypeError values.
/// </summary>
/// <remarks>
/// Every generated member uses Guard so exception translation has one policy. Native DomException names
/// and legacy codes are preserved. ArgumentException becomes TypeError; unsupported operations become
/// NotSupportedError. JavaScript exceptions, constraints and cancellation keep the engine's interop behavior.
/// DOM specifies error names and codes; diagnostic wording comes from the native exception.
/// </remarks>
internal static class DomFailures
{
    /// <summary>Translates failures and brackets a generated mutator's complete native call.</summary>
    internal static Func<JsValue, JsValue[], JsValue> GuardMutation(
        string member,
        Func<JsValue, JsValue[], JsValue> implementation)
    {
        var guarded = Guard(member, implementation);

        return (receiver, arguments) =>
        {
            var wrapper = receiver as IDomWrapper;
            if (wrapper is not null) PrepareMutation(wrapper.DomRealm, MutationNode(wrapper));
            using var mutation = wrapper?.DomRealm.MutateLayout() ?? default;
            var result = guarded(receiver, arguments);
            if (wrapper is not null) CompleteMutation(wrapper.DomRealm, MutationNode(wrapper));
            return result;
        };
    }

    private static Node? MutationNode(IDomWrapper wrapper) => wrapper.DomTarget switch
    {
        Node target => target,
        Attr attribute => attribute.OwnerElement,
        Collections.DomNamedNodeMap attributes => attributes.Owner,
        DomRange range => range.Start.Container.Node,
        _ => null,
    };

    internal static void CompleteMutation(DomRealm realm, Node? node)
    {
        var document = node as Document ?? node?.OwnerDocument;
        if (node is not null && document is not null)
        {
            var parser = Runtime.PageRuntime.FindBrowsingContext(realm.Engine, document)?.Parser
                ?? Runtime.PageRuntime.Find(realm.Engine)?.Parser;
            parser?.CompleteNativeMutation(node);
        }
        CustomElements.CustomElementRegistry.Of(realm.Engine)?.Drain();
        Files.FileTransferRealm.IfCreated(realm.Engine)?.FlushChanges();
    }

    internal static void PrepareMutation(DomRealm realm, Node? node)
    {
        Runtime.PageRuntime.Find(realm.Engine)?.Parser?.RecoverNativeMutationNotifications();
        if (node is not null)
        {
            CustomElements.CustomElementRegistry.Of(realm.Engine)?.EnsureWatchingNode(node);
            if ((node as Document ?? node.OwnerDocument) is { } document)
                Runtime.PageRuntime.FindBrowsingContext(realm.Engine, document)?.Parser?.EnsureWatchingNode(node);
        }
    }

    /// <summary>
    /// Wraps a generated member body so that the four CLR exceptions above cross into script as the throw
    /// the standard prescribes. The wrapping happens once per member, when the interface's shape is built.
    /// </summary>
    /// <param name="member">
    /// The qualified member name — <c>Node.appendChild</c> — the same label the body's brand check carries.
    /// </param>
    /// <param name="implementation">The member body.</param>
    /// <remarks>
    /// It costs one closure and one delegate per member, allocated when the shape is built and therefore once
    /// per process per interface a page actually reaches, and one delegate hop per member call — the
    /// <c>try</c> itself costs nothing until it fires. The alternative, a <c>try</c> inside every emitted
    /// body, saves the hop and buys two thousand copies of one decision.
    /// </remarks>
    internal static Func<JsValue, JsValue[], JsValue> Guard(
        string member,
        Func<JsValue, JsValue[], JsValue> implementation)
    {
        // DOM pre-insert/adopt can move descendants before they have wrappers. Select this
        // boundary once while building the shape; ordinary reads pay no traversal or name test.
        var operation = member[(member.LastIndexOf('.') + 1)..];
        if (operation is "adoptNode" or "insertAdjacentElement" or "insertNode" or "surroundContents")
        {
            var body = implementation;
            implementation = (receiver, args) =>
            {
                if (receiver is IDomWrapper wrapper && wrapper.DomTarget is Node or DomRange)
                {
                    if (operation == "insertAdjacentElement" && args.Length > 0 && args[0].IsObject())
                    {
                        var converted = new JsValue[args.Length];
                        Array.Copy(args, converted, args.Length);
                        converted[0] = JsString.Create(TypeConverter.ToString(args[0]));
                        args = converted;
                    }
                    var targetDocument = wrapper.DomTarget is Node target
                        ? target as Document ?? target.OwnerDocument
                        : null;
                    foreach (var argument in args)
                    {
                        if (argument is DomNodeObject { Node: { } source } && !ReferenceEquals(source.OwnerDocument, targetDocument))
                        {
                            wrapper.DomRealm.RecordSubtree(source);
                        }
                    }
                }
                return body(receiver, args);
            };
        }

        if (operation is "innerHTML" or "textContent")
        {
            var body = implementation;
            implementation = (receiver, args) =>
            {
                var result = body(receiver, args);
                if (args.Length > 0 && receiver is DomNodeObject { Node: { } treeNode } node)
                {
                    node.DomRealm.RecordSubtree(treeNode);
                }
                return result;
            };
        }

        // Six members validate a name before they do anything, and the two DOMExceptions DOM's
        // validate-and-extract chooses between are not interchangeable — see DomNames. The choice of wrapper
        // is made here, once, when the shape is built, so that the two thousand members that validate no
        // name pay nothing for the six that do.
        if (DomNames.ValidationOf(member) is { } validation)
        {
            var attributeOperation = validation.Context == DomNames.NameContext.Attribute;
            return (thisObject, arguments) =>
            {
                try
                {
                    if (attributeOperation)
                    {
                        arguments = validation.ConvertAttributeArguments(thisObject, arguments);
                    }
                    validation.Run(thisObject, member, arguments);
                    return implementation(thisObject, arguments);
                }
                catch (Exception exception) when (Translates(thisObject, exception))
                {
                    return Translate((ObjectInstance) thisObject, member, exception);
                }
            };
        }

        return (thisObject, arguments) =>
        {
            try
            {
                return implementation(thisObject, arguments);
            }
            catch (Exception exception) when (Translates(thisObject, exception))
            {
                return Translate((ObjectInstance) thisObject, member, exception);
            }
        };
    }

    /// <summary>Returns the native exception's WebIDL error name.</summary>
    internal static string NameOf(DomException exception) => exception.Name;

    /// <summary>
    /// Whether an exception is one of the four this file converts, on a receiver there is an engine to build
    /// the error in.
    /// </summary>
    private static bool Translates(JsValue thisObject, Exception exception)
        => thisObject is ObjectInstance
           && exception is DomException or ArgumentException or NotSupportedException or NotImplementedException or TypeErrorException;

    /// <summary>
    /// Raises a script-visible DOMException for a hand-written Browser refusal.
    /// </summary>
    /// <param name="engine">The engine the error is built in.</param>
    /// <param name="member">The qualified member name — <c>Element.insertAdjacentHTML</c>.</param>
    /// <param name="name">The error name; <see cref="DomExceptionNames"/> holds the ones with a legacy code.</param>
    /// <param name="detail">What went wrong, as one sentence.</param>
    [DoesNotReturn]
    internal static JsValue Refuse(Engine engine, string member, string name, string detail)
        => Refuse(DomRealm.Of(engine), member, name, detail);

    [DoesNotReturn]
    internal static JsValue Refuse(DomRealm realm, string member, string name, string detail)
    {
        var engine = realm.Engine;
        // Refusals follow the receiver's binding realm, independently of the running realm.
        var intrinsics = realm.OwningRealm.Intrinsics;
        var message = "Failed to execute '" + member + "': " + detail;

        // https://webidl.spec.whatwg.org/#quotaexceedederror is an interface of its own rather than a name a
        // DOMException wears, and `e.constructor === QuotaExceededError` is how a page tells them apart.
        var error = string.Equals(name, DomExceptionNames.QuotaExceeded, StringComparison.Ordinal)
            ? intrinsics.QuotaExceededError.CreateException(message)
            : intrinsics.DomException.CreateException(name, message);

        Throw.JavaScriptException(engine, error, engine.GetLastSyntaxElement()?.Location ?? default);
        return JsValue.Undefined;
    }

    [DoesNotReturn]
    private static JsValue Translate(ObjectInstance receiver, string member, Exception exception)
    {
        var engine = receiver.Engine;
        var realm = (receiver as IDomWrapper)?.DomRealm ?? DomRealm.Of(engine);


        if (exception is TypeErrorException)
        {
            Throw.TypeError(realm.OwningRealm, exception.Message);
        }

        if (exception is ArgumentException)
        {
            // WebIDL's answer for an argument no conversion accepts —
            // https://webidl.spec.whatwg.org/#es-type-mapping. A CLR signature that refused its argument is
            // that and nothing more specific; where the standard names a DOMException instead, the member is
            // written by hand and says so.
            Throw.TypeError(realm.OwningRealm, "Failed to execute '" + member + "': " + exception.Message);
        }

        // A DomException built from a string carries no message of its own — Exception.Message is then the
        // CLR's "Exception of type … was thrown." — and the string it was given is in Name instead.
        return exception is DomException dom
            ? Refuse(realm, member, NameOf(dom), dom.Message)
            : Refuse(realm, member, DomExceptionNames.NotSupported, exception.Message);
    }
}
