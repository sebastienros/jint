using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom;

/// <summary>The ToggleEvent data actually emitted by dialog transitions.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/interaction.html#the-toggleevent-interface</remarks>
internal sealed class BrowserDialogToggleEvent : JsEvent
{
    private static readonly ConditionalWeakTable<DomRealm, ObjectInstance> Prototypes = new();
    private static readonly JsObjectShape Shape = new JsObjectShape.Builder()
        .ToStringTag("ToggleEvent")
        .Accessor("oldState", static (receiver, _) => JsString.Create(Brand(receiver).OldState))
        .Accessor("newState", static (receiver, _) => JsString.Create(Brand(receiver).NewState))
        .Accessor("source", static (receiver, _) => { Brand(receiver); return JsValue.Null; })
        .Build();

    private BrowserDialogToggleEvent(DomRealm realm, JsString type, EventInit init, double timeStamp, string oldState, string newState)
        : base(realm.Engine, type, init, timeStamp)
    {
        OldState = oldState;
        NewState = newState;
        IsTrusted = true;
        _prototype = Prototypes.GetValue(realm, static dom => Shape.Instantiate(dom.Engine, dom.OwningRealm.Intrinsics.Event.PrototypeObject));
    }

    private string OldState { get; }
    private string NewState { get; }

    internal static BrowserDialogToggleEvent Create(DomRealm realm, string type, string oldState, string newState, bool cancelable)
    {
        var name = JsString.Create(type);
        var init = new EventInit(Bubbles: false, Cancelable: cancelable, Composed: false);
        return new BrowserDialogToggleEvent(realm, name, init, EventConstructor.TimeStampNow(realm.Engine), oldState, newState);
    }

    private static BrowserDialogToggleEvent Brand(JsValue receiver)
    {
        if (receiver is BrowserDialogToggleEvent value) return value;
        Throw.TypeErrorNoEngine("Illegal invocation of ToggleEvent member");
        return null!;
    }
}
