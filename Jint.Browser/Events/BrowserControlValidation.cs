using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.RegExp;
using Jint.Runtime;
using Jint.WebApi.Url.Parsing;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

[Flags]
internal enum ControlValidityFlags
{
    None = 0,
    ValueMissing = 1,
    TypeMismatch = 2,
    PatternMismatch = 4,
    TooLong = 8,
    TooShort = 16,
    RangeUnderflow = 32,
    RangeOverflow = 64,
    StepMismatch = 128,
    BadInput = 256,
    CustomError = 512,
}

internal readonly record struct ControlValiditySnapshot(bool WillValidate, ControlValidityFlags Flags)
{
    internal bool IsValid => Flags == ControlValidityFlags.None;
}

/// <summary>Demand-only, event-free HTML constraint facts over the authoritative native control states.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constraints</remarks>
internal static class BrowserControlValidation
{
    // These strings are Browser behavior, not a second value/dirty/selection model on a native control.
    private static readonly ConditionalWeakTable<Element, ControlBehaviorState> Behaviors = new();
    // Compiled expressions belong to a realm. Neither a native node nor process-shared state retains an Engine.
    private static readonly ConditionalWeakTable<DomRealm, ConditionalWeakTable<Element, PatternCache>> Patterns = new();

    internal static void SetCustomValidity(Element element, string message)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(message);
        var previous = Behaviors.TryGetValue(element, out var existing) ? existing.CustomMessage : string.Empty;
        if (string.Equals(previous, message, StringComparison.Ordinal)) return;
        if (message.Length == 0)
        {
            Behaviors.Remove(element);
        }
        else
        {
            Behaviors.GetOrCreateValue(element).CustomMessage = message;
        }
        if (element.OwnerDocument is { } document) BrowserSelectorSemanticRevision.Advance(document);
    }

    internal static bool WillValidate(DomRealm realm, Element element)
        => WillValidate(realm, element, Work(realm), null);

    internal static bool WillValidate(DomRealm realm, Element element, DomReadWork work,
        Func<Action<int>?>? nativeCheckpointFactory)
    {
        var result = Candidate(realm, element, work, nativeCheckpointFactory);
        work.Check();
        return result;
    }

    internal static ControlValiditySnapshot Read(DomRealm realm, Element element)
        => Read(realm, element, Work(realm), null);

    // A producer supplies a fresh cumulative-counter adapter for each native helper invocation.
    // Its Browser work is already invocation-owned; validity algorithms and caches stay authoritative here.
    internal static ControlValiditySnapshot Read(DomRealm realm, Element element, DomReadWork work,
        Func<Action<int>?>? nativeCheckpointFactory)
    {
        var candidate = Candidate(realm, element, work, nativeCheckpointFactory);
        var flags = Behaviors.TryGetValue(element, out var behavior) && behavior.CustomMessage.Length > 0
            ? ControlValidityFlags.CustomError : ControlValidityFlags.None;
        if (element.NamespaceUri == Namespaces.Html)
        {
            switch (element.LocalName)
            {
                case "input":
                    flags |= Input(realm, element, work, nativeCheckpointFactory);
                    break;
                case "textarea":
                    var area = element.GetHtmlState()!.TextArea!;
                    var value = area.GetValue(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
                    if (work.Attribute(element, "required") is not null && value.Length == 0
                        && work.Attribute(element, "readonly") is null && !Disabled(realm, element, work, nativeCheckpointFactory))
                    {
                        flags |= ControlValidityFlags.ValueMissing;
                    }
                    flags |= Length(realm, element, value, area.DirtyValue, area.LastValueChangeOrigin, work, nativeCheckpointFactory);
                    break;
                case "select":
                    flags |= Select(realm, element, work, nativeCheckpointFactory);
                    break;
            }
        }
        work.Check();
        return new ControlValiditySnapshot(candidate, flags);
    }

    internal static string ValidationMessage(DomRealm realm, Element element)
    {
        var snapshot = Read(realm, element);
        if (!snapshot.WillValidate || snapshot.IsValid) return string.Empty;
        if (Behaviors.TryGetValue(element, out var behavior) && behavior.CustomMessage.Length > 0)
            return behavior.CustomMessage;
        return snapshot.Flags switch
        {
            var flags when (flags & ControlValidityFlags.ValueMissing) != 0 => "Please fill out this field.",
            var flags when (flags & ControlValidityFlags.TypeMismatch) != 0 => "Please enter a value of the required type.",
            var flags when (flags & ControlValidityFlags.PatternMismatch) != 0 => "Please match the requested format.",
            var flags when (flags & ControlValidityFlags.TooLong) != 0 => "Please shorten this value.",
            var flags when (flags & ControlValidityFlags.TooShort) != 0 => "Please lengthen this value.",
            _ => "Please enter a valid value.",
        };
    }

    internal static bool CheckValidity(DomRealm realm, Element element)
    {
        if (!WillValidate(realm, element) || Read(realm, element).IsValid) return true;
        var invalid = realm.OwningRealm.Intrinsics.Event.CreateTrustedEvent(
            JsString.Create("invalid"), new EventInit(Bubbles: false, Cancelable: true, Composed: false));
        realm.WrapNode(element).DispatchEvent(invalid);
        return false;
    }

    private static DomReadWork Work(DomRealm realm)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        return work;
    }

    private static Action<int>? NativeCheckpoint(DomRealm realm, Func<Action<int>?>? factory)
        => factory is null ? realm.NativeReadCheckpoint : factory();

    private static bool Disabled(DomRealm realm, Element element, DomReadWork work, Func<Action<int>?>? factory)
        => HtmlDisabledness.GetState(element, NativeCheckpoint(realm, factory), work.Token) == HtmlDisabledState.Disabled;

    private static bool Candidate(DomRealm realm, Element element, DomReadWork work, Func<Action<int>?>? nativeCheckpointFactory)
    {
        if (element.NamespaceUri != Namespaces.Html) return false;
        var applicable = element.LocalName switch
        {
            "input" => HtmlInputTypes.Parse(work.Attribute(element, "type")) is not (HtmlInputType.Hidden or HtmlInputType.Reset or HtmlInputType.Button),
            "textarea" or "select" => true,
            "button" => BrowserFormDefaults.IsSubmitButton(element, work),
            _ => false,
        };
        if (!applicable)
        {
            if (CustomElements.CustomElementRegistry.Of(realm.Engine)?.TryGetRecord(element) is { FormAssociated: true })
                throw new NotSupportedException("Form-associated custom-element validity requires its ElementInternals state.");
            return false;
        }
        if (Disabled(realm, element, work, nativeCheckpointFactory)) return false;
        if (element.LocalName == "textarea" && work.Attribute(element, "readonly") is not null) return false;
        if (element.LocalName == "input" && HtmlInputTypes.Info(HtmlInputTypes.Parse(work.Attribute(element, "type"))).ReadOnlyApplies
            && work.Attribute(element, "readonly") is not null) return false;
        for (var ancestor = element.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            work.Step();
            if (ancestor is Element { NamespaceUri: Namespaces.Html, LocalName: "datalist" }) return false;
        }
        return true;
    }

    private static ControlValidityFlags Input(DomRealm realm, Element element, DomReadWork work,
        Func<Action<int>?>? nativeCheckpointFactory)
    {
        var state = element.GetHtmlState()!.GetInputValueState(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token)!;
        var required = work.Attribute(element, "required") is not null;
        if (state.Type == HtmlInputType.Checkbox)
            return required && !HtmlCheckableState.Get(element, NativeCheckpoint(realm, nativeCheckpointFactory), work.Token)!.Checked ? ControlValidityFlags.ValueMissing : ControlValidityFlags.None;
        if (state.Type == HtmlInputType.Radio)
        {
            var group = HtmlCheckableState.GetRadioGroupFacts(element, NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
            return group.RequiredCount > 0 && group.CheckedCount == 0 ? ControlValidityFlags.ValueMissing : ControlValidityFlags.None;
        }
        if (state.Type == HtmlInputType.File)
        {
            var files = Dom.Files.FileTransferRealm.Of(realm.Engine).InputFiles(element, work);
            return required && (files is null || files.Length == 0) ? ControlValidityFlags.ValueMissing : ControlValidityFlags.None;
        }
        // Unimplemented native value families throw here. They never become a successful validity result.
        var value = state.GetValue(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
        var flags = required && HtmlInputTypes.Info(state.Type).RequiredApplies
            && value.Length == 0 && !state.ReadOnly && !Disabled(realm, element, work, nativeCheckpointFactory)
            ? ControlValidityFlags.ValueMissing : ControlValidityFlags.None;
        // HTML's range and step constraints use the native numeric/temporal lattice; Browser does not
        // reparse attributes or derive a second coordinate from the exposed value string.
        // https://html.spec.whatwg.org/multipage/input.html#the-min-and-max-attributes
        var numeric = state.GetNumericFacts(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
        if (state.BadInput) flags |= ControlValidityFlags.BadInput;
        if (numeric.Applies)
        {
            if (numeric.Underflow) flags |= ControlValidityFlags.RangeUnderflow;
            if (numeric.Overflow) flags |= ControlValidityFlags.RangeOverflow;
            if (numeric.StepMismatch) flags |= ControlValidityFlags.StepMismatch;
            return flags;
        }
        if (!HtmlInputValueState.IsTextType(state.Type)) return flags;
        if (value.Length > 0)
        {
            if (state.Type == HtmlInputType.Email && !EmailList(value, work.Attribute(element, "multiple") is not null, work)
                || state.Type == HtmlInputType.Url && UrlParser.Parse(value) is null)
                flags |= ControlValidityFlags.TypeMismatch;
            if (PatternMismatch(realm, element, value, state.Type == HtmlInputType.Email && work.Attribute(element, "multiple") is not null, work))
                flags |= ControlValidityFlags.PatternMismatch;
        }
        return flags | Length(realm, element, value, state.DirtyValue, state.LastValueChangeOrigin, work, nativeCheckpointFactory);
    }

    private static ControlValidityFlags Length(DomRealm realm, Element element, string value, bool dirty, HtmlValueChangeOrigin origin,
        DomReadWork work, Func<Action<int>?>? nativeCheckpointFactory)
    {
        if (!dirty || origin != HtmlValueChangeOrigin.User) return ControlValidityFlags.None;
        var maximum = HtmlTextControlAttributes.GetMaximumAllowedLength(element, NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
        var minimum = HtmlTextControlAttributes.GetMinimumAllowedLength(element, NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
        var flags = maximum is { } max && value.Length > max ? ControlValidityFlags.TooLong : ControlValidityFlags.None;
        if (value.Length > 0 && minimum is { } min && value.Length < min) flags |= ControlValidityFlags.TooShort;
        return flags;
    }

    private static ControlValidityFlags Select(DomRealm realm, Element element, DomReadWork work,
        Func<Action<int>?>? nativeCheckpointFactory)
    {
        if (work.Attribute(element, "required") is null) return ControlValidityFlags.None;
        var state = element.GetHtmlState()!.GetSelectState(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token)!;
        var selected = state.SelectedOptions.Snapshot(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
        if (selected.Count == 0) return ControlValidityFlags.ValueMissing;
        var first = state.Options.Item(0, NativeCheckpoint(realm, nativeCheckpointFactory), work.Token);
        if (selected.Count == 1 && ReferenceEquals(selected[0], first) && state.GetDisplaySize(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token) == 1
            && first!.GetHtmlState()!.GetOptionState(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token)!
                .GetValue(NativeCheckpoint(realm, nativeCheckpointFactory), work.Token).Length == 0)
        {
            for (var parent = first.ParentNode; parent is not null && !ReferenceEquals(parent, element); parent = parent.ParentNode)
            {
                work.Step();
                if (parent is Element { NamespaceUri: Namespaces.Html, LocalName: "optgroup" }) return ControlValidityFlags.None;
            }
            return ControlValidityFlags.ValueMissing;
        }
        return ControlValidityFlags.None;
    }

    private static bool PatternMismatch(DomRealm realm, Element element, string value, bool multiple, DomReadWork work)
    {
        var pattern = work.Attribute(element, "pattern");
        if (pattern is null) return false;
        var cache = Patterns.GetOrCreateValue(realm).GetOrCreateValue(element);
        if (!cache.Initialized || !work.Equal(cache.Pattern, pattern))
        {
            JsRegExp? expression;
            try
            {
                // HTML §4.10.5.3.6 validates standalone before wrapping, always in ECMAScript Unicode Sets mode.
                realm.OwningRealm.Intrinsics.RegExp.RegExpCreate(JsString.Create(pattern), JsString.Create("v"));
                expression = realm.OwningRealm.Intrinsics.RegExp.RegExpCreate(JsString.Create("^(?:" + pattern + ")$"), JsString.Create("v"));
            }
            catch (JavaScriptException)
            {
                // Invalid authored pattern has no compiled expression; limits and cancellation are CLR failures.
                expression = null;
            }
            work.Check();
            cache.Pattern = pattern;
            cache.Expression = expression;
            cache.Initialized = true;
        }
        if (cache.Expression is not { } regex) return false;
        if (!multiple)
        {
            work.Check();
            var mismatch = RegExpPrototype.RegExpBuiltinExec(regex, value).IsNull();
            work.Check();
            return mismatch;
        }
        foreach (var token in EmailTokens(value, work))
        {
            work.Check();
            if (RegExpPrototype.RegExpBuiltinExec(regex, token).IsNull()) return true;
        }
        work.Check();
        return false;
    }

    private static bool EmailList(string value, bool multiple, DomReadWork work)
    {
        if (!multiple) return Email(value.AsSpan(), work);
        foreach (var token in EmailTokens(value, work))
            if (!Email(token.AsSpan(), work)) return false;
        return true;
    }

    private static IEnumerable<string> EmailTokens(string value, DomReadWork work)
    {
        var start = 0;
        for (var i = 0; i <= value.Length; i++)
        {
            work.Step();
            if (i == value.Length || value[i] == ',')
            {
                // The native email sanitizer already strips ASCII whitespace from each separately defined value.
                yield return value.Substring(start, i - start);
                start = i + 1;
            }
        }
    }

    // HTML email ABNF: ASCII atext/dot local part, nonempty labels bounded to 63 ASCII letters/digits/hyphens.
    private static bool Email(ReadOnlySpan<char> value, DomReadWork work)
    {
        var at = -1;
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            if (value[i] != '@') continue;
            if (at >= 0) return false;
            at = i;
        }
        if (at <= 0 || at == value.Length - 1) return false;
        for (var i = 0; i < at; i++)
        {
            work.Step();
            var c = value[i];
            if (!AlphaNumeric(c) && c != '.' && "!#$%&'*+-/=?^_`{|}~".IndexOf(c) < 0) return false;
        }
        var labelStart = at + 1;
        for (var i = labelStart; i <= value.Length; i++)
        {
            work.Step();
            if (i < value.Length && value[i] != '.')
            {
                if (!AlphaNumeric(value[i]) && value[i] != '-') return false;
                continue;
            }
            var length = i - labelStart;
            if (length is < 1 or > 63 || !AlphaNumeric(value[labelStart]) || !AlphaNumeric(value[i - 1])) return false;
            labelStart = i + 1;
        }
        return true;
    }

    private static bool AlphaNumeric(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';

    private sealed class ControlBehaviorState
    {
        internal string CustomMessage = string.Empty;
    }

    private sealed class PatternCache
    {
        internal bool Initialized;
        internal string? Pattern;
        internal JsRegExp? Expression;
    }
}
