using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>HTML §2.7.2.2's live named view, with native checkedness and no copied membership.</summary>
internal sealed class DomRadioNodeList(DomFormControlsCollection source, string name, bool images) : DomNodeList
{
    internal override int Length => ReadLength(source.Realm.NativeReadCheckpoint, source.Realm.CancellationToken);
    internal override Node this[int index] => ReadItem((uint) index, source.Realm.NativeReadCheckpoint, source.Realm.CancellationToken)
        ?? throw new ArgumentOutOfRangeException(nameof(index));

    internal override int ReadLength(Action<int>? checkpoint, CancellationToken token)
    {
        var count = 0;
        foreach (var unused in source.Matching(name, images, new(checkpoint, token))) count++;
        return count;
    }

    internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
    {
        foreach (var element in source.Matching(name, images, new(checkpoint, token)))
        {
            if (index == 0) return element;
            index--;
        }
        return null;
    }

    internal string Value(DomRealm realm)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        foreach (var element in source.Matching(name, images, work))
        {
            if (!IsRadio(element, work)) continue;
            // The native index realizes radio exclusion before this checked flag is observed.
            HtmlCheckableState.GetRadioGroupFacts(element, realm.NativeReadCheckpoint, work.Token);
            work.Check();
            if (!element.ExistingCheckedState!.Checked) continue;
            var value = work.Attribute(element, "value") ?? "on";
            work.Check();
            return value;
        }
        work.Check();
        return string.Empty;
    }

    internal JsValue SetValue(DomRealm realm, string value)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        foreach (var element in source.Matching(name, images, work))
        {
            if (!IsRadio(element, work) || !work.Equal(work.Attribute(element, "value") ?? "on", value)) continue;
            work.Check();
            HtmlCheckednessAlgorithms.Set(element, true, HtmlCheckedChangeOrigin.Algorithm, realm.NativeReadCheckpoint, work.Token);
            work.Check();
            break;
        }
        work.Check();
        return JsValue.Undefined;
    }

    private static bool IsRadio(Element element, DomReadWork work)
        => element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
            && HtmlInputTypes.Parse(work.Attribute(element, "type")) == HtmlInputType.Radio;

    internal static JsObjectShape Shape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor", enumerable: false)
        .ToStringTag("RadioNodeList")
        .Accessor("value",
            DomFailures.Guard("RadioNodeList.value", static (receiver, _) =>
            {
                var self = DomBindings.Bind<DomRadioNodeList>(receiver, "RadioNodeList.value");
                return DomConvert.Text(self.Target.Value(self.Realm));
            }),
            DomFailures.GuardMutation("RadioNodeList.value", static (receiver, arguments) =>
            {
                var self = DomBindings.Bind<DomRadioNodeList>(receiver, "RadioNodeList.value");
                return self.Target.SetValue(self.Realm, DomConvert.RequiredText(arguments, 0, "RadioNodeList.value"));
            }))
        .Build();
}
