using Jint.Native;
using Jint.Native.Intl;

namespace Jint.Runtime;

public sealed partial class Intrinsics
{
    private IntlInstance? _intl;
    private JsSymbol? _intlLegacyConstructedSymbol;

    /// <summary>
    /// The per-realm %Intl%.[[FallbackSymbol]] — a Symbol with the description
    /// "IntlLegacyConstructedSymbol" used by the normative-optional legacy constructor mode of
    /// Intl.Collator / Intl.DateTimeFormat / Intl.NumberFormat (ChainXxx / UnwrapXxx).
    /// </summary>
    internal JsSymbol IntlLegacyConstructedSymbol =>
        _intlLegacyConstructedSymbol ??= new JsSymbol("IntlLegacyConstructedSymbol");

    private CollatorConstructor? _collator;
    private DateTimeFormatConstructor? _dateTimeFormat;
    private DisplayNamesConstructor? _displayNames;
    private DurationFormatConstructor? _durationFormat;
    private ListFormatConstructor? _listFormat;
    private LocaleConstructor? _locale;
    private NumberFormatConstructor? _numberFormat;
    private PluralRulesConstructor? _pluralRules;
    private RelativeTimeFormatConstructor? _relativeTimeFormat;
    private SegmenterConstructor? _segmenter;

    internal IntlInstance Intl =>
        _intl ??= new IntlInstance(_engine, _realm, Object.PrototypeObject);

    internal CollatorConstructor Collator =>
        _collator ??= new CollatorConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal DateTimeFormatConstructor DateTimeFormat =>
        _dateTimeFormat ??= new DateTimeFormatConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal DisplayNamesConstructor DisplayNames =>
        _displayNames ??= new DisplayNamesConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal DurationFormatConstructor DurationFormat =>
        _durationFormat ??= new DurationFormatConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal ListFormatConstructor ListFormat =>
        _listFormat ??= new ListFormatConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal LocaleConstructor Locale =>
        _locale ??= new LocaleConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal NumberFormatConstructor NumberFormat =>
        _numberFormat ??= new NumberFormatConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal PluralRulesConstructor PluralRules =>
        _pluralRules ??= new PluralRulesConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal RelativeTimeFormatConstructor RelativeTimeFormat =>
        _relativeTimeFormat ??= new RelativeTimeFormatConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal SegmenterConstructor Segmenter =>
        _segmenter ??= new SegmenterConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);
}