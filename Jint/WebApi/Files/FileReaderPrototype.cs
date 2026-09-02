#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Files;

/// <summary>
/// <c>FileReader.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-filereader
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so a reader carries <c>addEventListener</c>
/// beside the six event handler IDL attributes declared here. The four <c>readAs*</c> operations differ only
/// in how the bytes are packaged at the end; everything else about them — the state change, the refusal of a
/// second read, and the three tasks that follow — is <see cref="FileReadOperation"/>.
/// </para>
/// <para>
/// <b><c>FileReaderSync</c> is deliberately absent.</b> It is <c>[Exposed=(DedicatedWorker,SharedWorker)]</c>
/// and its whole purpose is to block a worker's thread on I/O that a window may not block on. Here a blob is
/// already in memory, so the synchronous interface would read a byte array and return — the same answer
/// <c>blob.arrayBuffer()</c> and <c>blob.text()</c> already give, without a second spelling that exists only
/// to be unavailable on the main thread.
/// </para>
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class FileReaderPrototype : Prototype
{
    private const PropertyFlag AttributeFlags = PropertyFlag.Configurable | PropertyFlag.Enumerable;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly FileReaderConstructor _constructor;

    [JsProperty(Name = "EMPTY", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ReaderEmpty = JsNumber.Create(JsFileReader.Empty);
    [JsProperty(Name = "LOADING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ReaderLoading = JsNumber.Create(JsFileReader.Loading);
    [JsProperty(Name = "DONE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ReaderDone = JsNumber.Create(JsFileReader.Done);

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString FileReaderToStringTag = new("FileReader");

    internal FileReaderPrototype(
        Engine engine,
        Realm realm,
        FileReaderConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsArrayBuffer.</summary>
    [JsFunction(Name = "readAsArrayBuffer", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ReadAsArrayBuffer(JsValue thisObject, JsValue blob)
        => Read(thisObject, blob, FileReadType.ArrayBuffer, encoding: JsValue.Undefined, "readAsArrayBuffer");

    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsBinaryString.</summary>
    [JsFunction(Name = "readAsBinaryString", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ReadAsBinaryString(JsValue thisObject, JsValue blob)
        => Read(thisObject, blob, FileReadType.BinaryString, encoding: JsValue.Undefined, "readAsBinaryString");

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-readAsText — the encoding argument is optional with no default, so
    /// an omitted or explicitly undefined one leaves the blob's own <c>charset</c> parameter, and then UTF-8,
    /// to decide.
    /// </summary>
    [JsFunction(Name = "readAsText", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ReadAsText(JsValue thisObject, JsValue blob, JsValue encoding)
        => Read(thisObject, blob, FileReadType.Text, encoding, "readAsText");

    /// <summary>https://w3c.github.io/FileAPI/#dfn-readAsDataURL.</summary>
    [JsFunction(Name = "readAsDataURL", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ReadAsDataUrl(JsValue thisObject, JsValue blob)
        => Read(thisObject, blob, FileReadType.DataUrl, encoding: JsValue.Undefined, "readAsDataURL");

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-abort
    /// </summary>
    /// <remarks>
    /// The <c>abort</c> and <c>loadend</c> events are fired <b>synchronously</b>, from inside this call, which
    /// is what the algorithm says and what
    /// <c>FileAPI/reading-data-section/filereader_abort.any.js</c> depends on: it arms its watcher before
    /// calling <c>abort()</c> precisely because the events cannot be waited for afterwards.
    /// </remarks>
    [JsFunction(Name = "abort", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Abort(JsValue thisObject)
    {
        var reader = Brand(thisObject, "abort");

        // Step 1: an idle or finished reader forgets its result and nothing else happens — no events at all.
        if (reader.ReadyState is JsFileReader.Empty or JsFileReader.Done)
        {
            reader.Result = JsValue.Null;
            return Undefined;
        }

        // Step 2.
        reader.ReadyState = JsFileReader.Done;
        reader.Result = JsValue.Null;

        // Steps 3 and 4: the queued tasks are removed and the read is terminated, both by detaching.
        FileReadOperation.Detach(reader);

        // Step 5, and step 6's guard, which a handler that starts a new read is what makes false.
        reader.FireProgressEvent(JsFileReader.AbortEventType, loaded: 0, total: 0);
        if (reader.ReadyState != JsFileReader.Loading)
        {
            reader.FireProgressEvent(JsFileReader.LoadEndEventType, loaded: 0, total: 0);
        }

        return Undefined;
    }

    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-readystate.</summary>
    [JsAccessor("readyState", Flags = AttributeFlags)]
    private JsNumber ReadyStateGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject, "readyState").ReadyState);

    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-result.</summary>
    [JsAccessor("result", Flags = AttributeFlags)]
    private JsValue ResultGet(JsValue thisObject) => Brand(thisObject, "result").Result;

    /// <summary>https://w3c.github.io/FileAPI/#dom-filereader-error.</summary>
    [JsAccessor("error", Flags = AttributeFlags)]
    private JsValue ErrorGet(JsValue thisObject) => Brand(thisObject, "error").Error;

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onloadstart.</summary>
    [JsAccessor("onloadstart", Flags = AttributeFlags)]
    private JsValue OnLoadStartGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject, "onloadstart"), JsFileReader.LoadStartEventType.ToString());

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onloadstart, setter half.</summary>
    [JsAccessor("onloadstart", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue OnLoadStartSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject, "onloadstart"), JsFileReader.LoadStartEventType.ToString(), value);

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onprogress.</summary>
    [JsAccessor("onprogress", Flags = AttributeFlags)]
    private JsValue OnProgressGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject, "onprogress"), JsFileReader.ProgressEventType.ToString());

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onprogress, setter half.</summary>
    [JsAccessor("onprogress", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue OnProgressSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject, "onprogress"), JsFileReader.ProgressEventType.ToString(), value);

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onload.</summary>
    [JsAccessor("onload", Flags = AttributeFlags)]
    private JsValue OnLoadGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject, "onload"), JsFileReader.LoadEventType.ToString());

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onload, setter half.</summary>
    [JsAccessor("onload", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue OnLoadSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject, "onload"), JsFileReader.LoadEventType.ToString(), value);

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onabort.</summary>
    [JsAccessor("onabort", Flags = AttributeFlags)]
    private JsValue OnAbortGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject, "onabort"), JsFileReader.AbortEventType.ToString());

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onabort, setter half.</summary>
    [JsAccessor("onabort", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue OnAbortSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject, "onabort"), JsFileReader.AbortEventType.ToString(), value);

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onerror.</summary>
    [JsAccessor("onerror", Flags = AttributeFlags)]
    private JsValue OnErrorGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject, "onerror"), JsFileReader.ErrorEventType.ToString());

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onerror, setter half.</summary>
    [JsAccessor("onerror", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject, "onerror"), JsFileReader.ErrorEventType.ToString(), value);

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onloadend.</summary>
    [JsAccessor("onloadend", Flags = AttributeFlags)]
    private JsValue OnLoadEndGet(JsValue thisObject)
        => EventHandlerAttributes.Get(Brand(thisObject, "onloadend"), JsFileReader.LoadEndEventType.ToString());

    /// <summary>https://w3c.github.io/FileAPI/#dfn-onloadend, setter half.</summary>
    [JsAccessor("onloadend", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue OnLoadEndSet(JsValue thisObject, JsValue value)
        => EventHandlerAttributes.Set(Brand(thisObject, "onloadend"), JsFileReader.LoadEndEventType.ToString(), value);

    /// <summary>
    /// The body all four <c>readAs*</c> operations share: brand-check, convert the <c>Blob</c> argument, and
    /// start the read operation.
    /// </summary>
    private JsValue Read(JsValue thisObject, JsValue blob, FileReadType type, JsValue encoding, string operation)
    {
        var context = "Failed to execute '" + operation + "' on 'FileReader'";
        var reader = Brand(thisObject, operation);

        if (blob is not JsBlob source)
        {
            Throw.TypeError(_realm, context + ": parameter 1 is not of type 'Blob'.");
            return Undefined;
        }

        var encodingName = encoding.IsUndefined() ? null : TypeConverter.ToString(encoding);
        FileReadOperation.Start(_engine, _realm, reader, source, type, encodingName, context);
        return Undefined;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsFileReader Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsFileReader reader)
        {
            return reader;
        }

        Throw.TypeError(_realm, $"Failed to read the '{member}' member of 'FileReader': illegal invocation, receiver is not a FileReader.");
        return null!;
    }
}
#endif
