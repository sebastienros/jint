using Jint.Browser.Dom.Collections;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom.Files;

/// <summary>The HTML drag data store interfaces used to move files into file inputs.</summary>
internal static class FileTransferInstaller
{
    private static readonly JsObjectShape _fileList = new JsObjectShape.Builder()
        .ToStringTag("FileList")
        .PerRealmSlot("constructor", enumerable: false)
        .PerRealmSlot(GlobalSymbolRegistry.Iterator, DomIterator.ArrayValues)
        .Method("item", static (t, args) => JsFileList.Brand(t, "item").Item(args), length: 1)
        .Build();

    private static readonly JsObjectShape _dataTransfer = new JsObjectShape.Builder()
        .ToStringTag("DataTransfer")
        .PerRealmSlot("constructor", enumerable: false)
        .Accessor("dropEffect",
            static (t, _) => JsString.Create(JsDataTransfer.Brand(t, "dropEffect").DropEffect),
            static (t, args) => JsDataTransfer.Brand(t, "dropEffect").SetDropEffect(args))
        .Accessor("effectAllowed",
            static (t, _) => JsString.Create(JsDataTransfer.Brand(t, "effectAllowed").EffectAllowed),
            static (t, args) => JsDataTransfer.Brand(t, "effectAllowed").SetEffectAllowed(args))
        .Accessor("files", static (t, _) => JsDataTransfer.Brand(t, "files").Files)
        .Accessor("items", static (t, _) => JsDataTransfer.Brand(t, "items").Items)
        .Accessor("types", static (t, _) => JsDataTransfer.Brand(t, "types").Types())
        .Method("clearData", static (t, args) => JsDataTransfer.Brand(t, "clearData").ClearData(args))
        .Method("getData", static (t, args) => JsDataTransfer.Brand(t, "getData").GetData(args), length: 1)
        .Method("setData", static (t, args) => JsDataTransfer.Brand(t, "setData").SetData(args), length: 2)
        .Method("setDragImage", static (t, args) => JsDataTransfer.Brand(t, "setDragImage").SetDragImage(args), length: 3)
        .Build();

    private static readonly JsObjectShape _dataTransferItemList = new JsObjectShape.Builder()
        .ToStringTag("DataTransferItemList")
        .PerRealmSlot("constructor", enumerable: false)
        .Method("add", static (t, args) => JsDataTransferItemList.Brand(t, "add").Add(args), length: 1)
        .Method("clear", static (t, _) => JsDataTransferItemList.Brand(t, "clear").Clear())
        .Method("remove", static (t, args) => JsDataTransferItemList.Brand(t, "remove").Remove(args), length: 1)
        .Build();

    private static readonly JsObjectShape _dataTransferItem = new JsObjectShape.Builder()
        .ToStringTag("DataTransferItem")
        .PerRealmSlot("constructor", enumerable: false)
        .Accessor("kind", static (t, _) => JsString.Create(JsDataTransferItem.Brand(t, "kind").Kind))
        .Accessor("type", static (t, _) => JsString.Create(JsDataTransferItem.Brand(t, "type").MimeType))
        .Method("getAsFile", static (t, _) => JsDataTransferItem.Brand(t, "getAsFile").GetAsFile())
        .Method("getAsString", static (t, args) => JsDataTransferItem.Brand(t, "getAsString").GetAsString(args), length: 1)
        .Build();

    internal static JsObjectShape FileListShape => _fileList;

    internal static JsObjectShape DataTransferShape => _dataTransfer;

    internal static JsObjectShape DataTransferItemListShape => _dataTransferItemList;

    internal static JsObjectShape DataTransferItemShape => _dataTransferItem;

    /// <summary>Installs the four interface objects before the generated binding can claim <c>FileList</c>.</summary>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        Add(engine, "FileList", static realm => realm.FileListInterface);
        Add(engine, "DataTransfer", static realm => realm.DataTransferInterface);
        Add(engine, "DataTransferItemList", static realm => realm.DataTransferItemListInterface);
        Add(engine, "DataTransferItem", static realm => realm.DataTransferItemInterface);
    }

    private static void Add(Engine engine, string name, Func<FileTransferRealm, JsValue> factory)
        => engine.AddLazyGlobal(
            name,
            factory,
            static (e, f) => f(FileTransferRealm.Of(e)),
            PropertyFlag.NonEnumerable);

    internal static ObjectInstance Instantiate(
        Engine engine,
        JsObjectShape shape,
        string name,
        int length,
        Func<JsValue[], ObjectInstance>? construct,
        out HostInterfaceObject interfaceObject)
    {
        var realm = engine._mainRealm;
        var prototype = shape.Instantiate(engine, realm.Intrinsics.Object.PrototypeObject);
        interfaceObject = new HostInterfaceObject(engine, realm, name, prototype, length, construct);
        prototype.DefineOwnPropertyUnchecked(
            "constructor",
            new PropertyDescriptor(interfaceObject, PropertyFlag.NonEnumerable));
        return prototype;
    }
}
