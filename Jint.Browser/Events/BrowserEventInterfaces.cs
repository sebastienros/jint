using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The event interfaces the browser adds to the engine's <c>Event</c> and <c>CustomEvent</c>: the UI Events
/// family and the HTML events a page runtime fires.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written rather than generated, because there is nothing to generate them from: AngleSharp has no
/// <c>MouseEvent</c>, and every one of these is a Jint <c>Event</c> subclass whose state is CLR fields —
/// design doc §5, "one bus, Jint's".
/// </para>
/// <para>
/// <b>Deliberately absent: <c>DragEvent</c>.</b> Drag and drop is a v1 non-goal (design doc §2), and an
/// interface with no <c>DataTransfer</c> behind it would be a constructor for an event nothing can ever mean.
/// <c>ClipboardEvent</c> is absent for the same reason. The legacy <c>initUIEvent</c>, <c>initMouseEvent</c>
/// and <c>initKeyboardEvent</c> initializers are absent too: they are the pre-constructor way of building an
/// event through <c>document.createEvent</c>, which this package does not implement at all.
/// </para>
/// </remarks>
internal static class BrowserEventInterfaces
{
    /// <summary>https://w3c.github.io/uievents/#interface-uievent.</summary>
    internal static readonly BrowserEventDefinition UIEvent = Define(
        "UIEvent",
        parent: null,
        BuildUiEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsUiEvent(realm.Engine, Type(realm, args, "UIEvent"), EventInit(realm, args, "UIEvent"), realm.TimeStamp, view, detail, which);
        });

    /// <summary>https://w3c.github.io/uievents/#interface-mouseevent.</summary>
    internal static readonly BrowserEventDefinition MouseEvent = Define(
        "MouseEvent",
        UIEvent,
        BuildMouseEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsMouseEvent(
                realm.Engine,
                Type(realm, args, "MouseEvent"),
                EventInit(realm, args, "MouseEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                EventInitReader.MouseInit(realm.Engine, init));
        });

    /// <summary>https://w3c.github.io/pointerevents/#pointerevent-interface.</summary>
    internal static readonly BrowserEventDefinition PointerEvent = Define(
        "PointerEvent",
        MouseEvent,
        BuildPointerEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsPointerEvent(
                realm.Engine,
                Type(realm, args, "PointerEvent"),
                EventInit(realm, args, "PointerEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                EventInitReader.MouseInit(realm.Engine, init),
                new PointerEventState(
                    EventInitReader.Long(init, Names.PointerId),
                    EventInitReader.Number(init, Names.Width, 1),
                    EventInitReader.Number(init, Names.Height, 1),
                    EventInitReader.Number(init, Names.Pressure),
                    EventInitReader.Number(init, Names.TangentialPressure),
                    EventInitReader.Long(init, Names.TiltX),
                    EventInitReader.Long(init, Names.TiltY),
                    EventInitReader.Long(init, Names.Twist),
                    EventInitReader.Text(init, Names.PointerType),
                    EventInitReader.Bool(init, Names.IsPrimary)));
        });

    /// <summary>https://w3c.github.io/uievents/#interface-wheelevent.</summary>
    internal static readonly BrowserEventDefinition WheelEvent = Define(
        "WheelEvent",
        MouseEvent,
        BuildWheelEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsWheelEvent(
                realm.Engine,
                Type(realm, args, "WheelEvent"),
                EventInit(realm, args, "WheelEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                EventInitReader.MouseInit(realm.Engine, init),
                EventInitReader.Number(init, Names.DeltaX),
                EventInitReader.Number(init, Names.DeltaY),
                EventInitReader.Number(init, Names.DeltaZ),
                EventInitReader.Long(init, Names.DeltaMode));
        },
        constants: [("DOM_DELTA_PIXEL", 0), ("DOM_DELTA_LINE", 1), ("DOM_DELTA_PAGE", 2)]);

    /// <summary>https://w3c.github.io/uievents/#interface-keyboardevent.</summary>
    internal static readonly BrowserEventDefinition KeyboardEvent = Define(
        "KeyboardEvent",
        UIEvent,
        BuildKeyboardEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsKeyboardEvent(
                realm.Engine,
                Type(realm, args, "KeyboardEvent"),
                EventInit(realm, args, "KeyboardEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                new KeyboardEventState(
                    EventInitReader.Text(init, Names.Key),
                    EventInitReader.Text(init, Names.Code),
                    EventInitReader.Long(init, Names.Location),
                    EventInitReader.Bool(init, Names.Repeat),
                    EventInitReader.Bool(init, Names.IsComposing),
                    EventInitReader.Modifiers(init),
                    EventInitReader.OptionalUnsignedLong(init, Names.CharCode),
                    EventInitReader.OptionalUnsignedLong(init, Names.KeyCode)));
        },
        constants:
        [
            ("DOM_KEY_LOCATION_STANDARD", 0),
            ("DOM_KEY_LOCATION_LEFT", 1),
            ("DOM_KEY_LOCATION_RIGHT", 2),
            ("DOM_KEY_LOCATION_NUMPAD", 3),
        ]);

    /// <summary>https://w3c.github.io/uievents/#interface-inputevent.</summary>
    internal static readonly BrowserEventDefinition InputEvent = Define(
        "InputEvent",
        UIEvent,
        BuildInputEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsInputEvent(
                realm.Engine,
                Type(realm, args, "InputEvent"),
                EventInit(realm, args, "InputEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                EventInitReader.Any(init, Names.Data, JsValue.Null),
                EventInitReader.Bool(init, Names.IsComposing),
                EventInitReader.Text(init, Names.InputType));
        });

    /// <summary>https://w3c.github.io/uievents/#interface-compositionevent.</summary>
    internal static readonly BrowserEventDefinition CompositionEvent = Define(
        "CompositionEvent",
        UIEvent,
        BuildCompositionEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsCompositionEvent(
                realm.Engine,
                Type(realm, args, "CompositionEvent"),
                EventInit(realm, args, "CompositionEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                EventInitReader.Text(init, Names.Data));
        });

    /// <summary>https://w3c.github.io/uievents/#interface-focusevent.</summary>
    internal static readonly BrowserEventDefinition FocusEvent = Define(
        "FocusEvent",
        UIEvent,
        BuildFocusEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var (view, detail, which) = EventInitReader.UiInit(init);
            return new JsFocusEvent(
                realm.Engine,
                Type(realm, args, "FocusEvent"),
                EventInit(realm, args, "FocusEvent"),
                realm.TimeStamp,
                view,
                detail,
                which,
                EventInitReader.RelatedTarget(realm.Engine, init));
        });

    /// <summary>https://html.spec.whatwg.org/multipage/form-events.html#submitevent.</summary>
    internal static readonly BrowserEventDefinition SubmitEvent = Define(
        "SubmitEvent",
        parent: null,
        BuildSubmitEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            return new JsSubmitEvent(
                realm.Engine,
                Type(realm, args, "SubmitEvent"),
                EventInit(realm, args, "SubmitEvent"),
                realm.TimeStamp,
                EventInitReader.Any(init, Names.Submitter, JsValue.Null));
        });

    /// <summary>https://html.spec.whatwg.org/multipage/form-events.html#formdataevent.</summary>
    internal static readonly BrowserEventDefinition FormDataEvent = Define(
        "FormDataEvent",
        parent: null,
        BuildFormDataEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            var formData = init?.Get(Names.FormData);

            // formData has no default, so the dictionary is required and so is the member — the one interface
            // here whose second constructor argument cannot be omitted.
            if (formData is null || formData.IsUndefined())
            {
                Throw.TypeError(realm.PrincipalRealm, "Failed to construct 'FormDataEvent': required member formData is undefined.");
            }

            return new JsFormDataEvent(
                realm.Engine,
                Type(realm, args, "FormDataEvent"),
                EventInit(realm, args, "FormDataEvent"),
                realm.TimeStamp,
                formData!);
        },
        constructorLength: 2);

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-hashchangeevent-interface.</summary>
    internal static readonly BrowserEventDefinition HashChangeEvent = Define(
        "HashChangeEvent",
        parent: null,
        BuildHashChangeEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            return new JsHashChangeEvent(
                realm.Engine,
                Type(realm, args, "HashChangeEvent"),
                EventInit(realm, args, "HashChangeEvent"),
                realm.TimeStamp,
                EventInitReader.Text(init, Names.OldUrl),
                EventInitReader.Text(init, Names.NewUrl));
        });

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-popstateevent-interface.</summary>
    internal static readonly BrowserEventDefinition PopStateEvent = Define(
        "PopStateEvent",
        parent: null,
        BuildPopStateEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            return new JsPopStateEvent(
                realm.Engine,
                Type(realm, args, "PopStateEvent"),
                EventInit(realm, args, "PopStateEvent"),
                realm.TimeStamp,
                EventInitReader.Any(init, Names.State, JsValue.Null),
                EventInitReader.Bool(init, Names.HasUaVisualTransition));
        });

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-pagetransitionevent-interface.</summary>
    internal static readonly BrowserEventDefinition PageTransitionEvent = Define(
        "PageTransitionEvent",
        parent: null,
        BuildPageTransitionEvent,
        static (realm, args) =>
        {
            var init = EventInitReader.Dictionary(args);
            return new JsPageTransitionEvent(
                realm.Engine,
                Type(realm, args, "PageTransitionEvent"),
                EventInit(realm, args, "PageTransitionEvent"),
                realm.TimeStamp,
                EventInitReader.Bool(init, Names.Persisted));
        });

    /// <summary>https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-beforeunloadevent-interface.</summary>
    internal static readonly BrowserEventDefinition BeforeUnloadEvent = Define(
        "BeforeUnloadEvent",
        parent: null,
        BuildBeforeUnloadEvent,
        static (realm, args) => new JsBeforeUnloadEvent(
            realm.Engine,
            Type(realm, args, "BeforeUnloadEvent"),
            EventInit(realm, args, "BeforeUnloadEvent"),
            realm.TimeStamp));

    /// <summary>Every interface, parents before children so a prototype chain can be built by walking up.</summary>
    internal static readonly BrowserEventDefinition[] All =
    [
        UIEvent,
        MouseEvent,
        PointerEvent,
        WheelEvent,
        KeyboardEvent,
        InputEvent,
        CompositionEvent,
        FocusEvent,
        SubmitEvent,
        FormDataEvent,
        HashChangeEvent,
        PopStateEvent,
        PageTransitionEvent,
        BeforeUnloadEvent,
    ];

    /// <summary>
    /// Assigns each definition its dense index into <see cref="BrowserEventRealm"/>'s per-engine arrays. Done
    /// from <see cref="All"/> rather than as the definitions are declared, so the index can never drift from
    /// the position the arrays are sized by.
    /// </summary>
    static BrowserEventInterfaces()
    {
        for (var i = 0; i < All.Length; i++)
        {
            All[i].Index = i;
        }
    }

    private static BrowserEventDefinition Define(
        string name,
        BrowserEventDefinition? parent,
        Func<JsObjectShape> shape,
        Func<BrowserEventRealm, JsValue[], JsEvent> construct,
        int constructorLength = 1,
        (string Name, int Value)[]? constants = null)
        => new(name, parent, constructorLength, shape, construct, constants ?? []);

    private static JsString Type(BrowserEventRealm realm, JsValue[] args, string interfaceName)
        => EventConstructor.RequireType(realm.PrincipalRealm, args, interfaceName);

    private static Jint.WebApi.Events.EventInit EventInit(BrowserEventRealm realm, JsValue[] args, string interfaceName)
        => EventConstructor.ReadEventInit(realm.PrincipalRealm, args.Length > 1 ? args[1] : JsValue.Undefined, interfaceName);

    // -----------------------------------------------------------------------------------------------------
    // The shapes. Every attribute is an Accessor and every operation a Method, which is WebIDL's
    // { enumerable: true, configurable: true } and { writable: true, enumerable: true, configurable: true } —
    // the same rule Jint/WebApi/AGENTS.md states and WebIdlPropertyAttributeTests checks.
    // -----------------------------------------------------------------------------------------------------

    private static JsObjectShape BuildUiEvent() => Base("UIEvent")
        .Accessor("view", static (t, _) => Brand<JsUiEvent>(t, "UIEvent.view").View)
        .Accessor("detail", static (t, _) => JsNumber.Create(Brand<JsUiEvent>(t, "UIEvent.detail").Detail))
        .Accessor("which", static (t, _) => JsNumber.Create(Brand<JsUiEvent>(t, "UIEvent.which").Which))
        .Build();

    private static JsObjectShape BuildMouseEvent()
    {
        var builder = Base("MouseEvent");
        AddMouseMembers(builder);
        return builder.Build();
    }

    /// <remarks>
    /// <c>getCoalescedEvents()</c> and <c>getPredictedEvents()</c> answer empty lists, which is what a browser
    /// answers for a synthesized event too: both are filled by the platform's own digitizer sampling.
    /// </remarks>
    private static JsObjectShape BuildPointerEvent()
    {
        var builder = Base("PointerEvent")
            .Accessor("pointerId", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.pointerId").PointerId))
            .Accessor("width", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.width").Width))
            .Accessor("height", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.height").Height))
            .Accessor("pressure", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.pressure").Pressure))
            .Accessor("tangentialPressure", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.tangentialPressure").TangentialPressure))
            .Accessor("tiltX", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.tiltX").TiltX))
            .Accessor("tiltY", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.tiltY").TiltY))
            .Accessor("twist", static (t, _) => JsNumber.Create(Brand<JsPointerEvent>(t, "PointerEvent.twist").Twist))
            .Accessor("pointerType", static (t, _) => JsString.Create(Brand<JsPointerEvent>(t, "PointerEvent.pointerType").PointerType))
            .Accessor("isPrimary", static (t, _) => JsBoolean.Create(Brand<JsPointerEvent>(t, "PointerEvent.isPrimary").IsPrimary))
            .Method("getCoalescedEvents", static (t, _) => EmptyList(t, "PointerEvent.getCoalescedEvents"))
            .Method("getPredictedEvents", static (t, _) => EmptyList(t, "PointerEvent.getPredictedEvents"));

        return builder.Build();
    }

    private static JsObjectShape BuildWheelEvent() => Base("WheelEvent")
        .Constant("DOM_DELTA_PIXEL", JsNumber.PositiveZero)
        .Constant("DOM_DELTA_LINE", JsNumber.PositiveOne)
        .Constant("DOM_DELTA_PAGE", JsNumber.Create(2))
        .Accessor("deltaX", static (t, _) => JsNumber.Create(Brand<JsWheelEvent>(t, "WheelEvent.deltaX").DeltaX))
        .Accessor("deltaY", static (t, _) => JsNumber.Create(Brand<JsWheelEvent>(t, "WheelEvent.deltaY").DeltaY))
        .Accessor("deltaZ", static (t, _) => JsNumber.Create(Brand<JsWheelEvent>(t, "WheelEvent.deltaZ").DeltaZ))
        .Accessor("deltaMode", static (t, _) => JsNumber.Create(Brand<JsWheelEvent>(t, "WheelEvent.deltaMode").DeltaMode))
        .Build();

    private static JsObjectShape BuildKeyboardEvent() => Base("KeyboardEvent")
        .Constant("DOM_KEY_LOCATION_STANDARD", JsNumber.PositiveZero)
        .Constant("DOM_KEY_LOCATION_LEFT", JsNumber.PositiveOne)
        .Constant("DOM_KEY_LOCATION_RIGHT", JsNumber.Create(2))
        .Constant("DOM_KEY_LOCATION_NUMPAD", JsNumber.Create(3))
        .Accessor("key", static (t, _) => JsString.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.key").Key))
        .Accessor("code", static (t, _) => JsString.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.code").Code))
        .Accessor("location", static (t, _) => JsNumber.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.location").Location))
        .Accessor("repeat", static (t, _) => JsBoolean.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.repeat").Repeat))
        .Accessor("isComposing", static (t, _) => JsBoolean.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.isComposing").IsComposing))
        .Accessor("ctrlKey", static (t, _) => KeyboardModifier(t, "KeyboardEvent.ctrlKey", EventModifiers.Control))
        .Accessor("shiftKey", static (t, _) => KeyboardModifier(t, "KeyboardEvent.shiftKey", EventModifiers.Shift))
        .Accessor("altKey", static (t, _) => KeyboardModifier(t, "KeyboardEvent.altKey", EventModifiers.Alt))
        .Accessor("metaKey", static (t, _) => KeyboardModifier(t, "KeyboardEvent.metaKey", EventModifiers.Meta))
        .Accessor("charCode", static (t, _) => JsNumber.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.charCode").CharCode))
        .Accessor("keyCode", static (t, _) => JsNumber.Create(Brand<JsKeyboardEvent>(t, "KeyboardEvent.keyCode").KeyCode))
        .Method(
            "getModifierState",
            static (t, args) => JsBoolean.Create(EventModifierNames.IsActive(
                Brand<JsKeyboardEvent>(t, "KeyboardEvent.getModifierState").Modifiers,
                RequiredKeyArgument(t, args, "KeyboardEvent.getModifierState"))),
            length: 1)
        .Build();

    /// <remarks>
    /// <c>dataTransfer</c> is never non-null here: it is filled only by a paste or a drop, and this version has
    /// neither a clipboard nor drag and drop. <c>getTargetRanges()</c> answers an empty array, which is what it
    /// answers outside <c>contenteditable</c> in a browser.
    /// </remarks>
    private static JsObjectShape BuildInputEvent() => Base("InputEvent")
        .Accessor("data", static (t, _) => Brand<JsInputEvent>(t, "InputEvent.data").Data)
        .Accessor("isComposing", static (t, _) => JsBoolean.Create(Brand<JsInputEvent>(t, "InputEvent.isComposing").IsComposing))
        .Accessor("inputType", static (t, _) => JsString.Create(Brand<JsInputEvent>(t, "InputEvent.inputType").InputType))
        .Accessor("dataTransfer", static (t, _) => Null<JsInputEvent>(t, "InputEvent.dataTransfer"))
        .Method("getTargetRanges", static (t, _) => EmptyList(t, "InputEvent.getTargetRanges"))
        .Build();

    private static JsObjectShape BuildCompositionEvent() => Base("CompositionEvent")
        .Accessor("data", static (t, _) => JsString.Create(Brand<JsCompositionEvent>(t, "CompositionEvent.data").Data))
        .Build();

    private static JsObjectShape BuildFocusEvent() => Base("FocusEvent")
        .Accessor("relatedTarget", static (t, _) => TargetValue(Brand<JsFocusEvent>(t, "FocusEvent.relatedTarget").RelatedTarget))
        .Build();

    private static JsObjectShape BuildSubmitEvent() => Base("SubmitEvent")
        .Accessor("submitter", static (t, _) => Brand<JsSubmitEvent>(t, "SubmitEvent.submitter").Submitter)
        .Build();

    private static JsObjectShape BuildFormDataEvent() => Base("FormDataEvent")
        .Accessor("formData", static (t, _) => Brand<JsFormDataEvent>(t, "FormDataEvent.formData").FormData)
        .Build();

    private static JsObjectShape BuildHashChangeEvent() => Base("HashChangeEvent")
        .Accessor("oldURL", static (t, _) => JsString.Create(Brand<JsHashChangeEvent>(t, "HashChangeEvent.oldURL").OldUrl))
        .Accessor("newURL", static (t, _) => JsString.Create(Brand<JsHashChangeEvent>(t, "HashChangeEvent.newURL").NewUrl))
        .Build();

    private static JsObjectShape BuildPopStateEvent() => Base("PopStateEvent")
        .Accessor("state", static (t, _) => Brand<JsPopStateEvent>(t, "PopStateEvent.state").State)
        .Accessor("hasUAVisualTransition", static (t, _) => JsBoolean.Create(Brand<JsPopStateEvent>(t, "PopStateEvent.hasUAVisualTransition").HasUaVisualTransition))
        .Build();

    private static JsObjectShape BuildPageTransitionEvent() => Base("PageTransitionEvent")
        .Accessor("persisted", static (t, _) => JsBoolean.Create(Brand<JsPageTransitionEvent>(t, "PageTransitionEvent.persisted").Persisted))
        .Build();

    private static JsObjectShape BuildBeforeUnloadEvent() => Base("BeforeUnloadEvent")
        .Accessor(
            "returnValue",
            static (t, _) => JsString.Create(Brand<JsBeforeUnloadEvent>(t, "BeforeUnloadEvent.returnValue").ReturnValue),
            static (t, args) =>
            {
                Brand<JsBeforeUnloadEvent>(t, "BeforeUnloadEvent.returnValue").ReturnValue =
                    TypeConverter.ToString(args.Length > 0 ? args[0] : JsValue.Undefined);
                return JsValue.Undefined;
            })
        .Build();

    /// <summary>
    /// The members <c>MouseEvent</c> declares, added to a builder rather than returned as a shape, because
    /// they are the same members whichever of the three mouse interfaces is being built — WebIDL has them once
    /// on <c>MouseEvent</c> and inherited below it, which is exactly what the prototype chain gives.
    /// </summary>
    /// <remarks>
    /// <c>pageY</c> is <c>clientY</c> plus the page's scroll offset and <c>offsetY</c> is <c>clientY</c> minus
    /// the target's own box origin, both read from the flat box model when the event is read rather than
    /// captured when it was dispatched. That is a divergence a page can only observe by scrolling from inside
    /// a listener and then reading the event it is handling; capturing them instead means two more members on
    /// every <c>MouseEvent</c>, including the ones a script constructs from an init dictionary that has
    /// neither. <c>pageX</c> and <c>offsetX</c> are <c>clientX</c> itself and that is the same two formulas
    /// rather than a shortcut: the page never scrolls sideways and every box starts at <c>x = 0</c>.
    /// </remarks>
    private static void AddMouseMembers(JsObjectShape.Builder builder)
    {
        builder
            .Accessor("screenX", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.screenX").ScreenX))
            .Accessor("screenY", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.screenY").ScreenY))
            .Accessor("clientX", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.clientX").ClientX))
            .Accessor("clientY", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.clientY").ClientY))
            .Accessor("x", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.x").ClientX))
            .Accessor("y", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.y").ClientY))
            .Accessor("pageX", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.pageX").ClientX))
            .Accessor("pageY", static (t, _) => PageY(Brand<JsMouseEvent>(t, "MouseEvent.pageY")))
            .Accessor("offsetX", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.offsetX").ClientX))
            .Accessor("offsetY", static (t, _) => OffsetY(Brand<JsMouseEvent>(t, "MouseEvent.offsetY")))
            .Accessor("movementX", static (t, _) => Zero<JsMouseEvent>(t, "MouseEvent.movementX"))
            .Accessor("movementY", static (t, _) => Zero<JsMouseEvent>(t, "MouseEvent.movementY"))
            .Accessor("button", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.button").Button))
            .Accessor("buttons", static (t, _) => JsNumber.Create(Brand<JsMouseEvent>(t, "MouseEvent.buttons").Buttons))
            .Accessor("ctrlKey", static (t, _) => MouseModifier(t, "MouseEvent.ctrlKey", EventModifiers.Control))
            .Accessor("shiftKey", static (t, _) => MouseModifier(t, "MouseEvent.shiftKey", EventModifiers.Shift))
            .Accessor("altKey", static (t, _) => MouseModifier(t, "MouseEvent.altKey", EventModifiers.Alt))
            .Accessor("metaKey", static (t, _) => MouseModifier(t, "MouseEvent.metaKey", EventModifiers.Meta))
            .Accessor("relatedTarget", static (t, _) => TargetValue(Brand<JsMouseEvent>(t, "MouseEvent.relatedTarget").RelatedTarget))
            .Method(
                "getModifierState",
                static (t, args) => JsBoolean.Create(EventModifierNames.IsActive(
                    Brand<JsMouseEvent>(t, "MouseEvent.getModifierState").Modifiers,
                    RequiredKeyArgument(t, args, "MouseEvent.getModifierState"))),
                length: 1);
    }

    private static JsObjectShape.Builder Base(string name)
        => new JsObjectShape.Builder()
            .ToStringTag(name)
            .PerRealmSlot("constructor", enumerable: false);

    /// <summary>
    /// The WebIDL brand check every member performs — https://webidl.spec.whatwg.org/#dfn-create-operation-function.
    /// A receiver that is not an instance of the interface raises a <c>TypeError</c>, and the message is the
    /// one a browser gives.
    /// </summary>
    private static T Brand<T>(JsValue thisObject, string member) where T : class
    {
        if (thisObject is T typed)
        {
            return typed;
        }

        var message = "Failed to read the '" + member + "' property: Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    private static JsBoolean MouseModifier(JsValue thisObject, string member, EventModifiers flag)
        => JsBoolean.Create((Brand<JsMouseEvent>(thisObject, member).Modifiers & flag) != EventModifiers.None);

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-mouseevent-pagey — the client coordinate plus the page's own
    /// scroll offset, which is what makes a coordinate stay put as the page moves under it.
    /// </summary>
    private static JsNumber PageY(JsMouseEvent ev)
    {
        var scroll = Runtime.PageRuntime.Find(ev.Engine)?.Layout.ScrollY ?? 0;
        return JsNumber.Create(ev.ClientY + scroll);
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-mouseevent-offsety — the client coordinate relative to the
    /// top of the target's own box, or the client coordinate itself when the target has no box.
    /// </summary>
    private static JsNumber OffsetY(JsMouseEvent ev)
    {
        if (ev.Target is not Dom.DomNodeObject { Node: AngleSharp.Dom.IElement element } wrapper ||
            Runtime.PageRuntime.Find(wrapper.DomRealm.Engine) is not { } runtime ||
            runtime.Layout.Current().ClientBoxOf(element) is not { } box)
        {
            return JsNumber.Create(ev.ClientY);
        }

        return JsNumber.Create(ev.ClientY - box.Y);
    }

    private static JsBoolean KeyboardModifier(JsValue thisObject, string member, EventModifiers flag)
        => JsBoolean.Create((Brand<JsKeyboardEvent>(thisObject, member).Modifiers & flag) != EventModifiers.None);

    private static JsNumber Zero<T>(JsValue thisObject, string member) where T : class
    {
        Brand<T>(thisObject, member);
        return JsNumber.PositiveZero;
    }

    private static JsValue Null<T>(JsValue thisObject, string member) where T : class
    {
        Brand<T>(thisObject, member);
        return JsValue.Null;
    }

    private static JsArray EmptyList(JsValue thisObject, string member)
    {
        var engine = thisObject is ObjectInstance instance
            ? instance.Engine
            : null;

        if (engine is null)
        {
            Throw.TypeErrorNoEngine("Failed to execute '" + member + "': Illegal invocation");
        }

        return engine!._mainRealm.Intrinsics.Array.ArrayCreate(0);
    }

    /// <summary>
    /// A target slot's value: the object itself for a wrapper, and the global object for the engine's
    /// synthetic global target, which is the object script knows as the window.
    /// </summary>
    private static JsValue TargetValue(JsEventTarget? target) => target?.EventTargetValue ?? JsValue.Null;

    private static string RequiredKeyArgument(JsValue thisObject, JsValue[] arguments, string member)
    {
        if (arguments.Length == 0)
        {
            var message = "Failed to execute '" + member + "': 1 argument required, but only 0 present.";

            if (thisObject is ObjectInstance instance)
            {
                Throw.TypeError(instance.Engine.Realm, message);
            }

            Throw.TypeErrorNoEngine(message);
        }

        return TypeConverter.ToString(arguments[0]);
    }

    /// <summary>The dictionary member names, interned once — <see cref="EventInitReader"/>'s reason.</summary>
    private static class Names
    {
        internal static readonly JsString PointerId = new("pointerId");
        internal static readonly JsString Width = new("width");
        internal static readonly JsString Height = new("height");
        internal static readonly JsString Pressure = new("pressure");
        internal static readonly JsString TangentialPressure = new("tangentialPressure");
        internal static readonly JsString TiltX = new("tiltX");
        internal static readonly JsString TiltY = new("tiltY");
        internal static readonly JsString Twist = new("twist");
        internal static readonly JsString PointerType = new("pointerType");
        internal static readonly JsString IsPrimary = new("isPrimary");
        internal static readonly JsString DeltaX = new("deltaX");
        internal static readonly JsString DeltaY = new("deltaY");
        internal static readonly JsString DeltaZ = new("deltaZ");
        internal static readonly JsString DeltaMode = new("deltaMode");
        internal static readonly JsString Key = new("key");
        internal static readonly JsString Code = new("code");
        internal static readonly JsString Location = new("location");
        internal static readonly JsString Repeat = new("repeat");
        internal static readonly JsString IsComposing = new("isComposing");
        internal static readonly JsString CharCode = new("charCode");
        internal static readonly JsString KeyCode = new("keyCode");
        internal static readonly JsString Data = new("data");
        internal static readonly JsString InputType = new("inputType");
        internal static readonly JsString Submitter = new("submitter");
        internal static readonly JsString FormData = new("formData");
        internal static readonly JsString OldUrl = new("oldURL");
        internal static readonly JsString NewUrl = new("newURL");
        internal static readonly JsString State = new("state");
        internal static readonly JsString HasUaVisualTransition = new("hasUAVisualTransition");
        internal static readonly JsString Persisted = new("persisted");
    }

}

/// <summary>
/// UI Events' modifier key values, which is what <c>getModifierState()</c> takes.
/// <para>
/// https://w3c.github.io/uievents-key/#keys-modifier
/// </para>
/// </summary>
internal static class EventModifierNames
{
    /// <summary>
    /// https://w3c.github.io/uievents/#dom-mouseevent-getmodifierstate — whether the named modifier is active.
    /// A name the specification does not define answers <see langword="false"/>, which is what it says to do.
    /// </summary>
    internal static bool IsActive(EventModifiers modifiers, string key)
    {
        var flag = key switch
        {
            "Alt" => EventModifiers.Alt,
            "AltGraph" => EventModifiers.AltGraph,
            "CapsLock" => EventModifiers.CapsLock,
            "Control" => EventModifiers.Control,
            "Fn" => EventModifiers.Fn,
            "FnLock" => EventModifiers.FnLock,
            "Hyper" => EventModifiers.Hyper,
            "Meta" => EventModifiers.Meta,
            "NumLock" => EventModifiers.NumLock,
            "ScrollLock" => EventModifiers.ScrollLock,
            "Shift" => EventModifiers.Shift,
            "Super" => EventModifiers.Super,
            "Symbol" => EventModifiers.Symbol,
            "SymbolLock" => EventModifiers.SymbolLock,
            _ => EventModifiers.None,
        };

        return flag != EventModifiers.None && (modifiers & flag) != EventModifiers.None;
    }
}
