#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// <c>%FormDataIteratorPrototype%</c> — the default iterator object prototype a WebIDL
/// <c>iterable&lt;&gt;</c> declaration produces.
/// <para>
/// https://webidl.spec.whatwg.org/#es-iterator-prototype-object
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>%IteratorPrototype%</c> and its <c>@@toStringTag</c> is the interface's
/// name followed by <c>" Iterator"</c>, both as the specification prescribes. It is not reachable from any
/// global: the only way to obtain one is to call <c>entries</c>, <c>keys</c> or <c>values</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class FormDataIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString FormDataIteratorToStringTag = new("FormData Iterator");

    internal FormDataIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-iterator-prototype-object — "an iterator prototype object must
    /// have a <c>next</c> data property with attributes <c>{ [[Writable]]: true, [[Enumerable]]: true,
    /// [[Configurable]]: true }</c>".
    /// </summary>
    /// <remarks>
    /// The attributes are WebIDL's, not ECMA-262's, and enumerable is the one that has to be spelled out: a
    /// built-in function property is non-enumerable everywhere in the language
    /// (https://tc39.es/ecma262/#sec-ecmascript-standard-built-in-objects), which is what
    /// <c>[JsFunction]</c> defaults to. The <c>@@toStringTag</c> below keeps the language's shape, because
    /// a class string is <c>{ [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true }</c> in
    /// WebIDL too.
    /// </remarks>
    [JsFunction(Name = "next", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal FormDataIterator ConstructEntryIterator(JsFormData formData)
        => Construct(formData, FormDataIteratorKind.KeyAndValue);

    internal FormDataIterator ConstructKeyIterator(JsFormData formData)
        => Construct(formData, FormDataIteratorKind.Key);

    internal FormDataIterator ConstructValueIterator(JsFormData formData)
        => Construct(formData, FormDataIteratorKind.Value);

    private FormDataIterator Construct(JsFormData formData, FormDataIteratorKind kind)
    {
        return new FormDataIterator(Engine, formData, kind)
        {
            _prototype = this,
        };
    }
}

internal enum FormDataIteratorKind
{
    Key,
    Value,
    KeyAndValue,
}

/// <summary>
/// The default iterator object's stepping: an index into the <b>live</b> entry list, re-read on every step.
/// That is what the specification describes — the list of value pairs to iterate over is evaluated per step
/// — so an entry appended during iteration is reached, and one removed before its turn is not.
/// <para>
/// https://webidl.spec.whatwg.org/#es-iterator-prototype-object
/// </para>
/// </summary>
internal sealed class FormDataIterator : IteratorInstance
{
    private readonly JsFormData _formData;
    private readonly FormDataIteratorKind _kind;
    private int _position;

    internal FormDataIterator(Engine engine, JsFormData formData, FormDataIteratorKind kind) : base(engine)
    {
        _formData = formData;
        _kind = kind;
    }

    public override bool TryIteratorStep(out ObjectInstance nextItem)
    {
        var entries = _formData.Entries;
        if (_position >= entries.Count)
        {
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }

        var entry = entries[_position];
        _position++;

        nextItem = _kind switch
        {
            FormDataIteratorKind.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsString.Create(entry.Name)),
            FormDataIteratorKind.Value => IteratorResult.CreateValueIteratorPosition(_engine, entry.Value),
            _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsString.Create(entry.Name), entry.Value),
        };

        return true;
    }
}
#endif
