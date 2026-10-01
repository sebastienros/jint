using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Messaging;
using Jint.WebApi.StructuredClone;

namespace Jint.Browser.Runtime;

/// <summary>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-windowproxy-exotic-object
/// Engine isolation deliberately makes even same-origin cross-page windows cross-origin objects.
/// </summary>
internal sealed class RemoteWindowProxy : CrossPageObject
{
    internal RemoteWindowProxy(PageRuntime source, BrowsingContextHandle target) : base(source)
    {
        var location = new RemoteLocation(source, target);
        Accessor("window", () => this);
        Accessor("self", () => this);
        Accessor("location", () => location, value => location.Navigate(value, replace: false));
        Operation("close", 0, _ => { target.Close(source.Page); return Undefined; });
        Accessor("closed", () => target.Closed ? JsBoolean.True : JsBoolean.False);
        Operation("focus", 0, _ => Undefined);
        Operation("blur", 0, _ => Undefined);
        Accessor("frames", () => this);
        Accessor("length", () => JsNumber.PositiveZero);
        Accessor("top", () => this);
        Accessor("opener", () => target.Opener is { } opener ? source.WindowProxyFor(opener.WindowHandle) : Null);
        Accessor("parent", () => this);
        Operation("postMessage", 1, args => PostMessage(source, target, args));
    }

    /// <summary>https://html.spec.whatwg.org/multipage/web-messaging.html#window-post-message-steps</summary>
    private static JsValue PostMessage(PageRuntime source, BrowsingContextHandle target, JsValue[] arguments)
    {
        var engine = source.Engine;
        var realm = engine._mainRealm;
        if (arguments.Length == 0) Throw.TypeError(realm, "postMessage requires a message.");
        if (target.Closed) return Undefined;
        var second = arguments.At(1);
        string targetOrigin;
        List<JsValue>? transfers;
        if (arguments.Length < 3 && (second is ObjectInstance || second.IsNullOrUndefined()))
        {
            var origin = second is ObjectInstance options ? options.Get("targetOrigin") : Undefined;
            targetOrigin = origin.IsUndefined() ? "/" : TypeConverter.ToString(origin);
            transfers = StructuredSerializeOptions.ReadTransferOption(realm, second, "postMessage");
        }
        else
        {
            targetOrigin = TypeConverter.ToString(second);
            transfers = arguments.At(2).IsUndefined() ? null
                : StructuredSerializeOptions.ReadTransferSequence(realm, arguments[2], "postMessage");
        }
        var sourceOrigin = DomDocumentMetadata.CreatorOrigin(source.Dom);
        DomDocumentOrigin? expectedOrigin = null;
        if (targetOrigin == "/") expectedOrigin = sourceOrigin;
        else if (targetOrigin != "*")
        {
            var parsed = PageUrl.Parse(targetOrigin, null);
            if (parsed is null) WindowOpen.ThrowDom(source, DomExceptionNames.Syntax, "Invalid target origin '" + targetOrigin + "'.");
            expectedOrigin = DomDocumentOrigin.FromUrl(parsed!.Serialize());
        }
        if (transfers is { Count: > 0 })
            WindowOpen.ThrowDom(source, DomExceptionNames.DataClone, "Cross-page postMessage does not support transfer list entries.");

        var record = new StructuredSerializer(engine, realm).Serialize(arguments[0], transferList: null);
        var sender = source.Page;
        // Only the record, immutable origin and page handles cross the engine boundary.
        target.Enqueue(sender, page => page.RunOnLoopAsync(destinationEngine =>
        {
            var runtime = PageRuntime.Find(destinationEngine)!;
            if (expectedOrigin is not null && !expectedOrigin.IsSameOrigin(DomDocumentMetadata.CreatorOrigin(runtime.Dom))) return false;
            var destinationRealm = destinationEngine._mainRealm;
            var data = new StructuredDeserializer(destinationEngine, destinationRealm).Deserialize(record);
            var prototype = destinationRealm.Intrinsics.MessageEvent;
            var empty = prototype.CreateTrustedMessageEvent(JsString.Create("message"), Undefined);
            var message = new JsMessageEvent(destinationEngine, JsString.Create("message"), default,
                EventConstructor.TimeStampNow(destinationEngine), data, JsString.Create(sourceOrigin.Serialized),
                JsString.Empty, runtime.WindowProxyFor(sender.WindowHandle), empty.Ports)
            {
                IsTrusted = true,
                _prototype = prototype.PrototypeObject,
            };
            PageEvents.Dispatch(runtime, destinationEngine._webApi!.GlobalEventTarget!, message);
            return true;
        }));
        return Undefined;
    }

