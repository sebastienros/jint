#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// The <c>URLSearchParams</c> interface object.
/// <para>
/// https://url.spec.whatwg.org/#urlsearchparams
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(optional (sequence&lt;sequence&lt;USVString&gt;&gt; or record&lt;USVString, USVString&gt; or
/// USVString) init = "")</c>. Which arm of the union an argument takes is decided by
/// https://webidl.spec.whatwg.org/#es-union: an object with an <c>@@iterator</c> method is a sequence, any
/// other object is a record, and everything else — including <see langword="null"/>, which is not an object —
/// converts to a string.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class UrlSearchParamsConstructor : Constructor
{
    private static readonly JsString _functionName = new("URLSearchParams");

    internal UrlSearchParamsConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new UrlSearchParamsPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal UrlSearchParamsPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-urlsearchparams
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var instance = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WebApiUrlSearchParams.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsUrlSearchParams(engine));

        InitializeFrom(instance, arguments.At(0));
        return instance;
    }

    /// <summary>
    /// Builds a <c>URLSearchParams</c> from engine code, for the <c>URL</c> object's query object. The list is
    /// taken as given rather than re-parsed.
    /// </summary>
    internal JsUrlSearchParams CreateInstance(List<FormUrlEncodedEntry> list)
    {
        return new JsUrlSearchParams(_engine)
        {
            _prototype = PrototypeObject,
            List = list,
        };
    }

    /// <summary>
    /// The initialize steps, https://url.spec.whatwg.org/#concept-urlsearchparams-new, preceded by the union
    /// conversion that decides which of them applies.
    /// </summary>
    private void InitializeFrom(JsUrlSearchParams instance, JsValue init)
    {
        if (init is ObjectInstance obj)
        {
            // GetMethod raises a TypeError for an @@iterator that is present but not callable, which is what
            // the sequence conversion would do anyway.
            var iteratorMethod = obj.GetMethod(GlobalSymbolRegistry.Iterator);
            if (iteratorMethod is not null)
            {
                InitializeFromSequence(instance, obj, iteratorMethod);
            }
            else
            {
                InitializeFromRecord(instance, obj);
            }

            return;
        }

        // The IDL default is the empty string, so an omitted or explicitly undefined argument is "".
        var input = init.IsUndefined() ? string.Empty : UrlValues.ToUsvString(init);
        if (input.Length > 0 && input[0] == '?')
        {
            input = input.Substring(1);
        }

        instance.List = FormUrlEncoded.Parse(input);
    }

    /// <summary>
    /// The <c>sequence&lt;sequence&lt;USVString&gt;&gt;</c> arm: step 1 of the initialize steps, plus the
    /// nested sequence conversion of https://webidl.spec.whatwg.org/#js-sequence.
    /// </summary>
    private void InitializeFromSequence(JsUrlSearchParams instance, ObjectInstance init, ICallable iteratorMethod)
    {
        var iterations = 0;
        var iterator = init.GetIteratorFromMethod(_realm, iteratorMethod);
        while (iterator.TryIteratorStep(out var next))
        {
            // A script-supplied iterator is unbounded, so the engine stays interruptible.
            if (++iterations % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var element = next.Get(CommonProperties.Value);

            // Converting to sequence<USVString> requires an object; a primitive — including a number, which is
            // what WPT's `new URLSearchParams([[1]])` passes — is a TypeError before the size is ever counted.
            if (element is not ObjectInstance)
            {
                Throw.TypeError(_realm, "URLSearchParams: each element must be a sequence of two strings");
            }

            var innerIterator = element.GetIterator(_realm);
            string? name = null;
            string? value = null;
            var size = 0;

            // The sequence conversion drains the inner iterator before the size is judged, so a three-element
            // element still calls next() three times.
            while (innerIterator.TryIteratorStep(out var innerNext))
            {
                var item = UrlValues.ToUsvString(innerNext.Get(CommonProperties.Value));
                if (size == 0)
                {
                    name = item;
                }
                else if (size == 1)
                {
                    value = item;
                }

                size++;

                if (++iterations % Engine.ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }
            }

            if (size != 2)
            {
                Throw.TypeError(_realm, "URLSearchParams: each element must be a sequence of two strings");
            }

            instance.List.Add(new FormUrlEncodedEntry(name!, value!));
        }
    }

    /// <summary>
    /// The <c>record&lt;USVString, USVString&gt;</c> arm: the conversion of
    /// https://webidl.spec.whatwg.org/#js-record followed by step 2 of the initialize steps.
    /// </summary>
    /// <remarks>
    /// A record is an ordered map, so two distinct JavaScript keys that convert to the same USVString — a pair
    /// of differing unpaired surrogates, say — become one entry, holding the <i>last</i> value at the
    /// <i>first</i> key's position. WPT pins exactly that.
    /// </remarks>
    private static void InitializeFromRecord(JsUrlSearchParams instance, ObjectInstance init)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var key in init.GetOwnPropertyKeys(Types.String))
        {
            var descriptor = init.GetOwnProperty(key);
            if (descriptor == PropertyDescriptor.Undefined || !descriptor.Enumerable)
            {
                continue;
            }

            var typedKey = UrlValues.ToUsvString(key);
            var typedValue = UrlValues.ToUsvString(init.Get(key));

            if (positions.TryGetValue(typedKey, out var index))
            {
                instance.List[index] = new FormUrlEncodedEntry(typedKey, typedValue);
            }
            else
            {
                positions[typedKey] = instance.List.Count;
                instance.List.Add(new FormUrlEncodedEntry(typedKey, typedValue));
            }
        }
    }
}
#endif
