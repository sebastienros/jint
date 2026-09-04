using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Files;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom.Files;

/// <summary>HTMLInputElement's selected files and DataTransfer's live file projection.</summary>
internal sealed class JsFileList : ArrayLikeObject
{
    private readonly List<JsFile> _files = [];

    internal JsFileList(Engine engine, ObjectInstance prototype) : base(engine)
    {
        Prototype = prototype;
    }

    public override uint Length => (uint) _files.Count;

    public override bool TryGetIndex(uint index, out JsValue value)
    {
        if (index >= (uint) _files.Count)
        {
            value = JsValue.Undefined;
            return false;
        }

        value = _files[(int) index];
        return true;
    }

    protected override bool HasIndex(uint index) => index < (uint) _files.Count;

    internal IReadOnlyList<JsFile> Files => _files;

    internal event Action? Changed;

    internal void Add(JsFile file)
    {
        _files.Add(file);
        Changed?.Invoke();
    }

    internal void Clear()
    {
        if (_files.Count != 0)
        {
            _files.Clear();
            Changed?.Invoke();
        }
    }

    internal JsValue Item(JsValue[] arguments)
    {
        var index = DomConvert.RequiredUInt32(arguments, 0, "FileList.item");
        return TryGetIndex(index, out var value) ? value : JsValue.Null;
    }

    internal static JsFileList Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsFileList files)
        {
            return files;
        }

        IllegalInvocation(thisObject, "FileList", member);
        return null!;
    }

    public override string ToString() => "[object FileList]";

    internal static void IllegalInvocation(JsValue thisObject, string interfaceName, string member)
    {
        var message = "Failed to execute '" + member + "' on '" + interfaceName + "': Illegal invocation";
        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
    }
}

/// <summary>A read/write drag data store created by <c>new DataTransfer()</c>.</summary>
internal sealed class JsDataTransfer : ObjectInstance
{
    private static readonly HashSet<string> _dropEffects = new(StringComparer.Ordinal)
    {
        "none", "copy", "link", "move",
    };

    private static readonly HashSet<string> _effectsAllowed = new(StringComparer.Ordinal)
    {
        "none", "copy", "copyLink", "copyMove", "link", "linkMove", "move", "all", "uninitialized",
    };

    private IElement? _dragImage;
    private int _hotspotX;
    private int _hotspotY;
    private JsValue? _types;

    internal JsDataTransfer(FileTransferRealm realm, ObjectInstance prototype) : base(prototype.Engine)
    {
        Prototype = prototype;
        Files = realm.NewFileList();
        Items = realm.NewItemList(this, Files);
    }

    internal string DropEffect { get; private set; } = "none";

    internal string EffectAllowed { get; private set; } = "none";

    internal JsFileList Files { get; }

    internal JsDataTransferItemList Items { get; }

    internal JsValue SetDropEffect(JsValue[] arguments)
    {
        var value = TypeConverter.ToString(arguments.At(0));
        if (_dropEffects.Contains(value))
        {
            DropEffect = value;
        }

        return JsValue.Undefined;
    }

    internal JsValue SetEffectAllowed(JsValue[] arguments)
    {
        var value = TypeConverter.ToString(arguments.At(0));
        if (_effectsAllowed.Contains(value))
        {
            EffectAllowed = value;
        }

        return JsValue.Undefined;
    }

    internal JsValue Types()
    {
        if (_types is not null)
        {
            return _types;
        }

        var types = new List<JsValue>();
        foreach (var item in Items.Values)
        {
            if (item.IsStringItem && !types.Any(value => string.Equals(value.AsString(), item.MimeType, StringComparison.Ordinal)))
            {
                types.Add(JsString.Create(item.MimeType));
            }
        }

        if (Items.Values.Any(static item => !item.IsStringItem))
        {
            types.Add(JsString.Create("Files"));
        }

        var result = Engine._mainRealm.Intrinsics.Array.ConstructFast(types);
        result.SetIntegrityLevel(IntegrityLevel.Frozen);
        return _types = result;
    }

    internal JsValue GetData(JsValue[] arguments)
    {
        var format = DomConvert.RequiredText(arguments, 0, "DataTransfer.getData");
        var normalized = NormalizeToken(format);
        var type = ExpandLegacyFormat(normalized);
        var item = Items.Values.FirstOrDefault(candidate => candidate.IsStringItem && candidate.MimeType == type);
        var data = item?.StringData ?? string.Empty;
        return JsString.Create(normalized == "url" ? FirstUri(data) : data);
    }

    internal JsValue SetData(JsValue[] arguments)
    {
        var type = NormalizeFormat(DomConvert.RequiredText(arguments, 0, "DataTransfer.setData"));
        var data = DomConvert.RequiredText(arguments, 1, "DataTransfer.setData");
        Items.SetStringData(type, data);
        return JsValue.Undefined;
    }

