#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// <c>File.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/FileAPI/#file-section
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Blob.prototype</c>, which is what makes <c>file instanceof Blob</c> hold
/// and what gives a file <c>size</c>, <c>type</c>, <c>slice</c> and the read methods. Only <c>name</c> and
/// <c>lastModified</c> are declared here, both brand-checked against <c>File</c> rather than <c>Blob</c>:
/// a plain blob has no name.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class FilePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly FileConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString FileToStringTag = new("File");

    internal FilePrototype(
        Engine engine,
        Realm realm,
        FileConstructor constructor,
        BlobPrototype blobPrototype) : base(engine, realm)
    {
        _prototype = blobPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-name
    /// </summary>
    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject)
    {
        return JsString.Create(Brand(thisObject).Name);
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-lastModified
    /// </summary>
    [JsAccessor("lastModified", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber LastModifiedGet(JsValue thisObject)
    {
        return JsNumber.Create(Brand(thisObject).LastModified);
    }

    private JsFile Brand(JsValue thisObject)
    {
        if (thisObject is JsFile file)
        {
            return file;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a File");
        return null!;
    }
}
#endif
