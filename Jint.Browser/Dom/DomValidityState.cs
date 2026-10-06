using System.Runtime.CompilerServices;
using Jint.Browser.Events;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>HTML's live ValidityState identity, with flags demanded from the owning control.</summary>
internal sealed class DomValidityState
{
    private static readonly ConditionalWeakTable<Element, DomValidityState> _states = new();
    private readonly Element _element;

    private DomValidityState(Element element) => _element = element;

    internal static DomValidityState Of(Element element) => _states.GetValue(element, static e => new(e));

    // https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#the-constraint-validation-api
    internal bool IsValid(DomRealm realm) => BrowserControlValidation.Read(realm, _element).IsValid;

    internal bool Has(DomRealm realm, ControlValidityFlags flag)
        => (BrowserControlValidation.Read(realm, _element).Flags & flag) != 0;
}
