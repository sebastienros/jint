#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// <c>FormData.prototype</c> — the interface prototype object.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-formdata
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The IDL declares <c>iterable&lt;USVString, FormDataEntryValue&gt;</c>, which is what gives the interface
/// <c>entries</c>, <c>keys</c>, <c>values</c>, <c>forEach</c> and an <c>@@iterator</c> that is the very same
/// function object as <c>entries</c> — https://webidl.spec.whatwg.org/#es-iterable.
/// </para>
/// <para>
/// <c>append</c> and <c>set</c> are overloaded on their second argument: a <c>Blob</c> takes the
/// three-argument form, anything else the two-argument string form. Passing a filename therefore <i>is</i>
/// choosing the blob overload, so <c>fd.append('a', 'b', 'c')</c> is a <c>TypeError</c> — exactly as in a
/// browser — while <c>fd.append('a', 'b', undefined)</c> is not, because a trailing <c>undefined</c> for an
/// optional argument with no default value means the argument was not passed.
/// </para>
/// </remarks>
[JsSymbolAlias("Iterator", "entries")]
[JsObject(UseShape = true)]
internal sealed partial class FormDataPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly FormDataConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString FormDataToStringTag = new("FormData");

    internal FormDataPrototype(
        Engine engine,
        Realm realm,
        FormDataConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata-append
    /// </summary>
    [JsFunction(Name = "append", Length = 2)]
    private JsValue Append(JsValue thisObject, JsValue name, JsValue value, JsValue filename)
    {
        var formData = Brand(thisObject);
        formData.Entries.Add(CreateEntry(name, value, filename));
        return Undefined;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata-delete
    /// </summary>
    [JsFunction(Name = "delete", Length = 1)]
    private JsValue Delete(JsValue thisObject, JsValue name)
    {
        var formData = Brand(thisObject);
        var key = Name(name);

        var entries = formData.Entries;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (string.Equals(entries[i].Name, key, StringComparison.Ordinal))
            {
                entries.RemoveAt(i);
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata-get
    /// </summary>
    [JsFunction(Name = "get", Length = 1)]
    private JsValue GetEntry(JsValue thisObject, JsValue name)
    {
        var formData = Brand(thisObject);
        var index = formData.IndexOf(Name(name));
        return index < 0 ? Null : formData.Entries[index].Value;
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata-getall
    /// </summary>
    [JsFunction(Name = "getAll", Length = 1)]
    private JsArray GetAll(JsValue thisObject, JsValue name)
    {
        var formData = Brand(thisObject);
        var key = Name(name);

        var values = new List<JsValue>();
        foreach (var entry in formData.Entries)
        {
            if (string.Equals(entry.Name, key, StringComparison.Ordinal))
            {
                values.Add(entry.Value);
            }
        }

        return _realm.Intrinsics.Array.ConstructFast(values);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata-has
    /// </summary>
    [JsFunction(Name = "has", Length = 1)]
    private JsBoolean Has(JsValue thisObject, JsValue name)
    {
        var formData = Brand(thisObject);
        return JsBoolean.Create(formData.IndexOf(Name(name)) >= 0);
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata-set
    /// </summary>
    /// <remarks>
    /// The replacement keeps the first match's position and drops every later one, so <c>set</c> on an
    /// existing name never moves the entry to the end.
    /// </remarks>
    [JsFunction(Name = "set", Length = 2)]
    private JsValue Set(JsValue thisObject, JsValue name, JsValue value, JsValue filename)
    {
        var formData = Brand(thisObject);
        var entry = CreateEntry(name, value, filename);

        var entries = formData.Entries;
        var first = formData.IndexOf(entry.Name);
        if (first < 0)
        {
            entries.Add(entry);
            return Undefined;
        }

        entries[first] = entry;

        for (var i = entries.Count - 1; i > first; i--)
        {
            if (string.Equals(entries[i].Name, entry.Name, StringComparison.Ordinal))
            {
                entries.RemoveAt(i);
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-forEach
    /// </summary>
    /// <remarks>
    /// The index is re-read against the live list on every step, which is what lets a callback that appends
    /// see what it appended and one that deletes shorten the walk.
    /// </remarks>
    [JsFunction(Name = "forEach", Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsValue callbackfn, JsValue thisArg)
    {
        var formData = Brand(thisObject);
        var callable = GetCallable(callbackfn);

        for (var i = 0; i < formData.Entries.Count; i++)
        {
            var entry = formData.Entries[i];
            callable.Call(thisArg, [entry.Value, JsString.Create(entry.Name), thisObject]);
        }

        return Undefined;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-iterable
    /// </summary>
    [JsFunction(Name = "entries", Length = 0)]
    private FormDataIterator Entries(JsValue thisObject)
    {
        return _realm.Intrinsics.FormDataIteratorPrototype.ConstructEntryIterator(Brand(thisObject));
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-iterable
    /// </summary>
    [JsFunction(Name = "keys", Length = 0)]
    private FormDataIterator Keys(JsValue thisObject)
    {
        return _realm.Intrinsics.FormDataIteratorPrototype.ConstructKeyIterator(Brand(thisObject));
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-iterable
    /// </summary>
    [JsFunction(Name = "values", Length = 0)]
    private FormDataIterator Values(JsValue thisObject)
    {
        return _realm.Intrinsics.FormDataIteratorPrototype.ConstructValueIterator(Brand(thisObject));
    }

    /// <summary>
    /// Creating an entry,
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#create-an-entry, fused with
    /// the overload resolution that decides which of <c>append</c>/<c>set</c>'s two signatures was called.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A blob value always becomes a <i>new</i> <c>File</c>, even when it already was one: the algorithm
    /// says so, and it is what keeps an entry from aliasing a file object the script goes on to hold. The
    /// name is <c>"blob"</c> for a bare blob, the file's own name when no filename is given, and the
    /// filename whenever one is. The specification says only "representing the same bytes"; the media type
    /// and, for a file, the modification time are carried over, which is what every implementation does and
    /// the only reading under which a round trip through <c>FormData</c> preserves a file.
    /// </para>
    /// <para>
    /// Overload resolution happens first, and it is observable: with three arguments only the blob
    /// signature exists, so a string value there is a <c>TypeError</c> rather than a silently ignored
    /// filename.
    /// </para>
    /// </remarks>
    private FormDataEntry CreateEntry(JsValue name, JsValue value, JsValue filename)
    {
        var blob = value as JsBlob;

        // Overload resolution precedes every argument conversion, so a value that cannot satisfy the only
        // three-argument signature fails before the name's toString is ever reached.
        if (blob is null && !filename.IsUndefined())
        {
            Throw.TypeError(_realm, "FormData: parameter 2 is not of type 'Blob', which the three-argument overload requires");
        }

        // Arguments are then converted left to right.
        var entryName = Name(name);

        if (blob is null)
        {
            return new FormDataEntry(entryName, JsString.Create(FileApi.ToScalarValueString(TypeConverter.ToString(value))));
        }

        var file = value as JsFile;
        var entryFileName = filename.IsUndefined()
            ? file?.Name ?? FileApi.DefaultBlobFileName
            : FileApi.ToScalarValueString(TypeConverter.ToString(filename));

        var wrapped = new JsFile(_engine, blob.Data, blob.MediaType, entryFileName, file?.LastModified ?? FileConstructor.Now(_engine))
        {
            _prototype = _realm.Intrinsics.File.PrototypeObject,
        };

        return new FormDataEntry(entryName, wrapped);
    }

    private static string Name(JsValue name) => FileApi.ToScalarValueString(TypeConverter.ToString(name));

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsFormData Brand(JsValue thisObject)
    {
        if (thisObject is JsFormData formData)
        {
            return formData;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a FormData");
        return null!;
    }
}
#endif