    internal JsValue ClearData(JsValue[] arguments)
    {
        var format = DomConvert.OptionalText(arguments, 0, null);
        Items.ClearStringData(format is null ? null : NormalizeFormat(format));
        return JsValue.Undefined;
    }

    internal JsValue SetDragImage(JsValue[] arguments)
    {
        _dragImage = DomBindings.Argument<IElement>(arguments, 0, "DataTransfer.setDragImage");
        _hotspotX = DomConvert.RequiredInt32(arguments, 1, "DataTransfer.setDragImage");
        _hotspotY = DomConvert.RequiredInt32(arguments, 2, "DataTransfer.setDragImage");
        return JsValue.Undefined;
    }

    internal void InvalidateTypes() => _types = null;

    internal static string NormalizeFormat(string format) => ExpandLegacyFormat(NormalizeToken(format));

    private static string NormalizeToken(string format)
    {
        var start = 0;
        while (start < format.Length && IsAsciiWhitespace(format[start]))
        {
            start++;
        }

        var end = format.Length;
        while (end > start && IsAsciiWhitespace(format[end - 1]))
        {
            end--;
        }

        return UrlCharacters.AsciiLowercase(format[start..end]);
    }

    private static string ExpandLegacyFormat(string normalized)
        => normalized switch
        {
            "text" => "text/plain",
            "url" => "text/uri-list",
            var value => value,
        };

    private static string FirstUri(string data)
    {
        foreach (var line in data.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith('#'))
            {
                return line;
            }
        }

        return string.Empty;
    }

    private static bool IsAsciiWhitespace(char character) =>
        character is '\t' or '\n' or '\f' or '\r' or ' ';

    internal static string NormalizeItemType(string type) => UrlCharacters.AsciiLowercase(type);

    internal static JsDataTransfer Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsDataTransfer transfer)
        {
            return transfer;
        }

        JsFileList.IllegalInvocation(thisObject, "DataTransfer", member);
        return null!;
    }

    public override string ToString() => "[object DataTransfer]";
}

/// <summary>The live ordered item list owned by one DataTransfer.</summary>
internal sealed class JsDataTransferItemList : ArrayLikeObject
{
    private readonly FileTransferRealm _realm;
    private readonly JsDataTransfer _owner;
    private readonly JsFileList _files;
    private readonly List<JsDataTransferItem> _items = [];

    internal JsDataTransferItemList(
        FileTransferRealm realm,
        ObjectInstance prototype,
        JsDataTransfer owner,
        JsFileList files)
        : base(prototype.Engine)
    {
        _realm = realm;
        _owner = owner;
        _files = files;
        Prototype = prototype;
    }

    public override uint Length => (uint) _items.Count;

    public override bool TryGetIndex(uint index, out JsValue value)
    {
        if (index >= (uint) _items.Count)
        {
            value = JsValue.Undefined;
            return false;
        }

        value = _items[(int) index];
        return true;
    }

    protected override bool HasIndex(uint index) => index < (uint) _items.Count;

    internal IReadOnlyList<JsDataTransferItem> Values => _items;

    internal JsValue Add(JsValue[] arguments)
    {
        JsDataTransferItem item;
        if (arguments.Length == 1 && arguments[0] is JsFile file)
        {
            item = _realm.NewFileItem(file);
        }
        else if (arguments.Length >= 2)
        {
            var data = TypeConverter.ToString(arguments[0]);
            var type = JsDataTransfer.NormalizeItemType(TypeConverter.ToString(arguments[1]));
            if (_items.Any(candidate => candidate.IsStringItem && candidate.MimeType == type))
            {
                return DomFailures.Refuse(
                    Engine,
                    "DataTransferItemList.add",
                    DomExceptionNames.NotSupported,
                    "an item of type '" + type + "' already exists.");
            }

            item = _realm.NewStringItem(data, type);
        }
        else
        {
            Throw.TypeError(
                Engine.Realm,
                "Failed to execute 'add' on 'DataTransferItemList': the provided arguments match no overload.");
            return JsValue.Undefined;
        }

        _items.Add(item);
        RebuildFiles();
        _owner.InvalidateTypes();
        return item;
    }

    internal JsValue Clear()
    {
        if (_items.Count == 0)
        {
            return JsValue.Undefined;
        }

        foreach (var item in _items)
        {
            item.Disable();
        }

        _items.Clear();
        _files.Clear();
        _owner.InvalidateTypes();
        return JsValue.Undefined;
    }

    internal JsValue Remove(JsValue[] arguments)
    {
        var index = DomConvert.RequiredUInt32(arguments, 0, "DataTransferItemList.remove");
        if (index < (uint) _items.Count)
        {
            _items[(int) index].Disable();
            _items.RemoveAt((int) index);
            RebuildFiles();
            _owner.InvalidateTypes();
        }

        return JsValue.Undefined;
    }