    private sealed class RemoteLocation : CrossPageObject
    {
        private readonly BrowsingContextHandle _target;

        internal RemoteLocation(PageRuntime source, BrowsingContextHandle target) : base(source)
        {
            _target = target;
            Accessor("href", null, value => Navigate(value, replace: false));
            Operation("replace", 1, args =>
            {
                if (args.Length == 0) Throw.TypeError(Engine.Realm, "replace requires a URL.");
                Navigate(args[0], replace: true);
                return Undefined;
            });
        }

        internal void Navigate(JsValue value, bool replace)
            => _target.Navigate(Source.Page, WindowOpen.Navigation(Source, TypeConverter.ToString(value), replace: replace));
    }
}

/// <summary>
/// HTML's cross-origin internal methods shared by WindowProxy and Location. Descriptors are cached and
/// non-enumerable per CrossOriginGetOwnPropertyHelper, not ordinary WebIDL attribute descriptors.
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#crossorigingetownpropertyhelper-(-o,-p-)
/// </summary>
internal abstract class CrossPageObject : ObjectInstance
{
    private static readonly JsValue[] _fallbackKeys =
        [new JsString("then"), GlobalSymbolRegistry.ToStringTag, GlobalSymbolRegistry.HasInstance, GlobalSymbolRegistry.IsConcatSpreadable];
    private readonly Dictionary<JsValue, PropertyDescriptor> _members = [];
    protected PageRuntime Source { get; }

    protected CrossPageObject(PageRuntime source) : base(source.Engine) => Source = source;

    protected void Accessor(string name, Func<JsValue>? getter, Action<JsValue>? setter = null)
        => _members.Add(JsString.Create(name), new GetSetPropertyDescriptor(
            getter is null ? null : new ClrFunction(Engine, "get " + name, (_, _) => getter()),
            setter is null ? null : new ClrFunction(Engine, "set " + name, (_, args) =>
            {
                setter(args.At(0));
                return Undefined;
            }, 1), enumerable: false, configurable: true));

    protected void Operation(string name, int length, Func<JsValue[], JsValue> body)
        => _members.Add(JsString.Create(name), new PropertyDescriptor(
            new ClrFunction(Engine, name, (_, args) => body(args), length), writable: false, enumerable: false, configurable: true));

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (_members.TryGetValue(property, out var descriptor)) return descriptor;
        if (_fallbackKeys.Contains(property)) return new PropertyDescriptor(Undefined, false, false, true);
        Deny();
        return PropertyDescriptor.Undefined;
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        var descriptor = GetOwnProperty(property);
        if (descriptor.IsDataDescriptor()) return descriptor.Value;
        if (descriptor.Get is { } getter && !getter.IsUndefined()) return Engine.Call(getter, receiver);
        Deny();
        return Undefined;
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        var descriptor = GetOwnProperty(property);
        if (descriptor.Set is { } setter && !setter.IsUndefined())
        {
            Engine.Call(setter, receiver, [value]);
            return true;
        }
        return Deny();
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc) => Deny();
    public override bool Delete(JsValue property) => Deny();
    public override bool HasProperty(JsValue property) => GetOwnProperty(property) != PropertyDescriptor.Undefined;
    protected internal override ObjectInstance? GetPrototypeOf() => null;
    internal override bool SetPrototypeOf(JsValue value) => value.IsNull();
    public override bool PreventExtensions() => false;

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>(_members.Count + _fallbackKeys.Length);
        if ((types & Types.String) != 0) keys.AddRange(_members.Keys);
        foreach (var key in _fallbackKeys)
            if ((types & key.Type) != 0) keys.Add(key);
        return keys;
    }

    private bool Deny()
    {
        WindowOpen.ThrowDom(Source, DomExceptionNames.Security,
            "Blocked a frame from accessing a cross-page window.");
        return false;
    }
}