    internal void SetStringData(string type, string data)
    {
        var existing = _items.FirstOrDefault(item => item.IsStringItem && item.MimeType == type);
        if (existing is not null)
        {
            existing.Disable();
            _items.Remove(existing);
        }

        _items.Add(_realm.NewStringItem(data, type));
        _owner.InvalidateTypes();
    }

    internal void ClearStringData(string? type)
    {
        var removed = false;
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            var item = _items[index];
            if (item.IsStringItem && (type is null || item.MimeType == type))
            {
                item.Disable();
                _items.RemoveAt(index);
                removed = true;
            }
        }

        if (removed)
        {
            _owner.InvalidateTypes();
        }
    }

    private void RebuildFiles()
    {
        _files.Clear();
        foreach (var item in _items)
        {
            if (item.File is { } file)
            {
                _files.Add(file);
            }
        }
    }

    internal static JsDataTransferItemList Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsDataTransferItemList items)
        {
            return items;
        }

        JsFileList.IllegalInvocation(thisObject, "DataTransferItemList", member);
        return null!;
    }

    public override string ToString() => "[object DataTransferItemList]";
}

/// <summary>One string or file entry in a drag data store.</summary>
internal sealed class JsDataTransferItem : ObjectInstance
{
    private bool _disabled;
    private readonly JsFile? _file;
    private readonly string _mimeType;
    private string? _stringData;

    internal JsDataTransferItem(Engine engine, ObjectInstance prototype, JsFile file) : base(engine)
    {
        Prototype = prototype;
        _file = file;
        _mimeType = file.MediaType;
    }

    internal JsDataTransferItem(Engine engine, ObjectInstance prototype, string data, string type) : base(engine)
    {
        Prototype = prototype;
        _stringData = data;
        _mimeType = type;
    }

    internal string Kind => _disabled ? string.Empty : IsStringItem ? "string" : "file";

    internal string MimeType => _disabled ? string.Empty : _mimeType;

    internal bool IsStringItem => _file is null;

    internal JsFile? File => _disabled ? null : _file;

    internal string? StringData => _disabled ? null : _stringData;

    internal JsValue GetAsFile() => File ?? JsValue.Null;

    internal JsValue GetAsString(JsValue[] arguments)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(
                Engine.Realm,
                "Failed to execute 'getAsString' on 'DataTransferItem': 1 argument required, but only 0 present.");
        }

        var callbackValue = arguments.At(0);
        if (callbackValue.IsNullOrUndefined())
        {
            return JsValue.Undefined;
        }

        if (callbackValue is not ICallable callback)
        {
            Throw.TypeError(Engine.Realm, "Failed to execute 'getAsString' on 'DataTransferItem': parameter 1 is not of type 'Function'.");
            return JsValue.Undefined;
        }

        if (StringData is null)
        {
            return JsValue.Undefined;
        }

        var data = StringData;
        Engine.Tasks.Post(() => callback.Call(JsValue.Undefined, JsString.Create(data)));
        return JsValue.Undefined;
    }

    internal void Disable()
    {
        _disabled = true;
        _stringData = null;
    }

    internal static JsDataTransferItem Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsDataTransferItem item)
        {
            return item;
        }

        JsFileList.IllegalInvocation(thisObject, "DataTransferItem", member);
        return null!;
    }

    public override string ToString() => "[object DataTransferItem]";
}

/// <summary>The generated HTMLInputElement.files accessor delegates here.</summary>
internal static class FileTransferMembers
{
    internal static JsValue InputFiles(DomRealm realm, IHtmlInputElement input)
        => FileTransferRealm.Of(realm.Engine).InputFiles(input, create: true) ?? JsValue.Null;

    internal static JsValue SetInputFiles(DomRealm realm, IHtmlInputElement input, JsValue[] arguments)
    {
        var value = arguments.At(0);
        if (value.IsNullOrUndefined())
        {
            return JsValue.Undefined;
        }

        if (value is not JsFileList files)
        {
            Throw.TypeError(realm.Engine.Realm, "Failed to set 'files' on 'HTMLInputElement': The provided value is not of type 'FileList'.");
            return JsValue.Undefined;
        }

        FileTransferRealm.Of(realm.Engine).SetInputFiles(input, files);
        return JsValue.Undefined;
    }

    internal static JsValue InputValue(DomRealm realm, IHtmlInputElement input)
        => FileTransferRealm.Of(realm.Engine).InputValue(input);

    internal static JsValue SetInputValue(DomRealm realm, IHtmlInputElement input, JsValue[] arguments)
    {
        var value = DomConvert.At(arguments, 0);
        return FileTransferRealm.Of(realm.Engine).SetInputValue(
            input,
            value.IsNull() ? "" : TypeConverter.ToString(value));
    }

    internal static JsValue SetInputType(DomRealm realm, IHtmlInputElement input, JsValue[] arguments)
        => FileTransferRealm.Of(realm.Engine).SetInputType(
            input,
            DomConvert.RequiredText(arguments, 0, "HTMLInputElement.type"));
}
